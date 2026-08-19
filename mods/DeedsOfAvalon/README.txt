Deeds of Avalon - Character Statistics
Version 1.8.1

Platforms: Windows and Linux through Proton.

Deeds of Avalon records character statistics inside the active save game and
shows a right-anchored, two-column summary while the quick wheel or root ESC
system menu is open.

Identity:

DLL: DeedsOfAvalon.dll
GUID: ks.tgfoa.deeds-of-avalon
Config: BepInEx/config/ks.tgfoa.deeds-of-avalon.cfg
Readable files: BepInEx/config/DeedsOfAvalon/Characters/<character-id>/statistics.json

FoA Mod Manager section order:
General, Tooltip Behavior, Panel Layout, Panel Background, Panel Colors,
Text Outline, Text Backing, Panel Content, Integrations, Diagnostics, and the
final Import Previous Settings section.

Grail Floating Text 2.5.7 or newer is required for the menu panels. The
statistics and JSON export still work when GFT is absent.

Panel layout:

<CHARACTER NAME IN ALL CAPS>   FOES DEFEATED
Level and current/required XP  Total foes
HP / MP / SP                   One-Handed weapon types: Number
Cobweb / Gold / Encumbrance    Two-Handed weapon types: Number
Attribute/Skill/Catalyst/Arthur Bows, shields, unarmed: Number
Contextual Wyrd Whispers       Fire / Cold / Wet / Electric
Wyrdnights survived: Number    Poison / Blood / Pure / Wyrdness
                               Summons / Other
                               Positive category rows normally appear
Quests completed: Number       Limited rows show Other categories
Locations discovered: Number
Recipes learned: Number
Items crafted: Number
Total gold earned: Number
Food eaten (Orange) / Potions used (Blue)
Fish caught (Cyan): Number
Locks picked: Number
Items pickpocketed: Number
Crime and bounty: Number
Hours rested: Number
Blood Essence: X (Y)
                               Additional deed rows when limited
Simple: Corpses Drained        Detailed: Meager through Prime tiers
Deaths: Number

The normal weapon/spell tooltip remains enabled by default. While either quick-
wheel tooltip is visible, the statistics panel fades completely out by default.
TooltipPanelOpacity and TooltipFadeSeconds control that transition.
HideItemTooltipText is an optional off-by-default switch for players who do not
want the normal tooltip.

The left heading uses the current character's name in all caps. Live HP, MP,
and SP appear immediately below Level/XP. A compact icon line beneath them
shows Ethereal Cobweb, Gold, and current/maximum Encumbrance. A compact line
then shows eligible Attribute, Skill, Catalyst, and Arthur values in that order,
omitting every zero or exhausted category. A separate Wyrd Whispers row appears
only while the vanilla reminder to talk to Arthur at camp is active. Level/XP,
HP/MP/SP, the resource line, available points, and Wyrd Whispers form one
continuous character summary. A slight gap follows whichever summary row is
last, before Wyrdnights survived or the first other tracked statistic. Deaths is
always the final left-column row, including at zero.
Two charcoal column backplates sit behind the text and icons for reliable
contrast against bright scenery. Two separately seeded procedural textures give
them subtly different mottling, fine fibers, transparent outer gutters, and
softly irregular feathered edges inspired by the radial menu and tooltip
backgrounds without copying a game texture. Their variation is encoded into
black alpha masks, retaining contrast against bright scenery without becoming
gray rectangles over black interiors. PanelBackgroundOpacity defaults to 0.95
and can be set to zero to disable them;
PanelBackgroundPadding controls their surrounding space. PanelColumnWidth
controls each column's width in 1440p reference pixels, and ColumnGap controls
the space between them. PanelScale is the 1440p
reference scale; lower-height displays reduce it proportionally, and unusually
tall row sets shrink further to stay on-screen. The full rendered text block is
centered vertically against the quick wheel. RightOffset
keeps the panel anchored to the right edge, while VerticalOffset provides an
optional adjustment from the centered position.
The panel publishes immediately when the quick wheel or root ESC system menu
appears and clears as its close begins. It also clears while a nested save,
load, settings, or other overlay screen hides the root pause menu. Show In
Quick Wheel and Show In Pause Menu independently control the two surfaces and
both default to enabled. Deeds does not search the world for either menu each
frame; only its live values refresh five times per second while a panel surface
is open.

WeaponStatisticsMode defaults to Detailed for individual weapon types. Grouped
combines the same saved counters into One-Handed, Two-Handed, and Bows while
keeping Shield, Unarmed, Throwables, and Other separate. Detailed mode shows
specific icons for one-handed sword, axe, blunt, dagger, and spear kills, plus
two-handed sword, axe, blunt, and spear kills. Sickles count as One-Handed Axe.
It combines
unrecognized handed and ranged fallbacks into one Other row while retaining
their separate save-backed counters. Existing two-handed polearm statistics
remain under the renamed Two-Handed Spear row. With Blood
Magic Expansion 2.5.4 or newer, supported spell kills use a dedicated Blood
type. Previously recorded supported spell-name rows are included in that
displayed total without rewriting the save-backed counters.
SortFoesByKillCount defaults to true and orders the final visible weapon,
magic, Summons, and Other rows from highest to lowest displayed count. Equal
counts use label order. Disable it to retain the authored weapon and magic
grouping order. Row limits and collapsed Other values are resolved before the
optional sort, and Diagnostics preview values follow the same path. While
sorting is enabled, weapon and magic fallback or collapsed rows are labeled
Other Weapons and Other Magic. With sorting disabled, both retain the shorter
Other label because their authored position and icons distinguish them.
Summons records enemies killed by the player's summons through the game's
dedicated summon-kill event and excludes summoned targets from both hero and
summon kill counts. Fallback magic types appear as Fire, Cold, Wet, Electric,
Poison, Blood, Pure, and Wyrdness, followed by Summons and then Other. Named
spell rows remain above those fallback types and sort by kill count. Summons
uses GFT's configurable Pink group, which defaults to #E06AAE.

All panel text has a configurable native SDF outline plus a soft black underlay.
TextOutlineEnabled, TextOutlineColor, TextOutlineOpacity, TextOutlineWidth, and
TextOutlineStrength control the outline independently; the TextShadow settings
control the underlay. Width and offset accept up to 16, softness accepts zero
through one, and both strengths accept one through eight. The default underlay
uses full opacity, a 4-pixel diagonal offset, 0.5 softness, and maximum spread,
creating a broad black backing without copying the text mesh.
Semantic White text uses a fixed internal 1.1 outline-strength multiplier,
including Level/XP, HP/MP/SP, Total, and contextual point rows; it does not
affect icons or Pale text. Supported SDF fonts normally use one glyph mesh;
unsupported fonts use a bounded compatibility fallback.
HeaderColor controls both column headers and defaults to #D88B38. SubheaderColor
controls both the Level/XP and Total lines and defaults to the Grail Floating
Text White pool. Deed and foe rows use the matching GFT Red, Orange, Gold, Blue,
Cyan, Green, Wyrd, Pink, Pale, and Default pools; changing those GFT colors updates the
panel. Magic categories and named spell rows use distinct type icons. Named
spell rows use their dominant recorded damage subtype for both icon and color.
Wyrdness reuses GFT's Wyrd icon, while Other and unknown magic reuse its generic
Magic icon:
Fire is Orange, Cold is Blue, Wet is Cyan, Poison is Green, Electric is Gold,
Wyrdness uses the semantic Wyrd style, Pure is Pale, Blood is Red, and unknown
magic uses White. When Eyes in the Dark is loaded, Wyrdnights survived and
Wyrdness magic both follow its live Purple Wyrdness or Native Orange palette.
When Diagnostics is enabled, every otherwise-zero known panel statistic appears
with a stable, plausible mid-game value for layout testing and screenshots. Its
Total is the sum of the displayed weapon, summon, and magic preview rows. This
preview does not alter save-backed counters or readable exports. Blood Essence
and Blood Power remain visible as 0 during
normal play. Diagnostics also writes one detailed line for a fatal hit that
reaches a weapon or magic fallback, including its source metadata, without
polling or logging ordinary hits.

Points available is hidden while the quick wheel is open. If Glorious UI has
both its master Enabled switch and HideGameplayHudInQuickUseWheel enabled,
Deeds defers to Glorious UI. Otherwise, Deeds snapshots and hides only the live
VCCharacterPointsAvailable view, then restores only the state it changed. Deeds
never edits the Glorious UI setting.

Active bounty is reconciled from the save's current per-faction bounty values,
including bounty that already existed when this version began tracking.
Highest bounty is the highest value Deeds has recorded.

Total gold earned accumulates positive changes to the character's Gold and
ignores spending. It appears with the other positive deed statistics, uses
GFT's Gold text color, and selects one of five built-in coin-pile icons from the
saved total: Very Low at 1-999, Low at 1,000-4,999, Medium at 5,000-14,999,
High at 15,000-39,999, and Very High at 40,000 or more. Tracking begins when
this statistic first becomes available for that character; current Gold is not
treated as previous earnings.

Save boundary:

The authoritative counters live in GameplayMemory under the DeedsOfAvalon
context, so they travel with the save. Deeds captures the readable JSON snapshot
when GameplayMemory serializes, but does not publish it until the game confirms
that save succeeded. Loading saved data refreshes the JSON from that loaded
state. A death, reload, or quit without saving therefore discards the unsaved
session's counters and does not add them to the readable file.

Deleting statistics.json does not reset the save-backed counters. It is rebuilt
after a successful save or load. Deleting the mod does not remove its saved
GameplayMemory context from existing saves.

Blood Magic Expansion integration:

Blood Magic Expansion 2.6.7 or newer owns uncapped, save-specific Blood Essence
and its derived Blood Power scaling. With Grail Floating Text installed, Deeds
shows `Blood Essence: X (Y)` immediately above Corpses Drained, including
visible zeros before the first ritual; X is integer Blood Essence and Y is
Blood Power. Simple mode shows only the
total Corpses Drained row; default Detailed mode shows only Meager, Worthy,
Potent, and Prime rows. With Grail Floating Text 2.4.4+, each detailed row uses
its matching tier icon. The aggregate Blood Essence row uses the blood-magic
icon, while Simple-mode Corpses Drained uses the saved average corpse quality
and defaults an empty history to Meager. The total and tiers never appear
together. The readable
character export includes both reported progression values. Interrupted,
rolled-back, or failed rituals do not count. Existing tier counts and their
saved quality sum can initialize an older character, after which Blood Magic
Expansion periodically synchronizes the absolute ledger to prevent drift.

Killing Blow Mastery:

Deeds does not read, import, translate, or migrate any former Killing Blow
Mastery counters or TSV files. Its statistics begin independently when Deeds is
installed.

Important defaults:

Enabled = true
TrackStatistics = true
ExportOnSuccessfulSave = true
ShowCharacterStatistics = true
HideItemTooltipText = false
PanelOpacity = 1
TooltipPanelOpacity = 0
TooltipFadeSeconds = 0.15
PanelScale = 1.5
PanelColumnWidth = 190
ColumnGap = 30
PanelBackgroundOpacity = 0.95
PanelBackgroundPadding = 16
RightOffset = 28
VerticalOffset = 0
HeaderColor = #D88B38
SubheaderColor = White
TextOutlineEnabled = true
TextOutlineColor = #000000
TextOutlineOpacity = 0.5
TextOutlineWidth = 5
TextOutlineStrength = 2
TextShadowEnabled = true
TextShadowOpacity = 1
TextShadowOffset = 4
TextShadowSoftness = 0.5
TextShadowStrength = 8
MaximumDeedRows = 32
MaximumWeaponRows = 28
MaximumMagicRows = 20
WeaponStatisticsMode = Detailed
SortFoesByKillCount = true
ShowCollapsedOtherRows = true
HidePointsAvailable = true
ShowBloodMagicStatistics = true
BloodMagicStatisticsMode = Detailed

Troubleshooting:

If statistics track but no panel appears, install or update Grail Floating Text
to 2.5.7 or newer. Enable Diagnostics in the Deeds config and inspect
BepInEx/LogOutput.log for event binding, GFT API, or export messages.
