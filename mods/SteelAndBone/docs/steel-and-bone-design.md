# Steel and Bone Design Notes

Steel and Bone should enhance vanilla Tainted Grail combat by making the existing damage pipeline more legible and more tactical. It should not replace enemy AI, rewrite encounters, flatten the vanilla build system, or turn every fight into a puzzle lock.

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
| Keep implementation narrow | Prefer rule-table expansion, target terms, damage tags, preset multipliers, and compact feedback. Avoid broad stateful systems unless a hook is already easy. |

## Game-File Ground Truth

These notes are based on local Tainted Grail 1.25 files and the current Steel and Bone 0.9.0 source.

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

## Non-Goals

| Non-goal | Why |
|---|---|
| Full Requiem-style overhaul | Tainted Grail does not need a world, perk, AI, and encounter rebuild for this mod's purpose. |
| Global health/damage scaling | More enemy health or more enemy damage does not create better combat decisions by itself. Presets should scale matchup importance, not raw stat inflation. |
| Preset-exclusive matchups | Tempered, Hardened, and Crucible should not have different enemy rules. They should scale the same rules. |
| Stamina, armor, and AI rewrites | These can fight vanilla systems and create brittle bugs. Armor is also global in the decompiled code, so material identity belongs in subtype rules first. |
| Perfect taxonomy for every NPC | Start with family rules that catch common enemies. Add exceptions only when templates or testing prove they are needed. |

## Preset Philosophy

Steel and Bone presets are independent from the vanilla difficulties `Story`, `Easy`, `Normal`, `Hard`, `Survival`, and `Challenge`.

| Preset | Intended feel | Rule strength |
|---|---|---:|
| Tempered | Vanilla-plus flavor. Swapping helps but is rarely required. | 55% |
| Hardened | Default tactical mode. Damage type matters often. | 100% |
| Crucible | Harder tactical mode. Wrong tools are punished more and right tools are rewarded more. | 135% |

Presets should be a general matchup-strength and difficulty influence, not separate rulesets. Every Steel and Bone rule has one base multiplier. The preset scales that multiplier toward or away from neutral: Tempered is closer to vanilla, Hardened uses the base rule, and Crucible makes the same rule more decisive. Vanilla-authored multipliers are separate: Tempered leaves them unchanged by default, while Hardened and Crucible amplify their distance from neutral with clamps.

## Implemented

This section describes the current 0.9.0 behavior.

### Damage Hook

| Implemented item | Current behavior | Keep or change |
|---|---|---|
| Per-target damage modifier patch | Patches `HealthElement.ApplyDamageModifiers` and adjusts `dmgModifier` after vanilla has calculated subtype, armor, and target damage-received multipliers. The adjusted value folds into the same final outgoing modifier as crit, sneak, weakspot, and backstab. | Keep. This is the right low-impact surface. |
| Player-source guard | Applies only when the hero is the damage source and avoids modifying damage against the hero. | Keep. Steel and Bone should not surprise the player by changing incoming damage yet. |
| Event-driven evaluation | Runs only when damage is being processed. It does not scan enemies. | Keep. This matches the lightweight mod goal. |
| Cached metadata-first target classification | Caches target family classification by runtime object identity and target-term revision. Reachable surface type, NPC type, tags, and abstract types classify first; broad display-name terms fill in only when metadata does not identify a family. High-signal terms can refine only broad `Flesh` or `FleshUndead` metadata, not stronger metadata families. | Keep. This is the 0.9.0 atlas foundation. |

### Current Target Families

| Family | Seed terms | Current purpose | Accuracy note |
|---|---|---|---|
| BoneUndead | `Skeleton`, `Skull`, `Bone`, `Animated Armor`, `JollySkeleton`, `Keeper Of The Barrow`, `KeeperOfTheBarrow` | Catches bone and animated-armor-like enemies. | Best supported by skeleton template data, but still partly term-based. |
| Construct | `Stone`, `Golem`, `Construct`, `Automaton`, `Statue`, `Crystal`, `Lost Knight`, `LostKnight`, `Forgeborn`, `ForgeBorn`, `Cairnguard`, `Tibby`, `Sentinel`, `Barnaclator` | Catches stone, golem, and construct enemies. | Broad physical rules apply here, but elemental exceptions are left to vanilla when present. |
| ArmoredHumanoid | `Knight`, `Guard`, `Squire`, `Warrior`, `Deserter`, `Kamelot`, `Soldier`, `Armor`, `Armored` | Catches armored humanoid targets without overriding stronger construct, bone, sea, spirit, flora, or Wyrd metadata families. | Slash and generic physical resistance are conservative overlays; piercing is not treated as armor penetration. |
| Flesh | `Bandit`, `Outlaw`, `Human`, `Humanoid`, `Remor`, `Redcap`, `Corpse Eater`, `Wolf`, `Bear` | Gives ordinary flesh a very mild home for bleed, poison, slash, and pierce when no more specific family wins first. | Uses high-signal metadata such as Human, Humanoid, Bandit, and Cultist, but avoids using `HitFlesh` as a broad detector. |
| FleshUndead | `Zombie`, `Undead`, `Wight`, `Bloody`, `Frostbitten Warrior`, `Plaguewraith` | Covers fleshy undead where reliable zombie/bloody metadata exists but drowned or infected specifics do not. | Mild overlay only. DrownedZombie and InfectedFlesh terms can refine broad FleshUndead metadata when names expose them. |
| Wyrd | `Wyrdspawn`, `Wyrdspirit`, `Wyrd Spirit`, `WyrdSlime`, `Wyrd Slime`, `Wyrdness` | Catches Wyrd enemies. | `Abstract:WyrdnessBound` is a better detector when reachable. Wyrdstalker is not a confirmed WyrdnessBound enemy. |
| DrownedZombie | `Drowner`, `Drowned`, `Drowned Knight`, `Ghost Crew`, `Scourge` | Adds drowned-undead body logic without making them fire-weak. | Drowner Fire resistance is vanilla and is not duplicated as a Steel and Bone overlay. |
| InfectedFlesh | `Red Death`, `RedDeath`, `Infected` | Catches Red Death and infected flesh enemies. | Fire and Poison overlays are skipped if vanilla already has a non-neutral subtype multiplier; mild slash/pierce weaknesses retain living-flesh physical behavior. |
| SeaFlesh | `Sarras`, `Finbled`, `Tadpole`, `Tidewraith`, `Scion`, `Archivist`, `Floatling`, `Reefback`, `Wailcap`, `Grindylow`, `Croakmaw` | Adds modest aquatic identity. | Cold resistance is often vanilla in Sarras data, so `RespectVanillaMultipliers` matters here. |
| Spirit | `Ghost`, `Spirit`, `Wraith`, `Banshee`, `Melancholy`, `Mistling`, `Mistbearer`, `Strawchild`, `Strawfather` | Makes spirits less like ordinary flesh without full lockouts. | Physical resistance is deliberately modest until play testing confirms stronger values. |
| Flora | `Dryad`, `Gloomfrond`, `Fleshtree`, `Wailcap`, `Viridian` | Makes plant/fungus enemies favor Fire and slash. | Wailcap poison resistance is vanilla; broad flora rules are Steel and Bone overlays. |

### 0.9.0 Atlas Boundaries

| Path or signal | 0.9.0 behavior | Reason |
|---|---|---|
| `HitBones`, `Skeleton`, `BoneMask` | Classified as BoneUndead. | Strong material and template evidence. |
| `HitStone`, `Construct`, `Automaton`, `Golem` | Classified as Construct. | Strong material and template evidence. |
| `WyrdnessBound` | Classified as Wyrd. | Strong family marker, even though the Wyrdness resistance itself is a Steel and Bone design rule. |
| `Scourge` or drowned terms | Classified as DrownedZombie. | Specific drowned identity is safer than broad undead. |
| `SarrasCreature` or `ReefboundBody` | Classified as SeaFlesh. | Strong Sarras/sea marker; vanilla Cold multipliers still win where present. |
| `Ghost` or spirit terms | Classified as Spirit. | Stronger than broad `HitMagic`, which remains neutral by itself. |
| `Flora` or flora terms | Classified as Flora. | Specific plant/fungus identity; Wailcap overlap must be validated in game. |
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
| Wet | Checks damage subtype. | Confirmed native subtype. Detected for diagnostics and future rules. |
| Burn | Checks status damage type. | Currently feeds Fire-style matching because TG exposes Burn as status damage. |

### Current Damage Rules

The table below lists the base Hardened multiplier. Tempered applies 55% of the distance from neutral. Crucible applies 135% of the distance from neutral, clamped to the safe `0.05` to `2.0` range.

Vanilla enemy subtype multipliers are handled before these overlays. When `AmplifyVanillaMultipliers` is enabled, Steel and Bone adjusts detected non-neutral vanilla subtype multipliers by ratio so the final vanilla value becomes more decisive without double-counting the game's own multiplier. This only uses subtypes the hit actually carries; inferred weapon hints are Steel and Bone overlay evidence, not proof that vanilla applied that subtype.

| Target family | Damage tags | Base multiplier | Priority | Design intent | Accuracy note |
|---|---|---:|---:|---|---|
| BoneUndead | Blood magic, bleed | 0.25 | 100 | Dry bone should not care about blood or bleeding. | Bleed immunity is strongly supported by templates. Blood magic is a design extension. |
| BoneUndead | Slashing, piercing | 0.55 | 80 | Blades and points are worse into bone or empty armor. | Vanilla confirms blunt weakness, not slash/pierce resistance. Keep as an overlay. |
| BoneUndead | Bludgeoning | 1.08 | 70 | Blunt remains the expected physical answer. | Skipped when vanilla already has a non-neutral Bludgeoning multiplier. |
| BoneUndead | Generic Physical | 0.85 | 40 | Untyped physical should be safe but not a best answer against bone. | Fallback only. Specific slash, pierce, or blunt rules win when detected. |
| Construct | Blood magic, bleed, poison | 0.25 | 100 | Stone and constructs are not biological targets. | Fits many constructs, but element rules remain per subtype or vanilla exception. |
| Construct | Slashing, piercing | 0.75 | 70 | Edged and pointed weapons are less effective against hard bodies. | Broad physical overlay. |
| Construct | Bludgeoning | 1.15 | 80 | Impact weapons get a clear construct lane. | Steel and Bone overlay unless vanilla has a subtype rule. |
| Construct | Generic Physical | 0.85 | 40 | Untyped physical should not erase the construct weapon-choice lesson. | Fallback only. Specific slash, pierce, or blunt rules win when detected. |
| ArmoredHumanoid | Slashing, Generic Physical | 0.88 | 65 | Armor makes cuts and untyped physical attrition less efficient. | Conservative overlay; does not override stronger non-flesh families. |
| ArmoredHumanoid | Bludgeoning | 1.10 | 66 | Impact damage gives armor a readable physical counter. | Conservative overlay; piercing is left neutral until armor penetration can be detected reliably. |
| Flesh | Bleed, poison | 1.06 | 20 | Ordinary flesh gives status/body damage a small home. | Broad but mild; only applies after stronger families miss. |
| Flesh | Slashing, piercing | 1.04 | 15 | Blades and points stay slightly better into ordinary flesh. | Broad but mild; only applies after stronger families miss. |
| FleshUndead | Blood magic, bleed, poison | 0.78 | 55 | Fleshy undead are worse biological targets without using skeleton-level lockouts. | Broad but mild; drowned and infected specifics win when detected. |
| FleshUndead | Fire | 1.08 | 50 | Fire becomes a modest default answer where vanilla and specific families are silent. | Skipped when vanilla already has a non-neutral Fire multiplier. |
| FleshUndead | Bludgeoning | 1.05 | 45 | Blunt gives a small physical fallback. | Mild overlay. |
| Wyrd | Wyrdness | 0.35 | 70 | Current mod choice: Wyrd enemies resist Wyrdness. | Vanilla has `WyrdnessBound` abstracts, but no broad Wyrdness multiplier. |
| DrownedZombie | Blood magic, bleed | 0.65 | 80 | Waterlogged undead are worse blood/bleed targets. | Overlay; Drowners do not have vanilla bleed immunity. |
| DrownedZombie | Electric | 1.15 | 70 | Electric becomes a readable drowned counter. | Overlay; no broad vanilla Electric weakness found. |
| DrownedZombie | Bludgeoning | 1.10 | 60 | Blunt gives a physical fallback. | Overlay. |
| InfectedFlesh | Poison | 0.66 | 80 | Infected enemies are poor poison targets. | Red Death poison resistance is vanilla and will be skipped as duplicate. |
| InfectedFlesh | Fire | 1.15 | 70 | Fire is the clean infected counter when vanilla has not already handled it. | Red Death fire weakness is vanilla and will be skipped as duplicate. |
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
| `DamageNumberHorizontalDrift` and `DamageNumberVerticalDrift` | Independently scale each motion axis from `0` (off) through `1` (default) to `3` (exaggerated) while preserving the relative motion profiles for criticals, weaknesses, resistances, and immunities. | Keep. This supports stationary, straight-rise, wide-spray, and exaggerated feedback styles without separate animation modes. |
| `DamageOverTimeNumberHeightMultiplier` | Multiplies the initial world-space height for Bleed, Poison, Burn, and Breath status-tick numbers. The `1.25` default starts them 25% higher than ordinary damage numbers. | Keep. Status ticks often report a lower target position than direct hits, so they need a separate baseline without changing their motion. |
| `DamageNumberSizeContrast` and `DamageNumberColorContrast` | Independently scale weakness/resistance size and color differences from `0` (neutral) through `1` (default) to `3` (dramatic). Critical and weak-spot pop remain independent, and immunity styling is unchanged. | Keep. Players can emphasize color without oversized text, emphasize size without color dependence, or neutralize either channel. |
| Damage-number color scaling | The baseline number color is `#E3BD02`; stronger resistances shrink and desaturate toward grey, while stronger weaknesses grow and warm toward red-orange. | Tune after in-game visibility testing. |
| Final-damage outcome hook | Patches the post-health-decrease event and reads the game's final damage amount and hit position for display. | Keep. This is more accurate than showing the pre-final `Damage.Amount`. |
| Vanilla amplification config | `AmplifyVanillaMultipliers`, per-preset amplification values, and min/max clamps control how strongly vanilla-authored matchups are pushed. | Keep. This is central to the 0.9.0 atlas goal because it makes confirmed vanilla data matter more without duplicating it as custom rules. |
| Elite clamp config | `EliteRuleClampsEnabled`, `EliteWeaknessBonusReduction`, and `EliteMinimumResistanceMultiplier` reduce custom Steel and Bone weakness bonuses and floor custom resistances on elite-class targets. | Keep. Elite is not a family rule because the template research shows elite status is a weak resistance predictor by itself. |
| Metadata-first family evidence | Diagnostics report whether a family came from metadata or fallback terms, such as `metadata:Construct` or `terms:DrownedZombie`. | Keep. Runtime logs should drive 0.9 validation and polish. |
| Physical weapon hints | Generic physical hits can infer slash, pierce, or blunt from TG item identity when no specific physical subtype is present. | Keep. This is a release-readiness fix, not a new combat system. |
| Preset config | `Tempered`, `Hardened`, and `Crucible` scale the same rule table. | Keep. No preset-exclusive matchups. |
| Target-family term config | Lets users edit BoneUndead, Construct, ArmoredHumanoid, Flesh, FleshUndead, Wyrd, DrownedZombie, InfectedFlesh, SeaFlesh, Spirit, and Flora terms. | Keep. Add new families only when they ship. |
| Config schema reset | Backs up stale configs and regenerates defaults when schema changes. | Keep. Any new settings require a schema bump. |
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
| Armored humanoid | 0.88 | 1.00 | 1.10 | 0.88 | Design overlay. Piercing stays neutral because the subtype does not imply armor penetration. |
| Bone undead | 0.55 | 0.55 | 1.08 | 0.85 | Blunt `1.33` is common vanilla data and wins through vanilla-skip behavior; the table shows the fallback Steel and Bone overlay. |
| Drowned zombie | 1.00 | 0.90 | 1.10 | 1.00 | Dead organs make pierce mildly poor, severing slash stays neutral, and blunt disrupts the degraded body. |
| Flesh undead | 1.00 | 0.90 | 1.05 | 1.00 | Dead organs make pierce mildly poor while slash stays neutral and blunt is a mild structural counter. |
| Spirit | 0.85 | 0.85 | 0.85 | 0.85 | Cautious overlay; no broad vanilla physical resistance was found. Wyrdness supplies the positive counter. |
| Construct | 0.75 | 0.75 | 1.15 | 0.85 | Design overlay. Do not use one universal elemental rule for all constructs. |
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
| Wyrdness | Spirits | Current Wyrd enemies resist it; Ice Weaver, Blood Abomination, and Giant Sentinel-style enemies have confirmed partial Wyrdness resistance | Spirits now have a `1.15` weakness. Keep current Wyrd-family resistance until testing says Wyrdness should destabilize Wyrd targets. |
| Fire | Confirmed against Red Death, Ice Weaver, Cairnguard, and ice Stagfather variants; plants as a Steel and Bone overlay | Confirmed weak into Drowners, Forgeborn, Flamegobbler, Lost Knight, and fire Stagfather variants | `DamageSubType.Fire` and `StatusDamageType.Burn` detection are implemented. Vanilla fire-resistant exceptions are preserved by subtype skip logic. |
| Cold | Confirmed against Forgeborn, Grindylow, Blood Abomination, Giant Sentinel, and fire Stagfather variants | Confirmed weak into Sarras sea creatures, Ice Weaver, Cairnguard, Rimefiend, and many high-tier skeletons | `DamageSubType.Cold` detection is implemented. Use `Cold`, not frost, in config and feedback. |
| Electric | Confirmed against Lost Knight and Tibby; Steel and Bone overlay against drowned/sea targets | Electric-aligned enemies if found | `DamageSubType.Electric` detection is implemented. Broad construct Electric weakness is still avoided. |
| Wet | Not enough design data yet | Not enough design data yet | Detection is implemented for diagnostics and future rules. |
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
| 0.6 | Vanilla multiplier amplification | Implemented: Tempered leaves vanilla unchanged by default, while Hardened and Crucible amplify non-neutral vanilla subtype multipliers by ratio with clamps. |
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
| Armored humanoid sample | Slash and generic physical are modestly resisted, blunt is modestly rewarded, and stronger construct/bone/sea/spirit/flora/Wyrd families keep precedence. |
| Elite-class sample | Diagnostics show `targetFlags=EliteClass`; custom weaknesses are reduced, low custom resistances are floored, and mild custom weaknesses can become neutral. |
| Same spirit enemy, plain physical vs Wyrdness | Plain physical is modestly poor and Wyrdness is the clear strong answer. |
| Sea creature Cold/Electric/physical test | Cold resistance is respected, Electric weakness feels modest rather than mandatory, Slash/Pierce receive the living-flesh bonus, and Blunt stays neutral. |
| Preset scaling pass | The same matchup appears on Tempered, Hardened, and Crucible, but grows stronger as the preset rises. |
| Two-handed sword route | Can beat ordinary flesh but gets increasingly inefficient against bone, construct, spirit, and flora matchups as preset strength rises. |
| Feedback spam | Floating numbers appear often enough to teach, not often enough to annoy. |
| Diagnostics | Logs show target families, elite-class target flags, family evidence, damage tags, physical weapon hint, vanilla amplification, vanilla multiplier skip, elite clamp behavior, no-match reason, and applied rule. |

## Open Design Questions

| Question | Current recommendation |
|---|---|
| Should Steel and Bone skip rules when vanilla already has a non-neutral multiplier? | Yes by default for strong vanilla exceptions. Layer only when the Steel and Bone rule is explicit and documented. |
| Should Wyrdness hurt or resist Wyrd enemies? | Keep the implemented Wyrdness resistance until lore and in-game feel testing says otherwise. The vanilla templates do not prove broad Wyrdness resistance for Wyrdspawn. |
| Should every family have both a physical and magical answer? | Yes for high-identity families where possible, but the answer should exist on every preset and scale by preset strength. |
| Should ordinary humans get many rules? | No. Let vanilla handle most human combat unless armor, infection, or caster identity is obvious. |
| Should bosses ignore weaknesses? | No. Clamp weaknesses instead of disabling them. |
| Should Steel and Bone alter incoming player damage? | Not yet. Finish outgoing player damage identity first. |

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
