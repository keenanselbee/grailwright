# Blood and Soul Ecosystem

This document is the shared gameplay contract for Blood Magic Expansion (BME), Soul and Service (S&S), Steel and Bone, and Dishonored Dynamic Crosshair (DDC).

## Identity and balance

- Blood magic acts on flesh, circulation, and the body's remaining vitality.
- Necrotic magic acts on souls, spiritual resistance, and service after death.
- The two paths use the same long progression curve: 1,000 resource reaches Power 100 and 5,000 reaches Power 200.
- Blood Essence is permanent progression. Soul Vigor is both current progression and a spendable reanimation resource; spending it lowers Necromantic Power and may relock abilities.
- Blood and soul are independent claims on one body. A blood ritual does not consume the soul or simplify the corpse. Light Soul Rend always consumes the soul and reduces the corpse to bones, ending any remaining blood opportunity.

## Steel and Bone matchup matrix

Hardened target multipliers use these baseline values before preset intensity and elite clamps:

| Target | Blood | Necrotic |
| --- | ---: | ---: |
| Living flesh | 1.15 | 1.10 |
| Sea flesh | 1.10 | 1.10 |
| Infected or Wyrd flesh | 0.85 | 0.875 |
| Flesh undead | 0.75 | 0.60 |
| Drowned undead | 0.65 | 0.675 |
| Skeleton | 0.25 | 0.40 |
| Construct | 0.25 | 0.25 |
| Spirit | 0.30 | 1.225 |
| Flora or fungus | 0.65 | 1.175 |

Steel and Bone asks BME and S&S whether the exact `Damage` object is Blood or Necrotic. Text matching is only a compatibility fallback when the owning API is unavailable. Summoned servants retain their native attack damage types.

## Blood Essence extraction

The nominal integer awards remain 1, 3, 5, and 10 for Meager, Worthy, Potent, and Prime corpses. Each nominal point independently has a `5% + BloodPower * 0.05%` chance to yield one additional Essence. Bonuses are capped at 1, 1, 2, and 3 respectively, producing ranges of 1-2, 3-4, 5-7, and 10-13. Extraction can only improve the nominal award.

## Soul Vigor economy

Soul Vigor is stored and presented as an integer. Existing saves are rounded to the nearest non-negative integer when read; no retroactive award recalculation is performed.

Nominal natural-soul values are 3, 9, 15, and 30. Center-weighted tier rolls use these ranges:

- Meager 2-4 with weights 1/2/1.
- Worthy 7-11 with weights 1/2/3/2/1.
- Potent 12-18 with weights 1/2/3/4/3/2/1.
- Prime 24-36 with weights 1/2/3/4/5/6/7/6/5/4/3/2/1.

Exact corpse quality subtly shifts the weighted roll toward the low or high side of its tier. Necromantic Power adds an expected positive mastery bonus of `nominal * 5% * Power / 200`, probabilistically rounded to a whole point.

Reanimation costs are 1, 3, 5, and 10 Vigor by corpse tier. Native ordinary summons cost 3. An attempt with insufficient Vigor fails without committing the reanimation. Commands do not cost Vigor.

Current Power gates remain 10/20/30/50/70/90 for Attack, individual formation, global formation, behavior, Recall, and Swarm; Empower remains 100. Falling below a gate relocks it. Held servants return to Follow, Bulwark or Hunt resolves as Guard, an active Swarm finishes but cannot be started again, and a reduced capacity never kills existing servants but blocks new ones until legal.

Every servant has a once-only salvage pool: `native soul value + invested Vigor`. Ordinary summons have no native soul value; raised corpses retain the one natural-soul roll associated with their source. Light Soul Rend returns `round(pool * currentHealth / maxHealth)`, capped by the pool. A healthy ordinary 3-Vigor summon therefore returns 3; at 75% it returns 2; at 25% it returns 1; at death it returns 0. Restoration before salvage can recover that value. Death, reanimation, or restoration cannot reset the once-only claim.

## Exsanguination and reanimated servants

On a successful BME ritual, exsanguination severity is rolled once and attached to the source body. Its center is `30% - 10% * BloodPower / 200`, with uniform random variation of plus or minus two percentage points and a final clamp of 20-30%. Corpse quality does not affect this roll.

If S&S later reanimates that source, it first determines the servant's normal starting current Health, then removes the stored percentage of that current Health. Maximum Health is unchanged and Heavy Soul Rend can restore the loss.

An owned raised servant whose source still contains blood can itself complete the normal BME ritual. It awards the normal one-time healing, XP, and Blood Essence and marks the source blood spent. An owned ordinary flesh summon can also be drained for emergency healing, but grants no XP or Blood Essence because it has no source-corpse progression claim. At completion, a servant above 20% maximum Health loses the rolled exsanguination fraction of current Health. At or below 20%, the ritual succeeds and kills it; S&S preserves a raised servant's pre-execution Health fraction for the later light-rend salvage calculation. Skeletons, spirits, constructs, and other bloodless servants are invalid. Already-spent servants and sources remain recognizable only so the crosshair can show their desaturated spent state.

The live-servant ritual is an out-of-combat convenience interaction. S&S exposes raised-servant identity regardless of combat; BME owns permission and reports combat as a desaturated blocked reticle without channeling. While channeled, the servant is held in place and remains targetable. Damage, combat, a new command, excessive distance, or cancellation releases it. After completion it remains near its last safe position briefly so the player can immediately use light Soul Rend.

## Heavy Soul Rend feedback

Only held Heavy Soul Rend exposes contextual hover state. Light Soul Rend is instant and displays no hover text.

Soul Rend counts as equipped only while at least one hand containing it is currently available. Versatile Weapons may retain the spell in a paired hand hidden by a two-handed grip; that suppressed hand produces no Soul Rend targeting, hover state, or DDC necromantic reticle until it becomes available again.

- Valid corpse: `Reanimate: N Soul Vigor` or `Requires N Soul Vigor`.
- Eligible living hostile: `Claim Soul: N% Chance` using the final chance.
- Injured owned servant: `Restore Servant`.
- Healthy, eligible owned servant: `Empower Servant`.
- Fully restored servant that cannot currently be empowered: `Servant Fully Restored`.

S&S owns the authoritative state and text. DDC consumes one cached optional state call and maps Restore/Fully Restored to `custom_reticle_necromagic_heal.png` and Empower to `custom_reticle_necromagic_empower.png`. Affordable Reanimate, Restore, and Empower use saturated Necrotic green. Requires Soul Vigor and Servant Fully Restored use the same desaturated unavailable presentation as blocked or drained Blood Magic targets. Feedback-only hover text never adds the generic interaction hand.

BME remains text-free while a blood spell is equipped. An eligible corpse or owned undrained raised flesh servant uses the normal saturated red blood-ritual quality reticle; a drained or otherwise unavailable servant uses the established desaturated version.

## Integration ownership

- BME owns Blood provenance, blood claims, exsanguination rolls, and live blood rituals.
- S&S owns soul claims, natural soul values, Vigor spending, servant investment and salvage, reanimation, relocks, and Heavy Soul Rend hover state.
- Steel and Bone owns matchup multipliers and combat feedback.
- DDC only renders the state exposed by the owning mod.

All integrations are lazy, optional, and reflection-based. No plugin gains a hard dependency or performs another plugin's raycasts each frame.
