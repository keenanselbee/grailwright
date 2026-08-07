Glorious UI 1.7.3

Platforms: Windows and Linux through Proton.

Glorious UI provides a centered, resolution-independent gameplay HUD with
smart quick slots, immersive compass visibility, quieter notifications, and
automatic quick-use wheel cleanup. Its optional Sensible Rest Menu places noon
at the top of the rest clock and provides consistent time formatting.

DEFAULT LAYOUT
--------------

- Hero HUD: Bottom Center with the tuned -72 X, -25 Y, and 0.9 scale baked
  into the layout. User adjustments therefore default to 0, 0, and 1.
- Eyes in the Dark integration: Eyes creates, mirrors, updates, hides, and
  destroys its own Wyrd Threat meter. When both mods are enabled, Glorious
  requests that Eyes place that meter below the vanilla resource bars.
- Quick Slot HUD: Bottom Center with the tuned 196 X, 39.1 Y, and 0.8 scale
  baked into the layout. User adjustments therefore default to 0, 0, and 1.
- Arrow counter: tuned -7 X, -7 Y, and 1.38 scale baked into the layout, with
  its quantity text enlarged by 30 percent.
- Wyrd Power HUD: position control enabled, use prompt hidden, with tuned
  -2 X, -5 Y, and 0.9 scale baked into the layout. Glorious keeps the prompt
  hidden if a later vanilla HUD update tries to reactivate it.
- Buffs and debuffs: the tuned 140 X, -55 Y, and compact native-spacing
  adjustment are baked into the layout. User adjustments therefore default
  to 0 X, 0 Y, and 1 spacing. Nine icons fit on each row by default, with
  additional rows expanding upward from the same visual column.
- Screen-relative anchors keep the same composition at 1920x1080, 2560x1440,
  3440x1440, and other aspect ratios without separate presets.

IMMERSIVE HUD DEFAULTS
----------------------

- Compass control is enabled, but the compass is hidden.
- Compass modes are Hidden, Always Visible, Toggle with Hotkey, and Hold
  Hotkey to Show. The hotkey defaults to None.
- Top-right points, bonfire, Arthur memory, and Wyrd whisper reminders are
  disabled by default.
- Closing the Escape menu, Character Sheet, Inventory, or another modal menu
  does not restart a timed level notification that was already hidden.
- Notification modes are Disabled, Timed Fade, and Vanilla. Timed Fade uses
  configurable visible and fade durations.
- Native new, completed, and failed quest notices plus objective updates remain
  fully visible for 10 seconds by default. Quest and Objective Duration accepts
  0 to restore the game's timing or up to 60 seconds.
- The Hero HUD, quick slots, arrow counter, Wyrd Power HUD, compass, and level
  notifications, buffs, and debuffs are hidden while the quick-use wheel is
  open and restored when it closes.
- Hidden quick-slot use and cycle prompts remain suppressed if vanilla HUD
  refreshes try to reactivate them.
- Held Apply Changes prompts use the current Interact binding for both the
  displayed key and the action itself.
- When Interact is F, Settings Restore Defaults moves to E so holding F cannot
  trigger both actions.

SENSIBLE REST MENU
------------------

- Enabled by default and independently toggleable.
- Places noon and the sun at the top of the rest clock, 6 PM at the right,
  midnight and the moon at the bottom, and 6 AM at the left.
- Rotates the native hand, fill, and radial input mapping together while
  retaining the game's rest calculations and controls.
- Defaults to 12-hour labels and AM/PM popup times; 24-hour labels are optional.
- Formats the quick-menu clock consistently by default. That part can be
  disabled independently.
- Disabling the feature restores the native rest-clock layout and text. The
  quick-menu clock uses the selected format the next time that menu opens.

SMART QUICK SLOTS
-----------------

- The large quick slot stays pinned to the autofill food slot.
- The two small previews show smart health and mana potion choices.
- Health potion hotkey: C
- Mana potion hotkey: V
- Biggest and SmallestSufficient selection modes are available.

EXPANDED EQUIPMENT PANEL
------------------------

- Six virtual quick slots replace the two visible manual quick slots in the
  Equipment tab, arranged as two columns by three rows.
- The vanilla food autofill slot remains below the quick-slot grid.
- Click Q1 through Q6 to open the normal item chooser for that virtual slot.
- Assigned consumables display their current stack quantities.
- Each quick slot can optionally receive a direct-use gameplay hotkey.
- Vanilla weapon loadout 0 remains visible as the active editable row.
- Six compact weapon-loadout selectors appear below it. Selecting one loads
  it into the active row; normal weapon changes there are tracked back into
  the selected virtual loadout.
- The quick-use wheel uses those same six virtual loadouts: slots 1 through 4
  retain the vanilla left and upper positions, while slots 5 and 6 replace the
  bottom quick-item wedges. Selecting any wedge equips it through the active
  vanilla row without repurposing native loadouts 1 through 3.
- The wheel hides its center control diagram and bottom controls legend and
  supports immediate left-click selection by default. These presentation and
  input behaviors are configurable.
- Bow loadouts show their assigned arrow quantity with low-ammo color cues.
- Left-clicking a bow loadout's arrow icon cycles only that Glorious virtual
  loadout through the available arrow types without closing the wheel.
- The lower wedges mirror the upper-left and upper-right weapon-loadout
  sectors across the wheel. Their two-item icon positions are mirrored from
  the same sources while the item artwork remains upright.
- Each weapon selector displays its main hand and the appropriate off-hand or
  quiver assignment.
- Selected virtual quick and weapon slots receive gold highlighting.
- Disabling either Equipment Panel ownership setting restores the associated
  vanilla controls.
- Custom Equipment controls are removed as soon as the Equipment model closes,
  preventing them from remaining over the Bag, Map, or another character tab.
- The game's four configurable weapon-loadout actions activate Glorious
  loadouts 1 through 4, preserving vanilla keyboard and controller remapping.
- Glorious provides dedicated configurable hotkeys only for extended loadouts
  5 and 6, defaulting to number keys 5 and 6.
- Every wheel and hotkey selection uses the same typed equipment routine as
  More Weapon Loadouts: it writes the chosen weapons into native row 0 and
  lets the game's normal EquipItem path apply them.
- The Smart Bag / All hotkey defaults to Tab. It opens Bag > All from gameplay,
  closes an active equipment or quick-slot picker before navigating its parent,
  navigates there from another Character Sheet panel, and closes through the
  game's native Back hierarchy when Bag > All is already open.
- Optional direct Bag hotkeys can open Weapons, Magic, Armor, Jewelry, Gems,
  Potions, Consumables, Crafting, Readables, Recipes, Quest Items, or Other.
- If the same physical key also triggers the game's generic Character Sheet
  action, Glorious suppresses that duplicate press so Bag cannot immediately
  reopen. The separate Equipment action remains available and opens Equipment.
- Item-picker tooltips are cleared immediately when their picker closes.
- Closing the quick-use wheel restores the prior level-notification state
  without treating the close as a new five-second notification.

The expanded Equipment panel is standalone. Glorious stores its virtual
quick slots and weapon loadouts in GloriousEquipmentSlots.dat inside the
active save archive, with a per-save local backup under:
BepInEx/config/GloriousUI/Equipment/
Assignments store weapon template GUIDs. When several copies match, Glorious
uses the highest-level and then lightest copy, and keeps dual-wielded copies
distinct.

ONE-MENU EQUIPPING
------------------

- Left click equips the hovered weapon or spell to the main hand.
- Right click equips it to the off hand from the same equipment picker.
- The same hand shortcuts work while a weapon is hovered in the Bag.
- The behavior supports both the main-hand and off-hand pickers, preserves
  comparison tooltips, and can optionally redirect the off-hand picker.
- Selecting an already equipped hand item can unequip it.
- Click interception, hand shortcuts, notifications, picker redirection, and
  already-equipped behavior are independently configurable.
- No OneMenuEquip dependency is required. If owrocc.OneMenuEquip is still
  enabled, Glorious detects it and disables only its duplicate hooks for that
  session.

CONFIGURATION
-------------

Config path:
BepInEx/config/ks.tgfoa.glorious-ui.cfg

FoA Mod Manager presents General, Rest Menu, Hero HUD, Quick Slot HUD, Arrow HUD,
Wyrd Power HUD, Buffs and Debuffs, Compass, Notifications, Hotkeys, Bag
Category Hotkeys, Smart Selection, Equipment Panel, One-Menu Equipping,
diagnostics, and Import Previous Settings sections.

The Notifications section includes Quest and Objective Duration. It changes
only how long the game's native quest notices remain fully visible; their
presentation and track/open prompts stay native.

The Advanced / Diagnostics section includes a visual-only buff/debuff layout
test. Its temporary icons do not apply effects, change stats, or alter saves.

UPGRADING
---------

Glorious UI has a new DLL, package folder, plugin GUID, and config path.
Disable or remove Better Quick Slots or Wyrdframe before enabling Glorious UI
so two versions do not patch the same HUD methods.

COMPATIBILITY
-------------

Glorious UI replaces owrocc.ModifyHeroHUD. Disable or remove that plugin.
Glorious also includes the functionality of owrocc.OneMenuEquip; remove or
disable the standalone plugin when switching to Glorious's implementation.
Other mods controlling the same Hero HUD, status icon layout, compass,
notification, quick-use, arrow, Wyrd Power, or quick-slot components may
conflict.

Eyes in the Dark is an optional soft dependency. Glorious uses its versioned
HUD placement contract without creating or controlling the Wyrd Threat meter.
When Eyes is absent or the contract is unavailable, Glorious makes no meter
changes. Eyes can update the Wyrdnight REST-button state while Glorious
controls only the clock and time presentation.

Do not load owrocc.ModifyHeroHUD.dll, owrocc.ModifyQuickSlotsHud.dll,
owrocc.HideLevelUp.dll, owrocc.MoreWeaponSlots.dll, owrocc.OneMenuEquip.dll,
or owrocc.RebindQuickWheel.dll alongside Glorious UI. Do not load
owrocc.BagHotkeys.dll; Glorious includes its category hotkeys, and both plugins
polling the same key can reopen the Bag after Glorious closes it.

When Better UI is installed, disable its QuickSlotEffectEnabled,
AmmoCounterEnabled, and ArrowCycleEnabled settings. Glorious removes the
consumable wheel slots and provides virtual-loadout-aware replacements for
ammo counters and arrow cycling.
