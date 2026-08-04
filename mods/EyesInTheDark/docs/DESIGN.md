# Eyes in the Dark - Wyrdnight Encounters

## Status

This is the living design document for `EyesInTheDark`. Version `0.8.6` is the
current implementation and acceptance target.

## Product identity

- Display name: **Eyes in the Dark - Wyrdnight Encounters**
- Package and folder: `EyesInTheDark`
- DLL: `EyesInTheDark.dll`
- Plugin GUID: `ks.tgfoa.eyes-in-the-dark`
- Central resource: **Wyrd Threat**
- Gameplay presets: **Uneasy Night**, **Watchful Night**, and **Cursed Night**
- Default gameplay tuning: **Watchful Night**
- Reference world-clock cycle: **60-minute day / 15-minute night**

The project is inspired by Wyrd Hunt. Its defining difference is that Wyrd
Threat controls a probabilistic night director rather than a fixed sequence of
threshold-triggered spawns.

## Design goals

1. Make outdoor Wyrdnights tense without sending one enemy after another
   until dawn.
2. Own a configurable dynamic world clock balanced around a 60-minute day and
   15-minute night, while retaining safe behavior from `1.0` through `0.1`.
3. Make player behavior the main source of additional Wyrd Threat.
4. Use uncertainty, warnings, recovery, and varied encounters to create
   suspense.
5. Protect new characters through player-level, regional, template-safety, and
   danger-budget eligibility rules.
6. Incorporate the useful weighted-selection, mixed-pack, repetition-control,
   and preset principles previously explored in the companion addon directly
   into the main director.
7. Own the threat meter and Wyrd boundary presentation in this mod while
   exposing small, stable integration surfaces for other Grailwright mods.
8. Prefer a small release-ready system over experimental subsystems and broad
   abstractions.

## Non-goals for the first playable release

- Custom creatures, items, or asset bundles.
- Custom save data or progression mutation.
- Indoor Wyrd encounters.
- Quest, unique, boss, summon, or trial enemy reuse without explicit later
  research and approval.
- Broad AI ownership, faction replacement, or persistent actor management.
- Custom hunter rewards. Native loot and threat relief are sufficient for the
  first release.
- Compatibility shims for retired companion or visual plugins.

## Player experience

The intended loop is:

```text
Outdoor Wyrdnight begins
        |
Player explores and acts
        |
Wyrd Threat rises
        |
Atmospheric warning and increasing encounter risk
        |
A level- and region-appropriate hunt may be committed
        |
Player kills the official hunter or escapes
        |
Threat relief and a protected recovery period
        |
The night continues
```

The director must preserve quiet stretches. High Wyrd Threat makes an event
more likely and makes stronger or larger encounters more likely, but it does
not guarantee a spawn at an exact meter value.

## Runtime states

Keep the initial state machine small:

```text
Inactive -> Roaming -> Warning -> Active Hunt -> Recovery -> Roaming
```

- **Inactive:** daylight, loading, no playable hero, or an interior. The meter
  is hidden. Indoor Wyrd Threat decay may continue when appropriate.
- **Roaming:** outdoor Wyrdnight gameplay. Threat and encounter pressure can
  accumulate.
- **Warning:** the director has committed an encounter but has not started it.
  Atmospheric feedback can play and placement must still succeed.
- **Active Hunt:** one official hunt is active. No additional hunt can be
  committed.
- **Recovery:** the hunt resolved or the player escaped. Threat can change, but
  a new hunt cannot begin until recovery ends.

Do not build a generalized state-machine framework. A direct enum and explicit
transitions are sufficient.

## Time model

Do not use one clock for every system. Assign time intentionally:

- **World/Wyrdnight progress:** passive threat growth, nightly pacing, and the
  base danger budget.
- **Gameplay time:** AI scans, combat behavior, placement timeout, pursuit, and
  active-encounter lifecycle.
- **Active real time:** HUD animation, GFT cooldowns, indoor decay, and minimum
  anti-spam recovery. This clock must not advance while gameplay is paused.

Read actual game world time and Wyrdnight progress. Do not infer night length
from Unity `Time.timeScale` and do not inspect another mod's config.

Eyes owns the `GameRealTime` world-weather rate when both its master switch and
`EnableDynamicTimescale` are enabled:

- `DayTimescale = 0.23`, producing approximately 60 real minutes of daylight;
- `NightTimescale = 0.413`, producing approximately 15 real minutes of night;
- the reference complete cycle is approximately 75 real minutes;
- both multipliers accept `0.01` through `5.0`;
- all three gameplay presets use this same clock target.

The controller calls
`GameRealTime.SetWeatherDayDuration(baseDayDuration / multiplier)` only when
the live clock instance, day/night phase, enabled state, or configured phase
multiplier changes. It never writes Unity gameplay `Time.timeScale`, so combat,
animations, effects, and pause behavior are unaffected. Dawn/nightfall, loads,
and phase-changing time skips are observed on the next update. On disable or
plugin release, Eyes restores the native duration only when the current rate
still equals the last value Eyes applied; a later external change is preserved.

### Extended-night scaling

- Passive threat is normalized by the percentage of the current Wyrdnight
  that elapses.
- Meaningful player actions add fixed, independently throttled threat.
- Every preset has a base nightly danger budget.
- Longer nights receive a capped, sublinear danger-budget bonus rather than a
  linear multiplier.
- The initial Watchful Night tuning uses a base budget of `30`, a long-night
  bonus scale of `0.35`, and a maximum bonus fraction of `0.75`.
- Let `m` be the current world-clock night-duration multiplier relative to the
  game's native configured rate. Calculate
  `bonus = min(maximumBonus, max(0, sqrt(m) - 1) * bonusScale)` and
  `nightBudget = baseBudget * (1 + bonus)`.
- Read `m` from the game's current weather/world-clock rate and native day
  duration. Do not use Unity `Time.timeScale` or another mod's settings.
- Preset-specific budget bases and caps replace these initial Watchful values
  when the gameplay presets are implemented in 0.6.0.
- Minimum active-real-time recovery prevents compressed vanilla nights from
  producing back-to-back hunts.

At the default `0.413` night multiplier, the duration multiplier is about
`2.42`; Watchful therefore receives about a 19 percent bonus, not a linear
142 percent increase.

A night that lasts ten times longer receives the capped `0.75` bonus with the
initial tuning: `30 * 1.75 = 52.5`, not ten times the base budget.

## Wyrd Threat

Wyrd Threat is continuous internally, normally represented from 0 to 100.
Presentation may group it into four stages:

- Unnoticed
- Watched
- Hunted
- Marked

These stages select presentation and weighting curves. They are not guaranteed
spawn thresholds.

### Threat sources

Passive outdoor exposure supplies a modest baseline. Player behavior supplies
most additional pressure. Candidate sources for the first implementation are:

- sustained sprinting or fast swimming while exposed;
- meaningful combat actions observed through proven game events;
- confirmed melee impacts against scenery or non-damageable objects, limited
  to one contribution per attack;
- killing Wyrd creatures;
- looting corpses or containers while exposed;
- direct world pickup or stealing while exposed;
- powerful or noisy magic after a reliable event route is proven.

Every repeatable source requires a cooldown, aggregation window, or diminishing
return. Inputs such as empty weapon swings or repeatedly moving the same item
must not farm threat.

Normal combat can raise threat, but the director should defer a new hunt until
the unrelated combat ends. Active hunts suspend additional encounter rolls.

### Threat reduction

- Killing the official hunter provides the greatest immediate reduction.
- Escaping provides a smaller reduction and a longer `Recently Pursued`
  recovery state.
- Protected outdoor areas reduce threat at a moderate rate.
- Interiors hide the meter, suspend encounter generation, and slowly reduce
  threat using active real time.
- Leaving an interior grants a short outdoor grace period.
- Dawn resets threat and nightly budgets.
- Invalid or failed spawns consume no danger budget and grant no threat relief.

Entering an interior must not immediately erase threat.

## Encounter probability

Use accumulated hazard rather than independent frequent random rolls. Threat,
night progress, time since the last hunt, preset tuning, and remaining danger
budget contribute to an encounter-pressure accumulator. A randomized target
creates natural variation while ensuring that a long quiet period gradually
becomes more consequential.

The exact probability remains hidden from the player. Diagnostics may log the
accumulator, target, eligibility rejections, selection weights, and random seed.

### Initial 0.5.0 hazard tuning

The first-hunt milestone uses one accumulated hazard value and one randomized
target. While the hero is in eligible exposed outdoor play, add per minute:

`0.01 + 0.42 * (threat / 100)^1.5 + 0.08 * nightProgress`

The target is selected uniformly from `0.85` to `1.15`. Hazard and warning time
pause while gameplay is paused. Protection, interiors, unrelated combat,
transitions, travel, invalid hero state, and insufficient danger budget do not
advance hazard. Losing eligibility during the warning cancels placement and
retains at most half of the prior target as pressure; it spends no budget.

## Danger budgets

Use danger cost rather than only counting encounters:

- weak solo hunter: low cost;
- stronger solo hunter: moderate cost;
- small mixed pack: moderate to high cost;
- dangerous pack or apex encounter: high cost.

Spend budget only after successful placement. When most budget is spent,
atmospheric pressure may continue but full encounters become rare. The budget
resets at dawn. Version 0.4.0 calculates and reports the initial budget but does
not yet spend it; spending begins only after a placement is confirmed in the
first-hunt milestone.

The 0.6.0 curated profile costs are Wyrdspirit `8`, Redcap `10`, Corpse Eater
`12`, Sharg `16`, and Ogre `24`, multiplied by the configured danger-cost
multiplier. A native placement request alone is not confirmation. Eyes waits
for every planned `Location` and `NpcElement`, validates that every actor is
hostile, is not an ally or summon, can enter combat, and successfully receives
native combat entry before spending the complete composition cost. A failed or
invalid member atomically discards the volatile composition and costs zero. A
lost exact primary target refunds the exact composition cost.

Every member must report `NpcAI.InCombat` with the exact Hero as its current
target immediately after native combat entry. Failure discards the entire
composition and spends zero budget. During an active hunt, a disengaged member
within 60 metres may receive one native combat reassertion every two active
seconds, with at most three attempts per member. Reacquisition runs only while
outdoors, exposed, in the same initialized scene, and outside loading or travel
states. Eyes does not own navigation, perception, faction, guards, or general
AI behavior.

## Enemy eligibility

Selection has two phases.

### Hard eligibility

Filter candidates by:

- current map or region;
- minimum player level;
- validated native template and spawn route;
- exclusion of unique, quest, boss, summon, debug, and challenge templates;
- current encounter and placement safety.

Region behavior defaults to strict. Candidates may list one or more allowed
regions, while a small curated group of universal Wyrd creatures may be marked
for broader use. If no safe eligible candidate exists, skip the encounter.

Player level unlocks possible enemies; it does not force every encounter to use
the hardest unlocked enemy. High threat increases danger only inside the
eligible pool. Player level also caps pack size so a new character cannot be
overwhelmed by many individually weak enemies.

### Weighted selection

Weight eligible candidates by:

- Wyrd Threat;
- preset;
- danger cost;
- solo, primary, and sidecar suitability;
- recent candidate and family history;
- remaining nightly danger budget;
- recent encounter strength.

Candidate records need only the data the director uses: stable id, native
template identity, regions, minimum level, danger cost, family, selection
weights, pack limits, and safety flags.

## Mixed encounters

Carry these rules into the core director:

- prefer weaker sidecars;
- strongly reduce same-family repetition;
- reduce same-tier sidecars;
- avoid immediate primary-hunter repeats;
- temporarily reject templates that fail during the current session;
- allow Wyrdspirits to use a curated small-cluster rule;
- require sufficient level and threat for hard sidecars;
- cap pack size by level, preset, and available danger budget.

One primary actor is the official hunter. Killing it resolves the official hunt
and grants the main threat reduction. Surviving sidecars become ordinary
enemies and do not keep the director locked indefinitely.

## Hunt resolution

The director must distinguish:

- official hunter killed;
- player escaped for a sustained distance/time window;
- player entered an interior;
- dawn arrived;
- target or placement became invalid;
- player died or loaded another state.

Exact escape distance and duration require playtesting. Entering an interior
counts as escape rather than a kill and therefore grants less threat relief.
Target invalidation is a failed encounter and does not spend danger budget.

For the initial 0.5.0 candidate, outdoor escape requires at least `80` meters
for `10` active real-time seconds. Killing the exact official hunter removes
`35` threat and grants `90` active real-time seconds of recovery. Outdoor or
interior escape removes `15` threat and grants `180` seconds of Recently
Pursued recovery. These are explicit beta-tuning values to validate and refine
in the consolidated 0.8.3 in-game pass.

The reviewed universal profile is the tier-one native Wyrdspirit
`Spec_EnemyMonster_T1_Wyrdspirit`
(`[843643575fa01ba4292e60afb9291fea]`). It is treated as a reviewed universal
Wyrd creature for supported outdoor Wyrdnight maps and is safe for the initial
level floor. Eyes directly places encounter members through
`BaseLocationSpawner.VerifyPosition` and `LocationTemplate.SpawnLocation`,
marks every volatile runtime Location not saved, and owns only the exact primary
actor's hunt lifecycle.

### Curated regional roster

| Profile | Native identity | Region | Minimum level | Tier | Cost | Role |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Wyrdspirit | `Spec_EnemyMonster_T1_Wyrdspirit` | reviewed universal | 1 | 1 | 8 | primary, sidecar, cluster |
| Redcap | `Spec_EnemyMonster_T1_Redcap` | Horns of the South | 4 | 1 | 10 | primary, sidecar |
| Corpse Eater | `Spec_EnemyMonster_T1_CorpseEater` | Horns of the South | 7 | 1 | 12 | primary, sidecar |
| Sharg | `Spec_EnemyMonster_T2_ShargHoS` | Horns of the South | 12 | 2 | 16 | primary, rare sidecar |
| Ogre | `Spec_EnemyMonster_T3_Ogre` | Horns of the South | 20 | 3 | 24 | solo primary |
| Corpse Eater | `Spec_EnemyMonster_T3_CorpseEater_Cuanacht` | Cuanacht | 15 | 3 | 16 | primary, sidecar |
| Mistling | `Spec_EnemyMonster_T3_Mistling_Cuanacht` | Cuanacht | 18 | 3 | 18 | primary, sidecar |
| Sharg | `Spec_EnemyMonster_T4_ShargCuanacht` | Cuanacht | 22 | 4 | 22 | primary |
| Ogre | `Spec_EnemyMonster_T4_Ogre_Cuanacht` | Cuanacht | 26 | 4 | 28 | solo primary |
| Redcap | `Spec_EnemyMonster_T4_Redcap_Forlorn` | Forlorn | 22 | 4 | 18 | primary, sidecar |
| Mistling | `Spec_EnemyMonster_T4_Mistling_Forlorn` | Forlorn | 26 | 4 | 24 | primary |
| Corpse Eater | `Spec_EnemyMonster_T5_CorpseEater_Forlorn` | Forlorn | 30 | 5 | 28 | primary |
| Wyrdspawn | `Spec_EnemyMonster_T5_Wyrdspawn` | Sarras | 28 | 5 | 26 | primary |
| Greater Wyrdspawn | `Spec_EnemyMonster_T6_Wyrdspawn` | Sarras | 34 | 6 | 32 | primary |
| Wyrdheir | `Spec_EnemyMonster_T6_Wyrdheir` | Sarras | 36 | 6 | 34 | solo primary |

The exact supported open-world scene names are `CampaignMap_HOS`,
`CampaignMap_Cuanacht`, `CampaignMap_Forlorn`, and `CampaignMap_Sarras`.
Unknown names and empty regional pools fail closed. Every regional entry uses a
standard shipped location template; elite, friendly, summon, boss, challenge,
trial, story, and custom variants remain excluded. Wyrdspirit is the only
profile allowed to cluster, and every regional profile has a one-copy limit.

Threat changes weights smoothly; it never unlocks a profile by itself. Level,
region, session-failure state, safety flags, and budget are hard
filters. Player levels below `8` are capped at one member, levels `8` through
`14` at two, and levels `15` or higher at three, after which the configured
preset cap, profile cap, and budget can reduce the result further. Three failed
placements from the same template reject it for the rest of the session.

## Gameplay presets

The config selector contains `Custom` plus three one-shot templates. Applying a
preset writes its gameplay tuning and returns the selector to `Custom`, allowing
later individual edits.

- **Uneasy Night:** restrained growth, long recovery, low danger budget, mostly
  solo encounters, and rare packs.
- **Watchful Night:** recommended default; sustained tension, spaced meaningful
  encounters, and mixed packs primarily at elevated threat.
- **Cursed Night:** faster escalation, shorter recovery, larger danger budget,
  earlier strong-enemy weighting, and substantially more pack activity.

Presets modify only gameplay tuning. They never overwrite HUD, GFT, boundary,
diagnostic, or accessibility preferences.

The 0.6.0 one-shot values are:

| Setting | Uneasy Night | Watchful Night | Cursed Night |
| --- | ---: | ---: | ---: |
| Passive threat/night | 14 | 20 | 28 |
| Sprint threat/minute | 3 | 4 | 5.5 |
| Combat threat/window | 1.5 | 2 | 3 |
| Wyrd kill threat | 3 | 5 | 7 |
| Base danger budget | 22 | 30 | 42 |
| Long-night scale/cap | 0.25 / 0.5 | 0.35 / 0.75 | 0.45 / 1.0 |
| Base/threat/progress hazard | 0.005 / 0.28 / 0.05 | 0.01 / 0.42 / 0.08 | 0.02 / 0.58 / 0.12 |
| Hazard target | 1.05-1.35 | 0.85-1.15 | 0.70-0.95 |
| Warning seconds | 8 | 6 | 4 |
| Maximum pack / sidecar chance | 1 / 0 | 2 / 0.55 | 3 / 0.8 |
| Kill / escape / failed recovery | 120 / 240 / 45 | 90 / 180 / 30 | 60 / 120 / 20 |

## Threat meter

Eyes in the Dark owns creation, updates, visibility, and cleanup of the Wyrd
Threat meter.

- Always visible during an outdoor Wyrdnight, including while protected.
- Hidden during daylight, indoors, loading, title screens, and when no playable
  hero exists.
- Default placement is above the vanilla Hero HUD.
- Default presentation is a `#B878FF` bar without an exact number.
- RGB color, exact value display, and layout offsets are configurable. Standalone vanilla-HUD
  placement adds an internal +9, -9 baseline while the exposed adjustments
  remain 0, 0; Glorious UI placement does not use the standalone baseline.
- Glorious UI may request placement below its bars through a small versioned
  Eyes in the Dark HUD API. Eyes in the Dark remains the sole meter owner.
- If the Glorious layout request disappears or fails, the meter returns to its
  default position.

The existing Wyrd Threat meter implementation should be removed from Glorious
UI when this integration replaces it.

## Wyrd boundary presentation

The mod incorporates the standalone Purple Wyrdness presentation directly,
using a purple hue while retaining vanilla-adjacent presentation defaults:

- color `#B878FF`;
- HDR intensity `271.529`, matching the brightest channel of the shipped edge;
- visual radius `32`, matching vanilla;
- thickness `0.25`, matching vanilla;
- threat reactivity `Disabled`, matching vanilla's static presentation.

Planned settings:

- enable boundary customization;
- boundary color;
- HDR intensity;
- visual radius;
- thickness;
- threat reactivity mode;
- minimum and maximum threat intensity multipliers;
- maximum threat thickness multiplier.

Threat reactivity may subtly brighten and thicken the boundary. It must not
change the radius dynamically because radius is visual-only and could imply a
different protected gameplay area. Boundary settings never change protection,
mask intensity, or other gameplay rules.

## Grail Floating Text

Grail Floating Text is an optional presentation integration. It reports
meaningful transitions and must not duplicate every meter change.

Use a separate enable toggle and three directly selected notification presets:

- **Minimal:** committed hunts and hunt outcomes.
- **Atmospheric:** recommended default; night boundaries, upward threat-stage
  changes, committed hunts, and outcomes.
- **Detailed:** also includes downward stages, protection changes, major threat
  surges, and an optional exact value.

Each event category uses a built-in randomized text pool with immediate-repeat
prevention. Initial categories are night begin, night end, threat rise, high
threat, hunt committed, hunter killed, and player escaped. Do not notify for
every threat point or ordinary action.

Use the shared Wyrd icon and separate collapse lanes:

- `eyes-in-the-dark-night`
- `eyes-in-the-dark-threat`
- `eyes-in-the-dark-hunt`

Status messages use normal Status presentation. A committed hunt uses Warning
presentation. Text such as "Something has found you" appears only after an
encounter is committed, never merely because threat is high.

If GFT is absent or its API is unavailable, gameplay and the meter continue
normally.

### Diagnostics presentation

When the existing Diagnostics setting is enabled and GFT is available, Eyes in
the Dark also emits concise behind-the-scenes summaries as GFT System
notifications. This is diagnostic output, independent of the selected Minimal,
Atmospheric, or Detailed gameplay-notification preset. Diagnostics off emits no
diagnostic GFT messages.

Use System style and category, Low priority, short duration, immediate delivery,
and the single `eyes-in-the-dark-diagnostics` collapse lane. Do not defer these
messages through loading or menus because a stale diagnostic is misleading.
Suppress identical repeats and apply an active-real-time cooldown that stops
while paused.

Useful summaries are limited to meaningful commits and transitions:

- director activation or suspension with the reason;
- night initialization with progress, threat, and calculated danger budget;
- a throttled threat-source aggregate with delta, resulting threat, and stage;
- a director evaluation summary with hazard, randomized target, and final skip
  reason;
- hard-filter reasons, final candidate weights, encounter composition, cost,
  budget, or placement failure;
- encounter commitment and budget change;
- kill, escape, lost-target, dawn, death, or load resolution and resulting
  threat/recovery state;
- one-time optional-integration failures affecting GFT, the meter, or boundary.

Examples should remain compact: `EITD - Threat +2 combat -> 31 (Hunted)` and
`EITD - Hunt skipped: protected area`. Exact numbers and internal reasons are
appropriate here because Diagnostics is explicit testing output. Do not emit a
message for every state poll, threat point, filtered candidate, movement tick,
or repeated failed roll. The BepInEx log retains the fuller diagnostic record.
If the GFT bridge fails, log that failure once, stop attempting diagnostic GFT
delivery, and leave gameplay, normal logging, HUD, and boundary behavior intact.

## Compatibility boundary

Wyrd Hunt is incompatible with Eyes in the Dark. [Custom Timescale](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/76)
is also incompatible because its `DeathWrench.TimeMod` plugin modifies the same
`GameRealTime` weather rate Eyes now owns. Use only GFT's existing soft
compatibility-notice convention:

- GFT detects the exact loaded pair.
- GFT shows one restrained main-menu System notice stating that Wyrd Hunt is
  flagged as incompatible with Eyes in the Dark.
- GFT records the match in the BepInEx log.
- GFT does not disable, unload, reconfigure, or otherwise alter either plugin.
- Eyes in the Dark contains no separate compatibility scanner or automatic
  conflict behavior.

For the clock conflict, GFT matches plugin GUID `DeathWrench.TimeMod` with
assembly fallback `TimeMod` and shows exactly: `Custom Timescale is flagged as
incompatible with Eyes in the Dark.` It never disables or modifies either mod.

Do not add migration, detection, warnings, or compatibility code for the
retired companion addon or standalone boundary visual plugin.

## Save and transition behavior

The first release should not write custom save data. When loading during a Wyrd
Night, reconstruct a modest minimum threat from current night progress, grant a
short load grace period, and begin without restoring an active hunt. This
reduces reload-to-zero abuse without taking ownership of the save format.

Loading, portals, fast travel, scene changes, and missing hero state must never
produce catch-up threat, instant encounters, or spent danger budget.

## Configuration organization

When gameplay config is introduced, keep it grouped and comprehensible:

1. Core
2. World Timescale
2. Gameplay Preset (stable existing section name)
3. Wyrd Threat
4. Encounters
5. Enemy Eligibility
6. Mixed Encounters
7. Threat Meter
8. Wyrd Boundary
9. Grail Floating Text
10. Diagnostics
11. Import Previous Settings

Follow the repository config schema and previous-settings recovery contract as
soon as the first config entry is bound. Keep preset triggers and derived
status entries permanently excluded from recovery.

## Implementation roadmap

The milestone plan from the 0.1.0 scaffold through the hardened 0.8.3
beta is maintained in [ROADMAP.md](ROADMAP.md).

Do not begin with rewards, broad AI control, generalized extension frameworks,
or large compatibility layers. Add a public API only when a concrete integration
requires each member.
