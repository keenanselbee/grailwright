# Steel and Bone Docs

This folder has three living design documents and three background research reports.

## Start Here

| Question | Read |
|---|---|
| How does the 3.0 global difficulty layer work? | [steel-and-bone-3.0-difficulty.md](steel-and-bone-3.0-difficulty.md) |
| What should Steel and Bone implement next? | [steel-and-bone-design.md](steel-and-bone-design.md) |
| What should each enemy or family resist and fear? | [steel-and-bone-enemies.md](steel-and-bone-enemies.md) |
| What does the local NPC template data prove? | [research/npc-template-resistance-research-report.md](research/npc-template-resistance-research-report.md) |
| How does the TG damage pipeline work? | [research/combat-resistance-research-report.md](research/combat-resistance-research-report.md) |
| What did Requiem teach us philosophically? | [research/requiem-research-report.md](research/requiem-research-report.md) |

## Source Priority

When docs disagree, prefer sources in this order:

| Priority | Source | Use for |
|---:|---|---|
| 1 | Local Tainted Grail 1.25 game files and exported `NpcTemplate` data | Enemy resistances, weaknesses, status immunities, families, tiers, surface types |
| 2 | Decompiled `TG.Main.dll` behavior | Damage pipeline, hooks, subtype names, armor behavior, runtime implementation constraints |
| 3 | Current Steel and Bone source | What is already implemented |
| 4 | Top-level design docs | Chosen Steel and Bone overlays, roadmap, tuning philosophy |
| 5 | Requiem and web/wiki sources | Inspiration, naming, lore context, and enemy-list discovery |

## Maintenance Rules

Keep enemy numeric facts in [steel-and-bone-enemies.md](steel-and-bone-enemies.md). Keep matchup decisions and roadmap items in [steel-and-bone-design.md](steel-and-bone-design.md). Keep the current global modifier, compatibility, and verification contract in [steel-and-bone-3.0-difficulty.md](steel-and-bone-3.0-difficulty.md).

Use the research reports as evidence appendices. If a report conflicts with newer local TG data, add a dated correction note and update the two living docs rather than duplicating a second answer.

Label new ideas as one of these:

| Label | Meaning |
|---|---|
| Confirmed by TG data | Seen in local game files or decompiled runtime code |
| Steel and Bone overlay | A deliberate mod rule that extends vanilla behavior |
| Requiem-inspired | A design lesson borrowed from Requiem, not proof about TG data |
| Needs runtime test | Plausible, but not proven by exported templates or code inspection |

Use the engine names `Cold` and `Electric`. Treat Holy and Silver as item, effect, or text-detection ideas only unless a native subtype-like signal is found later.
