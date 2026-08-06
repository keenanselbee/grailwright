Steel and Bone
Version 3.1.4

Platforms: Windows and Linux through Proton.

Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.

Steel and Bone is a lightweight, knowledge-driven BepInEx 5 Mono difficulty mod for Tainted Grail: The Fall of Avalon. Material weaknesses and resistances define the experience: learn what an enemy is made from, choose the right physical or magical answer, and read the result through reactive damage numbers.

Its supporting difficulty systems make that preparation matter through farther-sighted enemies, faster arrows, clearer armor roles, resource pressure, steadier group aggression, poise tuning, and slower progression. It does not replace enemy AI, rewrite encounters, inflate enemy health, or modify coin rewards.

MATERIAL COMBAT
---------------

Vanilla-authored weaknesses, resistances, and immunities run first. Steel and Bone can preserve or amplify them by preset, then adds one focused material rule only where appropriate. More specific families take precedence over broad categories, and elite enemies soften custom extremes.

Mixed hits are resolved by damage part, so an enchanted weapon's physical strike, elemental payload, and status effect each keep their own matchup.

Bone undead favor blunt damage and resist blood, bleed, slash, and pierce. Constructs favor blunt and resist biological damage, slash, and pierce. Against ordinary humanoid armor, Slash loses effectiveness faster than Pierce while Blunt becomes progressively stronger from Light through Heavy armor. Ordinary flesh receives mild biological and edged-weapon weaknesses. Flesh undead, drowned corpses, infected flesh, sea creatures, spirits, Wyrd creatures, and flora each have distinct physical or magical answers.

Direct player arrows now have their own material identity instead of acting as generic Pierce. Against ordinary humanoids, equipped armor creates a clear curve: exposed flesh is most vulnerable, Light armor remains slightly favorable, Medium is neutral, and Heavy strongly resists arrows while also resisting ordinary Pierce. Other material families retain their own arrow reactions. Fire, Electric, and other payloads keep their own matchup rather than inheriting the physical arrow penalty.

Direct player spells receive a small tiered advantage against armor, while Fire, Electric, and Cold also react to the equipped armor's native Fabric, Leather, or Metal surface. Electricity is strongest against metal, while fire remains useful against fabric, leather, and heated metal. Blood, Wyrdness, biological effects, and spells that already ignore armor do not receive a duplicate armor-tier bonus. Set ArrowMaterialRulesEnabled or ArmoredSpellWeaknessEnabled to false to disable either feature independently.

The design is inspired by Requiem's emphasis on coherent rules, preparation, and intelligent tactical play, but Steel and Bone is not a port or total conversion. It translates that philosophy into Tainted Grail's native combat systems with independently toggleable features.

PRESETS
-------

Tempered keeps penalty and protection modifiers neutral, uses lighter material rules, and sets player arrows, hostile arrows, and hostile enemy sight distance to x1.10.

Hardened is the default. Incoming damage, stamina use, mana use, and native armor-weight penalties rise by 5%; enemy attack slots gain 1; enemy recovery, player poise damage, and experience gains change by 5%; light armor movement gains 2.5%; medium physical armor is x1.05; heavy and overloaded physical armor are x1.10; arrows and enemy sight use x1.30.

Crucible uses 10% pressure, adds 2 enemy attack slots, grants 5% light armor movement, makes medium physical armor x1.10 and heavy or overloaded physical armor x1.20, and uses x1.50 arrows and enemy sight.

PlayerDamageDealtMultiplier remains independent from presets and defaults to 1.

Every global modifier has its own toggle. Set DifficultyModifiersEnabled=false to retain material combat and damage feedback while disabling the complete preset-driven layer.

VANILLA SYSTEMS
---------------

Steel and Bone reads the game's current Light, Medium, Heavy, or Overload armor tier. It scales the native armor-penalty stat, so vanilla thresholds, individual penalty rules, armor proficiency, and overload behavior remain in control. Light receives a modest movement bonus; Medium and Heavy receive progressively stronger physical protection. Overload inherits Heavy protection but keeps its native overload penalties.

Player arrows are scaled at the native bow launch. Hostile NPC arrows are scaled before the game's ballistic trajectory and movement-prediction solve. Projectile behavior remains native; the optional material layer modifies only the physical share of direct player arrow hits.

Enemy sight tuning multiplies each active hostile NPC's native sight-distance stat. It preserves authored perception differences, line of sight, visibility, alert buildup, hearing, pursuit, and immediate-combat behavior. Friendly NPCs, summons, allies, inactive AI, and dead actors are excluded.

Vanilla attack slots are Story/Easy 1, Normal/Challenge 2, Hard 3, and Survival 4. Steel and Bone adds 0/1/2 and caps only its own increase at 6 by default.

DEFAULT DIFFICULTY SETTINGS
---------------------------

Enabled = true
Preset = Hardened
ArrowMaterialRulesEnabled = true
ArmoredSpellWeaknessEnabled = true
DifficultyModifiersEnabled = true
ModifyPlayerDamageDealt = true
PlayerDamageDealtMultiplier = 1
ModifyPlayerDamageTaken = true
ModifyStaminaUsage = true
ModifyManaUsage = true
ModifyPlayerPoiseDamageDealt = true
ModifyPlayerArrowVelocity = true
ModifyArmorWeightPenalties = true
ModifyLightArmorMobility = true
ModifyArmorPhysicalProtection = true
ModifyEnemyAttackSlots = true
EnemyAttackSlotCap = 6
ModifyEnemyAttackRecovery = true
ModifyHostileArrowVelocity = true
ModifyEnemySightRange = true
ModifyKillExperience = true
ModifyQuestExperience = true
ModifyProficiencyExperience = true

COMPATIBILITY
-------------

Custom Difficulty is flagged as incompatible because it changes many of the same difficulty systems. Both can load, but overlapping Steel and Bone settings must be disabled.

Tainted Combat is conditionally compatible. Disable matching stamina, attack-slot, recovery, poise, or armor-penalty settings when both mods alter that system.

Better Movement is compatible. Its movement multipliers stack with Steel and Bone's optional Light armor bonus; disable either modifier if the combined speed is not desired.

Tainted Instincts is flagged as incompatible because it can modify enemy sight, damage, cooldown, pursuit, and combat-slot behavior. Both can load, but overlapping Steel and Bone settings must be disabled.

Steel and Bone says nothing in game when no overlap is active. A confirmed overlap produces one short warning and lists the exact conflicting Steel and Bone toggles in BepInEx/LogOutput.log.

CONFIGURATION
-------------

BepInEx/config/ks.tgfoa.steel-and-bone.cfg

On an incompatible config update, Steel and Bone automatically restores compatible values that you customized while retaining new safe defaults where meanings changed. FoA Mod Manager also keeps a final Import Previous Settings tab for conservative manual recovery. Restart after importing manually.

INSTALLATION
------------

Install with Vortex as a BepInEx plugin, or place the SteelAndBone folder under BepInEx/plugins.

TROUBLESHOOTING
---------------

Enable Diagnostics for target classification, modifier, armor, projectile, enemy-awareness, and compatibility details. Disable other damage-number overlays if duplicate combat text appears.
