# Steel and Bone 3.0 Difficulty Contract

Current release: 3.2.1.

Steel and Bone 3.0 is a lightweight but impactful difficulty layer built on the game's native damage, stat, armor-weight, projectile, awareness, enemy-pressure, and reward routes.

## Preset Scope

| Lever | Tempered | Hardened | Crucible | Toggle |
|---|---:|---:|---:|---|
| Player health damage dealt | Config | Config | Config | `ModifyPlayerDamageDealt` |
| Player health damage taken | 1.00 | 1.05 | 1.10 | `ModifyPlayerDamageTaken` |
| Stamina and mana usage | 1.00 | 1.05 | 1.10 | Separate resource toggles |
| Player and hostile arrow velocity | 1.10 | 1.30 | 1.50 | Separate projectile toggles |
| Player arrow gravity | 0.75 | 0.75 | 0.75 | `ModifyPlayerArrowDrop` plus independent multiplier |
| Hostile enemy sight distance | 1.10 | 1.30 | 1.50 | `ModifyEnemySightRange` |
| Native armor-weight penalties | 1.00 | 1.05 | 1.10 | `ModifyArmorWeightPenalties` |
| Light armor movement | 1.00 | 1.025 | 1.05 | `ModifyLightArmorMobility` |
| Medium physical armor | 1.00 | 1.05 | 1.10 | `ModifyArmorPhysicalProtection` |
| Heavy/Overload physical armor | 1.00 | 1.10 | 1.20 | `ModifyArmorPhysicalProtection` |
| Passive shield share of effective Block | 8% | 10% | 12% | `PassiveShieldProtectionEnabled` |
| Enemy attack slots | +0 | +1 | +2 | `ModifyEnemyAttackSlots` |
| Enemy attack recovery | 1.00 | 0.95 | 0.90 | `ModifyEnemyAttackRecovery` |
| Common enemy combat movement | 1.00 | Up to 1.05 | Up to 1.10 | `ModifyEnemyMovementSpeed` |
| Player poise damage dealt | 1.00 | 0.95 | 0.90 | `ModifyPlayerPoiseDamageDealt` |
| Kill, quest, and proficiency XP | 1.00 | 0.95 | 0.90 | Separate XP toggles |

`DifficultyModifiersEnabled` disables this entire table without disabling material rules or feedback. `PlayerDamageDealtMultiplier` remains independent from presets.

## Native-System Contract

| System | Route | Contract |
|---|---|---|
| Enemy awareness | One-second loaded-NPC reconciliation plus non-saved `StatTweak` elements | Multiply `NpcStats.SightLengthMultiplier` only for living, active, hostile, non-allied native-AI actors. Preserve authored ranges, visibility, line of sight, alert buildup, hearing, pursuit, and immediate-combat behavior. |
| Enemy movement | The same one-second reconciliation plus non-saved `StatTweak` elements | Multiply `CharacterStats.MovementSpeedMultiplier` only for living, active, hostile combatants. Apply the full preset bonus to ordinary agile enemies; cap Medium, Elite, Beholder, and Slugholder enemies at half; exclude Heavy armor, bears, constructs, flora, bosses, minibosses, Critters, and non-pathing actors. |
| Player arrows | `BowFSM.FireProjectileInternal` prefix plus filtered `DamageDealingProjectile.ProcessFixedUpdate` postfix | Scale the native launch vector by preset, then apply the independent gravity multiplier only to active player-owned arrows. Preserve native aim, projectile offsets, draw strength, collision, payloads, and damage. |
| Hostile arrows | `CombatBehaviourUtils.FireProjectile` prefix/transpiler/finalizer | Scale the clamped speed before prediction and ballistic solving, only for hostile Quiver projectiles. |
| Armor penalties | Non-saved tweak on `HeroStats.ArmorPenaltyMultiplier` | Let native tier penalties, proficiency mitigation, and overload rules remain authoritative. |
| Light mobility | Non-saved tweak on `CharacterStats.MovementSpeedMultiplier` | Apply only while native `ArmorWeightType` is Light. |
| Physical protection | `Hero.TotalArmor(DamageSubType)` postfix | Scale only physical subtype queries. Medium and Heavy use distinct values; Overload inherits Heavy. |
| Passive shields | Hero-target branch of `HealthElement.ApplyDamageModifiers` | For direct physical hits within native `BlockAngle`, reduce damage by effective Block multiplied by the preset share. Require a readied shield, cap coverage to the forward 180 degrees, and skip active blocks, rear hits, magic, status effects, and damage over time. |
| Resources | Non-saved stat tweaks | Keep exactly one owned tweak per active lever. |
| Attack slots and recovery | Native `Difficulty` property postfixes | Add to current slots and scale current recovery without lowering another source's value. |
| Experience | Native reward getters and proficiency prefix | Scale positive rewards once at their authoritative route. |

## Enemy Runtime Safety

- Apply no template-specific runtime table.
- Exclude friends, summons, allies, inactive AI, dead actors, and discarded actors.
- Remove each owned tweak whenever eligibility, combat state, the preset, the individual toggle, or the master switch changes.
- Keep the game's native `SightLengthMultiplier` limit and every authored base distance.
- Keep enemy movement on the native multiplier route; do not alter attack animation speed, hearing, pursuit/leashing, alert gain, immediate combat, cooldowns, or factions.
- Use the extracted 469-enemy dataset only as an offline audit and testing matrix.

## Compatibility Contract

| Plugin | Policy |
|---|---|
| Custom Difficulty | Flag as incompatible publicly. Allow both to load; warn only for confirmed active overlapping values. |
| Tainted Combat | Conditionally compatible. Detect stamina, slots, recovery, poise, and armor-penalty overlaps. |
| Better Movement | Compatible. Its movement multiplier can stack with Light mobility; disclose that behavior without warning. |
| Flat Arrows | Conditionally compatible. Detect its active arrow modifications and warn for active Steel and Bone player velocity or gravity controls. Its bow timing and instant-fire options do not directly overlap. |
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
| Exposed, Light, or ordinary common combatant | Movement resolves to 1.00/1.05/1.10 times its current native and modded value. |
| Medium armor or template weight 150-249 | Movement resolves to 1.00/1.025/1.05 times its current native and modded value. |
| Heavy armor, template weight 250+, bear, construct, flora, boss, miniboss, Critter, or non-pathing actor | No Steel and Bone movement tweak exists. |
| Eligible enemy leaves combat | Its Steel and Bone movement tweak is removed within one second. |
| Player arrows | Velocity magnitudes are 1.10/1.30/1.50; damage is unchanged. |
| Player arrow with default gravity control | Uses 0.75x gravity on every preset without changing its native launch direction or draw strength. |
| Hostile arrow, thrown item, or non-arrow projectile | Receives no Steel and Bone gravity adjustment. |
| Standard hostile archer | Speed is scaled before trajectory solving and collision remains native. |
| Light/Medium/Heavy/Overload swap | Owned stat tweaks refresh within one second and protection follows the current tier. |
| Physical versus magical armor query | Only physical armor receives the preset multiplier. |
| Readied Block 50 shield, frontal physical hit | Passive reduction is 4%/5%/6%; active blocking is unchanged and never double-dips. |
| Rear, magical, status, damage-over-time, or sheathed-shield hit | No passive shield reduction. |
| Tainted Instincts sight tuning disabled | No sight-range overlap warning. |
| Tainted Instincts sight tuning active | Warning names `ModifyEnemySightRange`; other active exact overlaps are listed. |
| External overlap inactive | No in-game notification. |
| Schema reset from a supported backup | Restore compatible customized values automatically, retain the current Preset default through its schema-15 meaning-change rule, and clamp restored values to current ranges. |
| Package | One top-level `SteelAndBone` folder with DLL and installed-user docs only. |

Config schema remains 15 because 3.2.0 only added settings with safe defaults and 3.2.1 changes no setting names, types, meanings, ranges, or defaults; the fixed recovery baseline remains 14.
