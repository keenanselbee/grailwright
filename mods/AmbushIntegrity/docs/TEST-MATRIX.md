# Ambush Integrity Test Matrix

Version under test: 0.1.7

## Focused release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| AI-SMOKE-01 | Load a save with default settings. | Ambush Integrity 0.1.7 loads without exceptions and creates `ks.tgfoa.ambush-integrity.cfg`. | Not run |
| AI-SMOKE-02 | Approach an unaware hostile while crouched and use a normal native sneak melee hit. | Native sneak damage remains unchanged; the mod does not add a second damage bonus. | Not run |
| AI-SMOKE-03 | Acquire the backstab prompt, begin the strike, and let the target's awareness flicker during the animation. | The exact target retains sneak classification only inside the 0.45-second commitment window. | Not run |
| AI-SMOKE-04 | Compare the backstab prompt at the native edge with the range multiplier at 1.0 and 1.2. | 1.2 allows a modestly farther prompt without bypassing crouch, target, or PreventBackStab checks. | Not run |
| AI-SMOKE-05 | Enable Diagnostics and walk the same unseen-hostile route in Light or no armor, Medium, and Heavy or Overload. | Default normal-step strength resolves from native 0.50 to 0.60, 0.80, and 1.00 respectively; heavier armor makes the enemy investigate sooner without starting combat before visual confirmation. | Not run |
| AI-SMOKE-06 | Kill an isolated enemy with a sneak melee strike. | The victim's immediate hostile-action broadcast is skipped; normal death and corpse behavior continue. | Not run |
| AI-SMOKE-07 | Repeat the lethal sneak strike with a friendly NPC nearby or with line of sight. | Clean Execution is denied and vanilla alert handling runs. | Not run |
| AI-SMOKE-08 | With Grail Floating Text installed, enter searching, become detected, and hide again. | Detailed notifications show each state transition once without a continuous meter. | Not run |
| AI-SMOKE-09 | Repeat an eligible ambush with Grail Floating Text absent. | Gameplay remains active and no integration exception is logged. | Not run |
| AI-SMOKE-10 | Attack using a bow, magic, damage over time, and a secondary effect. | Ambush Integrity applies no damage or alert modification. | Not run |
| AI-SMOKE-11 | With Dishonored Dynamic Crosshair 3.1.6 installed, acquire and lose backstab eligibility on one target and then switch targets. | The lower-dagger overlay appears only for the final eligible current target, pulses once, and clears without stale carryover. | Not run |

## Extended experiments

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| AI-EXP-01 | Begin a committed attack, switch the raycast to another NPC, and land on the new target. | The commitment does not transfer to the new target. | Not run |
| AI-EXP-02 | Wait beyond the commitment window before striking. | No expired opportunity changes damage classification. | Not run |
| AI-EXP-03 | Repeat the armor-tier route while crouched. | Footstep Awareness logs native crouch mode and does not modify the game's existing crouched armor noise. | Not run |
| AI-EXP-04 | Execute an isolated enemy near geometry that blocks observer sight. | Only observers inside range with combat awareness or a clear ray count as witnesses. | Not run |
| AI-EXP-05 | Disable each feature independently. | Its behavior returns to vanilla without disabling the remaining experiments. | Not run |
| AI-EXP-06 | Enable Diagnostics and GFT diagnostics, then exercise positive and bypass paths. | The log records effective settings, range and target transitions, attack classifications and skip reasons, opportunity lifecycle, witness evidence, awareness transitions, and GFT delivery without per-frame spam. | Not run |
| AI-EXP-07 | Use FoA Mod Manager's Import Previous Settings action with and without a compatible backup. | Import is transactional, bounded, and safe when no compatible backup exists. | Not run |
| AI-EXP-08 | Repeat normal-step armor tests with Steel and Bone hearing enabled across Tempered, Hardened, and Crucible. | Ambush Integrity retains 0.60, 0.80, and 1.00 default strengths while Steel and Bone independently scales native hearing range by 1.10, 1.20, or 1.30. | Not run |
| AI-EXP-09 | With Steel and Bone enabled, land the same committed ambush under each available plugin load order. | Ambush Integrity adds the preserved sneak bonus once before Steel and Bone applies its final player-damage and material multipliers; the final modifier is identical across load orders. | Not run |
