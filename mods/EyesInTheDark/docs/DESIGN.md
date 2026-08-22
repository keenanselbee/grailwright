# Eyes in the Dark - Wyrdnight Overhaul

## Status

This is the living design document for `EyesInTheDark`. Version `1.3.9` is the
current implementation and acceptance target.

## Product identity

- Display name: **Eyes in the Dark - Wyrdnight Overhaul**
- Package and folder: `EyesInTheDark`
- DLL: `EyesInTheDark.dll`
- Plugin GUID: `ks.tgfoa.eyes-in-the-dark`
- Central resource: **Wyrd Threat**
- Gameplay presets: **Uneasy Night**, **Watchful Night**, and **Cursed Night**
- Default gameplay tuning: **Watchful Night**
- Reference world clock: **60-minute day / dynamic 6-to-12-minute night**

The project is inspired by Wyrd Hunt. Its defining difference is that Wyrd
Threat controls a probabilistic night director rather than a fixed sequence of
threshold-triggered spawns.

## Design goals

1. Make outdoor Wyrdnights tense without sending one enemy after another
   until dawn.
2. Own a configurable dynamic world clock balanced around a 60-minute day, a
   6-minute unnoticed night, and a 12-minute maximum-threat night.
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

During those quiet stretches, the separate ambient-stalker lane may place one
volatile map-native creature outside the camera. It watches, follows, or
retreats without becoming an official hunt. Seeing the watcher is atmosphere;
attacking it or allowing Wyrd Threat to reach its hidden aggression value turns
it into an ordinary hostile enemy.

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

### Rest presentation boundary

Eyes owns Wyrdnight rest availability and gameplay safety, not the rest-clock
layout or time formatting. `ShowWyrdnightRestAvailability` controls whether the
fireplace REST button reflects Eyes' active-night restrictions. The final
accepted-rest guard and interruption policy remain authoritative even when
that presentation setting is disabled.

Glorious UI can independently provide its toggleable noon-at-top rest clock,
popup time format, and quick-menu time format. Neither mod calls into the other
for this behavior, and their UI ownership does not overlap.

Eyes owns the `GameRealTime` world-weather rate when both its master switch and
`EnableDynamicTimescale` are enabled:

- `DayMinutes = 60` controls approximate real daylight duration;
- `BaseNightMinutes = 6` is the approximate duration at zero threat and remains
  close to the game's approximately `6.2`-minute Wyrdnight;
- `MaximumThreatNightMinutes = 12` is the approximate duration at 100 threat;
- live Wyrdnight duration interpolates linearly between those endpoints using
  current Wyrd Threat;
- all three gameplay presets share these clock settings.

This creates a readable consequence: hiding keeps threat low and lets darkness
pass quickly, while attracting attention draws the Wyrdnight out. The
controller converts the requested phase minutes to the complete-cycle value
expected by `GameRealTime.SetWeatherDayDuration`. It reapplies on clock, phase,
enabled-state, or config changes and whenever threat changes the requested
night length by at least `0.05` minute. It never writes Unity gameplay
`Time.timeScale`, so combat, animations, effects, and pause behavior are
unaffected. On disable or plugin release, Eyes restores the native duration
only when the current rate still equals its last applied value.

The Diagnostics tab provides a separate fixed world-clock testing override.
When `EnableTimescaleOverride` is enabled while Eyes itself is enabled,
`TimescaleOverrideMultiplier` replaces both dynamic phase targets with a
constant `0.01`-to-`5.0` multiplier of the native world clock. `1` is native
speed, `2` is twice as fast, and `0.5` is half speed. It works even when
`EnableDynamicTimescale` is off, never changes Unity gameplay `Time.timeScale`,
and retains the normal safe-restoration and external-owner protections.

### Extended-night scaling

- Passive threat is normalized by the percentage of the current Wyrdnight
  that elapses.
- Meaningful player actions add fixed, independently throttled threat.
- Every preset has a base nightly danger budget.
- Longer nights receive a capped, sublinear danger-budget bonus rather than a
  linear multiplier.
- The initial Watchful Night tuning uses a base budget of `30`, a long-night
  bonus scale of `0.35`, and a maximum bonus fraction of `0.75`.
- Let `m` be the configured maximum-threat night duration relative to the
  game's native configured night duration. Calculate
  `bonus = min(maximumBonus, max(0, sqrt(m) - 1) * bonusScale)` and
  `nightBudget = baseBudget * (1 + bonus)`.
- Derive `m` from `MaximumThreatNightMinutes` and the native day duration. Do
  not use Unity `Time.timeScale` or another mod's settings.
- Preset-specific budget bases and caps replace these initial Watchful values
  when the gameplay presets are implemented in 0.6.0.
- Minimum active-real-time recovery prevents compressed vanilla nights from
  producing back-to-back hunts.

At the default 12-minute maximum-threat night, the duration multiplier is
about `1.94`; Watchful therefore receives about a 14 percent bonus, not a
linear 94 percent increase. The budget is merely capacity: a quiet six-minute
night does not force the director to spend it.

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
- releasing an arrow or other projectile, even when it misses;
- successfully completing a spell cast, even when it hits nothing;
- confirmed melee impacts against scenery or non-damageable objects, limited
  to one contribution per attack;
- killing Wyrd creatures;
- looting corpses or containers while exposed;
- direct world pickup or stealing while exposed;
- completing a Blood Magic Expansion corpse ritual while exposed, scaled by
  the consumed corpse's normalized quality;
- powerful or noisy magic through the confirmed successful-cast event.

Every repeatable source requires a cooldown, aggregation window, or diminishing
return. Released projectiles and completed spells share the capped combat
window with damage and environment impacts. Failed or canceled casts and empty
melee swings add nothing; repeatedly moving the same item must not farm threat.

Normal combat can raise threat, but the director should defer a new hunt until
the unrelated combat ends. Active hunts suspend additional encounter rolls.

### Threat reduction

- Killing the official hunter provides the greatest immediate reduction.
- Escaping provides a smaller reduction and a longer `Recently Pursued`
  recovery state.
- Protected outdoor areas slowly reduce threat at the configured
  active-real-time rate and never add passive-exposure threat.
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
states. During an official outdoor hunt, Eyes may temporarily recruit nearby
eligible ordinary guards to assist the Hero against exact official hunters. It does
not own navigation, perception, faction, broad guard behavior, or general AI.

## Ambient stalkers

Ambient stalkers are a dedicated director and runtime lane, not lightweight
official hunts. At most one ambient stalker or official warning/hunt may be
owned at a time. Ambient selection and placement never read or spend the hunt
danger budget, and killing or escaping a stalker never grants official-hunter
threat relief.

### Eligibility and roster

New ambient placement requires a valid outdoor Wyrdnight, exposure, no Wyrd
protection, no unrelated Hero combat, a supported exterior map, an available
encounter lane, and an advancing active clock. Player level, exact region,
explicit template review, repetition penalties, and three-failure session
rejection remain hard gates. A confirmed placement clears that profile's prior
failure count. Unknown regions and empty pools fail closed.

Two disjoint bands are used:

- **Ordinary, below 50 threat:** 26 reviewed small-to-medium or humanoid
  creatures. Wyrdspirit is the sole universal level-1 fallback. Regional
  candidates include Grindylow, Redcap, Corpse Eater, Mistling, Drowner,
  Slugholder, Bonemask, Frostbitten, Drowned crew, Finbled, Tadpole, Wailcap,
  and Tidewraith profiles.
- **High pressure, 50 to below 75 threat:** seven Sharg, Lost Knight, Finbled
  Heavy, and Drowned Knight profiles. This band additionally requires
  `AllowEliteEnemies`; Uneasy and Watchful disable it and Cursed enables it.
- At 75 threat and above, no new ambient stalker is selected. The official
  hunt director remains the high-threat attack lane.

Flamegobblers, swarms, skeletons, Ogres, Barnaclators, Nuckelavees, bosses,
friendlies, summons, challenge/trial/story/custom variants, and other unsafe or
poor stalking candidates are excluded from this lane even when they remain
valid official-hunt profiles.

### Cooldown and hidden aggression

Each ambient resolution schedules a randomized active-real-time cooldown. The
Watchful defaults are 55 to 165 seconds at zero threat; the upper bound shrinks
linearly to 70 seconds by 50 threat and never falls below the configured
minimum. A rising threat value clamps an already scheduled remaining cooldown
to the new live upper bound. Loading, menus, pauses, protection, unrelated
combat, and an occupied encounter lane do not advance it.

Every confirmed stalker receives a hidden per-instance aggression value:

- ordinary: 45 through 55 threat;
- high pressure: 70 through 80 threat.

The player-facing meter and atmospheric GFT text never reveal this value.
Diagnostics may report it. Reaching it escalates the exact actor. Damage from
the exact Hero escalates immediately before damage resolution and applies the
configured provocation threat once per stalker; later hits cannot repeat that
threat input.

### Native movement and passive guards

A passive stalker retains its native faction, perception, combat attachment,
and actor template. Eyes temporarily owns only:

- `BlockEnterCombatMarker`, preventing premature native combat;
- `HideEnemyFromPlayer`, suppressing enemy HUD/compass presentation;
- the exact actor's current native movement state;
- an exact pre-damage listener for Hero provocation.

It begins in native `Observe`, changes to `FollowMovement` after a randomized
watch interval, and returns to Observe at a 20-metre buffer before crowding the
Hero. Deliberate
pursuit requires the Hero to face the stalker, move quickly, and measurably
close distance across a sample window; proximity alone is insufficient. That
transition uses native `Flee`, then returns to Observe after gaining distance
or exhausting the bounded flee interval. A completed flee has a five-active-
second rearm delay. If the Hero closes within 8 metres while the stalker is
still fleeing, the exact actor releases its passive guards and turns hostile;
this defensive escalation does not award attack-only provocation threat.

On escalation Eyes disposes its damage listener, releases its owned movement
state and both passive guards, and calls native `EnterCombatWith(hero)`. It may
reassert exact Hero acquisition at most three times at half-second intervals.
Eyes never changes factions, guards, global perception, or general AI.

### Placement, camera lifecycle, and volatility

Placement samples behind or well outside the current camera, then requires all
of the following before confirmation:

- a reviewed exact `LocationTemplate` identity;
- native `BaseLocationSpawner.VerifyPosition` walkable placement;
- a connected A* path between Hero and candidate position;
- no Wyrd repeller at the verified position;
- configured minimum/maximum distance bounds;
- the verified point and initialized renderer bounds outside an expanded
  camera margin.

The spawned Location is `MarkedNotSaved`. Visibility uses every renderer's
bounds corners and center, with a short continuous on-screen confirmation
before a sighting is reported. A passive actor may be discarded only when it
has been continuously outside the camera for the configured interval, is at
least the configured distance away, and was previously seen or has reached its
bounded ambient lifetime. A hostile stalker never disappears or releases its
lane because of distance. Death, native discard, dawn, feature shutdown,
gameplay load, an interior, or a scene transition may still perform required
volatile cleanup.

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

The catalog contains `50` reviewed profiles: one universal fallback and `49`
map-specific entries. It was cross-checked offline against the shipped
Addressables catalog, location-spec bundle, open-world scene references, and
the Steel and Bone NPC-template extraction. The extraction is research input;
Eyes retains no runtime dependency on it and never discovers templates
automatically.

| Horns of the South | Native identity | Level | Cost | Role |
| --- | --- | ---: | ---: | --- |
| Flamegobbler | `Spec_EnemyMonster_T1_Flamegobbler` | 4 | 9 | primary, sidecar |
| Grindylow | `Spec_EnemyMonster_T1_Grindylow` | 5 | 10 | primary, sidecar |
| Redcap | `Spec_EnemyMonster_T1_Redcap` | 4 | 10 | primary, sidecar |
| Corpse Eater | `Spec_EnemyMonster_T1_CorpseEater` | 7 | 12 | primary, sidecar |
| Wandering Dead | `Spec_EnemyZombie_T1_Classic` | 6 | 10 | primary, sidecar |
| Drowner | `Spec_EnemyZombie_T1_Drowner` | 7 | 11 | primary, sidecar |
| Restless Skeleton | `Spec_EnemySkeleton_Melee1H` | 8 | 13 | primary, sidecar |
| Mistling | `Spec_EnemyMonster_T2_Mistling_Hos` | 10 | 14 | primary, sidecar |
| Wyrd Bee Swarm | `Spec_EnemyMonster_T2_Swarm_Bees` | 10 | 12 | primary, sidecar |
| Sharg | `Spec_EnemyMonster_T2_ShargHoS` | 15 | 18 | elite primary |
| Ogre | `Spec_EnemyMonster_T3_Ogre` | 20 | 24 | solo primary |

| Cuanacht | Native identity | Level | Cost | Role |
| --- | --- | ---: | ---: | --- |
| Corpse Eater | `Spec_EnemyMonster_T3_CorpseEater_Cuanacht` | 15 | 16 | primary, sidecar |
| Flamegobbler | `Spec_EnemyMonster_T3_FlamegobblerCuanacht` | 15 | 16 | primary, sidecar |
| Grindylow | `Spec_EnemyMonster_T3_Grindylow_Cuanacht` | 15 | 18 | primary, sidecar |
| Redcap | `Spec_EnemyMonster_T3_Redcap_Cuanacht` | 15 | 17 | primary, sidecar |
| Cuanacht Dead | `Spec_EnemyZombie_T3_ZombieCuanacht` | 16 | 16 | primary, sidecar |
| Mistling | `Spec_EnemyMonster_T3_Mistling_Cuanacht` | 20 | 18 | primary, sidecar |
| Greatsword Skeleton | `Spec_EnemyMonster_T3_Skeleton2H_Cuanacht` | 20 | 20 | primary, sidecar |
| Drowner | `Spec_EnemyZombie_T3_DrownerCuanacht` | 20 | 20 | primary, sidecar |
| Lost Knight | `Spec_EnemyMonster_T3_LostKnight` | 20 | 22 | primary |
| Slugholder Mage | `Spec_EnemyMonster_T3_SlugholderMage` | 20 | 22 | primary |
| Ogre | `Spec_EnemyMonster_T4_Ogre_Cuanacht` | 26 | 28 | solo primary |
| Sharg | `Spec_EnemyMonster_T4_ShargCuanacht` | 30 | 30 | primary |
| Barnaclator | `Spec_EnemyMonster_T4_Barnaclator` | 30 | 28 | primary |
| Nuckelavee | `Spec_EnemyMonster_T4_Nuckelavee` | 30 | 30 | primary |

| Forlorn | Native identity | Level | Cost | Role |
| --- | --- | ---: | ---: | --- |
| Redcap | `Spec_EnemyMonster_T4_Redcap_Forlorn` | 25 | 20 | primary, sidecar |
| Mistling | `Spec_EnemyMonster_T4_Mistling_Forlorn` | 30 | 24 | primary |
| Bonemask Mage | `Spec_EnemyMonster_T4_Bonemask_Mage` | 30 | 24 | primary |
| Bonemask Warrior | `Spec_EnemyMonster_T4_Bonemask_Melee` | 30 | 25 | primary |
| Forlorn Dead | `Spec_EnemyZombie_T5_ZombieForlorn` | 30 | 28 | primary |
| Corpse Eater | `Spec_EnemyMonster_T5_CorpseEater_Forlorn` | 40 | 28 | primary |
| Frostbitten Warrior | `Spec_EnemyMonster_T5_FrostbittenWarrior_Male` | 40 | 32 | primary |
| Smaller Sharg | `Spec_EnemyMonster_T5_ShargSmallerForlorn` | 40 | 32 | primary |
| Skeleton Archer | `Spec_EnemyMonster_T5_SkeletonArcher` | 40 | 30 | primary |
| Swarm | `Spec_EnemyMonster_T5_Swarm` | 40 | 28 | primary |
| Elite Skeleton | `Spec_EnemyMonster_T6_SkeletonElite` | 50 | 38 | elite primary |
| Alpha Sharg | `Spec_EnemyMonster_T5_ShargForlorn` | 60 | 44 | elite solo primary |

| Sarras | Native identity | Level | Cost | Role |
| --- | --- | ---: | ---: | --- |
| Drowner | `Spec_SoS_EnemyZombie_T3_Drowner` | 25 | 18 | primary, sidecar |
| Drowner Brute | `Spec_SoS_EnemyZombie_T4_Drowner_2H` | 27 | 20 | primary, sidecar |
| Drowned Deckhand | `Spec_SoS_EnemyMonster_T4_DrownedDeckhand` | 28 | 20 | primary, sidecar |
| Drowned Mariner | `Spec_SoS_EnemyMonster_T4_DrownedMariner` | 28 | 22 | primary |
| Finbled Stalker | `Spec_SoS_EnemyMonster_T4_Finbled_Light` | 30 | 24 | primary, sidecar |
| Finbled Javelin Hunter | `Spec_SoS_EnemyMonster_T4_Finbled_JavelinThrower` | 30 | 26 | primary |
| Finbled Heavy | `Spec_SoS_EnemyMonster_T4_Finbled_Heavy` | 30 | 28 | primary |
| Tadpole | `Spec_SoS_EnemyMonster_T4_Tadpole` | 30 | 24 | primary, sidecar |
| Wailcap | `Spec_SoS_EnemyMonster_T4_Wailcap` | 30 | 26 | primary |
| Tidewraith | `Spec_SoS_EnemyMonster_T5_Tidewraith` | 30 | 28 | primary |
| Drowned Knight | `Spec_SoS_EnemyMonster_T6_DrownedKnight` | 35 | 36 | elite primary |
| Drowned Knight Huntress | `Spec_SoS_EnemyMonster_T6_DrownedKnight_Female` | 35 | 36 | elite primary |

Wyrdspirit (`Spec_EnemyMonster_T1_Wyrdspirit`) remains the universal level-1,
cost-8 primary/sidecar/cluster fallback.

The exact supported open-world scene names are `CampaignMap_HOS`,
`CampaignMap_Cuanacht`, `CampaignMap_Forlorn`, and `CampaignMap_Sarras`.
Unknown names and empty regional pools fail closed. Every regional entry uses a
standard shipped location template. Reviewed elites require the explicit
`AllowEliteEnemies` setting and threat strictly greater than `75`; they are
never sidecars. Uneasy and Watchful set this option off, while Cursed sets it
on. Friendly, summon, boss, miniboss, challenge, trial, story, custom, arena,
and hero-summon variants remain excluded regardless of configuration.
Wyrdspirit is the only profile allowed to cluster, and every regional profile
has a one-copy limit.

Threat changes normal-profile weights smoothly. Elite profiles are the one
explicit exception: both the elite setting and threat greater than `75` are
hard eligibility requirements. Level, region, session-failure state, safety
flags, elite policy, and budget are hard filters. Player levels below `8` are
capped at one member, levels `8` through
`14` at two, and levels `15` or higher at three, after which the configured
preset cap, profile cap, and budget can reduce the result further. Three failed
placements from the same template reject it for the rest of the session.

### Regional ambient roster

The explicit roster implements the eligibility and safety rules above. It is
smaller than the official-hunt catalog but deliberately varied by map.

| Region | Ordinary stalkers below 50 | High-pressure stalkers from 50 to below 75 |
| --- | --- | --- |
| Universal | Wyrdspirit | none |
| Horns of the South | Grindylow, Redcap, Corpse Eater, Mistling, Drowner | Sharg |
| Cuanacht | Grindylow, Redcap, Corpse Eater, Mistling, Slugholder Mage, Drowner | Lost Knight, Sharg |
| Forlorn | Redcap, Mistling, Bonemask Mage, Bonemask Warrior, Corpse Eater, Frostbitten Warrior | smaller Sharg |
| Sarras | Drowner, Drowned Deckhand, Drowned Mariner, Finbled Stalker, Finbled Javelin Hunter, Tadpole, Wailcap, Tidewraith | Finbled Heavy, Drowned Knight, Drowned Knight Huntress |

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
| Corpse-drain threat at average quality | 6 | 8 | 11 |
| Base danger budget | 22 | 30 | 42 |
| Long-night scale/cap | 0.25 / 0.5 | 0.35 / 0.75 | 0.45 / 1.0 |
| Base/threat/progress hazard | 0.005 / 0.28 / 0.05 | 0.01 / 0.42 / 0.08 | 0.02 / 0.58 / 0.12 |
| Hazard target | 1.05-1.35 | 0.85-1.15 | 0.70-0.95 |
| Warning seconds | 8 | 6 | 4 |
| Maximum pack / sidecar chance | 1 / 0 | 2 / 0.55 | 3 / 0.8 |
| Elite enemies above 75 threat | off | off | on |
| Ambient cooldown min / max / near-50 max | 75 / 210 / 105 | 55 / 165 / 70 | 40 / 125 / 55 |
| Stalker provocation threat | 4 | 6 | 8 |
| Kill / escape / failed recovery | 120 / 240 / 45 | 90 / 180 / 30 | 60 / 120 / 20 |

## Threat meter

Eyes in the Dark owns creation, updates, visibility, and cleanup of the Wyrd
Threat meter.

- Always visible during an outdoor Wyrdnight, including while protected.
- Hidden during daylight, indoors, loading, title screens, and when no playable
  hero exists.
- Default placement is above the vanilla Hero HUD.
- Default presentation is a `#8032FF` bar that approaches `#FF3028` as threat
  rises, without an exact number.
- Purple and Orange base colors, red targets, brightness, exact value display,
  and layout offsets are configurable. Standalone vanilla-HUD
  placement adds an internal +9, -9 baseline while the exposed adjustments
  remain 0, 0; Glorious UI placement does not use the standalone baseline.
- Glorious UI may request placement below its bars through a small versioned
  Eyes in the Dark HUD API. Eyes in the Dark remains the sole meter owner.
- If the Glorious layout request disappears or fails, the meter returns to its
  default position.
- The meter replaces inherited blue mana artwork with the game-owned neutral
  `MP_Bar_white` sprite and sets its private animated material color to white,
  so the configured palette remains visible without redistributing an asset.
  The artwork remains mirrored horizontally and vertically to match the
  intended Hero HUD layout. The private material reverses the known animated
  shader's speed axes affected by that mirror so its visible movement retains
  the vanilla Hero-bar direction without modifying shared resource-bar
  materials. The private material is destroyed with the meter.

The existing Wyrd Threat meter implementation should be removed from Glorious
UI when this integration replaces it.

## Wyrd boundary presentation

The mod incorporates the standalone Purple Wyrdness presentation directly.
Its default layered presentation draws three visual-only rings:

- color `#B878FF`;
- normalized brightness `1.0`, converted internally through the `271.529`
  vanilla-equivalent HDR baseline;
- near radius/intensity/thickness `10 / 0.05 / 0.25`;
- middle radius/intensity/thickness `20 / 0.05 / 0.25`;
- outer radius/intensity/thickness `30 / 0.05 / 0.25`;
- the shared Wyrd visual threat scale beginning at `0.8`, plus independent
  smooth bounded pulses with a `0.8` default amount.

Settings include:

- enable boundary customization;
- layered or native-style single rendering;
- boundary color;
- normalized boundary brightness from `0` to `3`;
- per-ring visual radius, brightness, and thickness;
- pulse enable, amount from `0` to `1`, and minimum/maximum transition duration.

EITD inserts its owned custom pass beside the native edge only after all three
materials are ready, then disables rather than destroys the native pass. Any
failure or feature shutdown removes the owned pass, releases its materials,
and restores the original native values and enabled state. The world threat
scale and red-shift curve affect ring brightness and hue; organic pulse remains
independent. Threat never changes ring thickness or radius. Boundary settings
never change protection, native mask intensity, or other gameplay rules.

## Wyrdnight environment palette

Eyes fully incorporates Purple Moon Test rather than depending on or loading
its standalone configuration. The integrated runtime owns the visible moon
surface, HDR corona, directional and volumetric moonlight, the full visible
Wyrdnight sky tint, and fueled-bonfire protection-bubble body and border.

Two independent palette choices are available:

- **Purple Wyrdness**, the default, uses the configured purple values;
- **Orange Wyrdness** derives each low-threat hue from the active region's
  original game-owned value instead of hard-coding one orange.

The promoted Purple defaults are moon surface `#3200FF`, tint `0.75`, intensity
`2`; corona `#8000FF`, intensity `2`; moonlight `#7E47FF`, tint `0.9`; full-sky
tint `#401C63`, strength `1`; and protection bubble `#B050FF`, body/border
intensity `1 / 1`.

One palette-aware `WyrdnightBrightness` control defaults to `1`, supports `0`
through `2`, remains independent of threat, and interpolates through the same
visual blend used by natural dusk, dawn, load, and disable transitions. At
`1`, Purple Wyrdness maps to a `1.75` exposure multiplier plus `+0.35 EV`;
Orange Wyrdness maps to the native `1` multiplier and `0 EV`. Other values
scale those palette-specific targets linearly. Automatic and physical-camera
exposure use `compensation * multiplier + EV`; fixed exposure uses
`fixedExposure * multiplier - EV`, matching the active mode's sign convention.
Light Control continues to own its settings and runs before Eyes. Eyes does not
modify HDRP post-exposure, gamma, colors, indirect diffuse lighting, direct
moonlight, reflections, or global volumes.

The world threat-brightness range interpolates linearly from configurable `0.8`
at zero threat to `1.2` at 100 threat. A separate world color-shift control
blends the moon surface, corona, moonlight, protection bubble, and boundary
toward configurable world target `#FF3028`, reaching a default maximum blend
of `0.8`. Configured palette tint strengths remain independent of the
brightness range, so zero threat retains the intended base hue. The full-sky
color is explicitly excluded from color shifting; its selected-color
brightness, not its tint strength, follows the world brightness range. It uses
the sky
material's `_SkyTint` property and does not directly own fog, clouds, terrain
lighting, or reflections.

The threat meter has separately configurable Purple and Orange base and target
colors, constant brightness, and maximum color-shift strengths. It
automatically selects the active palette's settings and uses the game-owned
neutral white mana-bar artwork as its tintable source. Every constant-brightness
point applies `3` times RGB before independent configurable `0.8`-to-`1.2`
meter brightness scaling. The meter and world use the same threat curve but
have separate brightness ranges, target colors, and maximum color shifts.

The authoritative threat value remains immediate for gameplay, the meter,
hunts, notifications, and dynamic Wyrdnight duration. World lighting and the
integrated palette use a separate visual threat value with a configurable
`2`-active-second default half-life. It advances only on the existing
five-times-per-second visual update, so sudden gains such as battlecries ease
into the scene without introducing per-frame calculation or rendering work.
Setting `ThreatVisualSmoothingSeconds` to `0` restores immediate visual changes.

Visual ownership targets the active outdoor Wyrdnight presentation. Natural
dusk in the same stable exterior centers the integrated environment and fueled
protection-bubble fade on nightfall: with the default
`WyrdVisualTransitionSeconds = 60`, it begins 30 real seconds before nightfall,
is halfway blended at the phase boundary, and finishes 30 real seconds after.
The dawn fade begins when the current weather rate
reports that duration remaining in the Wyrdnight and reaches the restored game
presentation at dawn. If threat changes the dynamic night rate during this
window, the blend never moves backward; it holds until the revised countdown
catches up. Pausing freezes both weather progress and the envelope. Wyrdnight
state, threat logic, weather-rate switching, meter visibility, boundary
gameplay, and protection remain exact at the phase boundary.

Resting does not invalidate presentation: opening or accepting the rest popup
keeps the current Wyrdnight palette until the native fade hides any time skip.
Short loading and transition states hold the last confirmed presentation, and
newly available exterior-night rendering is primed from the authoritative world
clock before the normal state poll. Confirmed daylight, interiors, title screen,
disablement, teardown, and visual failures restore immediately. Captured game
values are restored only when the current property still matches Eyes' last
applied value. The system never changes Wyrd protection, bonfire fuel, boundary
masks, gameplay time scale, or weather timing.

After loading into an active Wyrdnight, the authoritative threat value remains
immediate for gameplay, the meter, stage, and dynamic night duration, but its
red-shift contribution to world presentation ramps from zero to the loaded
value over 10 active real-time seconds. This avoids a hard color jump without
delaying or falsifying threat state.

### Runtime performance boundaries

The director, threat, HUD, boundary, and visual target calculations use one
five-times-per-second active-real-time cadence. The native day/night system
rewrites its lighting during every rendered frame, so Eyes' postfix may reapply
the already calculated light and emission values each frame, but it must not
repeat color parsing, threat curves, native-value sampling, or transition math
there. Parsed config colors remain cached until their source text changes.

Environment-lighting refreshes are coalesced to at most four per active second,
except for an immediate forced refresh when Eyes restores native presentation.
The layered boundary performs no custom fullscreen draws while the native edge
has zero visible intensity. Ambient visibility reuses each stalker's renderer
set and performs no steady-state corner-array allocation. The threat meter
rewrites layout only after its anchors, source bars, offsets, or placement mode
change. These limits must preserve phase, threat, meter, and AI response within
one normal 0.2-second update.

## Grail Floating Text

Grail Floating Text is an optional presentation integration. It reports
meaningful transitions and must not duplicate every meter change.

Use a separate enable toggle and three directly selected notification presets:

- **Minimal:** committed hunts and hunt outcomes.
- **Atmospheric:** recommended default; night boundaries, upward threat-stage
  changes, committed hunts and outcomes, plus one restrained message after a
  witnessed stalker disappears. The visual sighting remains implicit.
- **Detailed:** also includes downward stages, protection changes, major threat
  surges, stalker sightings and retreats, and escalation flavor. Optional exact
  threat is appended only to non-stalker text; hidden aggression is never
  exposed.

Each event category uses a built-in randomized text pool with immediate-repeat
prevention. Categories cover night boundaries, meaningful threat-stage changes,
official hunt commitments and outcomes, and the stalker events admitted by the
selected notification preset. Do not notify for every threat point or ordinary
action.

Use the shared Wyrd icon and separate collapse lanes:

- `eyes-in-the-dark-night`
- `eyes-in-the-dark-threat`
- `eyes-in-the-dark-hunt`
- `eyes-in-the-dark-stalker`

All atmospheric Wyrd messages use GFT's Purple color group under Purple
Wyrdness and its Orange group under Orange Wyrdness. This includes committed
hunt warnings; urgency remains represented independently by High priority.
GFT's built-in Wyrdnight and Wyrd-safety messages follow the same live palette.
Text such as "Something has found you" appears only after an encounter is
committed, never merely because threat is high.

If GFT is absent or its API is unavailable, gameplay and the meter continue
normally.

### Optional battlecry integration

Battlecry Voice Tuner may call Eyes' versioned soft API after a successful
player battlecry. Eyes accepts it only for a playable Hero who is exposed
outdoors during an active Wyrdnight. Repeated accepted cries apply the existing
full, half, quarter, and diminishing threat sequence down to its 10 percent
floor; 30 active seconds without an accepted cry restores the full gain.

Atmospheric and Detailed notifications may respond after a randomized two or
three accepted cries, drawing from the existing seven-line pool with
immediate-repeat prevention. The response lane has its own configurable
15-active-second default cooldown and does not change the battlecry action
cooldown. Minimal remains silent. The integration stays optional and must not
create a hard dependency in either direction.

### Optional corpse-drain integration

Blood Magic Expansion may call Eyes' versioned soft API after a corpse ritual
completes successfully. It reports only normalized corpse quality; Eyes remains
authoritative for activity eligibility and threat tuning. The default Watchful
Night value is 8 threat at average quality, with a linear 0.5x-to-1.5x quality
multiplier producing 4 to 12 threat. Uneasy uses 6 at average quality and
Cursed uses 11.

Daytime, indoor, protected, paused, loading-grace, interrupted, and failed
rituals add no threat. The one-shot consumed-corpse boundary prevents repeated
farming, so this source has no additional diminishing-return window.

### Rest and slept-through transitions

Eyes filters the native `HeroDevelopment.CanRest` result without writing or
owning that property. Native denials remain authoritative. A fueled protective
boundary always bypasses Eyes' additional active-night gate and interruption
risk.

The gameplay presets own the default unprotected-rest model:

- Uneasy Night allows rest during an active Wyrdnight and adds no Eyes
  interruption risk. Native interruption logic still applies.
- Watchful Night allows active-Wyrdnight rest and interpolates Eyes risk from
  45 percent at zero threat to 75 percent at maximum threat.
- Cursed Night prevents beginning new unprotected rest after Wyrdnight is
  active and uses 80 to 100 percent risk for rest begun before nightfall. The
  individual gate and chance settings remain editable after applying a preset.

Eyes patches the native time-skip interruption check. A native interruption
wins without modification. Otherwise Eyes uses one chance roll and one hidden
exposure threshold per Wyrdnight. Only accepted unprotected rest accumulates
the fraction of the Wyrdnight actually slept, so canceling the menu adds no
exposure and repeated short rests cannot create fresh rolls. A successful Eyes
interruption returns through the native wake presentation, then requests one
official hunt after the rest transition is stable. Normal region, player level,
elite, encounter budget, atomic placement, and zero-cost failure rules remain
authoritative. Native interruptions never create a duplicate Eyes hunt.

Any interruption that occurs during unprotected Wyrdnight exposure marks the
Hero disturbed and prevents further unprotected rest until dawn. Protected
rest remains available. Rest begun during daylight may cross nightfall on every
preset. Cursed makes that attempt highly likely, but not absolutely guaranteed,
to be interrupted before dawn.

`ShowWyrdnightRestAvailability` is enabled by default. When enabled, Eyes
applies its native greyed-out REST-button availability. When disabled, the
CanRest presentation filter returns the native result. The final
`RestPopupUI.Rest` guard still enforces gameplay policy silently.

Once allowed rest begins, atmosphere is suppressed until the first stable
post-rest context. Eyes then adopts the final daylight or Wyrdnight phase and
protection state without replaying night-begin, night-end, stage, protection,
hunt, or stalker flavor that occurred while the Hero was asleep. Diagnostics
may emit one concise reconciliation summary after waking. Canceling the Rest
popup without resting does not activate this suppression.

### Diagnostics presentation

When the existing Diagnostics setting is enabled and GFT is available, Eyes in
the Dark also emits concise behind-the-scenes summaries as GFT System
notifications. This is diagnostic output, independent of the selected Minimal,
Atmospheric, or Detailed gameplay-notification preset. Diagnostics off emits no
diagnostic GFT messages.

The Diagnostics tab also owns an explicit testing override. When
`EnableThreatOverride` is enabled during a valid Wyrdnight,
`ThreatOverrideValue` forces the authoritative 0-to-100 threat value. Natural
gain, protected/interior decay, hunter relief, and activity inputs are
suppressed while it is active. The forced value deliberately drives every
normal consumer, including the meter, visuals, dynamic night duration, ambient
stalkers, and official hunts. Dawn still resets threat. Both threat-override
settings are excluded from schema recovery/import. The world-timescale
override and multiplier are excluded for the same reason, so testing controls
cannot be silently re-enabled after configuration regeneration.

Use System style and category, Low priority, short duration, immediate delivery,
and the single `eyes-in-the-dark-diagnostics` collapse lane. Do not defer these
messages through loading or menus because a stale diagnostic is misleading.
Eyes diagnostic GFT is suppressed at the title screen, during loading, and
whenever no playable Hero exists; GFT's own compatibility notices remain
independent. Suppress identical repeats and apply an active-real-time cooldown
that stops while paused.

Useful summaries are limited to meaningful commits and transitions:

- director activation or suspension with the reason;
- night initialization with progress, threat, and calculated danger budget;
- a throttled threat-source aggregate with delta, resulting threat, and stage;
- a director evaluation summary with hazard, randomized target, and final skip
  reason;
- hard-filter reasons, final candidate weights, encounter composition, cost,
  budget, or placement failure;
- ambient cooldown/band selection, hidden threshold, off-camera/path placement,
  sighting, flee transition, escalation trigger, and zero-budget resolution;
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

FoA Mod Manager uses display-only metadata to present a concise primary path
while stable BepInEx section and key names remain unchanged except where a
schema change explicitly replaces an unsafe or unreadable setting.

Primary sections appear first:

1. General: master switch, one-shot gameplay preset, ambient and elite toggles,
   Wyrdnight REST-button presentation, and rest rule.
2. World Clock: dynamic ownership plus day, quiet-night, and maximum-threat
   durations in real minutes.
3. HUD, Boundary Appearance, Wyrdnight Appearance, and Notifications.

Detailed controls follow in clearly labeled Advanced sections for threat
tuning, hunt pacing/composition/outcomes, stalker tuning, boundary tuning,
visual layers, and diagnostics. Import Previous Settings remains last. Labels
state seconds, minutes, and metres directly and avoid internal terms such as
hazard, sidecar, and raw HDR intensity.

The one-shot gameplay selector explicitly returns to Custom after applying a
template and identifies Watchful Night as recommended. Normalized
`BoundaryBrightness = 1.0` maps to the preserved vanilla-equivalent HDR
baseline; the retired raw `BoundaryHdrIntensity` value is not migrated.

Follow the repository config schema and previous-settings recovery contract as
soon as the first config entry is bound. Keep preset triggers and derived
status entries permanently excluded from recovery.

## Implementation roadmap

The milestone plan from the 0.1.0 scaffold through the native-roster 0.9.0
beta is maintained in [ROADMAP.md](ROADMAP.md).

Do not begin with rewards, broad AI control, generalized extension frameworks,
or large compatibility layers. Add a public API only when a concrete integration
requires each member.
