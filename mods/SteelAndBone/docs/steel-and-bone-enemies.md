# Steel and Bone Enemy Design Inventory

This document is a working enemy-family inventory for expanding Steel and Bone's resistance and weakness rules.

Markdown is a good file type for this stage because the table is design-facing, readable in GitHub, easy to diff, and easy to edit while the rule set is still changing. If this becomes direct mod data later, promote the stable rows into JSON, CSV, or a C# rule table and keep this file as the human-readable design note.

This is the canonical human-readable enemy/resistance table. Keep implementation decisions in [steel-and-bone-design.md](steel-and-bone-design.md), and keep raw evidence in [research/](research/).

## Sources And Confidence

Sources checked:

- Local Tainted Grail 1.25 game files:
  - `Fall of Avalon_Data/Managed/TG.Main.dll`, decompiled to `.codex-temp/decompiled/TG.Main-1.25`.
  - `Fall of Avalon_Data/StreamingAssets/aa/catalog.json`, decoded to locate addressable NPC templates.
  - `Fall of Avalon_Data/StreamingAssets/aa/StandaloneWindows64/templates.npc_assets_all.bundle`, read with UnityPy using Unity `6000.0.64f1`.
- Tainted Grail wiki.gg `NPC-Enemy` category API: https://taintedgrail.wiki.gg/api.php?action=query&list=categorymembers&cmtitle=Category:NPC-Enemy&cmlimit=500&format=json
- Tainted Grail wiki.gg Journal page, which lists the Bestiary entries: https://taintedgrail.wiki.gg/wiki/Journal
- WeMod/Wand enemy checklist pages for Horns of the South, Cuanacht, Forlorn Swords, and Sanctuary of Sarras:
  - https://wand.com/maps/tainted-grail-the-fall-of-avalon/horns-of-the-south/checklist/enemies/enemy
  - https://wand.com/maps/tainted-grail-the-fall-of-avalon/cuanacht/checklist/enemies/enemy
  - https://wand.com/maps/tainted-grail-the-fall-of-avalon/forlorn-swords/checklist/enemies/enemy
  - https://wand.com/maps/tainted-grail-the-fall-of-avalon/sarras/checklist/enemies/enemy

The enemy names and broad wiki type/subtype labels are source-backed. Rows updated from the local game files are stronger evidence than the wiki labels because they come from serialized `NpcTemplate` prefabs. Remaining resistance and weakness ideas are Steel and Bone design hypotheses, not confirmed vanilla stats.

## Local Game-File Findings

Confirmed runtime damage taxonomy from `TG.Main.dll`:

| Area | Confirmed values |
|---|---|
| `DamageType` | `PhysicalHitSource`, `MagicalHitSource`, `Status`, `Fall`, `Interact`, `Environment`, `Trap` |
| Physical `DamageSubType` | `GenericPhysical`, `Slashing`, `Piercing`, `Bludgeoning` |
| Magical `DamageSubType` | `GenericMagical`, `Fire`, `Cold`, `Poison`, `Electric`, `Wet` |
| Other `DamageSubType` | `Pure`, `Wyrdness` |
| `StatusDamageType` | `Burn`, `Breath`, `Poison`, `Bleed` |
| `NpcType` | `Critter`, `Trash`, `Normal`, `Elite`, `MiniBoss`, `Boss`, `HeroSummon` |

Important implementation facts from `NpcTemplate`:

| Field | Why Steel and Bone should care |
|---|---|
| `damageReceivedMultipliers` | Vanilla already stores per-subtype damage taken percentages. Below `100` is resistance; above `100` is weakness. Steel and Bone should preserve or layer on these instead of blindly replacing them. |
| `_abstractTypes` and `tags` | Better family detection than display-name text when accessible. Steel and Bone 0.9.0 uses high-signal metadata such as `Skeleton`, `BoneMask`, `WyrdnessBound`, `ReefboundBody`, `Scourge`, `Ghost`, `Zombie`, `Bloody`, `Human`, `Humanoid`, `Bandit`, and `Cultist` before falling back to broad terms. Elite-like metadata such as `Elite`, `MiniBoss`, `Boss`, and `Type:Elite` is used as a target flag, not as a family. Other useful abstract templates include `Animal`, `Animal_Prey`, `BigHumanoid`, `ChallengeModeSpawn`, `DalRiataBody`, `Foredweller`, `Giant`, `Monster`, `Summon`, and `Tainted`. |
| `surfaceType` | Useful fallback cue: most enemies are `HitFlesh`, skeletons are `HitBones`, constructs/golems are often `HitStone`, and Banshee/Melancholy/Wyrdspawn are often `HitMagic`. Steel and Bone 0.9.0 directly uses `HitBones` for `BoneUndead` and `HitStone` for `Construct`; it deliberately does not use broad `HitFlesh` as a direct Flesh detector. `HitMagic` remains diagnostic context because it overlaps spirits and Wyrd enemies. |
| `statusStats.invulnerableToStatuses` | Confirms several immunities, especially `Status_Bleed` on skeletons, Lost Knights, Red Death, Banshee, Forgeborn, and Cairnguard. Drowner rows in the later CSV audit show `Status_Blind`, not `Status_Bleed`. |
| `level`, `tags`, `npcType` | Prefer these over guessed power when available. Example tags include `Tier:1` through `Tier:7` and `Type:Elite`. |

Local asset pass:

| Metric | Result |
|---|---:|
| NPC templates read from `templates.npc_assets_all.bundle` | 893 |
| Enemy templates under `NpcTemplates/Enemies` | 469 |
| Enemy templates with non-neutral vanilla damage multipliers | 151 |

High-signal corrections from vanilla templates:

| Enemy or family | Confirmed vanilla data | Design correction |
|---|---|---|
| Skeletons | 47 skeleton-bucket enemy templates; 40 have non-neutral direct multipliers; 39 carry `Bludgeoning 133%`; many T4-T6 templates also have `Cold 75%`; 46 have bleed/blind immunity. | Blunt weakness is strongly confirmed. Slash/pierce resistance and fire/holy weakness are Steel and Bone additions, not vanilla multipliers. |
| Drowners | 11 enemy templates; `Abstract:Zombie`, `Abstract:Bloody`, `Abstract:Monster`; `Fire 50%`; `Status_Blind`; no vanilla `Status_Bleed`, `Cold`, or `Electric` multiplier found. | Treat as drowned zombies first. An `Electric` weakness or bleed resistance can be a Steel and Bone tactical layer, but vanilla makes them fire-resistant and blind-immune. |
| Red Death infected | 6 templates; `Poison 66%`, `Fire 133%`; zombie/bloody/monster abstracts; bleed immunity. | Poison resistance and fire weakness are confirmed. |
| Wailcaps | 2 Sarras templates; `Poison 25%`; no fire weakness found. | Keep poison resistance. Fire/slash weakness remains a design add-on. |
| Sarras sea creatures | Finbled, Tadpole, Tidewraith, Scion, Archivist, Floatling, and Reefback variants commonly use `Cold 60%`; no vanilla `Electric` weakness found. | `SeaFlesh` should start with Cold resistance and add Electric weakness only as a shared Steel and Bone overlay scaled by preset strength. |
| Lost Knight | 3 templates; `HitStone`; `Electric 200%`, `Poison 50%`, `Fire 50%`; bleed immunity. | The construct Electric weakness is confirmed here. Also respect fire and poison resistance. |
| Forgeborn | Boss template; `HitStone`; `Fire 50%`, `Cold 150%`; bleed immunity. | Make it fire-resistant and Cold-vulnerable; do not assume generic Electric weakness. |
| Flamegobbler | 3 templates; `Fire 0%`. | Fire immunity is confirmed. Cold weakness is not present in vanilla but may be a readable Steel and Bone counter. |
| Ice Weaver | Main template has `Fire 150%`, `Cold 50%`, `Wyrdness 80%`. | Cold resistance and fire weakness are confirmed. |
| Cairnguard | `Cold 50%`, `Poison 50%`, `Fire 150%`; bleed immunity. | Fire weakness is confirmed; Electric weakness is not. |
| Tibby | `Electric 133%`. | Electric weakness is confirmed, but weaker than Lost Knight's. |
| Stagfather variants | Fire golems have `Fire 33%` and `Cold 133%`; ice golems have `Cold 33%` and `Fire 133%`; electric golems have `Electric 33%`; base Stagfather has no vanilla multiplier. | Elemental variants should override base family rules. |
| Wyrdspawn and Wyrdspirits | Wyrdspawn commonly inherits `Abstract:WyrdnessBound`; Wyrdspirits also do. No default `Wyrdness` resistance multiplier was found on their templates. | Use the abstract as a target-family detector, but keep the actual Wyrdness resistance a Steel and Bone design choice. |
| Wyrdstalker | 5 templates inherit `Abstract:Foredweller`, not `Abstract:WyrdnessBound`; no vanilla damage multiplier found. | Classify separately from ordinary Wyrdspawn. |
| Banshee, Melancholy, Ghosts | Banshee and Melancholy are `HitMagic`, but no broad physical resistance multiplier was found. Banshee has bleed/blind immunity. Many ghost-named templates do not inherit `Abstract:Ghost`. | Spirit physical resistance should be modest and configurable until in-game feel confirms it. |

Power bands:

When a local `NpcTemplate` row is available, prefer the template's `Tier:X` tag, `level`, and `NpcType` over this estimated band. The bands remain useful for wiki-only names and design pacing.

| Band | Meaning |
|---:|---|
| 1 | Low threat, tutorial, wildlife, peasants, trash groups |
| 2 | Early common enemies and simple humanoids |
| 3 | Dangerous common enemies, Wyrd packs, spirits, larger beasts |
| 4 | High-threat specialists, constructs, lich-like enemies, stronger undead |
| 5 | Late-region or Sarras-tier common/special enemies |
| 6 | Elite, boss, unique, or encounter-defining enemy |

Rule shorthand:

| Steel and Bone family | Use for | Candidate resistance identity | Candidate weakness identity |
|---|---|---|---|
| `Flesh` | Ordinary humans, animals, humanoid creatures | Minimal resistances | Bleed, poison, slash or pierce depending body shape |
| `ArmoredHumanoid` | Knights, guards, heavy deserters | Slash, `GenericPhysical`, mild bleed | `Bludgeoning`, `Electric`, armor-piercing `Piercing` |
| `InfectedFlesh` | Red Death, corrupted living bodies | Confirmed Red Death pattern: `Poison 66%` | Confirmed Red Death pattern: `Fire 133%`; slash/bleed only as Steel and Bone overlays |
| `Flora` | Dryads, trees, fungal enemies | Poison and pierce only where template or visuals support it | `Fire`, slash, blight/purge if detectable |
| `SeaFlesh` | Reef creatures, Sarras aquatic humanoids | Confirmed Sarras pattern: `Cold 60%`; Wailcaps have `Poison 25%` | `Electric` only as a shared Steel and Bone overlay scaled by preset strength; slash/bleed where fleshy |
| `DrownedZombie` | Drowners and similar drowned undead | Confirmed Drowner pattern: `Fire 50%`, `Status_Blind`; no Drowner bleed immunity found in the CSV audit | `Electric`, blunt, and bleed resistance only as Steel and Bone overlays; do not assume fire weakness |
| `BoneUndead` | Skeletons and dry undead | Bleed immunity, poison low/zero, optional slash/pierce resistance; many higher skeletons have `Cold 75%` | Confirmed: `Bludgeoning 133%`; holy/silver and fire are not confirmed engine subtypes |
| `FleshUndead` | Zombies, bloody undead, Wights, Scourge-type enemies | Bleed/poison/blood depending template; some have no vanilla multiplier | Fire, holy/silver item-term, blunt only where it reads well |
| `LichUndead` | Undead casters and boss casters | Bleed, poison, blood, `Cold` where template confirms it | `Fire`, `Electric`, holy/silver item-term if detectable |
| `Spirit` | Shades, ghosts, mist, banshees | Bleed/poison immunity where status refs confirm it; mild physical resistance if added | Holy/silver item-term, Wyrdness, or purge damage if lore-confirmed |
| `Construct` | Golems, wrought, animated armor | Bleed/poison/blood low; respect per-template elemental resistance such as Lost Knight `Fire 50%` or Cairnguard `Cold 50%` | `Electric` for Lost Knight/Tibby-style targets; `Bludgeoning` only as Steel and Bone overlay unless confirmed |
| `Wyrd` | Wyrdspawn, Wyrdspirits, Wyrdheir, Wyrd-bound enemies | `Abstract:WyrdnessBound` is confirmed on Wyrdspawn/Wyrdspirits, but default Wyrdness resistance is not | Fire, holy/silver item-term, or purifying damage if detectable |

## Common And Bestiary Enemies

| Power | Enemy | Source type/subtype | Candidate Steel and Bone family | Potential resistances | Potential weaknesses | Notes |
|---:|---|---|---|---|---|---|
| 2 | Bee Swarm | Creature / Animal | `SwarmFlesh` or `Flesh` | Pierce, blunt, single-target physical | Confirmed: `Fire 200%`; wide-area effects if detectable | Game template is Tier 2, level 10, and bleed-immune. Keep light rules so swarms do not become tedious. |
| 1 | Wolf | Creature / Animal | `Flesh` | None confirmed | Bleed, poison, slash, pierce | Game templates are ordinary `Abstract:Animal`; no vanilla damage multiplier found. |
| 1 | Desperate Peasant | Human / Peasant | `Flesh` | None | Bleed, poison, slash, pierce | Low-value human baseline. |
| 1 | Outcast Villager | Human / Peasant | `Flesh` | None | Bleed, poison, slash, pierce | Low-value human baseline. |
| 1 | Deranged Archer | Human / Bandit | `Flesh` | None | Bleed, poison, pierce | Light ranged humanoid. |
| 1 | Deranged Infantryman | Human / Bandit | `Flesh` | None or minor slash | Bleed, poison, blunt if armored | Early humanoid melee baseline. |
| 1 | Highwayman | Human / Bandit | `Flesh` | None | Bleed, poison, pierce | Common bandit-style target. |
| 1 | Outlaw | Human / Bandit | `Flesh` | None | Bleed, poison, slash, pierce | Common bandit-style target. |
| 5 | Wailcap | Sea Creature / Animal | `Flora` or `SeaFlesh` | Confirmed: `Poison 25%` | Fire/slash only as Steel and Bone overlay | Game templates are Sarras Tier 5, level 30, `HitFlesh`, not low-tier trash. |
| 5 | Viridian Wailcap | Sea Creature / Animal | `Flora` or `SeaFlesh` | Likely follows Wailcap `Poison` resistance if implemented as same template family | Fire/slash only as Steel and Bone overlay | Treat as Wailcap variant until a separate template is found. |
| 5 | Swarm Of Viridian Wailcaps | Sea Creature / Animal | `Flora` or `SwarmFlesh` | Poison, pierce only as overlay | Fire, slash, area effects if detectable | Keep mild because swarm enemies can become tedious. |
| 2 | Bear | Creature / Animal | `Flesh` | None confirmed for normal bear templates | Bleed, poison, pierce | Big animal; slightly tougher than wolf but still biological. |
| 2 | Corpse Eater | Creature / Humanoid | `Flesh` | Light poison or disease | Fire, bleed, slash | Common corpse-adjacent monster; avoid undead immunities unless testing confirms. |
| 2 | Croakmaw | Creature / Animal | `Flesh` | Poison, `Cold` only if template confirms | `Electric`, slash, bleed | Amphibian logic remains unvalidated in extracted templates. |
| 2 | Curlghast | Creature / Humanoid | `Flesh` | Poison, Wyrdness if corrupted | Fire, bleed, slash | Monster-flesh family. |
| 2 | Drowner | Undead / Draugr | `DrownedZombie` | Confirmed: `Fire 50%`; `Status_Blind`; zombie/bloody/monster abstracts | `Electric`, blunt, or bleed resistance only as Steel and Bone overlay | Vanilla contradicts the earlier fire-weakness assumption. No `Status_Bleed`, `Cold`, or `Electric` multiplier found. |
| 2 | Grindylow | Creature / Humanoid | `Wyrd` or `Flesh` | Confirmed: `Wyrdness 80%` | Confirmed: `Cold 120%`; bleed/slash as overlay | Game templates are `Abstract:Monster`, not zombie/sea abstracts. |
| 2 | Redcap | Creature / Humanoid | `Flesh` | Light poison | Bleed, slash, pierce | Small aggressive flesh target. |
| 2 | Red Death Infected | Human / Red Death Infected | `InfectedFlesh` | Confirmed: `Poison 66%`; bleed immunity | Confirmed: `Fire 133%`; slash as overlay | A clean candidate for a distinct infected family. |
| 2 | Frantic Berserker | Human / Red Death Infected | `Flesh` or `InfectedFlesh` | None confirmed on the named Frantic Berserker template | Fire, slash, bleed only as overlay | Do not inherit Red Death multipliers unless the target is actually a Red Death zombie template. |
| 2 | Wailing Effigy | Human / Red Death Infected | `InfectedFlesh` or `Spirit` | Poison, bleed if mostly effigy/spirit | Fire, holy/silver item-term | Verify whether body or spirit classification feels right. |
| 2 | Remor | Creature / Humanoid | `Flesh` | Light slash or generic physical | Bleed, poison, pierce | Baseline Remor. |
| 2 | Remor Archer | Creature / Humanoid | `Flesh` | None | Bleed, poison, pierce | Ranged variant; same family rules. |
| 2 | Remor Warrior | Creature / Humanoid | `Flesh` | Slash, generic physical | Bleed, poison, blunt | Armored or heavier variant candidate. |
| 2 | Remor Shaman | Creature / Humanoid | `Flesh` | Wyrdness or element used by shaman | Bleed, poison, pierce | Caster variant; keep body weakness. Not validated in extracted template pass. |
| 3 | Buggane | Creature / Animal | `Flesh` | Blunt, generic physical | Poison, bleed, pierce | Big beast; let status builds have a home. |
| 3 | Ogre | Creature / Humanoid | `Flesh` | Blunt, generic physical, stagger-like effects | Poison, bleed, pierce | High-health brute; do not over-resist all physical. |
| 3 | Dryad | Creature / Flora | `Flora` | None confirmed on extracted Dryad templates | Fire/slash only as Steel and Bone overlay | Extracted Dryads are `HitFlesh` with `Abstract:Monster` and female abstract, not a special flora abstract. |
| 3 | Gloomfrond | Creature / Flora | `Flora` | Bleed, poison, pierce | Fire, slash | Plant rule candidate. |
| 3 | Fleshtree | Creature / Flora | `Flora` | Bleed, poison, pierce | Fire, slash, axes if subtype detectable | Larger plant; can be stronger than Dryad. |
| 3 | Mistling | Spirit / Nature | `Spirit` | Some Mistling variants show `Wyrdness 80%`; otherwise no broad spirit multiplier confirmed | Fire, holy/silver item-term, or purge damage only as overlay | Low spirit baseline. |
| 3 | Mistbearer | Spirit / Nature | `Spirit` | Base boss has no vanilla damage multiplier found | Holy/silver item-term, Wyrdness, or purge damage only as overlay | Stronger mist spirit. |
| 3 | Strawchild | Spirit / Nature | `Spirit` or `Flora` | Bleed, poison, pierce only as overlay | Fire, slash, holy/silver item-term | Straw body suggests fire; spirit metadata suggests anti-spirit. Needs template validation. |
| 3 | Strawfather | Spirit / Nature | `Spirit` or `Flora` | Bleed, poison, pierce only as overlay | Fire, slash, holy/silver item-term | Stronger Strawchild variant. Needs template validation. |
| 3 | Wyrd-Touched Peasant | Wyrdtwisted / Human | `Wyrd` | Wyrdness, poison, partial bleed only as overlay | Fire, holy/silver item-term, slash | Keep readable: corrupted flesh is not normal flesh. Needs template validation. |
| 3 | Wyrddeer | Wyrdtwisted / Animal | `Wyrd` | Wyrdness and poison only as overlay | Fire, holy/silver item-term, pierce | No exact extracted template match. |
| 3 | Wyrdslime | Wyrdtwisted / Flora | `Wyrd` or `Flora` | No vanilla damage multiplier found | Fire, `Cold` if slime-solidifying exists | Slime may need special physical tuning. |
| 3 | Wyrdspawn | Wyrdtwisted / Human | `Wyrd` | Confirmed family marker: `Abstract:WyrdnessBound`; no vanilla Wyrdness multiplier found | Fire, holy/silver item-term, slash only as overlay | Existing Steel and Bone Wyrd term. |
| 3 | Wyrdspirit | Wyrdtwisted or Spirit | `Wyrd` and `Spirit` | Confirmed family marker: `Abstract:WyrdnessBound`; no vanilla Wyrdness multiplier found | Holy/silver item-term, purge damage only as overlay | Critter templates with `HP 1`; avoid heavy damage rules. |
| 3 | Wyrdheir | Wyrdtwisted | `Wyrd` | Confirmed one variant: `Cold 60%`; no Wyrdness multiplier found | Fire, holy/silver item-term, pierce | Higher Wyrd enemy; challenge variant differs. |
| 3 | Wyrdstump | Wyrdtwisted / Flora | `Wyrd` and `Flora` | Wyrdness, bleed, poison, pierce only as overlay | Fire, slash | No exact extracted template match. |
| 3 | Undead | Undead / Zombie | `FleshUndead` | Bleed, poison, blood depending template | Fire, holy/silver item-term, blunt only as overlay | Generic undead row. |
| 3 | Abandoned Warrior | Undead / Skeleton | `BoneUndead` or `ArmoredHumanoid` | Follow confirmed skeleton pattern if template inherits `Abstract:Skeleton` | Confirmed skeleton counter: `Bludgeoning`; holy/silver item-term or fire only as overlay | Skeleton plus warrior gear. |
| 3 | Skeleton Mage | Undead / Skeleton | `BoneUndead` | Confirmed skeleton pattern: bleed immunity, `Bludgeoning 133%`; high variants can have `Cold 75%` | Blunt confirmed; holy/silver item-term or `Electric` only as overlay | Good test for anti-undead plus anti-caster behavior. |
| 3 | Banshee | Spirit / Shade | `Spirit` | Confirmed bleed/blind immunity; no broad physical multiplier found | Holy/silver item-term, Wyrdness, or purge damage only as overlay | `HitMagic` surface supports special feedback, not necessarily hard physical resistance. |
| 3 | Ghost | Spirit / Shade | `Spirit` | No broad physical multiplier found; some ghost-named templates are not `Abstract:Ghost` | Holy/silver item-term, Wyrdness, or purge damage only as overlay | Use cautious spirit rules unless runtime target abstracts are available. |
| 3 | Melancholy | Spirit / Shade | `Spirit` | No vanilla damage multiplier found | Holy/silver item-term, Wyrdness, or purge damage only as overlay | `HitMagic` surface supports special feedback. |
| 3 | Squire | Spirit / Shade | `Spirit` or `ArmoredHumanoid` | Bleed, poison, slash, pierce only as overlay | Holy/silver item-term, blunt | If visually armored, blunt can remain relevant. |
| 4 | Lost Knight | Construct / Golem | `Construct` | Confirmed: `Poison 50%`, `Fire 50%`; bleed immunity | Confirmed: `Electric 200%`; blunt as overlay | Existing construct logic should preserve the very strong vanilla Electric weakness. |
| 4 | Golem | Construct / Golem | `Construct` | Elemental golems vary by subtype | Opposite element; blunt as overlay | Do not use one universal golem rule. Fire/Ice/Electric variants have different confirmed multipliers. |
| 4 | Bottomless | Construct / Golem | `Construct` | No extracted template match under that exact name | Blunt, `Electric` only as overlay | Needs runtime target-name validation. |
| 4 | Forgeborn | Construct / Golem | `Construct` | Confirmed: `Fire 50%`; bleed immunity | Confirmed: `Cold 150%`; blunt as overlay | Boss template is `HitStone`, Tier 6. |
| 4 | Brimshade | Construct / Fore-Dweller Wrought | `Construct` or `Spirit` | Bleed, poison, blood, slash, pierce only as overlay | `Electric`, holy/silver item-term | No exact extracted template match in NPC bundle; verify in runtime logs. |
| 4 | Flamegobbler | Undead / Draugr | `FleshUndead` or `FireFlesh` | Confirmed: `Fire 0%` | `Cold`, holy/silver item-term, blunt only as overlay | Vanilla fire immunity is stronger than the earlier "fire resistance" guess. |
| 4 | Frostbitten Warrior | Undead / Draugr | `FleshUndead` and `ArmoredHumanoid` | No vanilla damage multiplier found; zombie/bloody/monster abstracts | Fire, holy/silver item-term, blunt only as overlay | Name suggests cold, but templates do not confirm `Cold` resistance. |
| 4 | Ice Weaver | Undead / Lich | `LichUndead` or `ColdSpecial` | Confirmed main template: `Cold 50%`, `Wyrdness 80%` | Confirmed: `Fire 150%`; holy/silver item-term as overlay | Caster/lich plus Cold identity. |
| 4 | Blood Abomination | Undead / Lich | `BoneMask` or `LichUndead` | Confirmed: `Wyrdness 80%` | Confirmed: `Cold 120%`; fire/holy/silver only as overlay | Game uses `Abstract:BoneMask`, not a lich abstract. |
| 4 | Wight | Wyrdtwisted / Flora | `FleshUndead` | No vanilla damage multiplier found; zombie/bloody/boss/monster abstracts | Fire, holy/silver item-term, blunt only as overlay | Game reads more like boss undead than Wyrd flora. |
| 4 | Hungerfrost | Wyrdtwisted / Human | `Wyrd` or `ColdSpecial` | No exact extracted template match | Fire, holy/silver item-term, slash | Frost-corrupted human remains unvalidated. |
| 4 | Sleepwalker | Wyrdtwisted / Human | `Wyrd` or `Construct` | Confirmed: `Cold 75%`, `Poison 75%` | Confirmed: `Fire 133%`; holy/silver item-term as overlay | Template is Tier 6 MiniBoss with `HitStone`. |
| 4 | Ancient Beholder | Creature / Humanoid | `Flesh` or `Spirit` | Poison, psychic/Wyrd if detectable | Pierce, `Electric`, holy/silver item-term | Needs in-game validation. |
| 4 | Crimson Creeper | Creature / Animal | `Flesh` | Poison, bleed if carapaced | Fire, blunt, pierce | Verify carapace/creeper visuals. |
| 4 | Frostgrot | Creature / Animal | `Flesh` | `Cold` only if runtime target confirms | Fire, bleed, pierce | No exact extracted template match. |
| 4 | Nuckelavee | Creature / Animal | `Flesh` or `Wyrd` | Poison, Wyrdness if corrupted | Fire, bleed, pierce | High folklore monster; tune carefully. |
| 4 | Morgen | Creature / Humanoid | `SeaFlesh` or `Spirit` | Poison, `Cold`, physical if spirit-like | `Electric`, holy/silver item-term, slash | No exact extracted template match. |
| 5 | Cairnguard | Construct / Golem | `Construct` | Confirmed: `Cold 50%`, `Poison 50%`; bleed immunity | Confirmed: `Fire 150%`; blunt as overlay | Late construct/golem; vanilla does not confirm `Electric` weakness. |
| 5 | Rimefiend | Special, Cold enemy | `ColdSpecial` | Confirmed: `Cold 60%` | Fire, holy/silver item-term only as overlay | Template is Tier 5, level 100 Elite. |
| 5 | Reefbound | Sea Creature / Humanoid | `SeaFlesh` | `Cold` resistance likely from Sarras sea pattern, but no exact `Reefbound` NPC template except abstract `ReefboundBody` | `Electric`, slash, bleed only as overlay | Need runtime target validation. |
| 5 | Finbled | Sea Creature / Humanoid | `SeaFlesh` | Confirmed: `Cold 60%` | `Electric`, slash, bleed only as overlay | Sarras Tier 5 humanoid/monster pattern. |
| 5 | Tadpole | Sea Creature / Humanoid | `SeaFlesh` | Confirmed: `Cold 60%` | `Electric`, slash, bleed only as overlay | Sarras Tier 5 pattern. |
| 5 | Tidewraith | Sea Creature / Flora | `SeaFlesh` or `Spirit` | Confirmed: `Cold 60%` | `Electric`, holy/silver item-term, fire only as overlay | Template is `Abstract:Monster`, not a confirmed spirit abstract. |
| 5 | Scion Of The Depths | Sea Creature / Humanoid | `SeaFlesh` | Confirmed: `Cold 60%`; blind immunity on variants | `Electric`, slash, holy/silver item-term only as overlay | Higher sea humanoid. |
| 5 | Drowned Knight | Undead / Draugr | `SeaFlesh`, `DrownedZombie`, or `ArmoredHumanoid` | Confirmed Sarras Drowned Knight rows use `Cold 60%`; miniboss variants have `Status_Blind` | `Electric` and blunt only as overlays | Do not inherit Drowner `Fire 50%` unless runtime evidence shows it is actually a Drowner-template target. |
| 5 | Archivist | Sea Creature / Humanoid | `SeaFlesh` or `CasterFlesh` | Confirmed: `Cold 60%` | `Electric`, pierce, bleed only as overlay | Sarras caster/intellectual enemy. |
| 5 | Floatling | Sea Creature / Animal | `SeaFlesh` | Confirmed: `Cold 60%` | `Electric`, slash, pierce only as overlay | Critter templates have `HP 1`; avoid high-friction rules. |
| 5 | Kelpie | Sea Creature / Animal | `Flesh` or `Animal_Prey` | None confirmed | Bleed, poison, slash/pierce | Extracted template is `SoS_AnimalKelpie`, level 1, `Abstract:Animal_Prey`. |
| 5 | Reefback | Sea Creature / Animal | `SeaFlesh` | Confirmed ReefbackFleshTree variants: `Cold 60%` | `Electric`, blunt only as overlay | Extracted variants have `HP 1`, likely encounter props or weak points; validate in game. |
| 5 | Ghost Crew | Undead / Draugr | `DrownedZombie` or `Spirit` | No exact extracted template match | Holy/silver item-term, fire, `Electric` only as overlay | Hybrid undead/spirit candidate. |
| 6 | Keeper Of The Barrow | Special, likely undead/elite | `BoneUndead` | Confirmed: `Cold 66%`; bleed immunity | Confirmed: `Bludgeoning 133%`; holy/silver item-term as overlay | Template is Tier 6, level 100 Boss, `HitBones`, `Abstract:Skeleton`. |
| 6 | Giant Sentinel | Construct / Fore-Dweller Wrought | `Construct` or `BoneMask` | Confirmed: `Wyrdness 80%` | Confirmed: `Cold 120%`; blunt/`Electric` only as overlay | Template is Tier 1 but `NpcType.Boss`, so use `NpcType` over tier alone. |
| 6 | Scourge Of The Seas | Undead / Draugr | `DrownedZombie` | No vanilla damage multiplier found; zombie/bloody/monster abstracts | Fire, holy/silver item-term, `Electric` only as overlay | Boss, Tier 4, level 30. |
| 6 | Tibby | Construct / Golem | `Construct` | Bleed/blood/poison only as overlay | Confirmed: `Electric 133%`; blunt as overlay | Named MiniBoss; use elite clamp. |
| 6 | Stagfather | Spirit / Nature | `FleshUndead`, `Boss`, or elemental variant | Base boss has no multiplier; elemental golems resist their element | Opposite element for golem variants; holy/silver item-term only as overlay | Do not hard-counter the base boss with broad spirit rules. |

## Additional Named Or Non-Bestiary Watchlist

These names appeared in the broader `NPC-Enemy` category or map checklists but are less useful as "common enemy" rows. They are still useful for target-term seeding and elite-clamp testing.

| Power | Enemy or group | Likely family | Potential resistances | Potential weaknesses | Implementation note |
|---:|---|---|---|---|---|
| 2 | Bald Cait, Bromhar No Face, Exiled Nobleman, Fingerless Colm, Rusty Ardghal, Scumrot, Silent Silia | `Flesh` | None or light armor-based physical | Bleed, poison, pierce | Named bandits; usually inherit human rules. |
| 3 | Cindermar The Firebringer, Sanguinor The Crimson, Thorne The Bloodbound | `Flesh` or `InfectedFlesh` | Fire or blood depending kit | Pierce, poison, `Electric` | Named humans with theme-specific exceptions. |
| 3 | Red Guard, Red Priest, Tainted Red Priest, Corrupted Priestess | `Flesh`, `InfectedFlesh`, or `CasterFlesh` | Fire/blood/Wyrdness depending spell kit | Pierce, `Electric`, bleed if living | Red Church/caster terms may be useful. |
| 3 | Kamelot Deserter, Doubting Deserter, Poisoned Knight | `ArmoredHumanoid` | Slash, `GenericPhysical`, poison for Poisoned Knight | `Bludgeoning`, `Electric`, pierce | Good armor-tag candidates if target text exposes them. |
| 3 | Druag Fir Bolg, Mael Fir Bolg, Torc Fir Bolg | `ArmoredHumanoid` or `Flesh` | Slash, generic physical | Blunt, bleed, poison | Faction variants; tune after in-game samples. |
| 4 | Fae | `Spirit` or `Wyrd` | Bleed, poison, some physical only as overlay | Holy/silver item-term, iron/silver item-term if detectable, fire | Metadata parse was incomplete; verify before adding rules. |
| 4 | Eldritch Reaver | `Wyrd` | Wyrdness and poison only as overlay | Holy/silver item-term, fire, pierce | High-threat Wyrd human candidate. |
| 4 | Plaguewraith | `FleshUndead` or `Spirit` | No vanilla damage multiplier found on `EnemyZombie_T6_Plaguewraith` | Holy/silver item-term, fire, `Electric` only as overlay | Extracted template is zombie/bloody/monster, not confirmed spirit. |
| 4 | Wyrdstalker | `ForeDweller` | No vanilla damage multiplier found; confirmed `Abstract:Foredweller` | Holy/silver item-term, fire only as overlay | Wyrdnight stalker should not automatically inherit Wyrdspawn rules. |
| 5 | Queen Sagremor | `LichUndead` | Bleed, poison, blood only as overlay | Holy/silver item-term, fire, `Electric` only as overlay | Named undead boss candidate. |
| 5 | Perceval, Sir Bertilak, Sir Gawain, Sir Lohengrin, Sir Rennard, Sir Vaelin | `ArmoredHumanoid` or special boss | Slash, `GenericPhysical`, bleed if heavily armored | `Bludgeoning`, `Electric`, pierce | Knight terms can seed armored humanoid rules. |
| 5 | Kestrel The Piercer | `Flesh` or `ArmoredHumanoid` | None or light physical | Bleed, poison, blunt if armored | Named ranged/human enemy. |
| 5 | Nivera The Chillheart, The Frostbound Consort | `ColdSpecial` | Confirmed Nivera: `Cold 50%`; bleed/poison only if undead/spirit | Fire, `Electric`, holy/silver item-term | Frost-themed elites. |
| 6 | Crystal Kyrus, Drastus, Tainted Merlin, Ylvren The Awakened | Special boss/caster | Depends on element and story state | Pierce, `Electric`, holy/silver item-term if corrupted | Treat as elite exceptions layered on family rules. |
| 6 | Senan The Giant | Giant `Flesh` | Blunt, generic physical, stagger | Poison, bleed, pierce | Big-body rules; use conservative weakness values. |

## Candidate Implementation Families

These buckets are probably more useful than one-off rules for every enemy.

| Priority | Family | Status | Seed terms | Rules or validation focus |
|---:|---|---|---|---|
| 1 | Bone undead | Implemented; validate in game | `Skeleton`, `Bone`, `Abandoned Warrior`, `Skeleton Mage`, `Abstract:Skeleton`, `HitBones` | Current rules resist blood/bleed, slash/pierce, and generic physical; blunt is a small Steel and Bone weakness only when vanilla has not already handled it. Confirm target classification and vanilla `Bludgeoning 133%` skips. |
| 1 | Drowned zombies | Implemented; validate in game | `Drowner`, `Drowned`, `Drowned Knight`, `Ghost Crew`, `Scourge`, `Abstract:Zombie` plus water/dead naming | Current rules resist blood/bleed and add modest Electric/blunt weakness. Confirm that vanilla Drowner `Fire 50%` remains preserved. |
| 1 | Constructs and animated armor | Implemented; validate in game | `Golem`, `Construct`, `Sentinel`, `Forgeborn`, `Cairnguard`, `Lost Knight`, `Tibby`, `Bottomless`, `Brimshade`, `HitStone` | Current rules resist blood/bleed/poison, slash/pierce, and generic physical; blunt is a Steel and Bone weakness. Confirm template-specific elemental exceptions still win through vanilla skip logic. |
| 1 | Wyrd | Implemented; design-call validation | `Wyrd`, `Wyrdspawn`, `Wyrdspirit`, `Wyrdslime`, `Wyrdheir`, `Abstract:WyrdnessBound` | Current rule makes Wyrd targets resist Wyrdness. Validate whether that feels right, because vanilla templates do not prove a broad Wyrdspawn/Wyrdspirit Wyrdness multiplier. |
| 2 | Red Death infected | Implemented; validate in game | `Red Death`, `Infected`, `Abstract:Zombie`, `Abstract:Bloody` | Current rules resist Poison and add Fire weakness only when vanilla has not already handled Fire. Confirm Red Death vanilla Poison/Fire multipliers are skipped rather than duplicated. |
| 2 | Flesh undead | Implemented cautiously; validate in game | `Zombie`, `Undead`, `Wight`, `Bloody`, `Frostbitten Warrior`, `Plaguewraith` | Current rules mildly resist blood/bleed/poison and add modest Fire/blunt weakness. Confirm DrownedZombie and InfectedFlesh terms refine broad FleshUndead metadata when present. |
| 2 | Spirits and shades | Implemented; validate in game | `Ghost`, `Banshee`, `Melancholy`, `Mist`, `Spirit`, `Wraith`, `HitMagic` | Current rules resist blood/bleed/poison and modestly resist physical. Confirm the physical penalty is readable without becoming a hard wall. |
| 2 | Sarras sea flesh | Implemented; validate in game | `Finbled`, `Tadpole`, `Tide`, `Floatling`, `Archivist`, `Scion`, `Reef`, `Type:SarrasCreature` | Current rules resist Cold and add modest Electric weakness. Confirm common vanilla `Cold 60%` rows are preserved by subtype skip logic. |
| 3 | Flora and fungus | Implemented; validate in game | `Dryad`, `Frond`, `Tree`, `Wailcap`, `Stump`, `Fleshtree` | Current rules resist Poison/Bleed/Pierce and add Fire/Slash weakness. Confirm false positives, especially Wailcap overlap with `SeaFlesh`. |
| 3 | Armored humanoids | Implemented cautiously; validate in game | `Knight`, `Guard`, `Squire`, `Warrior`, `Deserter`, `Kamelot`, `Soldier`, `Armor`, `Armored` | Current rules resist slash/`GenericPhysical` and add `Bludgeoning` weakness. Armor terms can override broad `Flesh` only; armor-piercing `Piercing` stays neutral until runtime armor penetration evidence is reliable. |
| 4 | Ordinary flesh | Implemented cautiously; validate in game | `Wolf`, `Bear`, `Bandit`, `Outlaw`, `Human`, `Humanoid`, `Remor`, `Redcap`, `Corpse Eater` | Current rules add very mild bleed/poison/slash/pierce bonuses only when no more specific family wins first. |

## Completed Local Validations

| Finding | Result |
|---|---|
| Runtime damage subtype names | Confirmed: `Fire`, `Cold`, `Poison`, `Electric`, `Wet`, `Wyrdness`, `GenericPhysical`, `Slashing`, `Piercing`, `Bludgeoning`, `GenericMagical`, and `Pure`. There is no confirmed `Frost`, `Shock`, `Holy`, or `Silver` `DamageSubType`. |
| Enemy power metadata | Confirmed `NpcType` and `Tier:X` tags on serialized templates. Use both, because edge cases exist, such as Giant Sentinel being `NpcType.Boss` while tagged `Tier:1`. |
| Vanilla resistance source | Confirmed `NpcTemplate.damageReceivedMultipliers` stores per-subtype damage taken percentages. This is the best source for preserving vanilla intent. |
| Abstract family source | Confirmed `_abstractTypes` on templates. Steel and Bone 0.9.0 uses reachable metadata before name/term fallback, with `Flesh` and `FleshUndead` as cautious broad fallbacks and `ArmoredHumanoid` as a high-specificity term override only for broad flesh/flesh-undead cases. |

## Open Validation Tasks

| Task | Why it matters |
|---|---|
| Confirm whether weapon hit data reliably exposes `Slashing`, `Piercing`, and `Bludgeoning` for every melee/ranged path. | The enum exists, but the mod still needs to confirm each player attack carries the right subtype at the damage hook. |
| Confirm whether any item, status, perk, or spell text exposes holy/silver/purge/iron semantics. | These are not `DamageSubType` enum values, so Steel and Bone can only support them if another runtime marker exists. |
| Capture diagnostic logs for five samples per family. | Name-based target classification should be based on real object text, not just wiki names. |
| Validate broad `Flesh`, broad `FleshUndead`, and specific `ArmoredHumanoid` precedence. | 0.9.0 intentionally keeps broad body families mild and lets armor override only broad flesh/flesh-undead, so real logs should confirm it does not steal stronger families. |
| Validate elite-class flags and clamps. | `targetFlags=EliteClass`, `eliteClamp`, and `elite clamp neutralized custom rule` diagnostics should prove elite moderation is working without becoming a new resistance family. |
| Decide whether "Wyrdness" should resist Wyrd enemies or destabilize them. | `Abstract:WyrdnessBound` is confirmed, but vanilla does not apply a default Wyrdness multiplier to Wyrdspawn/Wyrdspirits. This is a design call. |
| Validate vanilla-skip behavior against `damageReceivedMultipliers`. | The design decision is implemented through `RespectVanillaMultipliers = true`: preserve vanilla first, then apply shared overlays only where vanilla is neutral for that same subtype. Diagnostics should confirm this for skeleton Bludgeoning, Drowner Fire, Red Death Fire/Poison, Sarras Cold, and construct elemental exceptions. |
| Capture runtime evidence for names not found in `templates.npc_assets_all.bundle`, such as Bottomless, Brimshade, Hungerfrost, Frostgrot, Morgen, and Ghost Crew. | They may be spawned through alternate templates, localized names, or encounter-specific prefabs. |
| Decide whether aquatic enemies are a separate family or just flesh/spirit/undead variants. | Sarras sea creatures share a `Cold 60%` pattern, but Drowners are zombie/bloody templates with `Fire 50%`, so one universal aquatic rule would be inaccurate. |
