# Food and Potion Template Audit

This audit records the current local game's concrete food and potion templates, their English localization, tooltip tokens, abstract-template ancestry, action types, effect graphs, and authored effect variables. It is intended to be the evidence base for Steel and Bone's food-duration and tooltip work.

The snapshot was generated on 2026-08-13 from addressables catalog build `bc8b9c56d14b0e0cf153e63bf83fb61b` using Unity fallback version `6000.0.64f1`.

## Stored artifacts

- [food-potion-template-audit.json](food-potion-audit/food-potion-template-audit.json) is the complete machine-readable inventory, including all item fields and every attached skill reference.
- [food-potion-template-audit.csv](food-potion-audit/food-potion-template-audit.csv) is the flattened item-by-item working sheet.
- [food-potion-tooltip-strings.csv](food-potion-audit/food-potion-tooltip-strings.csv) deduplicates the exact English name, flavor, and description strings and shows every template that uses each one.
- `tools/game-inspection/Export-FoodPotionAudit.py` reproduces the export against another installed game build.
- The first raw extraction remains under `.codex-temp/steel-and-bone-food-potion-audit/` for local comparison.

## Coverage

| Surface | Result |
|---|---:|
| Complete English localization entries decoded | 54,670 |
| Concrete food and potion templates inventoried | 299 |
| Food templates | 154 |
| Potion templates | 145 |
| Normal-path templates | 246 |
| `NotInUse` or `ZZZ` path templates | 53 |
| Unique name, flavor, and description records | 531 |
| Attached skill references | 528 |
| Unmapped skill references | 0 |
| Item-prefab parse failures | 0 |
| Name keys without current English text | 0 |
| Description keys without current English text | 14 |

The 14 empty English descriptions are present as authored localization keys but have no English value in this game build. They are retained as empty records rather than guessed. `NotInUse` and `ZZZ` are path-based availability hints only; they do not prove whether another runtime system can spawn an item.

## Extraction and classification

The exporter decodes the game's custom `languages.arch` layout using the same structure as `Awaken.Babel`: UTF-16 key and string blobs indexed by arrays of `StringPosition { charStart, charLength }`. This recovers the exact English localization entry associated with each `IdOverride` or generated localization ID.

The addressables catalog is decoded to map every serialized GUID to its asset path. The item bundle is then read prefab by prefab. A concrete template is classified as:

- a potion when its recursive abstract-template ancestry contains an `Abstract_ItemTemplate_Potion*` template;
- otherwise food when one of its item-effect attachments uses the `Eat` action.

Potion ancestry takes priority because the game uses the `Eat` action for ordinary potions as well as food. This avoids incorrectly classifying health and mana potions as food.

## Tooltip findings

The tooltip text can be replaced deliberately without appending a Steel and Bone suffix or duplicating the native description.

The important managed-code route is:

1. `ExistingItemDescriptor.ItemDescription` supplies the resolved item description used by the ordinary inventory tooltip.
2. `TempItemDescriptor` and `VendorItemDescriptor` inherit that descriptor behavior for crafting and vendors.
3. `ItemTooltipDescriptionsEffectsComponent` displays `descriptor.ItemDescription` when it is non-empty; it falls back to the separate effects collection only when that description is empty.
4. `Item.DescriptionFor` resolves the localized token text against the item's variable container.

That gives Steel and Bone one clean replacement point. The mod can build the final description from the item's real, preset-adjusted values and return it as `ItemDescription`. It does not need to append a second paragraph labelled with the mod or preset name.

The inventory also shows why the replacement must preserve per-item structure instead of substituting one universal sentence:

- 107 normal food templates use `NameSkill_Consumable_FoodHealForDurationDesc`: `Restores [HealValue] health over [DurationCalculated]s.`
- 12 normal food templates use the mana-over-time description.
- several foods combine healing with another stat effect, alcohol, poison, a quest action, or a unique effect;
- 14 descriptions currently resolve to an empty English value;
- potions have a much wider range of one-off descriptions and should not be rewritten by the food feature.

The safest scope is therefore to replace only the affected food-effect line while preserving all unrelated native lines and token formatting.

## Effect findings

The standard food-over-time graph is:

`Assets/Data/Templates/SkillGraphs/Consumable/Prefabs/Consumable_ApplyStatus_FoodHealForDuration.prefab`

It is referenced by 127 normal food templates:

| Authored target stat | Templates |
|---|---:|
| `HealthRegen` | 113 |
| `ManaRegen` | 12 |
| `StaminaRegen` | 2 |

Its authored durations are not uniform: 57 references use 10 seconds, 15 use 30 seconds, and 55 use 60 seconds. The item skill reference carries the important variables directly, normally including `AddValue` and `Duration`.

For the standard food graph, the native total shown as `HealValue` is derived by `ItemVariableAccessor.GetHealValue` as:

`AddValue * Duration`

This confirms that duration and per-second effectiveness can be adjusted independently while keeping the tooltip mathematically correct.

The implemented preset relationships retain the native health status while drawing recovery out over four times the authored duration:

| Preset | Per-second multiplier | Duration multiplier | Total multiplier |
|---|---:|---:|---:|
| Tempered | 0.50 | 4.0 | 2.0 |
| Hardened | 0.375 | 4.0 | 1.5 |
| Crucible | 0.25 | 4.0 | 1.0 |

Crucible keeps total healing at native parity, while Tempered and Hardened make food more sustaining without increasing its per-second rescue strength.

## Stamina-regeneration implications

A stamina-over-time channel can share the adjusted food duration. The implementation stores 1 stamina per second on the same native food status on every preset and restores it in discrete whole-point ticks. Ordinary action regeneration lockouts do not suppress those ticks. Native Overexertion deliberately does: active food halves its paired regeneration-lock and Stamina Depleted durations and pauses the added stamina channel without banking progress. The transition out preloads 0.9 seconds of the next interval so the first point arrives 0.1 seconds later, then normal one-second cadence resumes. This avoids fractional stamina every frame, since any positive fraction can satisfy the game's action-availability check, without letting the next whole food tick prematurely clear the zero-stamina movement penalty.

Three normal edible templates already author a stamina-regeneration effect: Grandma's Powermilk, Onion Bun, and Peppermint. Apple Fritters, Wyrddeer Stew, and Heart affect maximum stamina rather than stamina regeneration. A blanket preset bonus must therefore be additive by design and the tooltip builder must avoid producing two indistinguishable stamina lines.

## Confident implementation boundary

The following can be implemented with high confidence:

1. Identify concrete food at runtime without relying on display text.
2. Restrict healing scaling to the standard food-over-time graph when its `StatEnum` is `HealthRegen`.
3. Override the graph's effective `AddValue` and `Duration` using the selected preset.
4. Store the added stamina rate and adjusted duration on the same native food status, then restore one whole stamina point at each elapsed-second boundary on every preset. Suspend and reset that interval during native Overexertion while halving its paired regeneration-lock and Stamina Depleted durations, then preload 0.9 seconds of the next interval when the lock ends.
5. Replace the affected tooltip description through the descriptor route, render the final numeric healing and duration deliberately, preserve unrelated effect lines, and omit all Steel and Bone or preset branding.
6. Apply the same descriptor behavior to inventory, crafting previews, and vendor views through their shared descriptor base.

The native food template is source-separated, so the base game can retain several simultaneous food-health statuses. Steel and Bone instead compares each status's own contribution to native queued-healing prediction, keeps only the greatest remaining contribution, and uses remaining duration as the tie-breaker. This makes both health recovery and the attached stamina channel strictly non-stacking.
