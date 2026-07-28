Better Quick Slots 0.2.1

Better Quick Slots reworks the HUD quick-slot display into a focused food,
health potion, mana potion, and arrow utility bar.

Default behavior:
- The large HUD quick slot stays pinned to the game's autofill food slot.
- The game's existing quick-slot use key still uses food. No separate food
  hotkey is required.
- The vanilla quick-slot cycle is redirected back to food, so manual quick
  slots 1 and 2 no longer take over the large HUD slot.
- The two small HUD icons are repurposed as smart health and mana potion
  previews drawn by Better Quick Slots.
- The vanilla arrow counter is moved into the cluster at the same size as the
  health preview.
- Health potion hotkey: C
- Mana potion hotkey: V
- HUD position and scale can be adjusted from the config.

The inventory and quick wheel can still show the original food, slot 1, and
slot 2 entries. Better Quick Slots only decouples those manual slots from the
small HUD preview cycle.

Config path:
BepInEx/config/ks.tgfoa.better-quick-slots.cfg

Useful settings:
- HealthPotionHotkey and ManaPotionHotkey can be changed or set to None.
- FoodSelectionMode, HealthPotionSelectionMode, and ManaPotionSelectionMode can
  be Biggest or SmallestSufficient.
- HudOffsetX and HudOffsetY move the quick-slot HUD from its vanilla anchored
  position. Positive X moves right, and positive Y moves up.
- HudScale changes the quick-slot HUD size. The neutral value is 1.0.
- OwnArrowSlot moves and scales the vanilla arrow counter into the Better Quick
  Slots HUD cluster.
- ArrowSlotOffsetX and ArrowSlotOffsetY fine-tune only the arrow slot after it
  is placed. Positive X moves right, and positive Y moves up.
- ArrowSlotScale fine-tunes only the arrow slot size. The neutral value is 1.0.
- PreventPotionWasteAtFull avoids using health or mana potions while that
  resource is already full.
- IgnoreHotkeysWhenCursorVisible avoids using smart potions from menu screens.

Smart selection:
Biggest chooses the matching item with the largest detected restore amount.
SmallestSufficient chooses the smallest matching item that can cover the
missing health or mana; if none is large enough, it falls back to the largest
available.

Compatibility:
This mod patches the vanilla quick-slot HUD, hero HUD arrow counter, and
HeroItems quick-slot selection methods. It may conflict with other mods that
replace the same HUD components or rewrite vanilla quick-slot cycling. Disable
other quick-slot HUD position/scale mods if both mods try to move the same UI.
