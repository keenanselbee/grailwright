Steel and Bone
Version 3.4.6

Platforms: Windows and Linux through Proton.

Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.

Steel and Bone is a lightweight, knowledge-driven BepInEx 5 Mono difficulty mod for Tainted Grail: The Fall of Avalon. Material weaknesses and resistances define the experience: learn what an enemy is made from, choose the right physical or magical answer, and read the result through reactive damage numbers.

Its supporting difficulty systems make that preparation matter through sharper enemy sight and hearing, tiered combat movement, faster and flatter arrows, clearer armor roles, restorative-consumable pressure, steadier group aggression, poise tuning, and slower progression. It does not replace enemy AI, rewrite encounters, inflate enemy health, or modify coin rewards.

MATERIAL COMBAT
---------------

Vanilla weaknesses, resistances, and immunities run first. Steel and Bone can preserve or amplify them by preset, then adds one focused material rule only where appropriate. More specific families take precedence over broad categories, and elite enemies soften custom extremes.

Mixed hits are resolved by damage part, so an enchanted weapon's physical strike, elemental payload, and status effect each keep their own matchup.

Bone undead favor blunt damage and resist blood, bleed, slash, and pierce. Constructs favor blunt and resist biological damage, slash, and pierce. Against ordinary humanoids, exposed flesh is neutral to Blunt while armor makes Blunt progressively stronger from Light through Heavy. Ordinary flesh receives mild biological and edged-weapon weaknesses. Flesh undead, drowned corpses, infected flesh, sea creatures, spirits, Wyrd creatures, and flora each have distinct physical or magical answers.

Direct player arrows now have their own material identity instead of acting as generic Pierce. Against ordinary humanoids, equipped armor creates a clear curve: exposed flesh is most vulnerable, Light armor remains slightly favorable, Medium is neutral, and Heavy strongly resists arrows while also resisting ordinary Pierce. Other material families retain their own arrow reactions. Fire, Electric, and other payloads keep their own matchup rather than inheriting the physical arrow penalty.

Direct player spells receive a small tiered advantage against armor, while Fire, Electric, and Cold also react to the equipped armor's native Fabric, Leather, or Metal surface. Electricity is strongest against metal, while fire remains useful against fabric, leather, and heated metal. Blood, Wyrdness, biological effects, and spells that already ignore armor do not receive a duplicate armor-tier bonus. Set ArrowMaterialRulesEnabled or ArmoredSpellWeaknessEnabled to false to disable either feature independently.

An equipped and readied shield also provides modest passive protection against direct physical attacks from the front. Its effective vanilla Block rating supplies 8%, 10%, or 12% of that value as damage reduction by preset, while its vanilla BlockAngle controls coverage up to the forward 180 degrees. Rear attacks, magic, status effects, damage over time, sheathed shields, active blocks, and shields suppressed by Versatile Weapons receive no passive reduction.

The design is inspired by Requiem's emphasis on coherent rules, preparation, and intelligent tactical play, but Steel and Bone is not a port or total conversion. It translates that philosophy into Tainted Grail's native combat systems with independently toggleable features.

PRESETS
-------

Tempered increases incoming health damage by 5%, reduces outgoing health damage and experience gains by 5%, adds 10% base damage to confirmed weak-spot hits, keeps resource, armor-weight, recovery, poise, enemy combat movement, aggro persistence, and consumable recovery modifiers neutral, uses lighter material rules, and sets arrows, enemy sight, and hero footstep hearing range to x1.10.

Hardened is the default. Incoming health damage rises by 10%, while outgoing health damage and experience gains fall by 10%. Confirmed weak spots add 20% base damage. Stamina use, mana use, and native armor-weight penalties rise by 5%; restorative consumables recover x0.90; agile common enemies gain up to 5% combat movement; enemy attack slots gain 1; native aggro persistence rises to x1.10; enemy hearing uses x1.20; enemy recovery and player poise damage fall by 5%; light armor movement gains 2.5%; medium physical armor is x1.05; heavy and overloaded physical armor are x1.10; arrows and enemy sight use x1.30.

Crucible increases incoming health damage by 15%, reduces outgoing health damage and experience gains by 15%, adds 30% base damage to confirmed weak spots, uses 10% resource, armor-weight, recovery, poise, and enemy combat movement pressure, reduces restorative recovery to x0.80, raises native aggro persistence to x1.20 and hearing to x1.30, adds 2 enemy attack slots, grants 5% light armor movement, makes medium physical armor x1.10 and heavy or overloaded physical armor x1.20, and uses x1.50 arrows and enemy sight.

PlayerArrowGravityMultiplier also remains independent from presets and defaults to 0.75, reducing player-arrow gravity by 25% on every preset without tilting the native launch direction.

HostileArcherAimScatter changes to 1.50, 1.25, or 1.00 meters with Tempered, Hardened, or Crucible. It remains freely adjustable afterward, and 0 restores native accuracy.

Every global modifier has its own control. Set WeakSpotDamageBonus=0 to disable only the added weak-spot reward, or set DifficultyModifiersEnabled=false to retain material combat and damage feedback while disabling the complete preset-driven layer.

VANILLA SYSTEMS
---------------

Steel and Bone reads the game's current Light, Medium, Heavy, or Overload armor tier. It scales the native armor-penalty stat, so vanilla thresholds, individual penalty rules, armor proficiency, and overload behavior remain in control. Light receives a modest movement bonus; Medium and Heavy receive progressively stronger physical protection. Overload inherits Heavy protection but keeps its native overload penalties.

Player arrows are scaled at the native bow launch and use 0.75x gravity by default on every preset. The separate gravity control reduces drop without changing native aim, draw strength, projectile offsets, collision, elemental payloads, or damage. Hostile NPC arrows receive preset-scaled minimum aim scatter at the native target-point route, then their speed is scaled before the game's movement-prediction and ballistic solve. Larger authored scatter and native gravity are preserved. The optional material layer modifies only the physical share of direct player arrow hits.

Enemy awareness remains native-first. Sight tuning multiplies each active hostile NPC's sight-distance stat, while hearing tuning changes only the range of hero footstep noise and preserves native strength, wall checks, armor noise, and individual NPC hearing. Aggro persistence slows only the native combat decay rate on Hardened and Crucible. Steel and Bone does not force immediate combat, extend chase boundaries, suppress combat exit, or replace target-loss rules. Friendly NPCs, summons, allies, inactive AI, and dead actors remain outside owned NPC tuning.

Restorative-consumable tuning measures only positive health, stamina, or mana recovery from items carrying the game's matching consumable markers. Hardened retains 90% and Crucible retains 80% of the native recovered amount. Tempered and non-restorative item effects remain unchanged.

Standard food healing remains native on Tempered. Hardened restores health at 75% of the native rate for 1.5 times the duration (112.5% total) and adds 0.5 stamina per second for the food's original duration. Crucible restores health at 62.5% of the native rate for twice the duration (125% total) and adds 1 stamina per second for the original duration. The added stamina effect does not stack: another qualifying food replaces it while preserving the food's other authored effects. Food tooltips update from the current preset without adding Steel and Bone or preset labels.

Enemy movement tuning multiplies the game's native combat movement stat without changing attack animation speed. Exposed, Light-armored, and ordinary agile enemies such as wolves and swarms receive the full 0%/5%/10% preset bonus. Medium-armored, Elite, Beholder, and Slugholder enemies receive at most half. Heavy-armored enemies, bears, constructs, flora, bosses, minibosses, scripted Critters, and non-pathing actors retain their vanilla speed. It applies only to living, active, hostile combatants and can also affect native movement during lunging attacks.

Vanilla attack slots are Story/Easy 1, Normal/Challenge 2, Hard 3, and Survival 4. Steel and Bone adds 0/1/2 and caps only its own increase at 6 by default.

MAIN DIFFICULTY SETTINGS
------------------------

Enabled = true
Preset = Hardened
ArrowMaterialRulesEnabled = true
ArmoredSpellWeaknessEnabled = true
PassiveShieldProtectionEnabled = true
DifficultyModifiersEnabled = true
ModifyPlayerDamageDealt = true
WeakSpotDamageBonus = 0.20
ModifyPlayerDamageTaken = true
ModifyStaminaUsage = true
ModifyManaUsage = true
ModifyPlayerPoiseDamageDealt = true
ModifyPlayerArrowVelocity = true
ModifyPlayerArrowDrop = true
PlayerArrowGravityMultiplier = 0.75
ModifyArmorWeightPenalties = true
ModifyLightArmorMobility = true
ModifyArmorPhysicalProtection = true
ModifyConsumableRecovery = true
ModifyFoodRecovery = true
ModifyEnemyAttackSlots = true
EnemyAttackSlotCap = 6
ModifyEnemyAttackRecovery = true
ModifyEnemyMovementSpeed = true
ModifyHostileArrowVelocity = true
HostileArcherAimScatter = 1.25
ModifyEnemySightRange = true
ModifyEnemyHearingRange = true
ModifyEnemyAggroPersistence = true
ModifyKillExperience = true
ModifyQuestExperience = true
ModifyProficiencyExperience = true

COMPATIBILITY
-------------

Custom Difficulty is flagged as incompatible because it changes many of the same difficulty systems. Both can load, but overlapping Steel and Bone settings must be disabled.

Tainted Combat is conditionally compatible. Disable matching stamina, attack-slot, recovery, poise, or armor-penalty settings when both mods alter that system.

Better Movement is compatible. Its movement multipliers stack with Steel and Bone's optional Light armor bonus; disable either modifier if the combined speed is not desired.

Versatile Weapons 0.3.0+ is an optional soft integration. A shield hidden while
the opposite weapon uses both hands grants no passive shield protection; normal
protection returns when the shield hand becomes active.

Flat Arrows is conditionally compatible. Its bow pull, release, and instant-fire options do not directly overlap, but its arrow modifications stack with Steel and Bone's player velocity and gravity changes. Disable Flat Arrows' EnableArrowModifications or disable both ModifyPlayerArrowVelocity and ModifyPlayerArrowDrop in Steel and Bone.

HarderLife is conditionally compatible. Its incoming and outgoing damage, general and action stamina, mana, enemy vision, hearing, aggro persistence, and restorative-consumable modifiers stack with Steel and Bone. Set matching HarderLife multipliers to 1 or disable the corresponding Steel and Bone settings. HarderLife's parry health cost, backstab bonus, extended chase boundary, and debuff duration remain distinct.

Tainted Instincts is flagged as incompatible because it can modify enemy sight, damage, attack cadence, pursuit, and combat-slot behavior. Both can load, but overlapping Steel and Bone settings must be disabled.

Steel and Bone says nothing in game when no overlap is active. A confirmed overlap produces one short warning and lists the exact conflicting Steel and Bone toggles in BepInEx/LogOutput.log.

CONFIGURATION
-------------

BepInEx/config/ks.tgfoa.steel-and-bone.cfg

FoA Mod Manager presents common controls first, keeps related toggles and values together, and groups expert rule tuning and target-family terms under Advanced sections. Remaining stored section and setting keys stay stable for existing config files.

Direct melee damage numbers remain visible for 2x the final duration of other damage numbers by default, making them easier to catch while the camera follows a swing. Set MeleeDamageNumberDurationMultiplier to 1 for equal timing or tune it from 1 to 3.

Bleed, Poison, Burn, and Breath status-tick numbers begin at 3x the ordinary world-space height and 0.75x the ordinary text size by default so they remain visually distinct from direct hits. DamageOverTimeNumberHeightMultiplier can be tuned from 0 to 6, and DamageOverTimeNumberScale can be tuned from 0.5 to 2.

EffectivenessFeedbackSensitivity expands or compresses resistance and weakness distance from neutral for damage-number color and hit-marker tier selection only. Changing Preset sets it to 1.20 on Tempered, 1.10 on Hardened, or 1.00 on Crucible; customize the single value afterward without changing combat damage, number size, or duration.

WeakSpotDamageBonus changes to 0.10, 0.20, or 0.30 with Tempered, Hardened, or Crucible. It is added beside the game's native precision bonuses before outgoing and matchup multipliers. Native critical damage remains unchanged. Critical and weak-spot number size and red tint follow their combined real bonus up to x1.50 size and 50% red on unresisted hits, then fade with material resistance so heavily resisted hits remain dim; immunity remains grey. Hit-marker frames continue to report material effectiveness rather than being promoted by precision hits.

Dishonored Dynamic Crosshair 3.0.0 or newer can replace its current reticle
with eight effectiveness frames covering immunity or a direct physical or
magical hit that finalizes at exactly 1 damage, three resistance strengths, neutral
damage, and three weakness strengths, plus independent weak-spot and critical
overlays. On a killing blow, Steel and Bone also reports a Meager, Worthy,
Potent, or Prime corpse tier. Native enemy Tier tags provide the intrinsic
quality for nearly the entire roster, with a distinct anchor for each Tier from
0 through 7; kill XP and max health provide a shared fallback for untagged
enemies. Elite and MiniBoss enemies receive modest bonuses, while Bosses begin
at Prime quality. Enemy level then moves quality by 2.5% per level above or
below the hero, capped at 7.5% in either direction. This keeps level relevant
without turning the old roster uniformly Meager or distant enemies uniformly
Prime. Dishonored draws that tier above the weak-spot and critical layers. This
calculation shares Blood Magic Expansion's definitions but does not require
Blood Magic Expansion.
Steel and Bone shows RESISTED instead of 1 for ineffective hits. Damage-over-
time ticks use their separate marker sizing and do not use the 1-damage frame.
The markers use the same final colors and durations as Steel and Bone's damage
numbers and remain available when DamageNumbersEnabled=false.

On an incompatible config update, Steel and Bone automatically restores compatible values that you customized while retaining new safe defaults where meanings changed. FoA Mod Manager also keeps a final Import Previous Settings tab for conservative manual recovery. Restart after importing manually.

INSTALLATION
------------

Install with Vortex as a BepInEx plugin, or place the SteelAndBone folder under BepInEx/plugins.

TROUBLESHOOTING
---------------

Enable Diagnostics for target classification, modifier, armor, projectile, enemy-awareness, and compatibility details. ShowGrailFloatingTextDiagnostics defaults to true and, only while Diagnostics is enabled, controls short collapsed summaries when the final target, damage, or multiplier decision changes. Disabling the GFT switch leaves detailed BepInEx logging active. Disable other damage-number overlays if duplicate combat text appears.
