# Steel and Bone 3.0 Difficulty Contract

Current release: 3.1.4.

Steel and Bone 3.0 is a lightweight but impactful difficulty layer built on the game's native damage, stat, armor-weight, projectile, awareness, enemy-pressure, and reward routes.

## Preset Scope

| Lever | Tempered | Hardened | Crucible | Toggle |
|---|---:|---:|---:|---|
| Player health damage dealt | Config | Config | Config | `ModifyPlayerDamageDealt` |
| Player health damage taken | 1.00 | 1.05 | 1.10 | `ModifyPlayerDamageTaken` |
| Stamina and mana usage | 1.00 | 1.05 | 1.10 | Separate resource toggles |
| Player and hostile arrow velocity | 1.10 | 1.30 | 1.50 | Separate projectile toggles |
| Hostile enemy sight distance | 1.10 | 1.30 | 1.50 | `ModifyEnemySightRange` |
| Native armor-weight penalties | 1.00 | 1.05 | 1.10 | `ModifyArmorWeightPenalties` |
| Light armor movement | 1.00 | 1.025 | 1.05 | `ModifyLightArmorMobility` |
| Medium physical armor | 1.00 | 1.05 | 1.10 | `ModifyArmorPhysicalProtection` |
| Heavy/Overload physical armor | 1.00 | 1.10 | 1.20 | `ModifyArmorPhysicalProtection` |
| Enemy attack slots | +0 | +1 | +2 | `ModifyEnemyAttackSlots` |
| Enemy attack recovery | 1.00 | 0.95 | 0.90 | `ModifyEnemyAttackRecovery` |
| Player poise damage dealt | 1.00 | 0.95 | 0.90 | `ModifyPlayerPoiseDamageDealt` |
| Kill, quest, and proficiency XP | 1.00 | 0.95 | 0.90 | Separate XP toggles |

`DifficultyModifiersEnabled` disables this entire table without disabling material rules or feedback. `PlayerDamageDealtMultiplier` remains independent from presets.

## Native-System Contract

| System | Route | Contract |
|---|---|---|
| Enemy awareness | One-second loaded-NPC reconciliation plus non-saved `StatTweak` elements | Multiply `NpcStats.SightLengthMultiplier` only for living, active, hostile, non-allied native-AI actors. Preserve authored ranges, visibility, line of sight, alert buildup, hearing, pursuit, and immediate-combat behavior. |
| Player arrows | `BowFSM.FireProjectileInternal` prefix | Scale the native launch vector. Do not change damage. |
| Hostile arrows | `CombatBehaviourUtils.FireProjectile` prefix/transpiler/finalizer | Scale the clamped speed before prediction and ballistic solving, only for hostile Quiver projectiles. |
| Armor penalties | Non-saved tweak on `HeroStats.ArmorPenaltyMultiplier` | Let native tier penalties, proficiency mitigation, and overload rules remain authoritative. |
| Light mobility | Non-saved tweak on `CharacterStats.MovementSpeedMultiplier` | Apply only while native `ArmorWeightType` is Light. |
| Physical protection | `Hero.TotalArmor(DamageSubType)` postfix | Scale only physical subtype queries. Medium and Heavy use distinct values; Overload inherits Heavy. |
| Resources | Non-saved stat tweaks | Keep exactly one owned tweak per active lever. |
| Attack slots and recovery | Native `Difficulty` property postfixes | Add to current slots and scale current recovery without lowering another source's value. |
| Experience | Native reward getters and proficiency prefix | Scale positive rewards once at their authoritative route. |

## Enemy-Awareness Safety

- Apply no template-specific runtime table.
- Exclude friends, summons, allies, inactive AI, dead actors, and discarded actors.
- Remove the owned tweak whenever eligibility, the preset, the individual toggle, or the master switch changes.
- Keep the game's native `SightLengthMultiplier` limit and every authored base distance.
- Do not change hearing, pursuit/leashing, alert gain, immediate combat, cooldowns, movement, or factions.
- Use the extracted 469-enemy dataset only as an offline audit and testing matrix.

## Compatibility Contract

| Plugin | Policy |
|---|---|
| Custom Difficulty | Flag as incompatible publicly. Allow both to load; warn only for confirmed active overlapping values. |
| Tainted Combat | Conditionally compatible. Detect stamina, slots, recovery, poise, and armor-penalty overlaps. |
| Better Movement | Compatible. Its movement multiplier can stack with Light mobility; disclose that behavior without warning. |
| Tainted Instincts | Flag as incompatible publicly. Detect active sight-range, damage-taken, and attack-slot conflicts; allow individual Steel and Bone toggles to remove those overlaps. |

Normal operation is silent. A confirmed overlap produces one short native notification per unique signature and one detailed BepInEx warning naming the Steel and Bone toggles to disable.

## Acceptance Matrix

| Test | Expected |
|---|---|
| Master or individual toggle off | Governed route is an exact no-op and owned tweaks are removed. |
| Native 30-meter sight range | Resolves to 33/39/45 meters before other native clamping. |
| Friendly, allied, summon, inactive, or dead NPC | No Steel and Bone sight tweak exists. |
| Hostility or AI state changes | Eligibility reconciles within one second. |
| Existing NPC plus newly loaded NPC | Exactly one Steel and Bone sight tweak per eligible actor. |
| Player arrows | Velocity magnitudes are 1.10/1.30/1.50; damage is unchanged. |
| Standard hostile archer | Speed is scaled before trajectory solving and collision remains native. |
| Light/Medium/Heavy/Overload swap | Owned stat tweaks refresh within one second and protection follows the current tier. |
| Physical versus magical armor query | Only physical armor receives the preset multiplier. |
| Tainted Instincts sight tuning disabled | No sight-range overlap warning. |
| Tainted Instincts sight tuning active | Warning names `ModifyEnemySightRange`; other active exact overlaps are listed. |
| External overlap inactive | No in-game notification. |
| Schema reset from a supported backup | Restore compatible customized values automatically, retain the current Preset default through its schema-15 meaning-change rule, and clamp restored values to current ranges. |
| Package | One top-level `SteelAndBone` folder with DLL and installed-user docs only. |

Config schema remains 15 because 3.1.0 only adds settings with safe defaults and changes no existing setting names, types, meanings, ranges, or defaults; the fixed recovery baseline remains 14.
