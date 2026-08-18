Steel and Bone
Version 3.8.3

Platforms: Windows and Linux through Proton.

Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.

Steel and Bone is a lightweight, knowledge-driven BepInEx 5 Mono difficulty mod for Tainted Grail: The Fall of Avalon. Material weaknesses and resistances define the experience: learn what an enemy is made from, choose the right physical or magical answer, and read the result through reactive damage numbers.

Its supporting difficulty systems make that preparation matter through sharper enemy sight and hearing, tiered combat movement, faster and flatter arrows, clearer armor roles, slower food recovery, stronger overdrinking pressure, steadier group aggression, progressive late-game Tenacity, poise tuning, and slower progression. It does not replace enemy AI, rewrite encounters, inflate enemy health, or modify coin rewards.

MATERIAL COMBAT
---------------

Vanilla weaknesses, resistances, and immunities run first. Steel and Bone can preserve or amplify them by preset, then adds one focused material rule only where appropriate. More specific families take precedence over broad categories, and elite enemies soften custom extremes.

Mixed hits are resolved by damage part, so an enchanted weapon's physical strike, elemental payload, and status effect each keep their own matchup.

Bone bodies favor blunt damage and resist slash and pierce, while true Bone Undead also resist blood, bleed, and Cold. Stone bodies favor blunt and resist edges, while true Constructs additionally resist biological and Cold damage. Body material remains separate from creature identity: Stagfather and the Straw spirits keep bone-body weapon reactions without becoming Bone Undead, while Sleepwalker keeps its stone body without becoming a Construct. Against ordinary humanoids, exposed flesh is neutral to Blunt while armor makes Blunt progressively stronger from Light through Heavy. Ordinary flesh receives mild biological and edged-weapon weaknesses. Flesh undead, drowned corpses, infected flesh, sea creatures, spirits, Wyrd creatures, and flora each have distinct physical or magical answers. Wyrd creatures have no blanket Wyrdness resistance; their native and exact reactions remain authoritative. Axes gain a focused advantage against wood and flora without stacking with the ordinary slash rule. Bone Undead and Constructs take x0.66 Cold damage on Hardened where native data is neutral, while focused fire-body, crystal, slime, summon, and frost-aligned reactions remain authoritative. Cold resistance changes damage and impact, not independent spell effects such as Chill. Curlghasts, Marrowghasts, Slugholders, and Snail remain Cold-neutral unless their native data says otherwise. Wet answers exact fire-aligned bodies, and Electric answers drowned skeleton sailors. Singworms, Lir's summoned tentacle, Blood Abominations, Wyrd Slimes, and Tidewraiths use exact soft-body profiles that favor cutting or neutral points over absorbed Blunt impacts.

Direct player arrows now have their own material identity instead of acting as generic Pierce. Against ordinary humanoids, equipped armor creates a clear curve: exposed flesh is most vulnerable, Light armor remains slightly favorable, Medium is neutral, and Heavy strongly resists arrows while also resisting ordinary Pierce. Other material families retain their own arrow reactions. Fire, Electric, and other payloads keep their own matchup rather than inheriting the physical arrow penalty.

Direct player spells receive a small tiered advantage against armor, while Fire, Electric, and Cold also react to the equipped armor's native Fabric, Leather, or Metal surface. Electricity is strongest against metal, while fire remains useful against fabric, leather, and heated metal. Blood, Wyrdness, biological effects, and spells that already ignore armor do not receive a duplicate armor-tier bonus. Set ArrowMaterialRulesEnabled or ArmoredSpellWeaknessEnabled to false to disable either feature independently.

Material impact keeps reactions proportional to effectiveness. Every resisted direct player hit carries 60% of its resistance into reduced poise and force, without letting weaknesses amplify control. Immune or very strongly resisted hits also lose the routine small flinch. Real poise breaks, force stumbles, ragdolls, damage processing, effects, and aggro remain intact. MaterialImpactRulesEnabled disables this complete layer.

Progressive Tenacity is independent from presets. It remains inactive through hero level 20, then scales linearly to full strength at level 35. At full strength, Trash, Normal, Elite, MiniBoss, and Boss enemies resist 10%, 15%, 25%, 30%, or 40% of player-caused poise, force, and stamina damage; direct health damage uses half that amount. A confirmed native or Steel and Bone material weakness halves Tenacity for that hit. Hero-owned summon attacks count as player-caused, while Critter and Hero Summon targets receive none. Damage over time, environmental damage, unrelated NPC combat, enemy health pools, and preset values remain unchanged. Set ProgressiveTenacityEnabled to false to disable the complete curve.

The compact technique layer gives focused builds a fallback without replacing the best material counter. Pommel strikes borrow the Blunt matchup against bone, stone, and ordinary armor; heavy melee attacks partially breach custom rigid resistance; and direct area hits pressure otherwise-neutral swarms. Native reactions still take priority, and TechniqueMatchupRulesEnabled disables the complete layer.

An equipped and readied shield also provides modest passive protection against direct physical attacks from the front. Its effective vanilla Block rating supplies 8%, 10%, or 12% of that value as damage reduction by preset, while its vanilla BlockAngle controls coverage up to the forward 180 degrees. Rear attacks, magic, status effects, damage over time, sheathed shields, active blocks, and shields suppressed by Versatile Weapons receive no passive reduction.

The design is inspired by Requiem's emphasis on coherent rules, preparation, and intelligent tactical play, but Steel and Bone is not a port or total conversion. It translates that philosophy into Tainted Grail's native combat systems with independently toggleable features.

PRESETS
-------

Tempered increases incoming health damage by 5%, reduces outgoing health damage and experience gains by 5%, adds 10% base damage to confirmed weak-spot hits, keeps resource, armor-weight, recovery, poise, enemy combat movement, and aggro persistence neutral, adds 60 Potion Poisoning buildup to the matching potion class, and uses lighter material rules with arrows, enemy sight, and hero footstep hearing range at x1.10. Food remains usable during combat, heals at half rate for four times its native duration, and restores 1 stamina each second outside Stamina Depleted.

Hardened is the default. Incoming health damage rises by 10%, while outgoing health damage and experience gains fall by 10%. Confirmed weak spots add 20% base damage. Stamina use, mana use, and native armor-weight penalties rise by 5%; potions add 65 Potion Poisoning buildup to their matching class; food cannot be consumed during combat by default, heals at 37.5% rate for four times its native duration, and restores 1 stamina each second outside Stamina Depleted; agile common enemies gain up to 5% combat movement; enemy attack slots gain 1; native aggro persistence rises to x1.10; enemy hearing uses x1.20; enemy recovery and player poise damage fall by 5%; light armor movement gains 2.5%; medium physical armor is x1.05; heavy and overloaded physical armor are x1.10; arrows and enemy sight use x1.30.

Crucible increases incoming health damage by 15%, reduces outgoing health damage and experience gains by 15%, adds 30% base damage to confirmed weak-spot hits, uses 10% resource, armor-weight, recovery, poise, and enemy combat movement pressure, makes each potion add 70 Potion Poisoning buildup to its matching class, prevents food consumption during combat by default, makes food heal at 25% rate for four times its native duration with 1 stamina restored each second outside Stamina Depleted, raises native aggro persistence to x1.20 and hearing to x1.30, adds 2 enemy attack slots, grants 5% light armor movement, makes medium physical armor x1.10 and heavy or overloaded physical armor x1.20, and uses x1.50 arrows and enemy sight.

PlayerArrowGravityMultiplier also remains independent from presets and defaults to 0.75, reducing player-arrow gravity by 25% on every preset without tilting the native launch direction.

HostileArcherAimScatter changes to 1.50, 1.25, or 1.00 meters with Tempered, Hardened, or Crucible. It remains freely adjustable afterward, and 0 restores native accuracy.

Every global modifier has its own control. Set WeakSpotDamageBonus=0 to disable only the added weak-spot reward, or set DifficultyModifiersEnabled=false to retain material combat and damage feedback while disabling the complete preset-driven layer.

VANILLA SYSTEMS
---------------

Steel and Bone reads the game's current Light, Medium, Heavy, or Overload armor tier. It scales the native armor-penalty stat, so vanilla thresholds, individual penalty rules, armor proficiency, and overload behavior remain in control. Light receives a modest movement bonus; Medium and Heavy receive progressively stronger physical protection. Overload inherits Heavy protection but keeps its native overload penalties.

Player arrows are scaled at the native bow launch and use 0.75x gravity by default on every preset. The separate gravity control reduces drop without changing native aim, draw strength, projectile offsets, collision, elemental payloads, or damage. Hostile NPC arrows receive preset-scaled minimum aim scatter at the native target-point route, then their speed is scaled before the game's movement-prediction and ballistic solve. Larger authored scatter and native gravity are preserved. The optional material layer modifies only the physical share of direct player arrow hits.

Enemy awareness remains native-first. Sight tuning multiplies each active hostile NPC's sight-distance stat, while hearing tuning changes only the range of hero footstep noise and preserves native strength, wall checks, armor noise, and individual NPC hearing. Aggro persistence slows only the native combat decay rate on Hardened and Crucible. Steel and Bone does not force immediate combat, extend chase boundaries, suppress combat exit, or replace target-loss rules. Friendly NPCs, summons, allies, inactive AI, and dead actors remain outside owned NPC tuning.

Potion healing, auxiliary effects, item tooltips, and Better UI presentation remain native. Steel and Bone tracks overdrinking independently for Health, Mana, Stamina, and Utility potions. Each class receives 60 buildup on Tempered, 65 on Hardened, or 70 on Crucible and decays at the native 10 points per second, so repeating one class within 2, 3, or 4 seconds triggers Potion Poisoning while mixing classes does not combine their buildup. Direct multi-resource restoratives contribute once to every resource they restore; temporary buffs, cures, locks, regeneration effects, reset potions, and other non-restoratives share Utility. Triggering any class clears every bucket and pauses buildup during the native status. Health, Mana, or Stamina poisoning drains 30% of the matching snapshotted maximum over the status, while Utility drains 15% of maximum HP, MP, and SP. Normal recovery can offset the drain; Health stops at 1 HP, while Mana and Stamina can reach zero.

Standard food always lasts four times its native duration and restores 1 stamina in discrete one-second ticks. Tempered heals at 50% of the native rate (200% total), Hardened at 37.5% (150% total), and Crucible at 25% (100% total). Selecting Tempered sets PreventFoodUseInCombat off, while Hardened and Crucible set it on; customize the setting afterward if desired. When enabled, food and dishes cannot be consumed during combat, while noncombat use remains unrestricted. Blocked attempts are silent unless Grail Floating Text is installed, which shows a brief food-styled explanation. Food does not stack: when several native food statuses exist, Steel and Bone keeps only the one with the greatest remaining queued health recovery, using remaining time as the tie-breaker. Health and stamina share that status, timer, and icon. Direct stamina ticks remain effective through ordinary regeneration lockouts from sprinting, blocking, attacks, and ranged-weapon use. During true native Overexertion, however, active food halves the calculated regeneration lock and matching Stamina Depleted duration; its added stamina ticks pause for the complete lock. The first food stamina point arrives 0.1 seconds after that lock ends, then the normal one-second cadence resumes. Food health recovery and the shared status duration continue normally, so missed stamina ticks are not banked or repaid. If the food status expires or food recovery is disabled during exhaustion, the pending point is discarded. Exact-zero stamina and Potion Poisoning remain distinct because neither creates native Overexertion. The native health status still drives queued-healing prediction. Food tooltips update from the current preset without adding Steel and Bone or preset labels. Better UI receives the adjusted health and maximum possible stamina-recovery overlay values when installed; its normal slot refresh timing remains authoritative.

Stamina Depleted uses Smooth vignette presentation by default. Steel and Bone keeps the game's existing image but replaces its repeating flash and abrupt removal with one eased fade in and fade out over 0.30 seconds. Native restores the original presentation, while Off hides both the HUD vignette and stamina-depleted post-process. All modes retain native exhaustion audio, movement penalties, action restrictions, and status behavior. StaminaDepletedVignetteFadeSeconds adjusts the Smooth transition from 0.05 to 2 seconds.

Enemy movement tuning multiplies the game's native combat movement stat without changing attack animation speed. Exposed, Light-armored, and ordinary agile enemies such as wolves and swarms receive the full 0%/5%/10% preset bonus. Medium-armored, Elite, Beholder, and Slugholder enemies receive at most half. Heavy-armored enemies, bears, constructs, flora, bosses, minibosses, scripted Critters, and non-pathing actors retain their vanilla speed. It applies only to living, active, hostile combatants and can also affect native movement during lunging attacks.

Vanilla attack slots are Story/Easy 1, Normal/Challenge 2, Hard 3, and Survival 4. Steel and Bone adds 0/1/2 and caps only its own increase at 6 by default.

MAIN DIFFICULTY SETTINGS
------------------------

Enabled = true
Preset = Hardened
ArrowMaterialRulesEnabled = true
MaterialImpactRulesEnabled = true
ArmoredSpellWeaknessEnabled = true
TechniqueMatchupRulesEnabled = true
PassiveShieldProtectionEnabled = true
DifficultyModifiersEnabled = true
ProgressiveTenacityEnabled = true
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
ModifyPotionOverdrinking = true
ModifyFoodRecovery = true
PreventFoodUseInCombat = true
StaminaDepletedVignetteMode = Smooth
StaminaDepletedVignetteFadeSeconds = 0.30
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

Tainted Combat is conditionally compatible. Disable matching stamina, attack-slot, recovery, poise, or armor-penalty settings when both mods alter that system. Progressive Tenacity is included in warnings when its poise reduction overlaps. Its AffectFoodAndDishes option also overlaps Steel and Bone's food recovery and combat-use restriction; leave that option disabled when Steel and Bone owns food behavior.

Better Movement is compatible. Its movement multipliers stack with Steel and Bone's optional Light armor bonus; disable either modifier if the combined speed is not desired.

Versatile Weapons 0.3.0+ is an optional soft integration. A shield hidden while
the opposite weapon uses both hands grants no passive shield protection; normal
protection returns when the shield hand becomes active.

Flat Arrows is conditionally compatible. Its bow pull, release, and instant-fire options do not directly overlap, but its arrow modifications stack with Steel and Bone's player velocity and gravity changes. Disable Flat Arrows' EnableArrowModifications or disable both ModifyPlayerArrowVelocity and ModifyPlayerArrowDrop in Steel and Bone.

HarderLife is conditionally compatible. Its incoming and outgoing damage, general and action stamina, mana, enemy vision, hearing, aggro persistence, and consumable-effectiveness modifiers can stack with Steel and Bone's matching food or difficulty systems, including Progressive Tenacity's direct-health reduction. Steel and Bone's Potion Poisoning buildup tuning is distinct from HarderLife's potion effectiveness. Set matching multipliers to 1 or disable the corresponding Steel and Bone settings when an overlap warning appears. HarderLife's parry health cost, backstab bonus, extended chase boundary, and debuff duration remain distinct.

Tainted Instincts is flagged as incompatible because it can modify enemy sight, damage, attack cadence, pursuit, and combat-slot behavior. Both can load, but overlapping Steel and Bone settings must be disabled.

Steel and Bone says nothing in game when no overlap is active. A confirmed overlap produces one short warning and lists the exact conflicting Steel and Bone toggles in BepInEx/LogOutput.log.

CONFIGURATION
-------------

BepInEx/config/ks.tgfoa.steel-and-bone.cfg

FoA Mod Manager presents common controls first, keeps related toggles and values together, groups expert rule tuning and target-family terms under Advanced sections, and places Diagnostics immediately before the final Import Previous Settings section. Version 3.8.3 removes numeric prefixes from all stored section names; the schema reset backs up the previous config and regenerates the clean layout.

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

Enable Diagnostics for target classification, modifier, armor, projectile, enemy-awareness, and compatibility details. Repeated EnemyHearingRange messages are capped at one every two seconds. ShowGrailFloatingTextDiagnostics defaults to true and, only while Diagnostics is enabled, controls short collapsed summaries when the final target, damage, or multiplier decision changes. Disabling the GFT switch leaves detailed BepInEx logging active. Disable other damage-number overlays if duplicate combat text appears.
