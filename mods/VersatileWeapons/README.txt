Versatile Weapons - Dynamic Grip
Version 0.4.0

Standalone dynamic weapon-grip mod for Tainted Grail: The Fall of Avalon.

What it does

Versatile Weapons allows native two-handed swords, axes, hammers, and spears to
occupy one hand as soon as the game's normal weapon requirements allow them.
Handling penalties steadily improve as Strength rises toward twice the weapon's
normal Strength requirement.

Converted native two-handed weapons in those four families use the matching
one-handed controller when paired with a shield. The integration includes
first-person and third-person support plus ordered animation recovery after
equipment changes and direct loadout switches.

A converted native two-handed weapon with an empty opposite hand keeps its
native two-handed grip by default. Hold Toggle Weapon to use it in one hand
without equipping an offhand item, then hold again to return to its native
two-handed grip. Its native grip always retains completely vanilla combat
speed and potency; Strength-scaled penalties apply only after changing to the
one-handed grip.

While a supported weapon is drawn, hold the game's remappable Toggle Weapon
action for 0.45 seconds to change grip. Converted two-handed swords, axes,
hammers, and spears return to their native animations and stow their shields.
Native one-handed weapons in the same four families adopt the matching native
two-handed animation profile and stow any equipped offhand item. Hold the same
action again to restore the one-handed grip and offhand. A short press still
sheathes or draws normally.

After a grip change, new attacks and blocks wait briefly for the selected
grip's equip animation to reach its stable idle or movement state. This prevents
an interrupted equip from leaving a weapon off-screen between attacks. A
three-second fail-safe restores normal input if the animation cannot settle.

Grip switching supports swords, axes, hammers or other blunt weapons, and
spears or other polearms. Daggers, rods, staves, tools, bows, and other ranged
weapons retain their normal equipment and animation behavior.

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
damage, and deals 110 percent force damage. Its hidden offhand cannot cast,
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
cap at 2x by default.

Config file

BepInEx/config/ks.tgfoa.versatile-weapons.cfg

Defaults

1. General
Enabled = true

2. Native Two-Handed Weapon - One-Handed Grip
FullPotencyStrengthMultiplier = 2
DamageAtWeaponRequirement = 0.75
DamageAtFullPotency = 1
AttackSpeedAtWeaponRequirement = 0.5
AttackSpeedAtFullPotency = 0.75
PoiseAtWeaponRequirement = 0.6
PoiseAtFullPotency = 0.95
ForceAtWeaponRequirement = 0.65
ForceAtFullPotency = 1

3. Native One-Handed Weapon - Two-Handed Grip
DamageMultiplier = 1.5
AttackSpeedMultiplier = 1.2
PoiseMultiplier = 1.2
ForceMultiplier = 1.1
AxeMeleeRangeMultiplier = 1.5
MaceMeleeRangeMultiplier = 1.5

4. Grip Switching
GripHoldSeconds = 0.45
ProficiencyFollowsGrip = true

5. Advanced First-Person Alignment
OneHandedSwordPositionY = 0.02
OneHandedMacePositionY = -0.35
OneHandedAxePositionY = -0.35

6. Diagnostics
Enabled = false
StrengthTestMode = Actual
ShowGrailFloatingTextDiagnostics = true

7. Reverse Hands Compatibility
TwoHandedGripUsesNormalHands = true
SingleSpellUsesNormalHands = true

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
in the hidden offhand is visually suspended and cannot remain active or cast;
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
restores the correct single melee animation FSM if the game left conflicting
one-handed and two-handed layers active.

If a drawn supported weapon remains hidden after an interrupted draw transition,
the mod now restores it after 1.5 seconds once weapon loading and Hero actions
have settled. Ordered sword-and-shield controller reloads also recover after a
four-second timeout instead of waiting indefinitely.

Troubleshooting

If a supported two-handed weapon does not become available in one hand, confirm
that the game allows the weapon to be equipped and that it is a sword, axe,
hammer, or spear. If grip switching does not trigger, draw a supported weapon
with a shield, spell, or empty opposite hand and hold Toggle Weapon until the
transition starts.

Set Enabled = true under 6. Diagnostics and reproduce the transition before
sharing the newest BepInEx log. Diagnostics record input claiming, hold
completion, current grip, offhand pairing, perspective, weapon visibility,
animator loading, and ordered reload stages. With Grail Floating Text installed,
ShowGrailFloatingTextDiagnostics is the subordinate switch for every VW
System summary. It defaults to true and remains inactive while Diagnostics is
off.
