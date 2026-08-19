# Steel and Bone Design Notes

Steel and Bone should be a lightweight but impactful difficulty mod. It enhances vanilla Tainted Grail combat by making the existing damage pipeline more legible and tactical, then adds small preset-driven pressures through existing game systems. It should not replace enemy AI, rewrite encounters, flatten the vanilla build system, or turn every fight into a puzzle lock.

The goal is simple: the player should look at an enemy, look at the weapon or spell in hand, and have a reason to switch.

This is the living implementation and tuning spec. Keep detailed enemy facts in [steel-and-bone-enemies.md](steel-and-bone-enemies.md), and use the reports in [research/](research/) as supporting evidence.

## Design Goals

| Goal | Meaning |
|---|---|
| Respect vanilla combat | Work through the game's existing damage processing, damage subtypes, status damage, target names, vanilla target multipliers, template metadata, and configuration. Do not rebalance the whole game. |
| Make material identity matter | Bone, flesh, armor, stone, spirits, Wyrd, plants, and sea creatures should not all answer the same weapon. |
| Make physical damage types necessary | Slashing, piercing, and bludgeoning should each have real targets. Higher presets should make wrong-tool play more expensive without adding preset-exclusive matchups. |
| Give magic clear lanes | Blood, poison, bleed, Wyrdness, Fire, Cold, Electric, and Wet should have strengths and bad matchups where those tags can be detected. Holy or silver should only be added if item, effect, or skill text exposes reliable terms. |
| Preserve ordinary builds | Tempered, Hardened, and Crucible should use the same rule table. The difference is how strongly the rules pull away from neutral. |
| Keep feedback readable | If damage changes, the player should get a clear visual outcome signal, not just a hidden multiplier. |
| Keep implementation narrow | Prefer event-driven hooks, model-owned stat tweaks, small preset multipliers, rule-table expansion, and compact feedback. Avoid broad stateful rewrites when the game already exposes a focused multiplier or getter. |

## Game-File Ground Truth

These notes are based on local Tainted Grail 1.25 files and the current Steel and Bone 3.4.3 source. The global difficulty contract is documented separately in [steel-and-bone-3.0-difficulty.md](steel-and-bone-3.0-difficulty.md).

| Evidence | Confirmed finding | Design consequence |
|---|---|---|
| `TG.Main.dll` `DamageSubType` | Native subtypes are `Default`, `Pure`, `Wyrdness`, `GenericPhysical`, `Slashing`, `Piercing`, `Bludgeoning`, `GenericMagical`, `Fire`, `Cold`, `Poison`, `Electric`, and `Wet`. | Use the engine names `Cold` and `Electric`, not guessed names like frost or shock. Treat holy/silver as optional text or item-term rules unless a reliable subtype-like signal is found. |
| `TG.Main.dll` `StatusDamageType` | Native status damage types are `Default`, `Burn`, `Breath`, `Poison`, and `Bleed`. | Bleed and Poison are good current hooks. Burn should be considered separately from Fire subtype if the runtime damage exposes it. |
| `TG.Main.dll` `NpcType` | Native NPC tiers are `Critter`, `Trash`, `Normal`, `Elite`, `MiniBoss`, `Boss`, and `HeroSummon`. | Elite and boss handling can use real runtime metadata if reachable instead of named-enemy guesswork. |
| `HealthElement.OnDamage` and `DamageTypeDataBase.CalculateMultiplier` | Vanilla applies armor and `DamageReceivedMultiplierData` before `ApplyDamageModifiers`. | Steel and Bone's current hook layers after vanilla subtype resistances/weaknesses. Vanilla amplification must adjust by ratio, while custom rules remain overlays rather than replacements. |
| `DamageReceivedMultiplierDataBase` | Vanilla multipliers can be below or above `1.0`, and generic physical/magical multipliers apply to their subtype families. | Weaknesses are native to the game model. Steel and Bone supports multipliers above `1.0` instead of staying resistance-only. |
| `Damage.CanBeReducedByArmor`, `Hero.TotalArmor`, and `NpcElement.TotalArmor` | Armor reduction is global for the target; the subtype parameter is ignored by both hero and NPC armor. | Do not implement material identity by rewriting armor. Use subtype rules. Also keep `DamageSubType.Piercing` distinct from `DamageParameters.Piercing()` and `ArmorPenetration`. |
| `ItemStatsAttachment` and `DamageTypeData` | Items and damage can carry subtype parts. | Adding Fire, Cold, Electric, and Wet detection should be a small extension of the current `DamageHasSubtype(...)` pattern. |
| `DamageTypeDataBase` and `Item` weapon flags | Physical hit sources default to `GenericPhysical` unless a more specific subtype is present. Items expose weapon identity such as `IsSword`, `IsAxe`, `IsDagger`, `IsPolearm`, `IsRanged`, `IsArrow`, and `IsBlunt`. | Steel and Bone should treat native subtypes as authoritative, then infer slash/pierce/blunt from item identity only when a physical hit is otherwise generic. |
| Addressable `NpcTemplate` assets | Templates expose `damageReceivedMultipliers`, `_abstractTypes`, `tags`, `surfaceType`, `level`, `npcType`, and status immunities. | Target classification should eventually prefer template metadata over display-name text while keeping term fallback for compatibility. |
| Vanilla difficulty data | Vanilla difficulties are `Story`, `Easy`, `Normal`, `Hard`, `Survival`, and `Challenge`. | `Tempered`, `Hardened`, and `Crucible` are Steel and Bone presets, not replacements for the game's difficulty modes. |

Keep enemy-specific numeric facts in [steel-and-bone-enemies.md](steel-and-bone-enemies.md). Current implementation anchors from the local NPC templates are:

- Preserve vanilla multipliers before applying Steel and Bone overlays.
- Skeletons, Drowners, Red Death infected, Sarras sea creatures, Lost Knights, Forgeborn, Flamegobblers, Ice Weavers, Cairnguard, Tibby, Stagfather variants, Wyrd-linked enemies, and spirits all need family-specific handling.
- Do not generalize all undead, constructs, spirits, Wyrd enemies, bosses, or aquatic enemies into one resistance package.
- Treat Holy and Silver as optional item/effect text rules only, not native damage subtypes.

## Arrow Delivery Identity

Version 3.1.0 treats a direct Arrow projectile as a delivery tag layered onto the physical share of its native damage parts. It does not add or replace a game damage subtype. Throwing knives are excluded, and elemental or magical payload shares retain their own material rules.

| Hardened target | Physical arrow multiplier | Purpose |
|---|---:|---|
| Exposed humanoid flesh | 1.20 | Primary bow target and strongest readable weakness. |
| Infected / sea / ordinary flesh | 1.15 / 1.10 / 1.12 | Keeps bows broadly useful against appropriate living bodies. |
| Armored humanoid | 0.75 | Makes armor a meaningful answer without invalidating bows. |
| Flesh undead / drowned | 0.85 | Distinguishes dead tissue from exposed living flesh. |
| Flora or wood | 0.60 | Arrows lodge in or pass poorly through fibrous bodies. |
| Spirit | 0.55 | Ordinary physical impact is unreliable against incorporeal bodies. |
| Construct or stone | 0.50 | Hard bodies strongly resist arrow impact. |
| Swarm | 0.35 | A single projectile is a poor answer to many small bodies. |
| Confirmed skeleton | 0.20 | Arrows pass through or glance from sparse bone structure. |

Tempered and Crucible apply the shared 55% and 135% rule-intensity scaling. Elite clamps remain authoritative. Wyrd creatures receive no special Arrow overlay until their body evidence supports a clearer rule.

Direct player spells use a tiered Hardened base of 1.02/1.07/1.12 against Light/Medium/Heavy armor. Fire, Electric, and Cold also react to the equipped cuirass's native Fabric, Leather, or Metal surface. Blood, Wyrdness, biological effects, and armor-ignoring spells do not receive the generic tier bonus, and vanilla-authored subtype reactions still take priority. Arrow and spell rules retain independent Core toggles.

## Passive Shield Protection

Readied player shields turn a small share of their effective vanilla Block value into passive protection from frontal direct physical hits: 8% on Tempered, 10% on Hardened, and 12% on Crucible. Coverage uses vanilla BlockAngle capped to a centered forward 180-degree arc. The check runs only on incoming damage, performs no physics query or continuous polling, and skips active blocks, rear hits, magic, statuses, damage over time, and sheathed weapons.

## Native Awareness And Recovery Pressure

Version 3.8.6 strengthens the lightweight awareness layer without replacing enemy AI. Enemy sight distance, hero footstep noise range, and native combat-aggro persistence use x1.20/x1.40/x1.60 by preset. Native hearing strength, wall checks, armor noise, and NPC hearing remain authoritative, while chase boundaries, forced combat exit, and target-loss rules remain untouched.

Version 3.8.8 adds two native-first sustainability controls. Positive mana regeneration is multiplied only during native hero combat by 1.00/0.75/0.50 across Tempered, Hardened, and Crucible. Mana Shield interpolates that added multiplier back toward neutral while retaining the game's own shield-based regeneration reduction and regeneration lock. Accumulated positive parry-window bonuses use the same preset sequence, but the native 0.05-second base and non-positive total bonuses remain unchanged. Both values reset with Preset and remain customizable afterward.

Potion healing, auxiliary effects, item tooltips, and Better UI presentation stay completely native. Steel and Bone suppresses potion-originated contributions to the game's single buildup pool and instead tracks independent Health, Mana, Stamina, and Utility buckets. Direct flat, percentage, and timed restoration graphs select the resource buckets; a multi-resource restorative contributes once to each restored resource, while every other consumed potion uses Utility. Each bucket receives 60/65/70 buildup by preset and lazily decays at the native 10 points per second. Completing any bucket clears all four and activates the one native Potion Poisoning status; mixing classes does not combine buildup, and buildup pauses while poisoning is active. The native icon and active decay remain authoritative. Activation snapshots the relevant maximum resources, then actual native buildup-progress loss meters a 30% drain for each completed Health, Mana, or Stamina bucket, or a 15% drain of all three resources for Utility. Ordinary recovery can offset the drain; Health is floored at 1 while Mana and Stamina can reach zero.

Standard food health and added stamina recovery share one native food status, its adjusted duration, item identity, and icon. All presets use four times the native duration and store 1 stamina per second. Health rate is 0.50 on Tempered, 0.375 on Hardened, and 0.25 on Crucible. If native source separation creates multiple food statuses, Steel and Bone retains only the one with the greatest remaining predicted healing and uses remaining duration to break a tie. The player-stat update applies whole stamina points at one-second boundaries rather than increasing vanilla `StaminaRegen` or adding fractional points each frame. Ordinary action lockouts do not suppress those ticks. Native Overexertion does: active food halves its paired regeneration-lock and Stamina Depleted durations and resets the food tick accumulator throughout the lock. The actual transition out of Overexertion preloads 0.9 seconds of the next interval, making the first point arrive 0.1 seconds later before normal one-second cadence resumes. The shared health recovery and food duration continue normally, while an expired food status or disabled food recovery discards the pending point. Active-effect and item descriptions show both channels in one entry, native health prediction remains authoritative, and optional Better UI compatibility recomputes the adjusted food overlay during its normal slot refresh.

Stamina Depleted presentation is independently configurable. Smooth is the default: it lets the native view start and stop its audio and snapshot emitters, kills only the repeating HUD-image tween, then drives the existing image through one eased unscaled fade in and fade out. Native leaves the complete game presentation untouched. Off hides the HUD image and forces only the dedicated stamina-depleted post-process volume to zero. Movement penalties, continuous-action restrictions, status timing, and resource behavior remain native in every mode.

## Progressive Tenacity

Version 3.8.0 adds one preset-independent late-game curve rather than changing the three preset profiles. Tenacity remains inactive through hero level 20 and scales linearly to full strength at level 35. Native NPC type supplies a fixed cap: 10% for Trash, 15% for Normal, 25% for Elite, 30% for MiniBoss, and 40% for Boss enemies; Critters and Hero Summons receive none.

Tenacity applies at full strength to player-caused poise, force, and enemy stamina damage, including the direct and parry routes that can drive native stamina stagger. Native hero-owned summons count as player-caused sources, while Hero Summon targets remain exempt. Direct non-damage-over-time health damage uses half strength as a bounded burst brake without changing enemy maximum health. Damage over time is excluded from every Tenacity route. A confirmed native or Steel and Bone material weakness halves Tenacity for that hit, while critical, weak-spot, backstab, and generic damage bonuses do not. The system adds no timers, adaptive performance tracking, stagger immunity, saved NPC state, preset mutation, or material-feedback reclassification.

## Non-Goals

| Non-goal | Why |
|---|---|
| Full Requiem-style overhaul | Tainted Grail does not need a world, perk, AI, and encounter rebuild for this mod's purpose. |
| Enemy health scaling | Larger health pools do not create better combat decisions. Steel and Bone changes incoming pressure and player choices without turning enemies into health sponges. |
| Preset-exclusive matchups | Tempered, Hardened, and Crucible should not have different enemy rules. They should scale the same rules. |
| Broad armor and AI rewrites | Steel and Bone narrowly scales native armor penalties, tier mobility/protection, and arrow velocity; enemy AI and armor classification remain native. |
| Perfect taxonomy for every NPC | Start with family rules that catch common enemies. Add exceptions only when templates or testing prove they are needed. |

## Preset Philosophy

Steel and Bone presets are independent from the vanilla difficulties `Story`, `Easy`, `Normal`, `Hard`, `Survival`, and `Challenge`.

| Preset | Intended feel | Rule strength |
|---|---|---:|
| Tempered | Vanilla-plus flavor. Swapping helps but is rarely required. | 55% |
| Hardened | Default tactical mode. Damage type matters often. | 100% |
| Crucible | Harder tactical mode. Wrong tools are punished more and right tools are rewarded more. | 135% |

Presets should be a general matchup-strength and difficulty influence, not separate rulesets. Every Steel and Bone rule has one base multiplier. The preset scales that multiplier toward or away from neutral: Tempered is closer to vanilla, Hardened uses the base rule, and Crucible makes the same rule more decisive. Vanilla-authored multipliers are separate: Tempered leaves them unchanged by default, while Hardened and Crucible amplify their distance from neutral with clamps.

For the 3.0 global layer, Tempered applies 5% incoming, outgoing, and experience pressure while keeping resource, armor, poise, recovery, and enemy movement neutral. Hardened applies 10% damage and experience pressure plus the existing 5% supporting profile and one additional enemy attack slot. Crucible applies 15% damage and experience pressure plus the existing 10% supporting profile and two additional slots. Positive combat mana regeneration and accumulated positive parry-window bonuses use 1.00/0.75/0.50 across Tempered, Hardened, and Crucible. Enemy sight, hero footstep hearing range, and native aggro persistence use x1.20/x1.40/x1.60 across Tempered, Hardened, and Crucible; arrows retain x1.10/x1.30/x1.50. Potion Poisoning buildup is 60/65/70 in each independent class bucket; Health, Mana, and Stamina triggers drain 30% of the matching maximum while Utility drains 15% of all three. Standard food uses 0.50/0.375/0.25 health rate, x4 duration, and 1 discrete stamina point per second. Hostile archer aim scatter uses 1.50/1.25/1.00 meters by preset through the game's native target-point inaccuracy, keeping Crucible archers dangerous without perfectly centered aim. Confirmed weak spots add 10%, 20%, or 30% base damage by preset, while native critical damage remains unchanged. Outgoing and incoming player damage remain independently toggleable, while their exact values come directly from the selected preset.

## Implemented

This section describes the material-rule engine introduced before 1.0 and extended in 3.1.0.

### Damage Hook

| Implemented item | Current behavior | Keep or change |
|---|---|---|
| Per-target damage modifier patch | Patches `HealthElement.ApplyDamageModifiers` and adjusts `dmgModifier` after vanilla has calculated subtype, armor, and target damage-received multipliers. The adjusted value folds into the same final outgoing modifier as crit, sneak, weakspot, and backstab. | Keep. This is the right low-impact surface. |
| Weak-spot reward | Adds `0.10`/`0.20`/`0.30` beside the game's native precision components on confirmed weak spots, before outgoing pressure and material matchups. Native critical damage and build stats remain untouched. | Keep. Deliberate hit placement offsets some preset pressure without globally amplifying random criticals. |
| Player and target guards | Material rules and outgoing scaling require a hero source. Incoming preset scaling instead requires the hero target and remains safe when the damage dealer is missing. | Keep the two paths explicit so environmental damage never needs a dealer dereference. |
| Event-driven evaluation | Runs only when damage is being processed. It does not scan enemies. | Keep. This matches the lightweight mod goal. |
| Cached metadata-first target classification | Caches creature identity and body material by runtime object identity and target-term revision. Reachable NPC type, tags, and abstract types classify identity first; `HitBones`, `HitStone`, and `HitWood` classify body material without deciding biological identity. Broad display-name terms fill gaps, and exact corrections resolve known metadata conflicts. | Keep. This preserves material weapon logic without turning every matching hit surface into Bone Undead or a Construct. |

### Current Target Families

| Family | Seed terms | Current purpose | Accuracy note |
|---|---|---|---|
| BoneUndead | `Skeleton`, `Skull`, `Bone`, `Animated Armor`, `JollySkeleton`, `Keeper Of The Barrow`, `KeeperOfTheBarrow` | Catches bone and animated-armor-like enemies. | Best supported by skeleton template data, but still partly term-based. |
| Construct | `Stone`, `Golem`, `Construct`, `Automaton`, `Statue`, exact Crystal Crawler/Walker terms, `Lost Knight`, `LostKnight`, `Forgeborn`, `ForgeBorn`, `Cairnguard`, `Tibby`, `Sentinel`, `Barnaclator` | Catches stone, golem, and construct enemies. | Broad physical rules apply here, but elemental exceptions are left to vanilla when present. Bare `Crystal` is intentionally excluded because substring matching also catches Crystal Kyrus. |
| ArmoredHumanoid | `Knight`, `Guard`, `Squire`, `Warrior`, `Deserter`, `Kamelot`, `Soldier`, `Armor`, `Armored` | Catches armored humanoid targets without overriding stronger construct, bone, sea, spirit, flora, or Wyrd metadata families. | Slash and generic physical resistance are conservative overlays; piercing is not treated as armor penetration. |
| Flesh | `Bandit`, `Outlaw`, `Human`, `Humanoid`, `Remor`, `Redcap`, `Corpse Eater`, `Wolf`, `Bear` | Gives ordinary flesh a very mild home for bleed, poison, slash, and pierce when no more specific family wins first. | Uses high-signal metadata such as Human, Humanoid, Bandit, and Cultist, but avoids using `HitFlesh` as a broad detector. |
| FleshUndead | `Zombie`, `Undead`, `Bloody`, `Frostbitten Warrior`, `Plaguewraith` | Covers fleshy undead where reliable zombie/bloody metadata exists but drowned or infected specifics do not. | Wights are corrected to Flora from their Wyrd-flora identity. DrownedZombie and InfectedFlesh terms can refine broad FleshUndead metadata when names expose them. |
| Wyrd | `Wyrdspawn`, `Wyrdspirit`, `Wyrd Spirit`, `WyrdSlime`, `Wyrd Slime`, `Wyrdness` | Catches Wyrd enemies. | `Abstract:WyrdnessBound` is a better detector when reachable. Wyrdstalker is not a confirmed WyrdnessBound enemy. |
| DrownedZombie | `Drowner`, `Drowned`, `Drowned Knight`, `Ghost Crew`, `Scourge` | Adds drowned-undead body logic without making them fire-weak. | Drowner Fire resistance is vanilla and is not duplicated as a Steel and Bone overlay. |
| InfectedFlesh | `Red Death`, `RedDeath`, `Infected` | Catches Red Death and infected flesh enemies. | Fire and Poison overlays are skipped if vanilla already has a non-neutral subtype multiplier; mild slash/pierce weaknesses retain living-flesh physical behavior. |
| SeaFlesh | `Sarras`, `Finbled`, `Tadpole`, `Tidewraith`, `Scion`, `Archivist`, `Floatling`, `Reefback`, `Wailcap`, `Grindylow`, `Croakmaw` | Adds modest aquatic identity. | Cold resistance is often vanilla in Sarras data, so `RespectVanillaMultipliers` matters here. |
| Spirit | `Ghost`, `Spirit`, `Wraith`, `Banshee`, `Melancholy`, `Mistling`, `Mistbearer`, `Strawchild`, `Strawfather` | Makes spirits less like ordinary flesh without full lockouts. | Physical resistance is deliberately modest until play testing confirms stronger values. |
| Flora | `Dryad`, `Gloomfrond`, `Fleshtree` | Makes plant/fungus enemies favor Fire and slash. | Wights are exact Wyrd-flora corrections. Wailcaps remain Sea Creatures rather than inheriting broad flora rules. |

### Historical 0.9.0 Atlas Boundaries

| Path or signal | 0.9.0 behavior | Reason |
|---|---|---|
| `HitBones` | Sets the bone-body material flag. | A hit surface describes impact material, not necessarily undead identity. Skeleton and BoneMask evidence still identify BoneUndead where appropriate. |
| `HitStone` | Sets the stone-body material flag. | A stone surface can belong to a Wyrd Sleepwalker or misleading flesh template rather than a Construct. Construct, Automaton, Golem, and exact-name evidence decide identity. |
| `WyrdnessBound` | Classified as Wyrd. | Strong family marker; authored native and exact reactions decide Wyrdness damage. |
| `Scourge` or drowned terms | Classified as DrownedZombie. | Specific drowned identity is safer than broad undead. |
| `SarrasCreature` or `ReefboundBody` | Classified as SeaFlesh. | Strong Sarras/sea marker; vanilla Cold multipliers still win where present. |
| `Ghost` or spirit terms | Classified as Spirit. | Stronger than broad `HitMagic`, which remains neutral by itself. |
| `Flora` or flora terms | Classified as Flora. | Specific plant/fungus identity; Wailcaps are explicitly corrected to SeaFlesh. |
| `Zombie` or `Bloody` with no stronger family | Classified as FleshUndead. | Broad undead flesh identity, kept mild and refineable by terms. |
| `Human`, `Humanoid`, `Bandit`, `Cultist`, `Animal`, or `Animal_Prey` with no stronger family | Classified as Flesh. | Ordinary flesh baseline, kept very mild. |
| Armor terms on broad `Flesh` or `FleshUndead` | Adds ArmoredHumanoid. | Lets gear identity refine broad body metadata without stealing stronger families. |
| `Elite`, `MiniBoss`, `Boss`, or `Type:Elite` | Sets `targetFlags=EliteClass`, not a family. | Template research shows elite status is not a universal resistance rule. |
| `HitFlesh` alone | Intentionally neutral. | Too broad; many special enemies use this surface type. |
| `HitMagic` alone | Intentionally neutral. | Overlaps spirits, Wyrd, and other special cases. |
| `Monster`, `BigHumanoid`, `Giant`, `Tainted`, `Summon`, level, and tier tags alone | Intentionally neutral. | Useful diagnostic context but not reliable enough as a family rule. |
| Holy, silver, purge, iron, armor penetration, block state, AI state, or incoming player damage | Intentionally neutral for 0.9.0. | No reliable current marker or outside the outgoing-damage atlas scope. |

### Current Damage Tags

| Tag | Current detection | Accuracy note |
|---|---|---|
| Blood magic | Searches damage text for blood-related terms such as `blood`, `transfusion`, `abhartach`, `sanguine`, `sanguis`, and `hematic`. | Useful but heuristic. Keep diagnostics enabled while expanding. |
| Bleed | Checks status damage type and damage search text. | Good native status hook through `StatusDamageType.Bleed`. |
| Poison | Checks damage subtype, status damage type, and damage search text. | Good native hook through `DamageSubType.Poison` and `StatusDamageType.Poison`. |
| Wyrdness | Checks damage subtype and Wyrd text terms. | Good native subtype hook through `DamageSubType.Wyrdness`; target behavior still needs lore/game-feel decision. |
| Slashing | Checks damage subtype, then falls back to `IsSword` or `IsAxe` item identity on otherwise generic physical hits. | Confirmed native physical subtype. The fallback keeps swords and axes from becoming untyped physical when TG exposes item identity but not a specific subtype part. |
| Piercing | Checks damage subtype, then falls back to `IsDagger`, `IsPolearm`, `IsRanged`, or `IsArrow` item identity on otherwise generic physical hits. | Confirmed native physical subtype. This is not the same thing as armor ignore or armor penetration. |
| Bludgeoning | Checks damage subtype, then falls back to `IsBlunt` item identity on otherwise generic physical hits. | Confirmed native physical subtype. The fallback is important because blunt is the cleanest known answer to skeleton and construct-style targets. |
| Generic Physical | Checks damage subtype. | Confirmed native physical fallback. Bone undead and constructs have mild generic-physical resistance so untyped physical damage does not bypass material matchups. |
| Generic Magical | Checks damage subtype. | Confirmed native magical fallback. |
| Fire | Checks damage subtype and also treats `StatusDamageType.Burn` as Fire for rule matching. | Confirmed native subtype. |
| Cold | Checks damage subtype. | Confirmed native subtype. This is the engine name for frost-like damage. |
| Electric | Checks damage subtype. | Confirmed native subtype. This is the engine name for shock-like damage. |
| Wet | Checks damage subtype. | Confirmed native subtype. Exact fire-aligned bodies and golems receive the first focused Wet weakness. |
| Burn | Checks status damage type. | Currently feeds Fire-style matching because TG exposes Burn as status damage. |

### Current Damage Rules

The table below lists the base Hardened multiplier. Tempered applies 55% of the distance from neutral. Crucible applies 135% of the distance from neutral, clamped to the safe `0.05` to `2.0` range.

Vanilla enemy subtype multipliers are handled before these overlays. Steel and Bone evaluates each runtime damage part independently, applies any vanilla amplification and custom rule to that part, then recombines the hit from its post-vanilla shares. This keeps physical, elemental, and status payloads independent on arrows, enchanted weapons, and multi-element spells without double-counting the game's own multiplier. Inferred weapon hints remain overlay evidence, not proof that vanilla applied a subtype.

| Target family | Damage tags | Base multiplier | Priority | Design intent | Accuracy note |
|---|---|---:|---:|---|---|
| BoneUndead | Cold | 0.66 | 60 | Inert bone has no living warmth for Cold to attack. | Matches the final Hardened Cold resistance of many higher-tier skeletons; native subtype reactions still win, and independent Chill buildup remains intact. |
| BoneUndead | Blood magic, bleed | 0.25 | 100 | Dry bone should not care about blood or bleeding. | Bleed immunity is strongly supported by templates. Blood magic is a design extension. |
| BoneBody | Slashing, piercing | 0.55 | 80 | Blades and points are worse into bone or empty armor. | Applies from bone material independently of Spirit, undead, or other identity. |
| BoneBody | Bludgeoning | 1.08 | 70 | Blunt remains the expected physical answer. | Skipped when vanilla already has a non-neutral Bludgeoning multiplier. |
| BoneBody | Generic Physical | 0.85 | 40 | Untyped physical should be safe but not a best answer against bone. | Fallback only. Specific slash, pierce, or blunt rules win when detected. |
| Construct | Cold | 0.66 | 60 | Inert stone and animated armor resist thermal injury without becoming immune to magical hindrance. | Applies only when native Cold data is neutral; exact fire, crystal, ice, and other elemental reactions remain authoritative. |
| Construct | Blood magic, bleed, poison | 0.25 | 100 | Stone and constructs are not biological targets. | Fits many constructs, but element rules remain per subtype or vanilla exception. |
| StoneBody | Slashing, piercing | 0.75 | 70 | Edged and pointed weapons are less effective against hard bodies. | Applies from stone material independently of Construct or Wyrd identity. |
| StoneBody | Bludgeoning | 1.15 | 80 | Impact weapons get a clear hard-body lane. | Steel and Bone overlay unless vanilla has a subtype rule. |
| StoneBody | Generic Physical | 0.85 | 40 | Untyped physical should not erase the material weapon-choice lesson. | Fallback only. Specific slash, pierce, or blunt rules win when detected. |
| ArmoredHumanoid | Physical weapon types | 0.82-1.15 | 90 | Slash loses effectiveness faster than Pierce while Blunt improves with armor weight. | Uses the equipped Light/Medium/Heavy tier and dampens added resistance when vanilla numerical armor is already active. |
| Flesh | Bleed, poison | 1.06 | 20 | Ordinary flesh gives status/body damage a small home. | Broad but mild; only applies after stronger families miss. |
| Flesh | Slashing, piercing | 1.04 | 15 | Blades and points stay slightly better into ordinary flesh. | Broad but mild; only applies after stronger families miss. |
| FleshUndead | Blood magic, bleed, poison | 0.78 | 55 | Fleshy undead are worse biological targets without using skeleton-level lockouts. | Broad but mild; drowned and infected specifics win when detected. |
| FleshUndead | Fire | 1.08 | 50 | Fire becomes a modest default answer where vanilla and specific families are silent. | Skipped when vanilla already has a non-neutral Fire multiplier. |
| FleshUndead | Bludgeoning | 1.05 | 45 | Blunt gives a small physical fallback. | Mild overlay. |
| DrownedZombie | Blood magic, bleed | 0.65 | 80 | Waterlogged undead are worse blood/bleed targets. | Overlay; Drowners do not have vanilla bleed immunity. |
| DrownedZombie | Electric | 1.15 | 70 | Electric becomes a readable drowned counter. | Overlay; no broad vanilla Electric weakness found. |
| DrownedZombie | Bludgeoning | 1.10 | 60 | Blunt gives a physical fallback. | Overlay. |
| InfectedFlesh | Poison | 0.66 | 80 | Infected enemies are poor poison targets. | Red Death poison resistance is vanilla and will be skipped as duplicate. |
| InfectedFlesh | Fire | 1.15 | 70 | Fire is the clean infected counter when vanilla has not already handled it. | Red Death fire weakness is vanilla and will be skipped as duplicate. |
| Grindylow/Blood Abomination/Bonemask summon gaps | Cold | 1.20 | 130 | Missing summon reactions should match their ordinary family's native Cold weakness. | Exact summon terms only; the Grindylow rule outranks SeaFlesh Cold resistance. |
| Flamegobbler | Cold | 1.15 | 130 | Fire-immune bodies gain a readable opposing-element counter. | Exact Flamegobbler term; no broad fire-body inference. |
| Crystal body | Cold | 1.20 | 130 | Crystal Walker joins the Cold-weak Crystal Crawler pattern. | Exact Crawler/Walker terms; existing native Crawler multipliers remain authoritative and bosses soften the custom bonus. |
| Wyrd Slime | Cold / Blunt | 1.10 / 0.80 | 130 | Freezing and congealing remains the positive answer while its formless mass absorbs impact. | Exact Wyrd Slime terms; Slash and Pierce remain neutral rather than becoming explicit weaknesses. |
| Frostgrot | Fire / Cold | 1.15 / 0.75 | 130 | Its explicit frost identity receives the opposing-element answer. | Exact runtime name; native subtype reactions remain authoritative. |
| Frozen undead | Fire / Cold | 1.15 / 0.75 | 130 | Frostbitten Warriors should read as frozen flesh undead rather than stone constructs. | Exact Frostbitten Warrior terms. |
| Missing Corpse Eater variants | Fire / Wyrdness | 1.20 / 0.80 | 130 | Repairs the summon and large variant to match the established Corpse Eater reactions. | Exact gap terms only. |
| Electric Stagfather golem | Poison | 1.33 | 130 | Completes the elemental golem counter pattern without making all constructs poison-weak. | Exact electric variant; native reactions remain authoritative. |
| Mistbearer | Fire / Wyrdness | 1.20 / 0.80 | 130 | Follows the established Mistling material pattern. | Exact base and mimic terms. |
| Wyrdheir challenge | Cold | 0.60 | 130 | Repairs the challenge variant to match the ordinary Wyrdheir. | Exact challenge term. |
| Nivera / Rimefiend | Fire | 1.33 / 1.20 | 130 | Fire provides a clear caster answer to strongly frost-themed enemies. | Exact archetypes only. |
| Frost Wolf | Fire / Cold | 1.15 / 0.75 | 130 | Adds a mild, readable elemental identity without generalizing to wolves. | Exact summoned Frost Wolf variants. |
| Straw Dad/Son | Fire / slashing | 1.20 / 1.15 | 130 | Fire and cutting fit dry straw bodies. | Exact two templates. |
| Wyrdspawn | Slashing | 1.10 | 130 | Supplies a modest weapon lane without imposing a universal elemental answer. | Exact Wyrdspawn identity; no broad Wyrd Slash or Wyrdness rule. |
| Ogre | Piercing / biological / Blunt | 1.15 / 1.10 / 0.90 | 130 | Living brutes reward precise and biological attacks while resisting crushing blows. | Exact Ogre templates, layered above ordinary Flesh. |
| Fire-aligned bodies and golems | Wet | 1.20 | 130 | Gives the two direct Wet spells a narrow counter without inventing a universal Wet chart. | Exact Flamegobbler, Cindermar, Forgeborn, fire Stagfather golem, and fire elemental golem terms. |
| Drowned skeleton sailors | Electric | 1.12 | 130 | Waterlogged Deckhand and Mariner skeletons gain a caster answer while retaining BoneUndead physical rules. | Exact DrownedDeckhand and DrownedMariner terms. |
| Frost Angel / Ice Weaver Champion / Ice Weaver Wolf | Fire / Cold | 1.20 / 0.75 | 130 | Visibly frost-aligned variants gain a consistent opposing-element answer. | Exact archetypes only; the ordinary Ice Weaver keeps its stronger native reactions. |
| Ice Trial Wyrdspawn/Wyrdspirit | Fire / Cold | 1.15 / 0.75 | 130 | Adds a mild frost identity without weakening all Wyrd enemies to Fire. | Exact Ice Trial terms; Wyrd remains the body family. |
| Charred Conclave Wyrdspawn | Cold / Fire | 1.15 / 0.75 | 130 | The charred variant receives the inverse elemental profile. | Exact Charred Conclave term; Wyrd remains the body family. |
| Trial Ice Statue | Fire / Cold | 1.20 / 0.60 | 130 | The animated ice statue receives a strong but readable elemental identity. | Exact trial statue; corrected to Construct. |
| Ancient Beholder | Piercing / biological / Blunt | 1.12 / 1.08 / 0.90 | 130 | A large living horror rewards precise and biological attacks rather than inheriting stone-construct rules. | Exact Tier 6 Ancient Beholder; corrected to Flesh. |
| Singworm / Lir tentacle | Slashing / Blunt | 1.15 / 0.85 | 130 | Soft, flexible bodies favor cutting over crushing. | Exact Singworm and summoned Lir tentacle terms; corrected to Flesh. |
| Blood Abomination | Slashing / Blunt | 1.20 / 0.80 | 130 | A fluid, formless blood mass rewards cutting while absorbing impact. | Exact variants only; the MiniBoss clamp leaves a final x1.10 Slash weakness. Misleading BoneMask metadata is cleared instead of applying BoneUndead physical rules. Native Cold and Wyrdness reactions remain authoritative. |
| Tidewraith | Blunt | 0.90 | 130 | Its flexible aquatic plant body absorbs some impact. | Exact Tidewraith term; existing SeaFlesh rules already provide mild Slash and Pierce advantages. |
| Flora or wood | Axe | 1.20 | 125 | Axes should be the strongest intuitive cutting answer to wood and plant bodies. | Replaces the ordinary `1.15` flora Slash rule for axe hits; never stacks. |
| SeaFlesh | Cold | 0.70 | 70 | Aquatic/Sarras enemies lean cold-resistant. | Many Sarras Cold rules are vanilla and will be skipped as duplicate. |
| SeaFlesh | Electric | 1.12 | 60 | Electric gives sea creatures a mild counter. | Overlay; tune after play testing. |
| Spirit | Blood magic, bleed, poison | 0.35 | 90 | Spirits are bad biological targets. | Broad overlay; status-immunity data is uneven. |
| Spirit | Physical | 0.85 | 50 | Plain physical is slightly worse without hard-walling weapon builds. | Deliberately modest. |
| Flora | Poison, bleed, piercing | 0.70 | 70 | Plants and fungus should not behave exactly like flesh. | Overlay; Wailcap poison resistance is vanilla. |
| Flora | Fire, slashing | 1.15 | 70 | Fire and cutting are natural plant/fungus answers. | Overlay. |

When multiple rules match, the resolver chooses the highest-priority matching rule, then the largest absolute distance from neutral. It does not stack rules by default.

### Vanilla Multiplier Amplification

| Preset | Default extra distance from neutral | Example `0.50` resistance | Example `1.33` weakness |
|---|---:|---:|---:|
| Tempered | 0% | 0.50 | 1.33 |
| Hardened | 35% | 0.325 | 1.445 |
| Crucible | 70% | 0.20 after clamp | 1.561 |

True vanilla immunities remain true vanilla immunities. Non-immune amplified resistances clamp at `MinimumAmplifiedVanillaResistance` and amplified weaknesses clamp at `MaximumAmplifiedVanillaWeakness`. If a hit carries multiple native subtypes, Steel and Bone applies the product of each amplified-to-native ratio.

### Current Feedback And Config

| Implemented item | Current behavior | Keep or change |
|---|---|---|
| `DamageNumbersEnabled` | Shows built-in floating damage numbers for outgoing player hits. Neutral hits use the base color, while resistance and weakness hits still scale color/size from the applied multiplier. | Keep. This replaces the older reason-text feedback route. |
| `DamageNumberFontMode` | Follows the game's Accessibility font choice by default and can force the simple Sans, stylized Serif, or Unity IMGUI fallback font. | Keep in parity with Grail Floating Text's font support. |
| `MeleeDamageNumberDurationMultiplier` | Multiplies the final duration of direct melee numbers after normal resistance, weakness, immunity, and critical timing is resolved. Defaults to `2`; projectiles, spells, and damage-over-time ticks are excluded. | Keep. Camera movement during weapon swings makes the ordinary timing easier to miss. |
| `DamageNumberHorizontalDrift` and `DamageNumberVerticalDrift` | Independently scale each motion axis from `0` (off) through `1` (default) to `3` (exaggerated) while preserving the relative motion profiles for criticals, weaknesses, resistances, and immunities. | Keep. This supports stationary, straight-rise, wide-spray, and exaggerated feedback styles without separate animation modes. |
| `DamageOverTimeNumberHeightMultiplier` | Multiplies the initial world-space height for Bleed, Poison, Burn, and Breath status-tick numbers from `0` to `6`. The `3` default starts them three times higher than ordinary damage numbers. | Keep. Status ticks often report a lower target position than direct hits, so they need a separate baseline without changing their motion. |
| `DamageOverTimeNumberScale` | Multiplies the final text scale for Bleed, Poison, Burn, and Breath status-tick numbers from `0.5` to `2`. The `0.75` default keeps ticks subordinate to direct-hit numbers while retaining their normal effectiveness and precision sizing. | Keep. Height and text emphasis should remain independently tunable. |
| `DamageNumberSizeContrast` and `DamageNumberColorContrast` | Independently scale weakness/resistance size and color differences from `0` (neutral) through `1` (default) to `3` (dramatic). Precision pop remains independent and immunity styling is unchanged. | Keep. Players can emphasize color without oversized text, emphasize size without color dependence, or neutralize either channel. |
| `EffectivenessFeedbackSensitivity` | Expands or compresses effectiveness distance from neutral for damage-number color and optional hit-marker tiers only. Preset changes set the single value to `1.20` Tempered, `1.10` Hardened, or `1.00` Crucible; later customization persists until the preset changes again. | Keep. Lower presets retain more visual variety without changing combat damage, number size, or duration. |
| Precision feedback | Reads native critical and weak-spot bonus components, adds the active Steel and Bone weak-spot bonus, and uses the combined value for number size and red tint up to `0.50` on unresisted hits. Material resistance scales down only that tint and size emphasis, while hit-marker frames and separate critical or weak-spot overlays retain their normal identities. | Keep. One channel communicates matchup and another communicates execution without falsely promoting resisted hits into weakness frames. |
| Damage-number color scaling | The baseline number color is `#E3BD02`; stronger resistances shrink and desaturate toward grey, while stronger weaknesses grow and warm toward red-orange. | Tune after in-game visibility testing. |
| Final-damage outcome hook | Patches the post-health-decrease event and reads the game's final damage amount and hit position for display. | Keep. This is more accurate than showing the pre-final `Damage.Amount`. |
| Vanilla amplification config | `AmplifyVanillaMultipliers`, per-preset amplification values, and min/max clamps control how strongly vanilla-authored matchups are pushed. | Keep. This is central to the 0.9.0 atlas goal because it makes confirmed vanilla data matter more without duplicating it as custom rules. |
| Elite clamp config | `EliteRuleClampsEnabled`, `EliteWeaknessBonusReduction`, and `EliteMinimumResistanceMultiplier` reduce custom Steel and Bone weakness bonuses and floor custom resistances on elite-class targets. | Keep. Elite is not a family rule because the template research shows elite status is a weak resistance predictor by itself. |
| Metadata-first family evidence | Diagnostics report whether a family came from metadata or fallback terms, such as `metadata:Construct` or `terms:DrownedZombie`. | Keep. Runtime logs should drive 0.9 validation and polish. |
| Physical weapon hints | Generic physical hits can infer slash, pierce, or blunt from TG item identity when no specific physical subtype is present. PhysicalHitSource attacks from Mining tools are forced to Pierce before the Axe fallback; their Interact mining route remains excluded from all modifiers. | Keep. This preserves pickaxe identity in combat without changing ore extraction or granting the special Axe-versus-flora bonus. |
| Preset config | `Tempered`, `Hardened`, and `Crucible` scale the same rule table. | Keep. No preset-exclusive matchups. |
| Target-family term config | Lets users edit BoneUndead, Construct, ArmoredHumanoid, Flesh, FleshUndead, Wyrd, DrownedZombie, InfectedFlesh, SeaFlesh, Spirit, and Flora terms. | Keep. Add new families only when they ship. |
| Player arrow gravity | Applies a preset-independent `0.75` gravity multiplier to player-owned arrows while preserving native launch direction, offsets, draw strength, collision, payloads, and damage. Hostile arrows, thrown items, and other projectiles are excluded. | Keep. The `0.25` to `1.00` range supports flatter trajectories without adding an upward aiming bias. |
| Config schema reset | Backs up stale configs and regenerates defaults when incompatible settings change. | Keep. Additive settings with safe defaults do not require a schema bump. |
| Diagnostics | Logs damage checks, detected target families, elite-class target flags, family evidence, damage tags, physical weapon-type hints, no-match reasons, selected rules, elite-clamp adjustments, amplified vanilla multipliers, and skipped vanilla multiplier cases. | Keep through the 0.9.0 atlas validation pass. |

## Not Implemented

The ideas below are prioritized for maximum combat identity per implementation hour while staying aligned with the game files.

## Priority 1: Runtime Accuracy And Tuning

The 0.9.0 rule engine is intentionally small, table-driven, and feature-complete for the metadata paths that are reliable in the current evidence. The next work should prove that metadata-first classification, vanilla amplification, floating damage-number feedback, flesh/flesh-undead/armor baselines, and elite clamps behave correctly in real fights before adding more complexity.

| Idea | Player-facing result | Minimal implementation | Preset behavior |
|---|---|---|---|
| Validate target families in game | Rules trigger on the intended enemies and stay quiet elsewhere. | Enable diagnostics and sample BoneUndead, Construct, ArmoredHumanoid, Flesh, FleshUndead, DrownedZombie, InfectedFlesh, SeaFlesh, Spirit, Flora, and Wyrd fights. | Same families on every preset; only multiplier strength changes. |
| Tune base multipliers | Wrong tools feel bad without turning fights into HP chores. | Adjust the Hardened base multipliers in `DamageRules`; let preset intensity derive Tempered and Crucible. | Tempered remains forgiving; Crucible remains harsher through scaling only. |
| Validate metadata detection | Fewer false positives from display names. | Compare `metadata:*` and `terms:*` evidence in diagnostics against representative enemies. | Same detection on every preset. |
| Validate flesh, flesh-undead, and armor baselines | Broad body families get useful but not overbearing physical/status identity. | Confirm broad `Flesh` and `FleshUndead` only fire after stronger families miss, and `ArmoredHumanoid` does not steal construct, bone, sea, spirit, flora, or Wyrd cases. | Scale through the same preset intensity. |
| Split high-overlap families | Drowned, sea, flora, and spirit overlap less. | Add priority or exclusion helpers only for repeatedly bad classifications. | Same rule resolver on every preset. |
| Tune elite clamps | Correct weaknesses stay useful without deleting bosses. | Review `targetFlags=EliteClass` diagnostics and adjust `EliteWeaknessBonusReduction` or `EliteMinimumResistanceMultiplier` only if real fights need it. | Clamp is shared by all presets; do not make it Crucible-only. |

Recommended implementation order:

| Step | Change | Why first |
|---:|---|---|
| 1 | Runtime-test the 0.9.0 metadata families, vanilla amplification, damage-number feedback, flesh/flesh-undead/armor baselines, and elite clamps with diagnostics enabled. | Metadata reachability, fallback terms, vanilla ratio adjustments, clamp behavior, and visible feedback readability are still the biggest sources of risk. |
| 2 | Tune the Hardened base multipliers, not per-preset special rows. | One base table keeps presets easy to reason about. |
| 3 | Promote proven template fields into direct classification helpers. | Metadata should beat display-name text where available. |
| 4 | Tune the new `Flesh`, `FleshUndead`, and `ArmoredHumanoid` baselines only after false-positive checks. | Broad body families should create identity without swallowing other families. |
| 5 | Tune elite clamps from `targetFlags=EliteClass` diagnostics. | Bosses should have answers without being erased by the right one. |

## Priority 2: Physical Damage Identity

Physical weapon choice should matter on every preset. Tempered makes the lesson light, Hardened makes it regular, and Crucible makes the same lesson harsher.

| Physical type | Should be strong against | Should be weak against | Design note |
|---|---|---|---|
| Slashing | Flesh, plants, soft humanoids, tendons | Bone, stone, animated armor, heavy armor | A two-handed sword should be excellent at some fights and clearly wrong at others. |
| Piercing | Light flesh, casters, weak points, lightly armored humanoids, some sea flesh | Bone, stone, swarms, some spirits | Spears, arrows, and thrusting weapons should have identity without pretending `DamageSubType.Piercing` automatically ignores armor. Use vanilla `ArmorPenetration` or `DamageParameters.Piercing()` separately if that route is ever implemented. |
| Bludgeoning | Confirmed skeleton weakness; armor, stone, shields, and large rigid targets as overlays | Slimes, swarms, soft evasive targets | The obvious answer to skeletons, golems, and armor. For constructs this is a Steel and Bone overlay unless a template confirms it. |
| Generic Physical | Untagged fallback | Special material enemies | Keep it safe but mediocre so it never becomes the best universal damage type. |

Recommended Hardened baseline:

| Family | Slash | Pierce | Blunt | Generic Physical | Evidence note |
|---|---:|---:|---:|---:|---|
| Flesh | 1.04 | 1.06 | 1.00 | 1.00 | Mild living-flesh baseline; vanilla usually leaves ordinary flesh neutral. |
| Infected flesh | 1.04 | 1.06 | 1.00 | 1.00 | Shares the living-flesh physical baseline while keeping its Poison/Fire identity. |
| Light armor | 0.98 | 1.03 | 1.00 | 0.98 | Edges remain usable and Pierce retains a slight advantage. |
| Medium armor | 0.92 | 1.00 | 1.08 | 0.94 | Slash begins losing ground while Blunt becomes favorable. |
| Heavy armor | 0.82 | 0.90 | 1.15 | 0.88 | Slash is the poorest ordinary weapon match, Pierce remains better, and Blunt is the clear counter. |
| Bone body | 0.55 | 0.55 | 1.08 | 0.85 | Blunt `1.33` is common skeleton data and wins through vanilla-skip behavior; the table shows the fallback body-material overlay. |
| Drowned zombie | 1.00 | 0.90 | 1.10 | 1.00 | Dead organs make pierce mildly poor, severing slash stays neutral, and blunt disrupts the degraded body. |
| Flesh undead | 1.00 | 0.90 | 1.05 | 1.00 | Dead organs make pierce mildly poor while slash stays neutral and blunt is a mild structural counter. |
| Spirit | 0.85 | 0.85 | 0.85 | 0.85 | Cautious overlay; no broad vanilla physical resistance was found. Wyrdness supplies the positive counter. |
| Stone body | 0.75 | 0.75 | 1.15 | 0.85 | Design overlay. Biological Construct identity is resolved separately, and elemental reactions remain archetype-specific. |
| Flora | 1.15 | 0.70 | 1.00 | 1.00 | Design overlay; Wailcap poison resistance is confirmed, but broad flora damage data is not. |
| SeaFlesh | 1.04 | 1.06 | 1.00 | 1.00 | Shares the living-flesh physical baseline; the confirmed vanilla family pattern remains Cold resistance. |
| Wyrd | 1.00 | 1.00 | 1.00 | 1.00 | Physical damage stays neutral; vanilla WyrdnessBound does not imply a physical multiplier. |

Preset scaling:

- Keep one Hardened base multiplier per rule.
- Tempered should pull that multiplier closer to `1.0`.
- Crucible should push that multiplier farther from `1.0`.
- Do not add physical matchups that exist only on one preset.
- Preserve at least one physical answer for bone, armor, stone, and flesh.
- Keep spirit and sea penalties modest until testing proves stronger values are fun.

Target examples:

| Enemy family | Bad single-weapon route | Required answer |
|---|---|---|
| Bone undead | Two-handed sword, dagger, arrows | Blunt is confirmed. Fire, holy, or silver only if a reliable damage or item signal exists. |
| Lost Knight style construct | Sword, poison, blood magic | Electric is confirmed for Lost Knight; blunt can be a Steel and Bone physical overlay. |
| Forgeborn style construct | Sword, poison, blood magic, Fire | Cold is confirmed; blunt can be a Steel and Bone physical overlay. |
| Cairnguard style construct | Sword, poison, blood magic, Cold | Fire is confirmed; blunt can be a Steel and Bone physical overlay. |
| Spirit | Plain sword or spear | Wyrdness is the implemented answer; holy/silver or purge remain deferred until detectable. Keep the physical penalty modest. |
| Flora | Spear, poison, bleed | Slash and Fire, with poison resistance strongest for Wailcap-style fungi. |
| Armored humanoid | Sword-only attrition | Blunt, Electric if template supports it, or separate armor-penetrating pierce if implemented through vanilla armor penetration. |
| Flesh brute | Hammer-only attrition | Bleed, poison, pierce, Fire if appropriate. |
| Drowned zombie | Pierce, bleed, fire-only undead plan | Electric or blunt as shared overlays; slash remains neutral and confirmed Fire resistance is preserved. |

## Priority 3: Magic And Status Identity

Different magic should have strengths and weaknesses, but only where Steel and Bone can detect the damage cleanly.

| Magic or status | Strong against | Weak against | Minimal implementation |
|---|---|---|---|
| Blood magic | Living flesh | Bone undead, drowned zombies, constructs, spirits | Ordinary flesh now has a `1.10` weakness; keep validating that blood-magic text classification does not produce false positives. |
| Bleed | Flesh, beasts, unarmored humanoids | Bone, constructs, Red Death, Banshee, spirits, plants if not fleshy; Drowners as a Steel and Bone overlay | Positive flesh weakness is implemented cautiously at `1.06`; tune only after false-positive checks. |
| Poison | Flesh, beasts, some humans | Bone, constructs, undead, plants/fungus, Red Death, Wailcap-style enemies | Positive flesh weakness is implemented cautiously at `1.06`. Red Death `Poison 66%`, Wailcap `Poison 25%`, Lost Knight/Cairnguard `Poison 50%` are confirmed and respected by vanilla-skip logic. |
| Wyrdness | Spirits | Ice Weaver, Blood Abomination, and Giant Sentinel-style enemies have confirmed partial native resistance | Spirits have a `1.15` weakness. Neutral Wyrd enemies remain neutral instead of receiving a blanket family resistance. |
| Fire | Confirmed against Red Death, Ice Weaver, Cairnguard, and ice Stagfather variants; plants as a Steel and Bone overlay; exact additions for Frostbitten Warriors, Mistbearers, Nivera, Rimefiends, Frost Wolves, the Frost Angel, Ice Weaver variants, Ice Trial creatures, the Trial Ice Statue, and straw parents | Confirmed weak into Drowners, Forgeborn, Flamegobbler, Lost Knight, fire Stagfather variants, and the Charred Conclave Wyrdspawn | `DamageSubType.Fire` and `StatusDamageType.Burn` detection are implemented. Vanilla fire-resistant exceptions are preserved by subtype skip logic. |
| Cold | Confirmed against Forgeborn, Grindylow, Blood Abomination, Giant Sentinel, and fire Stagfather variants; Steel and Bone adds Flamegobbler, crystal-body, Wyrd Slime, and Charred Conclave lanes | Bone Undead and Constructs receive x0.66 on Hardened where native data is neutral. Confirmed resistances remain on Sarras sea creatures, Ice Weaver, Cairnguard, Rimefiend, and many high-tier skeletons; exact additions cover Frostgrot, Frostbitten Warriors, Frost Wolves, the Frost Angel, Ice Weaver variants, Ice Trial creatures, the Trial Ice Statue, and the Wyrdheir challenge | `DamageSubType.Cold` detection is implemented. Damage resistance does not suppress independent Chill buildup. Curlghast, Marrowghast, Slugholder, and Snail remain neutral without stronger evidence. Use `Cold`, not frost, in config and feedback. |
| Electric | Confirmed against Lost Knight and Tibby; Steel and Bone overlay against drowned/sea targets and exact drowned skeleton sailors | Electric-aligned enemies if found | `DamageSubType.Electric` detection is implemented. Broad construct Electric weakness is still avoided. |
| Wet | Exact Flamegobbler, Cindermar, Forgeborn, and fire-aligned golem bodies | No resistance chart yet | `DamageSubType.Wet` detection is implemented. The first rule is deliberately narrow because only two audited direct player spells use Wet and vanilla enemies define no Wet multipliers. |
| Holy or silver | Undead, spirits, Wyrd if item/effect text exposes reliable terms | Ordinary flesh | No native subtype found. Only add if runtime item, skill, enchantment, or effect text can be detected reliably. |

Magic should not become "physical, but better." The desired pattern is:

| Build behavior | Tempered | Hardened | Crucible |
|---|---|---|---|
| One physical weapon plus one magic school | Comfortable | Usually fine | Harsher in bad matchups, using the same rules |
| One physical weapon plus several magic schools | Strong | Strong | Good if the magic schools cover material gaps |
| Multiple physical weapons, no magic | Fine | Good | Viable but requires weapon swapping |
| One two-handed sword only | Fine for many fights | Noticeably punished by some families | Strongly punished by the same family rules |

## Priority 4: Feedback Improvements

Feedback is part of balance. If the player cannot see the rule, the rule feels arbitrary.

| Idea | Current state | Action |
|---|---|---|
| Floating numbers | Implemented through Steel and Bone's own final-damage outcome display. | Test readability in rapid combat. |
| Resistance and weakness styling | Implemented through `#E3BD02` baseline color, grey/smaller resistance scaling, red-orange/larger weakness scaling, and critical pop scaling. | Tune color, size, and duration after in-game visibility testing. |
| Rule labels | Kept as short internal labels such as `Bone`, `Construct`, `Armor`, `Flesh`, `Drowned`, and `Flora` for diagnostics and pending feedback context. | Keep labels short and stable. |
| Active-number limit | Implemented through `DamageNumberMaximumActive` and pending feedback pruning. | Tune only if rapid hits spam text. |
| Diagnostics | Implemented for damage checks, target families, elite-class target flags, metadata or term family evidence, damage tags, physical weapon hints, no-match reasons, selected rules, elite-clamp behavior, vanilla amplification, and vanilla skip decisions. | Use as the main 0.9.0 atlas validation tool. |

Diagnostic label examples:

| Situation | Text |
|---|---|
| Resistance | `Bone resists Pierce` |
| Immunity | `Construct ignores Bleed` |
| Weakness | `Stone weak to Blunt` |
| Vanilla-preserved rule | `Vanilla Fire resistance preserved` only in diagnostics, not floating combat text. |
| Overlapping rules | Show only the chosen reason. |

## Priority 5: Preset And Config Model

The current fixed per-rule multipliers work, but expansion will be easier if presets become a small policy layer.

| Config idea | Minimal version | Why |
|---|---|---|
| `Preset` | Implemented. | Main player-facing strength selector; scales all rules from the same base table. |
| `RespectVanillaMultipliers` | Implemented, default `true`. | Prevents Steel and Bone from overwriting obvious vanilla exceptions such as Drowner Fire resistance or Lost Knight Electric weakness. |
| `AmplifyVanillaMultipliers` | Implemented, default `true`. | Makes confirmed vanilla weaknesses and resistances more decisive on Hardened and Crucible while Tempered stays unchanged by default. |
| Elite clamp settings | Implemented through `EliteRuleClampsEnabled`, `EliteWeaknessBonusReduction`, and `EliteMinimumResistanceMultiplier`. | Lets elite-class moderation tune custom Steel and Bone overlays without changing vanilla multipliers. |
| Feedback toggle | Implemented through `DamageNumbersEnabled`. | Controls the built-in floating damage-number route for outgoing player hits. |
| Per-family term configs | Implemented for shipped families. | Lets users fix false positives without a new build. |
| Per-family enable toggles | Later, only if requested. | Useful for compatibility but more surface area to test. |

Avoid exposing every multiplier immediately. Too much config turns the mod into a spreadsheet and makes feedback harder to explain. Keep one source multiplier table in code until testing shows which values need user control.

## Priority 6: Elite Handling

Elite handling should preserve family logic without letting the right answer delete every boss.

| Idea | Minimal implementation | Preset behavior |
|---|---|---|
| Runtime elite detection | Current implementation reads reachable metadata text and treats `Elite`, `MiniBoss`, `Boss`, and `Type:Elite` as elite-class target flags. | Shared across presets. Continue validating `targetFlags=EliteClass` in diagnostics. |
| Elite weakness clamp | Current default reduces custom Steel and Bone positive weakness bonuses by `0.10`. | Shared clamp, then preset strength still applies. |
| Elite resistance floor | Current default prevents custom Steel and Bone non-immunity resistance from dropping below `0.20`. | Shared floor, not a Crucible-only rule. |
| Named enemy terms | Add names like `Giant Sentinel`, `Tibby`, `Scourge`, and `Stagfather` to an elite term list only as fallback. | Good fallback if no real elite flag is reachable. |
| Boss override table | Only for enemies that test badly. | Avoid until family rules fail. |

## Candidate Version Plan

| Version | Scope | Actionable contents |
|---|---|---|
| 0.3 | Rule engine and first expansion | Implemented: `DamageRule`, weakness multipliers, the earlier reason-feedback route, Fire/Cold/Electric/Wet/Burn tags, vanilla multiplier skipping, and the first expanded family set. |
| 0.4 | Release-readiness cleanup | Implemented: preset rename, Hardened default, generic physical fallback resistance, physical weapon-type hints from TG item identity, and expanded diagnostics for runtime validation. |
| 0.5 | Built-in damage numbers and runtime validation | Implemented built-in floating damage numbers with final-damage display; remaining work is in-game family detection and readability tuning. |
| 0.6 | Vanilla multiplier amplification | Implemented: Tempered leaves vanilla unchanged by default, while Hardened and Crucible amplify each non-neutral vanilla subtype by ratio with clamps and recombine mixed hits by post-vanilla share. |
| 0.7 | Metadata-first classification | Implemented: reachable surface type, tags, abstracts, and NPC type classify before broad display-name terms. |
| 0.8 | Flesh, armored humanoid, and elite pass | Implemented: cautious `Flesh` and `ArmoredHumanoid` baselines, broad-flesh-safe armor precedence, elite-class target flags, and shared elite weakness/resistance clamps. |
| 0.9 | Feature-complete enemy atlas | Implemented: every reliably detectable current metadata path is classified or intentionally neutral, with vanilla amplification, overlays, diagnostics, feedback, and docs aligned. |
| Later | Release polish | Testing, tuning, compatibility, named exceptions, Nexus copy, and small polish toward 1.0.0. |

## Next Testing Pass: 0.9.0 Release Validation

The next testing pass should prove that the 0.9.0 rule engine works in real fights, especially metadata-first classification, vanilla amplification, flesh/flesh-undead/armor baselines, elite clamps, and floating feedback. After this build, releases should focus mainly on testing, balance, compatibility, and polish unless runtime evidence reveals a missing reliable family path.

### 0.9.0 Scope

| Priority | Work | Action | Done when |
|---:|---|---|---|
| 1 | Runtime diagnostics pass | Enable `Diagnostics = true` and test the shipped families: BoneUndead, Construct, ArmoredHumanoid, Flesh, FleshUndead, DrownedZombie, InfectedFlesh, SeaFlesh, Spirit, Flora, and Wyrd. | Logs show the intended family, `metadata:*` or `terms:*` evidence, `targetFlags=EliteClass` when relevant, damage tags, vanilla skip result, elite clamp result, and selected rule for representative fights. |
| 2 | Metadata validation pass | Compare metadata evidence against template-backed expectations such as HitBones skeletons, HitStone constructs, SarrasCreature sea targets, and WyrdnessBound Wyrd targets. | Metadata is used where reachable, fallback terms only fill gaps, and high-overlap targets such as Wailcap are not misclassified by names alone. |
| 3 | Vanilla amplification pass | Compare Tempered, Hardened, and Crucible against known vanilla multipliers such as skeleton Bludgeoning, Drowner Fire, Red Death Fire/Poison, and Sarras Cold. | Tempered matches vanilla, Hardened/Crucible amplify by the configured ratio, and true immunities stay intact. |
| 4 | Broad body pass | Validate broad `Flesh` and `FleshUndead` metadata/terms, ArmoredHumanoid override behavior, and elite-clamp neutrality on low-impact elite weaknesses. | Common flesh, flesh-undead, and armored targets classify correctly, while stronger families keep precedence. |
| 5 | Hardened multiplier tuning | Tune the base `DamageRules` values around `Hardened`; let `Tempered` and `Crucible` derive from preset intensity. | Wrong tools are noticeably worse on `Hardened` but do not turn fights into HP chores. |
| 6 | Preset spread check | Compare the same matchup on `Tempered`, `Hardened`, and `Crucible`. | The same rule appears on all presets, and only the strength changes. |
| 7 | Feedback polish | Adjust floating-number size, color, timing, or active-count behavior where combat text is confusing or too noisy. | The player can read resistance, weakness, and critical outcomes from the on-hit numbers. |
| 8 | Atlas documentation sync | Update README, Nexus copy, and enemy/design notes with classified, intentionally neutral, and deferred enemy paths. | Public docs promise only what the current build actually does. |
| 9 | Release-candidate package | Build and stage the version after tuning. | Vortex package installs cleanly, zip shape is valid, metadata checks pass. |

### 0.9.0 Test Matrix

| Enemy group | Required comparisons |
|---|---|
| Skeleton and bone undead | Sword, dagger/polearm/bow, mace/hammer, Cold if vanilla template has Cold resistance. |
| Construct and animated armor | Sword/pierce, blunt, blood magic, poison, Electric only where vanilla or testing supports it. |
| Drowned | Blood/bleed, Fire, Electric, blunt, Pierce, and a neutral Slash control. |
| Red Death and infected flesh | Poison, Fire, slash, pierce, and a neutral Blunt control. |
| Sarras and sea creatures | Cold, Electric, slash, pierce, and a neutral Blunt control. |
| Spirit | Plain physical, blood/bleed/poison, and Wyrdness. |
| Flora | Poison, bleed, pierce, Fire, slash. |
| Wyrd enemies | Wyrdness, poison/bleed if available, ordinary physical. |
| Ordinary flesh | Bleed, poison, slash, pierce, and a control hit that should stay neutral. |
| Flesh undead | Blood/bleed/poison, Fire, blunt, Pierce, a neutral Slash control, and a specific-family sample that should refine into DrownedZombie or InfectedFlesh. |
| Armored humanoid | Slash, generic physical, blunt, and a specific-family overlap sample such as construct or bone armor. |
| Elite-class target | Weakness bonus reduction, resistance floor, and a mild weakness that should become neutral. |

### 0.9.0 Out Of Scope

| Avoid | Save for |
|---|---|
| Holy or Silver rules | Only after runtime item, skill, or effect text proves a reliable marker exists. |
| Incoming player damage changes | Later or never; release identity is outgoing player damage knowledge. |
| Stamina, armor, AI, anti-cheese, or encounter changes | Outside the first release scope. |

### 0.9.0 Acceptance Criteria

| Check | Pass condition |
|---|---|
| Family accuracy | Representative enemies trigger the intended family or no Steel and Bone rule when they should not match. |
| Atlas boundary | Broad `HitFlesh`, `HitMagic`, `Monster`, level, tier, and boss signals remain neutral unless a specific family marker is also present. |
| Metadata evidence | Logs identify family source as metadata when reachable and terms only when metadata does not classify the target. |
| Vanilla respect | Known vanilla rules remain intact, especially skeleton Bludgeoning, Drowner Fire, Red Death Fire/Poison, Sarras Cold, and construct elemental exceptions. |
| Vanilla amplification | Tempered leaves non-neutral vanilla multipliers unchanged by default, Hardened and Crucible amplify them by ratio, and clamps prevent extreme non-immune values. |
| Knowledge difficulty | A player who brings the right damage type gets a meaningful advantage; a one-weapon route stays possible but inefficient in bad matchups. |
| Preset behavior | `Tempered`, `Hardened`, and `Crucible` use the same rule table with different strength only. |
| Feedback | Floating numbers make resistance or weakness visible without spamming. |
| Release package | Build, staged Vortex copy, package shape, metadata, and markdown checks pass. |

## Testing Checklist

| Check | Target result |
|---|---|
| Same skeleton, sword vs mace | Skeleton targets die clearly faster to blunt, with the gap scaling by preset strength. |
| Generic physical fallback | Untyped physical hits against bone and constructs are modestly resisted, while item-identified swords, daggers, polearms, bows, axes, and blunt weapons use the appropriate physical lane. |
| Skeleton Cold test | High-tier skeletons should not accidentally become Cold-weak if their vanilla template is `Cold 75%`. |
| Drowner Fire test | Fire remains resisted or neutral; Steel and Bone does not override vanilla into fire weakness. |
| Red Death Poison/Fire test | Poison is poor and Fire is strong, matching vanilla template data. |
| Lost Knight Electric test | Electric is very strong; Poison and Fire are poor. |
| Forgeborn Cold/Fire test | Cold is strong and Fire is poor. |
| Cairnguard Fire/Cold test | Fire is strong and Cold is poor. |
| Same enemy, poison/bleed vs construct | Damage is resisted or ignored and the floating number makes the resistance visible. |
| Ordinary flesh sample | Mild Flesh rules should trigger only when no more specific family is detected. |
| Flesh-undead sample | Pierce is mildly resisted, Slash stays neutral, and Blunt is mildly rewarded on zombie/bloody paths; DrownedZombie and InfectedFlesh terms refine them when present. |
| Armored humanoid sample | Slash falls behind Pierce as armor gets heavier, Blunt becomes increasingly effective, existing numerical armor does not create excessive duplicate resistance, and stronger material families keep precedence. |
| Elite-class sample | Diagnostics show `targetFlags=EliteClass`; custom weaknesses are reduced, low custom resistances are floored, and mild custom weaknesses can become neutral. |
| Same spirit enemy, plain physical vs Wyrdness | Plain physical is modestly poor and Wyrdness is the clear strong answer. |
| Sea creature Cold/Electric/physical test | Cold resistance is respected, Electric weakness feels modest rather than mandatory, Slash/Pierce receive the living-flesh bonus, and Blunt stays neutral. |
| Preset scaling pass | The same matchup appears on Tempered, Hardened, and Crucible, but grows stronger as the preset rises. |
| Enemy awareness pass | Sight distance, footstep range, and native combat-aggro persistence resolve to x1.20/x1.40/x1.60; chase and forced-exit behavior remain native. |
| Combat mana regeneration pass | Out-of-combat regeneration remains native. In combat, positive actual and predicted regeneration use x1.00/x0.75/x0.50, Mana Shield proportionally relieves only Steel and Bone's added penalty, and native regeneration locks remain authoritative. |
| Positive parry-window pass | The total window preserves the native 0.05-second base and non-positive bonuses while scaling only accumulated positive bonus time by x1.00/x0.75/x0.50. |
| Pickaxe combat and mining pass | A pickaxe damages an enemy as Pierce, still mines normally through DamageType.Interact, and no longer receives the special Axe-versus-flora bonus. |
| Potion overdrinking pass | Potion healing and presentation remain native; direct restoratives feed independent Health, Mana, and Stamina buckets, all other consumed potions feed Utility, and each bucket receives 60/65/70 buildup with native decay. Completing one clears every bucket and activates the single native status with a 30% matching-resource drain, or a 15% all-resource drain for Utility. |
| Food stamina lockout pass | Only the food status with the greatest remaining predicted healing survives. It restores exactly 1 whole point per elapsed second through ordinary action lockouts. During native Overexertion, the paired regeneration lock and Stamina Depleted status last half as long and food stamina pauses without banking ticks. The first point follows 0.1 seconds after the lock ends, then normal one-second cadence resumes. |
| Two-handed sword route | Can beat ordinary flesh but gets increasingly inefficient against bone, construct, spirit, and flora matchups as preset strength rises. |
| Feedback spam | Floating numbers appear often enough to teach, not often enough to annoy. |
| Diagnostics | Logs show target families, elite-class target flags, family evidence, damage tags, physical weapon hint, vanilla amplification, vanilla multiplier skip, elite clamp behavior, no-match reason, and applied rule. |

## Open Design Questions

| Question | Current recommendation |
|---|---|
| Should Steel and Bone skip rules when vanilla already has a non-neutral multiplier? | Yes by default for strong vanilla exceptions. Layer only when the Steel and Bone rule is explicit and documented. |
| Should Wyrdness hurt or resist Wyrd enemies? | Keep neutral Wyrd enemies neutral. Preserve authored native resistance where present and use exact archetype rules only when lore or game data supports them. |
| Should every family have both a physical and magical answer? | Yes for high-identity families where possible, but the answer should exist on every preset and scale by preset strength. |
| Should ordinary humans get many rules? | No. Let vanilla handle most human combat unless armor, infection, or caster identity is obvious. |
| Should bosses ignore weaknesses? | No. Clamp weaknesses instead of disabling them. |
| Should Steel and Bone alter incoming player damage? | Yes, through the 5%/10%/15% preset layer, independently toggleable and applied after vanilla routes the hit. |

## Design Rule Of Thumb

Add a rule only when it makes the player ask a better question than "do I have enough damage?"

Good rules ask:

- Is this target flesh, bone, armor, stone, spirit, plant, sea, infected, or Wyrd?
- Is my current weapon slash, pierce, blunt, or generic physical?
- Is this spell biological, elemental, Wyrd, or a detectable special item/effect?
- Did vanilla already give this enemy a specific resistance or weakness?
- Should I switch now?

Bad rules only ask:

- Did I bring enough raw DPS?
- Did I guess the hidden immunity correctly?
- Did the mod override vanilla combat for no visible reason?
