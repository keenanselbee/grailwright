Versatile Weapons - Dynamic Grip
Version 0.7.7

Standalone dynamic weapon-grip mod for Tainted Grail: The Fall of Avalon.

What it does

Versatile Weapons allows native two-handed swords, axes, hammers, and spears to
occupy one hand as soon as the game's normal weapon requirements allow them.
Handling penalties steadily improve as Strength rises toward twice the weapon's
normal Strength requirement.

Converted native two-handed weapons in those four families use the matching
one-handed controller beside any one-slot hand item, including a shield, spell,
rod, or another melee weapon. Either hand order is supported. Both equipped
items remain active in one-handed grip, using the game's matching main-hand,
offhand-melee, or dual-wield combat behavior.

A converted native two-handed weapon with an empty opposite hand keeps its
native two-handed grip by default. Hold Toggle Weapon to use it in one hand
without equipping an offhand item, then hold again to return to its native
two-handed grip. Its native grip always retains completely vanilla combat
speed and potency; Strength-scaled penalties apply only after changing to the
one-handed grip.

While a supported weapon is drawn, hold the game's remappable Toggle Weapon
action for 0.45 seconds to change grip. Converted two-handed swords, axes,
hammers, and spears return to their native animations and stow the paired hand.
Native one-handed weapons in the same four families adopt the matching native
two-handed animation profile and stow any equipped offhand item. Hold the same
action again to restore the one-handed grip and offhand. A short press still
sheathes or draws normally.

By default, each native or Glorious UI weapon loadout remembers its last
manually selected grip. The remembered grip is restored only when that loadout
still has the exact same weapon, paired item, and grip-owning hand. Changing
equipment makes the new setup use its normal default grip until changed
manually, so stale grip choices cannot carry onto replacement weapons.

When both equipped weapons can change grip, the main-hand weapon owns Toggle
Weapon. An offhand greatweapon owns the grip control when the main hand is a
spell, shield, rod, or another item that Versatile Weapons does not grip-switch.

After a grip change, new attacks and blocks wait briefly for the selected
grip's equip animation to reach its stable idle or movement state. This prevents
an interrupted equip from leaving a weapon off-screen between attacks. A
three-second fail-safe restores normal input if the animation cannot settle.

Grip switching supports swords, axes, hammers or other blunt weapons, and
spears or other polearms. Daggers, rods, staves, tools, bows, and other ranged
weapons retain their normal equipment and animation behavior, but any one-slot
member of those categories can remain active beside a supported greatweapon.

In first person, native one-handed swords, maces, and axes receive separate
adjustable Y-position corrections while held with both hands. Swords default to
0.02 meters, while maces and axes default to -0.35 meters to bring short handles closer
to the support hand. Setting any value to 0 disables that family's correction.
The original position returns in every other grip and perspective.

Damage and proficiency

Each weapon retains its native stamina costs, requirements, and
template-filtered item effects. By default, its active grip selects the combat
proficiency: one-handed grips use One-Handed damage scaling and earn One-Handed
XP, while two-handed grips use Two-Handed damage scaling and earn Two-Handed
XP. No additional XP is created. Native weapons in their proper grip retain
their normal proficiency.

A supported native one-handed weapon used with both hands deals 150 percent
melee damage, uses 120 percent attack-animation speed, deals 120 percent poise
damage, and deals 110 percent force damage. Its hidden paired hand cannot cast,
supply the blocking item, or supply the blocking weapon. Because enemy block
stamina damage derives from final damage, the larger hit also applies more
guard pressure automatically. Native one-handed axes, maces, and other blunt
weapons also use 150 percent melee hit-detection range by default to compensate
for their lower first-person handle position; their visible models do not grow.

A native two-handed weapon used in one hand scales smoothly with Strength. At
its normal Strength requirement it deals 75 percent damage at 50 percent attack
speed, 60 percent poise, and 65 percent force. At 2x Strength it reaches 100
percent damage, 75 percent attack speed, 95 percent poise, and 100 percent
force. Values between those thresholds interpolate continuously and benefits
cap at 2x by default. A weapon with no normal Strength requirement instead
scales from the lower values at 0 Strength to the upper values at 10 Strength.

Config file

BepInEx/config/ks.tgfoa.versatile-weapons.cfg

Defaults

General
Enabled = true

Grip Switching
GripHoldSeconds = 0.45
ProficiencyFollowsGrip = true
RememberGripPerLoadout = true

Native Two-Handed Weapon - One-Handed Grip
FullPotencyStrengthMultiplier = 2
ZeroRequirementFullPotencyStrength = 10
DamageAtWeaponRequirement = 0.75
DamageAtFullPotency = 1
AttackSpeedAtWeaponRequirement = 0.5
AttackSpeedAtFullPotency = 0.75
PoiseAtWeaponRequirement = 0.6
PoiseAtFullPotency = 0.95
ForceAtWeaponRequirement = 0.65
ForceAtFullPotency = 1

Native One-Handed Weapon - Two-Handed Grip
DamageMultiplier = 1.5
AttackSpeedMultiplier = 1.2
PoiseMultiplier = 1.2
ForceMultiplier = 1.1
AxeMeleeRangeMultiplier = 1.5
MaceMeleeRangeMultiplier = 1.5

Advanced First-Person Alignment
OneHandedSwordPositionY = 0.02
OneHandedMacePositionY = -0.35
OneHandedAxePositionY = -0.35

Reverse Hands Compatibility
TwoHandedGripUsesNormalHands = true
SingleSpellUsesNormalHands = true

Diagnostics
Enabled = false
StrengthTestMode = Actual
ShowGrailFloatingTextDiagnostics = true

Import Previous Settings is always the final FoA Mod Manager section.

These compatibility settings act only when the game's Reverse Hands option is
enabled. By default, normal hand input is restored when a two-handed grip stows
its paired spell and whenever exactly one spell is equipped. Two-spell loadouts
can still use the game's reversed input. Disable either exception to retain the
vanilla behavior for that case.

StrengthTestMode can simulate WeaponRequirement or FullPotency for native
two-handed weapons used in one hand while Diagnostics is enabled. It affects
combat scaling without changing the character's actual Strength or save data.
Actual restores normal behavior.

Requirements

- Tainted Grail: The Fall of Avalon on the Mono branch.
- BepInEx 5 for the Mono build.

Compatibility

Remove or disable Dual Two-Handed and KS Dual Two-Handed Addon before using
Versatile Weapons. This standalone mod replaces that pair's equipment and
animation behavior; it declares both incompatible so conflicting plugins
cannot run together.

Battlecry Voice Tuner 1.1.3+ uses held Take All for its voice action and does
not conflict with Versatile Weapons' held Toggle Weapon grip control.

Blood Magic Expansion 2.7.2+ detects two-handed grips. A blood spell equipped
in the hidden paired hand is visually suspended and cannot remain active or cast;
it also stops counting as a relevant equipped blood spell for optional UI
integrations. Normal spell behavior returns with the one-handed grip.

Killing Blow Mastery 1.6.3+ awards its killing-blow bonus, notification skill,
weapon-family icon, and finisher sound from the effective grip. Steel and Bone
3.3.2+ ignores passive shield protection while Versatile Weapons suppresses
that shield, then restores it when the hand becomes active.

Grail Floating Text is optional. While Diagnostics is enabled,
ShowGrailFloatingTextDiagnostics controls every VW System message: completed
grip confirmations, weapon recognition, unsupported pairing,
blocked-transition, and recovery summaries. Detailed animator, input, pairing,
and FSM context remains in the BepInEx log.

The public VersatileWeaponsApi v2 reports suppressed hands, current two-handed
grip state, and effective One-Handed or Two-Handed proficiency. Integrations
fall back to native behavior when Versatile Weapons is absent or disabled.

Equipment changes made through the inventory UI are monitored independently of
loadout-index switches. Once the new weapon animator is ready, Versatile Weapons
restores the correct main-hand, offhand-melee, dual-wield, or two-handed combat
FSM if the game left conflicting layers active. When a converted greatweapon is
paired with a spell, the spell controller and visible gauntlet settle first;
Versatile Weapons then restarts the matching melee and spell FSMs together
without hiding or reloading either hand or animator controller. Shield and
native one-handed equipment changes use the same settled equip barrier so both
hands enter their equip animation together.

If a drawn supported weapon remains hidden after an interrupted transition,
the mod restores it after 1.5 seconds once weapon loading and Hero actions have
settled. When newer equipment work cancels a VW grip restoration, the same
watchdog applies only to the exact paired hand VW left hidden and starts after
gameplay resumes. Ordered grip-restoration reloads also recover after a
four-second timeout instead of waiting indefinitely.

Troubleshooting

If a supported two-handed weapon does not become available in one hand, confirm
that the game allows the weapon to be equipped and that it is a sword, axe,
hammer, or spear. Its paired item must occupy one hand by itself; bows,
two-handed magic, and other items that inherently require both slots cannot be
paired. If grip switching does not trigger, draw the loadout and hold Toggle
Weapon until the transition starts.

Set Enabled = true under Diagnostics and reproduce the transition before
sharing the newest BepInEx log. Diagnostics record input claiming, hold
completion, current grip, offhand pairing, perspective, weapon visibility,
animator loading, controller selection, transition ownership, and settled
equip-FSM stages. With Grail Floating Text installed,
ShowGrailFloatingTextDiagnostics is the subordinate switch for every VW
System summary. It defaults to true and remains inactive while Diagnostics is
off.

Glorious UI is optional. When its six virtual weapon loadouts control
equipment, Versatile Weapons recognizes the active Glorious slot instead of
collapsing every grip choice into native loadout row 0. Each Glorious loadout
therefore keeps independent exact-equipment grip memory.
