# NPC Template Resistance Summary

This is the evidence appendix for exported NPC template resistances. Use [../steel-and-bone-enemies.md](../steel-and-bone-enemies.md) for the maintained enemy table and [../steel-and-bone-design.md](../steel-and-bone-design.md) for implementation choices.

## Scope and methodology

This summary is an evidence-first audit of `exports/npc-template-resistance-export.csv`. All numeric claims, counts, direct damage patterns, status-immunity counts, and family observations below are derived from that CSV unless explicitly labeled otherwise. The other files in the archive were treated as secondary context only: `README.txt` for export interpretation, the existing `exports/npc-template-resistance-summary.md` as a draft to supersede, and the included game files (`templates.npc_assets_all.bundle`, `TG.Main.dll`, `catalog.json`, `link.xml`) as support material rather than primary numeric evidence.

Interpretation follows the package rules exactly. `damageReceivedMultipliers` are percentage multipliers by `DamageSubType`; values below `100%` are resistances, values above `100%` are weaknesses, and `0%` is a direct-damage immunity. Status immunities in `status_invulnerabilities` are separate from direct damage multipliers and should not be merged conceptually with them. This report therefore treats direct damage multipliers and status immunities as two different systems.

Enemy-only counts use `is_enemy = true`. Family analysis uses heuristic bucketing from `abstract_families`, `surface_type`, `tags`, `category`, template names, and `asset_path` patterns. That is useful for finding stable family themes, but it is not proof that any single metadata field alone drives runtime combat logic. Public web sources were used only to confirm outward-facing context and naming for the Red Death, Wyrdness, Wyrd-linked enemies, Drowners, and Sanctuary of Sarras. Those web sources do **not** override the CSV; they are used only to anchor terminology and lore context. citeturn4search5turn1search1turn1search11turn3search2turn3search3turn0search3

This report intentionally does **not** invent unsupported damage types. In this dataset, the relevant subtype list is the one provided with the research package: `Default`, `Pure`, `Wyrdness`, `GenericPhysical`, `Slashing`, `Piercing`, `Bludgeoning`, `GenericMagical`, `Fire`, `Cold`, `Poison`, `Electric`, and `Wet`.

## CSV field glossary

| CSV field | How it is used here |
| --- | --- |
| readable_name | Human-readable export name used in the tables below. |
| template_name | Underlying prefab/template identifier. Useful when readable names are duplicated or variant-heavy. |
| asset_path | Prefab path. Strong signal for region/variant buckets such as `Enemies/Sarras`, `Enemies/Bosses`, `Summons`, or tutorial/test content. |
| category | Folder-derived bucket from the export, e.g. `Enemies/Monsters`, `Enemies/Sarras`, `Npc/...`. |
| is_enemy | Whether the template is exported as an enemy. Enemy-only counts in this report require `is_enemy = true`. |
| is_abstract | Whether the row is an abstract/base template rather than a concrete spawned NPC template. |
| level | Template level from the export. |
| max_health | Template max HP from the export. |
| armor | Base armor value from the export. |
| armor_multiplier | Armor scaling multiplier from the export. Not a damage-subtype resistance field. |
| status_resistance | Separate numeric status-resistance field. This summary does not treat it as an immunity unless `status_invulnerabilities` explicitly says so. |
| surface_type | Hit material/surface tag such as `HitFlesh`, `HitBones`, `HitStone`, `HitMagic`, or `HitWood`. Useful for family clustering, not a direct resistance field by itself. |
| abstract_families | Semicolon-separated family labels inherited/associated with the template, e.g. `Abstract:Skeleton`, `Abstract:Zombie`, `Abstract:Ghost`. |
| damage_multipliers | All exported direct damage multiplier entries. This can include neutral entries such as `GenericPhysical:100% neutral`. |
| non_neutral_damage_multipliers | Only non-neutral direct damage multipliers. This is the main direct-resistance field used for counts and rule extraction. |
| status_invulnerabilities | Explicit status immunities. Separate from direct damage multipliers. |
| tags | Semicolon-separated metadata tags such as tier, elite status, or special family flags like `Type:SarrasCreature` and `merlin:Golem`. |

`difficulty_tag` is entirely empty in this export. `damage_multipliers` is broader than `non_neutral_damage_multipliers`: it can include explicitly neutral entries, while `non_neutral_damage_multipliers` is the correct field for extracting actual resistances, weaknesses, and direct immunities.

## Dataset totals and pattern frequencies

The CSV contains both enemy and non-enemy NPC templates, plus abstract/base templates. For resistance research aimed at combat balance, the most important split is enemy-only versus all rows.

| Metric | Count |
| --- | --- |
| Total NPC templates | 893 |
| Enemy templates (`is_enemy = true`) | 469 |
| Abstract templates (`is_abstract = true`) | 30 |
| Templates with any `damage_multipliers` entry | 170 |
| Templates with non-neutral direct multipliers (`non_neutral_damage_multipliers`) | 163 |
| Enemy templates with non-neutral direct multipliers | 151 |
| Templates with status immunities (`status_invulnerabilities`) | 179 |
| Enemy templates with status immunities | 163 |
| Enemy templates with both non-neutral direct multipliers and status immunities | 99 |

Two methodological findings matter immediately. First, all `30` abstract templates have **no** non-neutral direct damage multipliers and **no** status immunities in this export, so the usable resistance data lives on concrete templates rather than abstract bases. Second, `170` templates have some `damage_multipliers` entry, but only `163` have a non-neutral direct multiplier. The other `7` are neutral-only rows with `GenericPhysical:100% neutral`, so they should not be interpreted as resistances or weaknesses.

Only six damage subtypes appear with any non-neutral direct multiplier in the enemy data: `Wyrdness`, `Bludgeoning`, `Fire`, `Cold`, `Poison`, and `Electric`. No enemy template in this export has a non-neutral direct multiplier for `Default`, `Pure`, `GenericPhysical`, `Slashing`, `Piercing`, `GenericMagical`, or `Wet`.

| Damage subtype | Enemy templates affected | Observed non-neutral values |
| --- | --- | --- |
| Wyrdness | 20 | 80%×20 |
| Bludgeoning | 39 | 133%×39 |
| Fire | 49 | 0%×4, 25%×1, 33%×2, 50%×18, 120%×8, 133%×10, 150%×4, 200%×2 |
| Cold | 74 | 33%×2, 50%×5, 60%×34, 66%×1, 75%×14, 120%×11, 133%×2, 150%×5 |
| Poison | 25 | 0%×8, 25%×2, 50%×6, 66%×6, 75%×1, 150%×2 |
| Electric | 10 | 33%×2, 50%×2, 133%×1, 150%×2, 200%×3 |

The full frequency of exact direct-multiplier patterns is:

| Pattern | All templates | Enemy templates |
| --- | --- | --- |
| Cold:60% | 41 | 34 |
| Bludgeoning:133% | 26 | 26 |
| Bludgeoning:133%; Cold:75% | 12 | 12 |
| Wyrdness:80%; Cold:120% | 12 | 11 |
| Fire:50% | 11 | 11 |
| Wyrdness:80%; Fire:120% | 8 | 8 |
| Poison:0% | 8 | 8 |
| Fire:50%; Poison:50%; Electric:200% | 7 | 3 |
| Fire:133%; Poison:66% | 6 | 6 |
| Fire:50%; Cold:150% | 4 | 4 |
| Fire:0% | 3 | 3 |
| Electric:33% | 2 | 2 |
| Poison:25% | 2 | 2 |
| Fire:150%; Cold:50% | 2 | 2 |
| Poison:50%; Electric:150% | 2 | 2 |
| Poison:150%; Electric:50% | 2 | 2 |
| Fire:33%; Cold:133% | 2 | 2 |
| Fire:133%; Cold:33% | 2 | 2 |
| Fire:200% | 2 | 2 |
| Bludgeoning:133%; Cold:66% | 1 | 1 |
| Fire:0%; Cold:150% | 1 | 1 |
| Cold:50% | 1 | 1 |
| Fire:150%; Cold:50%; Poison:50% | 1 | 1 |
| Electric:133% | 1 | 1 |
| Wyrdness:80%; Fire:150%; Cold:50% | 1 | 1 |
| Fire:133%; Cold:75%; Poison:75% | 1 | 1 |
| Fire:133%; Cold:75% | 1 | 1 |
| Fire:25% | 1 | 1 |

True direct-damage immunities (`0%`) appear on `12` enemy templates:

| Template | Direct immunity pattern | Category |
| --- | --- | --- |
| EnemyMonster_T1_Flamegobbler | Fire:0% | Enemies/Monsters |
| EnemyMonster_T3_FlamegobblerCuanacht | Fire:0% | Enemies/Monsters |
| EnemyMonster_T6_Flamegobbler_EndGameFireTrial | Fire:0% | Enemies/Monsters |
| Enemy_Elite_Tier4_CindermarTheFirebringer | Fire:0%; Cold:150% | Enemies/Humans/Elite |
| EnemyMonster_T4_Barnaclator | Poison:0% | Enemies/Monsters |
| EnemyMonster_T4_BarnaclatorElite | Poison:0% | Enemies/Monsters |
| EnemyMonster_T4_Barnaclator_SmallStory | Poison:0% | Enemies/Monsters |
| EnemyMonster_T4_GhostInPainting | Poison:0% | Enemies/Monsters |
| EnemyMonster_T4_Nuckelavee | Poison:0% | Enemies/Monsters |
| EnemyMonster_T4_Nuckelavee_GrandpaInWell | Poison:0% | Enemies/Monsters/Custom |
| Enemy_Generic_Tier4_PoisonedWarrior_1H | Poison:0% | Enemies/Humans/Soldiers |
| Enemy_Generic_Tier4_PoisonedWarrior_2H | Poison:0% | Enemies/Humans/Soldiers |

A few pattern summaries are especially stable across the CSV. `Bludgeoning:133%` is always a weakness pattern and appears on `39` enemy templates. `Wyrdness:80%` is always a resistance pattern and appears on `20` enemy templates. `Cold:60%` is the single most common exact pattern, appearing on `34` enemy templates and concentrating heavily in Sarras/aquatic content. The export contains no evidence for non-neutral `holy`, `silver`, or any other unsupported subtype, so those should not be described as vanilla damage rules on the basis of this dataset alone.

## Enemy templates with non-neutral direct damage multipliers

The table below lists all `151` enemy templates with non-neutral direct damage multipliers, sorted by exact direct pattern and then template name. `Flags` are heuristic labels derived from template/category/path naming to highlight summons, arena variants, scalable variants, trials, tutorials, and similar research caveats.

<details>
<summary>Enemy templates with non-neutral direct damage multipliers</summary>

| Template | Category | Lvl | HP | Surface | Direct pattern | Status immunities | Flags |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EnemyBoss_Skeleton_Archmage | Enemies/Bosses | 30 | 8000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T3_Skeleton1H_Cuanacht | Enemies/Monsters | 20 | 450 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T3_Skeleton2H_Cuanacht | Enemies/Monsters | 20 | 450 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T4_Skeleton1H_Sagremor | Enemies/Monsters | 30 | 1400 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonGalahad1H_Random | Enemies/Monsters | 30 | 2800 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonGalahad1H_Unique1 | Enemies/Monsters | 45 | 2500 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonGalahad1H_Unique2 | Enemies/Monsters | 45 | 2500 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonGalahad1H_Unique3 | Enemies/Monsters | 45 | 2500 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonGalahad1H_Unique4 | Enemies/Monsters | 45 | 2500 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemySkeleton_Mage | Enemies/Monsters | 40 | 1000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemySkeleton_Mage_Summon | Enemies/Monsters | 30 | 3000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon |
| EnemySkeleton_Mage_Summon_Merlin | Enemies/Monsters | 100 | 8000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon |
| EnemySkeleton_Mage_Summon_Thrash | Enemies/Monsters | 30 | 600 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon |
| EnemySkeleton_Mage_Summon_Thrash_Merlin | Enemies/Monsters | 30 | 3500 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon |
| EnemySkeleton_Melee1H | Enemies/Monsters | 5 | 50 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemySkeleton_Melee1H_MistbearerSummon | Enemies/Monsters/Custom | 5 | 75 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_Melee1H_Summon | Enemies/Monsters/Custom | 5 | 60 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_Melee1H_Summon_Better | Enemies/Monsters/Custom | 15 | 140 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_Melee2H | Enemies/Monsters | 5 | 60 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemySkeleton_Melee2H_KamelotDefender | Enemies/Monsters/Custom | 10 | 1000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Custom |
| EnemySkeleton_MeleeShieldSpear_Summon_Arthur | Enemies/Monsters/Custom | 40 | 1000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_MeleeShieldman_Summon_Arthur | Enemies/Monsters/Custom | 40 | 1000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_Melee_Summon_Arthur | Enemies/Monsters/Custom | 40 | 1000 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon; Custom |
| EnemySkeleton_T2_UndeadHOS_Melee1H | Enemies/Special | 5 | 150 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemySkeleton_T3_IndependentSummon | Enemies/Monsters | 15 | 140 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind | Summon |
| EnemySkeleton_T4_UndeadRanger_Melee1H | Enemies/Special | 30 | 1200 | HitBones | Bludgeoning:133% | Status_Bleed; Status_Blind |  |
| EnemyBoss_KeeperOfTheBarrow | Enemies/Bosses | 100 | 15000 | HitBones | Bludgeoning:133%; Cold:66% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T4_Skeleton1H | Enemies/Monsters | 30 | 1400 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T4_Skeleton2H | Enemies/Monsters | 30 | 1400 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T4_SkeletonArcher | Enemies/Monsters | 30 | 900 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_Skeleton1H | Enemies/Monsters | 40 | 2000 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_Skeleton2H | Enemies/Monsters | 40 | 2200 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonArcher | Enemies/Monsters | 40 | 2200 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonWatchful1H | Enemies/Monsters | 40 | 2800 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_SkeletonWatchful2H | Enemies/Monsters | 40 | 3000 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T6_SkeletonDalRiataElite | Enemies/Monsters/Custom | 50 | 7000 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind | Custom |
| EnemyMonster_T6_SkeletonElite | Enemies/Monsters/Custom | 50 | 6500 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind | Custom |
| EnemyMonster_T6_SkeletonKnight | Enemies/Monsters | 60 | 7000 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T6_SkeletonKnightElite | Enemies/Monsters/Custom | 50 | 8000 | HitBones | Bludgeoning:133%; Cold:75% | Status_Bleed; Status_Blind | Custom |
| Enemy_Elite_Tier4_NiveraTheChillheart | Enemies/Humans/Elite | 35 | 1750 | HitFlesh | Cold:50% |  |  |
| EnemyMonster_T6_Rimefiend_IceTrial | Enemies/Monsters/Custom | 100 | 1400 | HitFlesh | Cold:60% | Status_Blind | Trial; Custom |
| EnemyMonster_T6_Wyrdheir | Enemies/Monsters | 30 | 1400 | HitFlesh | Cold:60% | Status_Blind |  |
| SoS_EnemyMonster_T4_Finbled_Heavy | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Finbled_HeavyHatchery | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Finbled_Heavy_ArenaVariant | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Arena variant; Scalable |
| SoS_EnemyMonster_T4_Finbled_JavelinThrower | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Finbled_JavelinThrower_HeroSummon | Enemies/Sarras/HeroSummons | 30 | 1300 | HitFlesh | Cold:60% |  | Summon; Scalable |
| SoS_EnemyMonster_T4_Finbled_Light | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Finbled_LightHatchery | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Finbled_Light_ArenaVariant | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Arena variant; Scalable |
| SoS_EnemyMonster_T4_Finbled_Light_Friendly | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_Floatling | Enemies/Sarras | 20 | 1 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_GoldFloatling | Enemies/Sarras/Custom | 20 | 1 | HitFlesh | Cold:60% |  | Custom; Scalable |
| SoS_EnemyMonster_T4_RedFloatling | Enemies/Sarras/Custom | 20 | 1 | HitFlesh | Cold:60% |  | Custom; Scalable |
| SoS_EnemyMonster_T4_Tadpole | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T4_TadpoleHatchery | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T5_Archivist | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T5_ArchivistMerlin | Enemies/Sarras/Custom | 30 | 300 | HitFlesh | Cold:60% |  | Custom; Scalable |
| SoS_EnemyMonster_T5_Mermaid_Frantic | Enemies/Sarras | 30 | 1400 | HitFlesh | Cold:60% | Status_Blind | Scalable |
| SoS_EnemyMonster_T5_Tadpole_HeroSummon | Enemies/Sarras/HeroSummons | 30 | 1300 | HitFlesh | Cold:60% |  | Summon; Scalable |
| SoS_EnemyMonster_T5_Tidewraith | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T6_DrownedKnight | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_ArenaVariant | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Arena variant; Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_Female | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_Female_ArenaVariant | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Arena variant; Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_Female_MiniBoss | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% | Status_Blind | Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_MiniBoss | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% | Status_Blind | Scalable |
| SoS_EnemyMonster_T6_DrownedKnight_ProvingGroundsWeaker | Enemies/Sarras | 30 | 1300 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T6_ReefbackFleshTree | Enemies/Sarras | 20 | 1 | HitFlesh | Cold:60% |  | Scalable |
| SoS_EnemyMonster_T6_ReefbackFleshTree_Weak_Boss | Enemies/Sarras/Custom | 20 | 1 | HitFlesh | Cold:60% |  | Custom; Scalable |
| SoS_EnemyMonster_T6_ScionOfTheDepths | Enemies/Sarras | 30 | 1400 | HitFlesh | Cold:60% | Status_Blind | Scalable |
| SoS_EnemyMonster_T6_ScionOfTheDepths_HeraldOfFear | Enemies/Sarras/Custom | 30 | 1400 | HitFlesh | Cold:60% | Status_Blind | Custom; Scalable |
| SoS_EnemyMonster_T6_ScionOfTheDepths_TheBeastsBastard | Enemies/Sarras/Custom | 30 | 1400 | HitFlesh | Cold:60% | Status_Blind | Custom; Scalable |
| SoS_EnemyMonster_T6_TheCrawlingRot | Enemies/Sarras | 30 | 1 | HitFlesh | Cold:60% |  | Scalable |
| EnemyMonster_T6_Tibby | Enemies/Bosses | 50 | 5000 | HitStone | Electric:133% | Status_Blind |  |
| EnemyBoss_T5_StagFather_ElectricGolem | Enemies/Bosses | 40 | 6500 | HitBones | Electric:33% | Status_Blind |  |
| EnemyBoss_T5_StagFather_ElectricGolem_Summon | Enemies/Bosses | 40 | 3000 | HitBones | Electric:33% | Status_Blind | Summon |
| EnemyMonster_T1_Flamegobbler | Enemies/Monsters | 5 | 60 | HitFlesh | Fire:0% |  |  |
| EnemyMonster_T3_FlamegobblerCuanacht | Enemies/Monsters | 5 | 180 | HitFlesh | Fire:0% |  |  |
| EnemyMonster_T6_Flamegobbler_EndGameFireTrial | Enemies/Monsters | 5 | 180 | HitFlesh | Fire:0% |  | Trial |
| Enemy_Elite_Tier4_CindermarTheFirebringer | Enemies/Humans/Elite | 35 | 2400 | HitFlesh | Fire:0%; Cold:150% | Status_Blind |  |
| EnemyBoss_T5_StagFather_IceGolem | Enemies/Bosses | 40 | 6500 | HitBones | Fire:133%; Cold:33% | Status_Blind |  |
| EnemyBoss_T5_StagFather_IceGolem_Summon | Enemies/Bosses | 40 | 3000 | HitBones | Fire:133%; Cold:33% | Status_Blind | Summon |
| EnemyMonster_T6_Yeti | Enemies/Monsters | 60 | 9000 | HitFlesh | Fire:133%; Cold:75% | Status_Blind |  |
| EnemyMonster_T6_Sleepwalker | Enemies/Monsters | 60 | 10000 | HitStone | Fire:133%; Cold:75%; Poison:75% | Status_Blind |  |
| EnemyZombie_T0_RedDeath_Tutorial | Enemies/Monsters/Custom | 3 | 45 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind | Tutorial; Custom |
| EnemyZombie_T1_RedDeath_Infected_GoodDruid | Enemies/Monsters | 5 | 70 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind |  |
| EnemyZombie_T4_RedDeath_2H | Enemies/Monsters | 30 | 1500 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind |  |
| EnemyZombie_T4_RedDeath_Frantic | Enemies/Monsters | 30 | 1300 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind |  |
| EnemyZombie_T4_RedDeath_Infected | Enemies/Monsters | 30 | 1500 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind |  |
| EnemyZombie_T4_RedDeath_Shield | Enemies/Monsters | 30 | 1450 | HitFlesh | Fire:133%; Poison:66% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T6_ElementalGolemIce_Merlin | Enemies/Monsters | 60 | 9000 | HitStone | Fire:150%; Cold:50% | Status_Blind |  |
| EnemyMonster_T6_ElementalGolemIce_MerlinTrial | Enemies/Monsters | 60 | 12000 | HitStone | Fire:150%; Cold:50% | Status_Blind | Trial |
| EnemyBoss_T6_Cairnguard | Enemies/Bosses | 60 | 12000 | HitStone | Fire:150%; Cold:50%; Poison:50% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T2_Swarm_Bees | Enemies/Monsters | 10 | 70 | HitFlesh | Fire:200% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T5_Swarm | Enemies/Monsters | 40 | 2200 | HitFlesh | Fire:200% | Status_Bleed; Status_Blind |  |
| ScalableNpcTemplate_SoS_EnemyMonster_T4_Skeleton1H | Enemies/Sarras | 30 | 100 | HitBones | Fire:25% | Status_Bleed; Status_Blind | Scalable |
| EnemyBoss_T5_StagFather_FireGolem | Enemies/Bosses | 30 | 6500 | HitBones | Fire:33%; Cold:133% | Status_Blind |  |
| EnemyBoss_T5_StagFather_FireGolem_Summon | Enemies/Bosses | 30 | 3000 | HitBones | Fire:33%; Cold:133% | Status_Blind | Summon |
| EnemyZombie_Drowner | Enemies/Monsters | 5 | 60 | HitFlesh | Fire:50% | Status_Blind |  |
| EnemyZombie_T1_DrownerArmorVariant | Enemies/Monsters | 20 | 80 | HitFlesh | Fire:50% | Status_Blind |  |
| EnemyZombie_T3_DrownerCuanacht | Enemies/Monsters | 20 | 300 | HitFlesh | Fire:50% | Status_Blind |  |
| EnemyZombie_T4_DrownerSagremor | Enemies/Special | 30 | 1700 | HitFlesh | Fire:50% | Status_Blind |  |
| SoS_EnemyMonster_T3_Drowner | Enemies/Sarras | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Scalable |
| SoS_EnemyMonster_T3_DrownerArmored | Enemies/Sarras | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Scalable |
| SoS_EnemyMonster_T3_DrownerArmored_ArenaVariant | Enemies/Sarras | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Arena variant; Scalable |
| SoS_EnemyMonster_T3_Drowner_HeroSummon | Enemies/Sarras/HeroSummons | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Summon; Scalable |
| SoS_EnemyMonster_T4_DrownerSpecial_Elite | Enemies/Sarras/Custom | 5 | 100 | HitFlesh | Fire:50% | Status_Blind | Custom; Scalable |
| SoS_EnemyMonster_T4_Drowner_2H | Enemies/Sarras | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Scalable |
| SoS_EnemyMonster_T4_Drowner_2H_ArenaVariant | Enemies/Sarras | 25 | 100 | HitFlesh | Fire:50% | Status_Blind | Arena variant; Scalable |
| EnemyMonster_T4_ElementalGolemFire | Enemies/Monsters | 20 | 1600 | HitStone | Fire:50%; Cold:150% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T6_ElementalGolemFire_Merlin | Enemies/Monsters | 60 | 9000 | HitStone | Fire:50%; Cold:150% | Status_Blind |  |
| EnemyMonster_T6_ElementalGolemFire_MerlinTrial | Enemies/Monsters | 20 | 12000 | HitStone | Fire:50%; Cold:150% | Status_Blind | Trial |
| EnemyMonster_T6_ForgeBorn | Enemies/Bosses | 50 | 8000 | HitStone | Fire:50%; Cold:150% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T2_LostKnight | Enemies/Monsters | 10 | 300 | HitStone | Fire:50%; Poison:50%; Electric:200% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T3_LostKnightCuanacht | Enemies/Monsters | 20 | 600 | HitStone | Fire:50%; Poison:50%; Electric:200% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T6_LostKnight | Enemies/Monsters | 60 | 6500 | HitStone | Fire:50%; Poison:50%; Electric:200% | Status_Bleed; Status_Blind |  |
| EnemyMonster_T4_Barnaclator | Enemies/Monsters | 30 | 1450 | HitWood | Poison:0% | Status_Blind |  |
| EnemyMonster_T4_BarnaclatorElite | Enemies/Monsters | 30 | 1500 | HitWood | Poison:0% | Status_Blind |  |
| EnemyMonster_T4_Barnaclator_SmallStory | Enemies/Monsters | 30 | 1450 | HitWood | Poison:0% | Status_Blind |  |
| EnemyMonster_T4_GhostInPainting | Enemies/Monsters | 30 | 650 | HitWood | Poison:0% |  |  |
| EnemyMonster_T4_Nuckelavee | Enemies/Monsters | 30 | 1650 | HitFlesh | Poison:0% |  |  |
| EnemyMonster_T4_Nuckelavee_GrandpaInWell | Enemies/Monsters/Custom | 30 | 1650 | HitFlesh | Poison:0% |  | Custom |
| Enemy_Generic_Tier4_PoisonedWarrior_1H | Enemies/Humans/Soldiers | 30 | 800 | HitFlesh | Poison:0% |  |  |
| Enemy_Generic_Tier4_PoisonedWarrior_2H | Enemies/Humans/Soldiers | 30 | 850 | HitFlesh | Poison:0% |  |  |
| EnemyMonster_T6_ElementalGolemLightning_Merlin | Enemies/Monsters | 60 | 9000 | HitStone | Poison:150%; Electric:50% | Status_Blind |  |
| EnemyMonster_T6_ElementalGolemLightning_MerlinTrial | Enemies/Monsters | 65 | 12000 | HitStone | Poison:150%; Electric:50% | Status_Blind | Trial |
| SoS_EnemyMonster_T4_Wailcap | Enemies/Sarras | 30 | 1300 | HitFlesh | Poison:25% |  | Scalable |
| SoS_EnemyMonster_T4_Wailcap_ArenaVariant | Enemies/Sarras | 30 | 1300 | HitFlesh | Poison:25% |  | Arena variant; Scalable |
| EnemyMonster_T6_ElementalGolemPoison_Merlin | Enemies/Monsters | 60 | 9000 | HitStone | Poison:50%; Electric:150% | Status_Blind |  |
| EnemyMonster_T6_ElementalGolemPoison_MerlinTrial | Enemies/Monsters | 60 | 18000 | HitStone | Poison:50%; Electric:150% | Status_Blind | Trial |
| BonemaskArcher_Summon | Enemies/Monsters/Custom | 30 | 1000 | HitFlesh | Wyrdness:80%; Cold:120% |  | Summon; Custom |
| EnemyMonster_GiantSentinel | Enemies/Monsters | 5 | 125 | HitStone | Wyrdness:80%; Cold:120% | Status_Blind |  |
| EnemyMonster_T1_Grindylow | Enemies/Monsters | 5 | 80 | HitFlesh | Wyrdness:80%; Cold:120% |  |  |
| EnemyMonster_T2_BloodAbomination | Enemies/Monsters | 15 | 500 | HitFlesh | Wyrdness:80%; Cold:120% | Status_Blind |  |
| EnemyMonster_T2_BloodAbominationArchspire | Enemies/Monsters | 15 | 550 | HitFlesh | Wyrdness:80%; Cold:120% | Status_Blind |  |
| EnemyMonster_T3_CrystalCrawler | Enemies/Monsters | 15 | 630 | HitFlesh | Wyrdness:80%; Cold:120% | Status_Blind |  |
| EnemyMonster_T3_Grindylow_Cuanacht | Enemies/Monsters | 15 | 550 | HitFlesh | Wyrdness:80%; Cold:120% |  |  |
| EnemyMonster_T4_Bonemask_Mage | Enemies/Monsters | 30 | 1200 | HitFlesh | Wyrdness:80%; Cold:120% |  |  |
| EnemyMonster_T4_Bonemask_Melee | Enemies/Monsters | 30 | 1300 | HitFlesh | Wyrdness:80%; Cold:120% |  |  |
| EnemyMonster_T4_Bonemask_Ranged | Enemies/Monsters | 30 | 1000 | HitFlesh | Wyrdness:80%; Cold:120% |  |  |
| Summon_CrystalCrawler | Enemies/Monsters/Custom | 15 | 250 | HitFlesh | Wyrdness:80%; Cold:120% | Status_Blind | Summon; Custom |
| EnemyMonster_T1_CorpseEater | Enemies/Monsters | 5 | 55 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T2_Mistling_HoS | Enemies/Monsters | 10 | 125 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T2_Mistling_Mistbearer | Enemies/Monsters | 10 | 55 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T3_CorpseEater_Cuanacht | Enemies/Monsters | 15 | 375 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T3_Mistling_Cuanacht | Enemies/Monsters | 20 | 250 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T4_CorpseEater_Forlorn | Enemies/Monsters | 40 | 1200 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T4_Mistling_Forlorn | Enemies/Monsters | 30 | 1500 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T5_CorpseEater_Merlin | Enemies/Monsters | 100 | 1200 | HitFlesh | Wyrdness:80%; Fire:120% |  |  |
| EnemyMonster_T5_IceWeaver | Enemies/Monsters | 30 | 4000 | HitFlesh | Wyrdness:80%; Fire:150%; Cold:50% |  |  |

</details>

## Status immunities and family analysis

Status immunities are common, but the export’s status vocabulary is narrow. Across all `893` templates, only four explicit status flags appear at all.

| Status flag | All templates | Enemy templates |
| --- | --- | --- |
| Status_Blind | 179 | 163 |
| Status_Bleed | 70 | 66 |
| Status_Frenzy | 1 | 1 |
| Status_Prey | 1 | 1 |

For enemy templates specifically, the exact status-immunity pattern frequency is:

| Status immunity pattern | Enemy templates | Observed concentration |
| --- | --- | --- |
| Status_Blind | 97 | Most common on monsters, drowners, Sarras aquatic enemies, constructs, many zombies/bloody |
| Status_Bleed; Status_Blind | 65 | All 46 skeleton-family enemies plus a smaller set of bosses, swarms, and scalable undead |
| Status_Bleed; Status_Blind; Status_Frenzy; Status_Prey | 1 | EnemyBoss_T5_TaintedMerlin only |

Every enemy template with any status immunity is immune to `Status_Blind`. `Status_Bleed` appears only as a secondary pairing, except for `EnemyBoss_T5_TaintedMerlin`, which uniquely adds `Status_Frenzy` and `Status_Prey` on top of bleed/blind immunity:

| Template | Category | Direct pattern |
| --- | --- | --- |
| EnemyBoss_T5_TaintedMerlin | Enemies/Bosses |  |

Because the request specifically asked for family analysis based on `abstract_families`, `surface_type`, `tags`, and `asset_path`, the table below uses metadata-defined buckets rather than implying a single canonical internal taxonomy. These buckets are useful for seeing stable families versus misleading ones.

| Bucket | Bucket rule | Templates | With non-neutral multipliers | With status immunities | Dominant direct patterns | Dominant status patterns |
| --- | --- | --- | --- | --- | --- | --- |
| Skeletons | Regex on template/readable name `Skeleton` or `Abstract:Skeleton` family. | 47 | 40 | 46 | Bludgeoning:133% ×26; Bludgeoning:133%; Cold:75% ×12; Bludgeoning:133%; Cold:66% ×1 | Status_Bleed; Status_Blind ×46 |
| Drowners | Regex on template/readable name `Drowner`. | 11 | 11 | 11 | Fire:50% ×11 | Status_Blind ×11 |
| Red Death | Regex on template/readable name `RedDeath`. | 6 | 6 | 6 | Fire:133%; Poison:66% ×6 | Status_Bleed; Status_Blind ×6 |
| Sarras | Category contains `Sarras` or template/readable name contains `SoS_` or `Sarras`. | 54 | 42 | 22 | Cold:60% ×32; Fire:50% ×7; Poison:25% ×2 | Status_Blind ×15; Status_Bleed; Status_Blind ×7 |
| Wyrd | Template/category/name contains `Wyrd`, `Abstract:WyrdnessBound`, or direct `Wyrdness` multiplier. | 51 | 21 | 13 | Wyrdness:80%; Cold:120% ×11; Wyrdness:80%; Fire:120% ×8; Wyrdness:80%; Fire:150%; Cold:50% ×1 | Status_Blind ×13 |
| Ghosts/Spirits | Template/name contains `Ghost`, `Spirit`, `Wraith`, or `Banshee`, or `Abstract:Ghost`. | 25 | 2 | 6 | Poison:0% ×1; Cold:60% ×1 | Status_Blind ×4; Status_Bleed; Status_Blind ×2 |
| Golems/Construct-like | Template/name contains `Golem`, `Construct`, `Sentinel`, `Barnaclator`, `ForgeBorn`, or `Cairnguard`, or tag `merlin:Golem`. | 22 | 21 | 22 | Fire:50%; Cold:150% ×4; Poison:0% ×3; Fire:33%; Cold:133% ×2 | Status_Blind ×19; Status_Bleed; Status_Blind ×3 |
| Bosses/Elites | Boss/Elite categories, `Abstract:Boss` / `Abstract:MiniBoss`, or tags `Type:Elite` / `boss:`. | 90 | 19 | 30 | Electric:33% ×2; Fire:133%; Cold:33% ×2; Fire:33%; Cold:133% ×2 | Status_Blind ×22; Status_Bleed; Status_Blind ×7 |

A few metadata signals are especially strong. `surface_type = HitBones` is one of the clearest resistance-family indicators in the export: `61` enemy templates use it, `46` of those have non-neutral direct multipliers, and `56` have status immunities. It is dominated by skeleton-style bludgeoning weakness plus bleed/blind immunity. `surface_type = HitStone` strongly clusters the golem/construct and elemental-polarity rules: `22` enemy templates total, `17` with non-neutral direct multipliers, and `19` with status immunities. By contrast, `surface_type = HitMagic` is **not** a direct-resistance signal by itself: there are `24` enemy `HitMagic` templates, but `0` of them have non-neutral direct multipliers in this export.

`tags = Type:SarrasCreature` is a strong Sarras rule signal: all `16` tagged enemy templates have non-neutral direct multipliers, specifically `Cold:60%` on `14` rows and `Poison:25%` on `2` rows. `tags = Type:Elite` is a weak resistance signal: only `3` of `43` such enemy templates have non-neutral direct multipliers, so “elite” should not be turned into a generic vanilla resistance rule.

## Notable findings, caveats, and Steel and Bone implications

### Confirmed by data

**Skeletons.** Skeleton-tagged content is one of the strongest and cleanest resistance families in the CSV. The skeleton bucket contains `47` enemy templates; `40` have non-neutral direct multipliers and `46` have status immunities. The dominant direct rule is blunt weakness: `Bludgeoning:133%` on `26` templates, plus `Bludgeoning:133%; Cold:75%` on `12`, and `Bludgeoning:133%; Cold:66%` on `EnemyBoss_KeeperOfTheBarrow`. All `46` status-immune skeleton-family enemies are `Status_Bleed; Status_Blind`. The family also aligns almost perfectly with `HitBones`.

**Drowners.** Every Drowner-named enemy template in the export shares the same direct pattern: `Fire:50%`, with `Status_Blind` on all `11` such enemy templates. That is a clear vanilla rule in the CSV. Community reference pages also treat Drowners as water-linked corpse enemies, which matches the naming theme but does not change the underlying numeric claim. citeturn0search3turn3search0

**Red Death enemies.** All `6` RedDeath-named enemy templates use the same package: `Fire:133%; Poison:66%` plus `Status_Bleed; Status_Blind`. This is one of the cleanest disease/plague-flavored families in the export. Community sources describe the Red Death as a plague and Red Death Infected as former humans overtaken by it; that contextual label fits the data pattern, but the actual weakness/resistance numbers come from the CSV. citeturn1search1turn3search1

**Sarras enemies.** The Sarras bucket contains `54` enemy templates by name/category matching. `42` of them have non-neutral direct multipliers and `22` have status immunities. The dominant pattern is `Cold:60%` on `32` rows, covering much of the aquatic roster. The next most common Sarras pattern is `Fire:50%` on `7` Drowner-family rows. There are also `2` Wailcap rows at `Poison:25%`, and one scalable Sarras skeleton exception at `Fire:25%`. In other words, Sarras is not a single resistance family; it is a region with several families, most notably a large cold-resistant aquatic cluster. Official store material also frames Sanctuary of Sarras as a drowned expansion area, which fits the export’s cold-leaning aquatic naming. citeturn4search5turn1search2

**Wyrd enemies.** Wyrd-adjacent content is easy to overgeneralize, and the CSV shows why that is risky. A broad Wyrd bucket built from names/categories/families contains `51` enemy templates, but only `21` have non-neutral direct multipliers and only `13` have status immunities. The biggest confirmed Wyrd-pattern clusters are `Wyrdness:80%; Cold:120%` on `11` templates, including Bonemask/Blood Abomination/Crystal Crawler/Grindylow/Giant Sentinel style enemies; `Wyrdness:80%; Fire:120%` on `8` templates, concentrated on Corpse Eater and Mistling variants; `Wyrdness:80%; Fire:150%; Cold:50%` on `EnemyMonster_T5_IceWeaver`; and `Cold:60%` on `EnemyMonster_T6_Wyrdheir`. Just as important is what is **not** present: Wyrdspawn, Wyrdspirit, Wyrdstalker, and Foredweller-related templates frequently have no non-neutral direct multiplier at all. Community sources frame Wyrdness as a primordial force and identify Wyrdspirit/Wyrdstalker as Wyrd-linked enemies, but the export does not support a blanket “all Wyrd enemies resist Wyrdness” rule. citeturn1search11turn3search2turn3search3turn3search6

**Ghosts and spirits.** The export does **not** support a universal ghost resistance package. A ghost/spirit/wraith/banshee bucket contains `25` enemy templates, but only `2` have non-neutral direct multipliers: `EnemyMonster_T4_GhostInPainting` with `Poison:0%`, and `SoS_EnemyMonster_T5_Tidewraith` with `Cold:60%`. Some ghost/spirit templates have `Status_Blind` or `Status_Bleed; Status_Blind`, but many have no explicit status immunity at all. So generic physical resistance, silver weakness, or other broad “ethereal” rules would be speculative, not data-backed.

**Golems and construct-like enemies.** This is another strong data-backed family. A construct/golem bucket contains `22` enemy templates; `21` have non-neutral direct multipliers and all `22` have status immunities. The patterns are explicit rather than vague. Fire golems and fire-construct types use `Fire:50%; Cold:150%`. Ice golems use `Fire:150%; Cold:50%`. Lightning golems use `Poison:150%; Electric:50%`. Poison golems use `Poison:50%; Electric:150%`. Stag Father’s linked golems use boss-tuned versions of those polarities with `33%`/`133%`. Barnaclator variants are `Poison:0%`. Giant Sentinel is `Wyrdness:80%; Cold:120%`. ForgeBorn is `Fire:50%; Cold:150%`. Cairnguard is `Fire:150%; Cold:50%; Poison:50%`. This is the clearest place where vanilla already encodes strong, family-like elemental logic.

**Bosses and elites.** Bosses and elites do not have a single shared resistance rule. A boss/elite metadata bucket contains `90` enemy templates, but only `19` have non-neutral direct multipliers and `30` have status immunities. `Type:Elite` is especially weak as a resistance predictor: just `3` of `43` `Type:Elite` rows have non-neutral direct multipliers. Notable boss/elite-specific patterns do exist, but they are template-level rather than universal. Examples include `EnemyBoss_KeeperOfTheBarrow` at `Bludgeoning:133%; Cold:66%`, `EnemyBoss_Skeleton_Archmage` at `Bludgeoning:133%`, `Enemy_Elite_Tier4_CindermarTheFirebringer` at `Fire:0%; Cold:150%`, `Enemy_Elite_Tier4_NiveraTheChillheart` at `Cold:50%`, `EnemyMonster_T6_Yeti` at `Fire:133%; Cold:75%`, and the Sarras Drowned Knight minibosses at `Cold:60%`.

### Data-quality caveats

The CSV is highly usable, but several buckets should not be over-read when designing “vanilla rules.”

| Caveat bucket | Templates | With non-neutral multipliers | With status immunities | Why it matters |
| --- | --- | --- | --- | --- |
| `is_abstract = true` templates | 30 | 0 | 0 | No abstract base template carries non-neutral multipliers or status immunities in this export. |
| Non-enemy templates with non-neutral multipliers | 12 |  |  | These include Sarras mermaids, Mallory, and non-enemy summon templates; exclude them when stating enemy counts. |
| Non-enemy templates with status immunities | 16 |  |  | Status-only data also appears on a small number of friendly/story NPC templates. |
| Enemy templates with `Summon` in name/path | 35 | 19 | 19 | Summons inherit or duplicate several family rules; do not treat them as separate “species” for rule design without intent. |
| Enemy templates with `ScalableNpcTemplate` | 52 | 42 | 22 | Scaled variants heavily amplify the Sarras cold-resist bucket. |
| Enemy templates with `ArenaVariant` | 9 | 7 | 4 | Arena variants duplicate live enemy rules and should not be double-counted as distinct design archetypes. |
| Enemy templates with `Trial` in name/path | 9 | 6 | 7 | Merlin trials and similar challenge copies often reuse boss/construct rules. |
| Enemy templates with `Tutorial` in name/path | 12 | 1 | 1 | Tutorial content is mostly neutral, except the Red Death tutorial infected. |
| Enemy templates with `Passive` in name/path | 1 | 0 | 0 | The passive Wyrdstalker template has no non-neutral direct multipliers or status immunities. |
| Enemy templates with HP ≤ 5 | 18 | 6 | 4 | Includes several HP-1 floatlings / Sarras set pieces that still carry cold resistance, so HP alone is not a filter for “real combatant”. |

Two caveats deserve special emphasis. First, `ScalableNpcTemplate_` entries materially inflate some pattern counts, especially Sarras `Cold:60%` content. Second, several HP-1 templates still carry non-neutral multipliers, especially Sarras floatlings/flesh-tree/crawling-rot style entries. That means “low HP = ignorable fluff” is not a safe filter for this export.

### Steel and Bone implications

The safest mod-facing reading is simple: preserve clear template-backed vanilla rules, and explicitly label any broader family design as a mod addition rather than a vanilla fact.

| Steel and Bone recommendation | Status | Why |
| --- | --- | --- |
| Preserve skeleton-family blunt weakness and bleed/blind immunity. | Confirmed by data | 46 skeleton-family enemies are bleed+blind immune; 39 skeleton-family rows carry bludgeoning weakness patterns, usually `Bludgeoning:133%`. |
| Preserve Drowner fire resistance and blind immunity. | Confirmed by data | All 11 Drowner-named enemy templates are `Fire:50%` and `Status_Blind`. |
| Preserve Red Death fire weakness, poison resistance, and bleed/blind immunity. | Confirmed by data | All 6 RedDeath-named enemy templates are `Fire:133%; Poison:66%` and `Status_Bleed; Status_Blind`. |
| Preserve template-level Sarras cold resistance where the CSV already has it. | Confirmed by data | The dominant Sarras pattern is `Cold:60%` on 32 enemy templates, but not all Sarras enemies share it. |
| Preserve construct/elemental polarity rules exactly where present. | Confirmed by data | Golem/construct templates encode explicit fire/cold/poison/electric matchups; boss variants sometimes use 33/133 rather than 50/150. |
| Keep singleton immunities singleton unless additional CSV evidence appears. | Confirmed by data | Examples: Flamegobbler fire immunity, Barnaclator poison immunity, GhostInPainting poison immunity, Cindermar fire immunity. |
| Do not describe holy, silver, anti-undead elemental rules, or generic ghost intangibility as vanilla resistance facts. | Speculative unless added by the mod | Those rules are not present as non-neutral multiplier subtypes in this CSV export. |
| Do not generalize Wyrdness resistance to every Wyrd/Foredweller/Wyrdspawn template. | Speculative unless added by the mod | Many Wyrd-themed templates have no direct non-neutral multipliers at all. |
| Do not generalize aquatic enemies into a universal electric weakness rule. | Speculative unless added by the mod | Sarras aquatic templates mostly show cold resistance, not electric weakness. |
| Treat boss/elite tuning as per-template, not global. | Speculative to generalize | Only 19 of 90 boss/elite-bucket templates have non-neutral direct multipliers, and only 3 of 43 `Type:Elite` tagged rows do. |

Overall conclusion: vanilla Tainted Grail already contains a **real but selective** resistance system in these NPC templates. It is strongest and most consistent for skeletons, drowners, Red Death enemies, Sarras aquatic content, and elemental/construct enemies. It is **not** strong enough to justify broad unsupported claims about all undead, all ghosts, all Wyrd creatures, or all bosses. For Steel and Bone, the most faithful path is to preserve the rules that are already explicit in the CSV and to clearly label any broader family-wide design expansion as a deliberate mod hypothesis rather than a vanilla fact.
