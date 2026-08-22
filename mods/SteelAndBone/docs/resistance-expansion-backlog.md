# Resistance Expansion Backlog

This file keeps the promising ideas deliberately left out of the first technique-matchup pass. They are not commitments. Each needs stronger gameplay evidence or a clearer implementation boundary before entering the live ruleset.

## Implemented Baseline

- Pommel strikes borrow the existing Blunt matchup against bone, stone, and ordinary armor.
- Heavy melee attacks recover 40% of a custom resistance's distance from neutral against those rigid targets.
- Direct, non-arrow area hits gain a modest fallback against otherwise-neutral swarms.

These rules use the existing preset intensity, mixed-hit resolution, native-reaction precedence, elite clamps, and combat feedback.

## Focused Runtime Validation

- **Armor penetration as resistance recovery:** promising for Pierce-focused melee, but it may double-count the game's numerical armor penetration and needs weapon-by-weapon testing.
- **Charged spell fallback:** a possible pure-caster recovery tool against resistant families, pending a reliable distinction between charged attacks and ordinary spell projectiles.
- **Lunge identity:** lunges could reward Pierce against soft or bulky targets, but the available signal must be checked across weapons and NPC-driven attacks.
- **Bow draw and projectile force:** stronger draw could partly recover physical arrow resistance without erasing material counters, but native draw scaling already changes damage.
- **Weak-spot resistance recovery:** mechanically readable, but it risks making weak spots a universal answer and flattening the material system.
- **Backstab specialization:** potentially useful against living and flesh targets, but it needs a dependable player-backstab signal and careful interaction with critical damage.
- **Poise-break opening:** a temporary resistance reduction after a real poise break could reward melee skill, but it introduces target state, timing, and stacking complexity.

## Optional or Expansion-Sized Ideas

- **Cold chill, brittle, and shatter:** strong thematic potential, especially for bone and crystal, but better suited to a dedicated Cold Magic expansion with visible status feedback.
- **Wet interactions:** reconsider only after confirming a broadly player-accessible Wet damage or application path. Do not treat Cold as Wet or preserve unsupported Wet-magic entries in integrations.
- **Elemental sequencing:** effects such as Cold followed by Blunt or Wet followed by Electric are readable in theory but require stateful combo tracking and clear expiry rules.
- **Status-threshold material reactions:** poison, bleed, burn, or other buildup thresholds could provide focused-build fallbacks, but native immunities and status ownership must remain authoritative.
- **Coating-based fallback routes:** weapon coatings may help pure melee builds cover difficult families, but availability, subtype composition, and overlap with native reactions need a full inventory audit.
- **Dedicated swarm handling:** if `Damage.Radius` proves inconsistent for important spells or attacks, inspect their concrete damage producers before adding name-based exceptions.
- **Additional school-specific arcane rules:** keep Generic Magical modest. Soul Rend's exact Soul and Service API provenance now supports the dedicated Necrotic rules; every other new school still needs equally reliable provenance rather than names or universal magic bonuses.
- **Generic Magical resonance:** removed after the live spell-template audit found that only The Hollow Core actually consumes Generic Magical for direct player damage. Reconsider only if future content gives the subtype meaningful spell coverage.

## Rejected Unless New Evidence Appears

- Universal Holy or Silver damage rules without a supported native subtype.
- Blanket Cold weakness for cave dwellers, bone undead, or all constructs.
- Universal boss resistance or family-wide health inflation.
- Generic Physical or Generic Magical as the best answer to every family.
- Hidden adaptive resistance, repeated-hit counters, or other opaque stateful mechanics.
