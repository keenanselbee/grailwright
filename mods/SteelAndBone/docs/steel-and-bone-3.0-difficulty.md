# Steel and Bone 3.0 Difficulty Contract

Current release: 3.4.5.

Steel and Bone 3.0 is a lightweight but impactful difficulty layer built on the game's native damage, stat, armor-weight, projectile, awareness, enemy-pressure, and reward routes.

## Preset Scope

| Lever | Tempered | Hardened | Crucible | Toggle |
|---|---:|---:|---:|---|
| Player health damage dealt | 0.95 | 0.90 | 0.85 | `ModifyPlayerDamageDealt` |
| Added weak-spot base damage | +0.10 | +0.20 | +0.30 | `WeakSpotDamageBonus` |
| Player health damage taken | 1.05 | 1.10 | 1.15 | `ModifyPlayerDamageTaken` |
| Stamina and mana usage | 1.00 | 1.05 | 1.10 | Separate resource toggles |
| Player and hostile arrow velocity | 1.10 | 1.30 | 1.50 | Separate projectile toggles |
| Player arrow gravity | 0.75 | 0.75 | 0.75 | `ModifyPlayerArrowDrop` plus independent multiplier |
| Hostile archer aim scatter (meters) | 1.50 | 1.25 | 1.00 | `HostileArcherAimScatter`; 0 restores native accuracy |
| Hostile enemy sight distance | 1.10 | 1.30 | 1.50 | `ModifyEnemySightRange` |
| Hero footstep hearing range | 1.10 | 1.20 | 1.30 | `ModifyEnemyHearingRange` |
| Native combat aggro persistence | 1.00 | 1.10 | 1.20 | `ModifyEnemyAggroPersistence` |
| Native armor-weight penalties | 1.00 | 1.05 | 1.10 | `ModifyArmorWeightPenalties` |
| Light armor movement | 1.00 | 1.025 | 1.05 | `ModifyLightArmorMobility` |
| Medium physical armor | 1.00 | 1.05 | 1.10 | `ModifyArmorPhysicalProtection` |
| Heavy/Overload physical armor | 1.00 | 1.10 | 1.20 | `ModifyArmorPhysicalProtection` |
| Passive shield share of effective Block | 8% | 10% | 12% | `PassiveShieldProtectionEnabled` |
| Enemy attack slots | +0 | +1 | +2 | `ModifyEnemyAttackSlots` |
| Enemy attack recovery | 1.00 | 0.95 | 0.90 | `ModifyEnemyAttackRecovery` |
| Common enemy combat movement | 1.00 | Up to 1.05 | Up to 1.10 | `ModifyEnemyMovementSpeed` |
| Player poise damage dealt | 1.00 | 0.95 | 0.90 | `ModifyPlayerPoiseDamageDealt` |
| Restorative consumable recovery | 1.00 | 0.90 | 0.80 | `ModifyConsumableRecovery` |
| Kill, quest, and proficiency XP | 0.95 | 0.90 | 0.85 | Separate XP toggles |

`DifficultyModifiersEnabled` disables this entire table without disabling material rules or feedback. Outgoing and incoming player damage each have one toggle, and their exact values come directly from the selected preset.

## Native-System Contract

| System | Route | Contract |
|---|---|---|
| Enemy sight | One-second loaded-NPC reconciliation plus non-saved `StatTweak` elements | Multiply `NpcStats.SightLengthMultiplier` only for living, active, hostile, non-allied native-AI actors. Preserve authored ranges, visibility, line of sight, and alert buildup. |
| Enemy hearing | `AINoises.MakeHeroFootstepNoise` prefix | Scale only the native hero footstep noise range. Preserve noise strength, wall checks, armor noise, and each NPC's authored hearing. |
| Aggro persistence | `NpcAIDistancesUtils.CombatAggroDecreaseModifierByDistanceToLastIdlePoint` postfix | Slow positive native combat-aggro decay only for living hostile enemies. Do not patch chase boundaries, forced combat/alert exit, or target-loss rules. |
| Enemy movement | The same one-second reconciliation plus non-saved `StatTweak` elements | Multiply `CharacterStats.MovementSpeedMultiplier` only for living, active, hostile combatants. Apply the full preset bonus to ordinary agile enemies; cap Medium, Elite, Beholder, and Slugholder enemies at half; exclude Heavy armor, bears, constructs, flora, bosses, minibosses, Critters, and non-pathing actors. |
| Player arrows | `BowFSM.FireProjectileInternal` prefix plus filtered `DamageDealingProjectile.ProcessFixedUpdate` postfix | Scale the native launch vector by preset, then apply the independent gravity multiplier only to active player-owned arrows. Preserve native aim, projectile offsets, draw strength, collision, payloads, and damage. |
| Hostile arrows | `CombatBehaviourUtils.FireProjectile` prefix/transpiler/finalizer | Apply the configured minimum native aim-point scatter, then scale clamped speed before movement prediction and ballistic solving, only for hostile NPC Quiver projectiles. Preserve larger authored scatter, native gravity, and damage. |
| Armor penalties | Non-saved tweak on `HeroStats.ArmorPenaltyMultiplier` | Let native tier penalties, proficiency mitigation, and overload rules remain authoritative. |
| Light mobility | Non-saved tweak on `CharacterStats.MovementSpeedMultiplier` | Apply only while native `ArmorWeightType` is Light. |
| Physical protection | `Hero.TotalArmor(DamageSubType)` postfix | Scale only physical subtype queries. Medium and Heavy use distinct values; Overload inherits Heavy. |
| Passive shields | Hero-target branch of `HealthElement.ApplyDamageModifiers` | For direct physical hits within native `BlockAngle`, reduce damage by effective Block multiplied by the preset share. Require a readied shield, cap coverage to the forward 180 degrees, and skip active blocks, rear hits, magic, status effects, and damage over time. |
| Weak-spot reward | Hero-source branch of `HealthElement.ApplyDamageModifiers` | Add the preset's `WeakSpotDamageBonus` beside native critical, weak-spot, sneak, and backstab components, then apply outgoing pressure and material matchups. Do not alter native critical damage, hero stats, item stats, or hitbox definitions. |
| Resources | Non-saved stat tweaks | Keep exactly one owned tweak per active lever. |
| Restorative consumables | Transactional `ItemSkillsInvoker.PerformImmediate` prefix/postfix | Scale only positive health, stamina, and mana deltas on hero-owned items carrying the matching native consumable marker. Leave non-restorative effects and negative deltas unchanged. |
| Attack slots and recovery | Native `Difficulty` property postfixes | Add to current slots and scale current recovery without lowering another source's value. |
| Experience | Native reward getters and proficiency prefix | Scale positive rewards once at their authoritative route. |

## Enemy Runtime Safety

- Apply no template-specific runtime table.
- Exclude friends, summons, allies, inactive AI, dead actors, and discarded actors.
- Remove each owned tweak whenever eligibility, combat state, the preset, the individual toggle, or the master switch changes.
- Keep the game's native `SightLengthMultiplier` limit and every authored base distance.
- Keep enemy movement on the native multiplier route; do not alter attack animation speed, chase/leash boundaries, forced combat exit, alert gain, immediate combat, per-attack cooldowns, or factions.
- Use the extracted 469-enemy dataset only as an offline audit and testing matrix.

## Corpse Quality Contract

Corpse quality is a shared presentation and reward signal used by Steel and Bone killing-blow feedback and Blood Magic Expansion. It does not alter an enemy's actual XP award.

| Native enemy tag | Intrinsic quality | Bucket |
|---|---:|---|
| `Tier:0` | 0.050 | Meager |
| `Tier:1` | 0.125 | Meager |
| `Tier:2` | 0.230 | Meager |
| `Tier:3` | 0.425 | Worthy |
| `Tier:4` | 0.625 | Potent |
| `Tier:5` | 0.800 | Prime |
| `Tier:6` | 0.900 | Prime |
| `Tier:7` | 1.000 | Prime |

Exact native Tier tags are authoritative. Untagged enemies fall back to the 50/50 base-kill-XP and maximum-health calculation with fixed references of 700 XP and 3400 health. The audited 469-template dataset contains 449 tagged templates. Before threat-class and relative-level weighting, applying the fallback to the other 20 produces 118 Meager, 88 Worthy, 116 Potent, and 147 Prime.

After the native anchor or fallback, Elite enemies add 0.10 quality, MiniBosses add 0.175, and Bosses receive a minimum quality of 0.875. Relative enemy level then adds or subtracts 0.025 per level, capped at 0.075 in either direction. This bounded tie-breaker lets a Tier 2 Lost Knight two levels above the hero become Worthy while preventing distant future enemies from being promoted across the whole scale or old enemies from collapsing indefinitely. Do not include Steel and Bone's XP pressure, Blood Magic Expansion's XP payout handling, or another difficulty multiplier in corpse quality.

The full template audit remains varied under this formula. At hero levels 10, 20, 30, and 40, Meager stays between 23.0% and 24.1%; Worthy between 15.8% and 18.1%; Potent between 22.2% and 30.7%; and Prime between 27.3% and 38.4%. These are template counts, not encounter-frequency weights; the 20 untagged templates use the export-available health fallback because their runtime kill XP is not present in the audit CSV.

## Compatibility Contract

| Plugin | Policy |
|---|---|
| Custom Difficulty | Flag as incompatible publicly. Allow both to load; warn only for confirmed active overlapping values. |
| Tainted Combat | Conditionally compatible. Detect stamina, slots, recovery, poise, and armor-penalty overlaps. |
| Better Movement | Compatible. Its movement multiplier can stack with Light mobility; disclose that behavior without warning. |
| Flat Arrows | Conditionally compatible. Detect its active arrow modifications and warn for active Steel and Bone player velocity or gravity controls. Its bow timing and instant-fire options do not directly overlap. |
| HarderLife | Conditionally compatible. Detect active damage, stamina, mana, sight, hearing, aggro-persistence, and restorative-consumable overlaps; keep its parry health, backstab, extended chase boundary, and debuff duration distinct. |
| Tainted Instincts | Flag as incompatible publicly. Detect active sight-range, damage-taken, attack-slot, attack-cadence, and pursuit conflicts; allow individual Steel and Bone toggles to remove those overlaps. |

Normal operation is silent. A confirmed overlap produces one short native notification per unique signature and one detailed BepInEx warning naming the Steel and Bone toggles to disable.

## Acceptance Matrix

| Test | Expected |
|---|---|
| Master or individual toggle off | Governed route is an exact no-op and owned tweaks are removed. |
| Native 30-meter sight range | Resolves to 33/39/45 meters before other native clamping. |
| Native 20-meter hero footstep noise | Resolves to 22/24/26 meters while strength and wall handling remain native. |
| Native combat-aggro decay | Tempered is unchanged; Hardened and Crucible divide positive decay by 1.10 and 1.20 without changing chase or forced-exit decisions. |
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
| Confirmed weak spot with no native precision bonus | Adds 10%/20%/30% before the 0.95/0.90/0.85 outgoing multiplier and the material matchup. |
| Critical without a weak spot | Uses native critical damage only; Steel and Bone adds no critical damage. |
| Critical plus weak spot | Native critical, native weak spot, and Steel and Bone weak-spot bonuses are summed once before outgoing and matchup multipliers. |
| Rear, magical, status, damage-over-time, or sheathed-shield hit | No passive shield reduction. |
| Restorative item with health, stamina, or mana marker | Positive recovered deltas retain 100%/90%/80%; costs and non-restorative effects remain unchanged. |
| HarderLife overlap active | Warning lists only the matching active Steel and Bone toggles, including hearing, persistence, or consumable recovery when applicable. |
| Tainted Instincts sight tuning disabled | No sight-range overlap warning. |
| Tainted Instincts sight tuning active | Warning names `ModifyEnemySightRange`; other active exact overlaps are listed. |
| External overlap inactive | No in-game notification. |
| Schema reset from a supported backup | Restore compatible customized values automatically, retain the current Preset default through its schema-16 meaning-change rule, skip removed settings, and clamp restored values to current ranges. |
| Package | One top-level `SteelAndBone` folder with DLL and installed-user docs only. |

Config schema is 19 because 3.4.5 materially raised the preset defaults for HostileArcherAimScatter. Untouched old defaults regenerate at the stronger values while compatible custom values remain recoverable. The fixed recovery baseline remains 14.
