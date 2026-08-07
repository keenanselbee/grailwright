Deeds of Avalon - Character Statistics
Version 1.0.1

Platforms: Windows and Linux through Proton.

Deeds of Avalon records character statistics inside the active save game and
shows a right-anchored, two-column summary while the quick wheel is open.

Identity:

DLL: DeedsOfAvalon.dll
GUID: ks.tgfoa.deeds-of-avalon
Config: BepInEx/config/ks.tgfoa.deeds-of-avalon.cfg
Readable files: BepInEx/config/DeedsOfAvalon/Characters/<character-id>/statistics.json

Grail Floating Text 1.11.1 or newer is required for the quick-wheel panel. The
statistics and JSON export still work when GFT is absent.

Quick-wheel layout:

DEEDS OF AVALON                 FOES DEFEATED
Level and current/required XP   Total foes
Wyrdnights survived            One-handed weapon types
Quests completed               Two-handed weapon types
Locations discovered           Bows, shields, unarmed, throwables
Crime and bounty records       Magic by spell/damage type
Rest records                   Only positive category rows appear
Corpses Drained integration    Limited rows collapse into Other

The normal weapon/spell tooltip remains enabled by default. While either quick-
wheel tooltip is visible, the statistics panel fades to 50% of its configured
opacity. TooltipPanelOpacity and TooltipFadeSeconds control that transition.
HideItemTooltipText is an optional off-by-default switch for players who do not
want the normal tooltip.

Points available is hidden while the quick wheel is open. If Glorious UI has
both its master Enabled switch and HideGameplayHudInQuickUseWheel enabled,
Deeds defers to Glorious UI. Otherwise, Deeds snapshots and hides only the live
VCCharacterPointsAvailable view, then restores only the state it changed. Deeds
never edits the Glorious UI setting.

Recorded active bounty is the total Deeds has observed through crime and bounty-
clear events since it began tracking the character. Existing bounty in factions
that have not produced either event is not presented as a complete current total.

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

Blood Magic Expansion 2.4.6 or newer reports a corpse only after its ritual
completes successfully. Deeds displays Corpses Drained as a total and by Meager,
Worthy, Potent, and Prime quality. Interrupted or failed rituals do not count.

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
TooltipPanelOpacity = 0.5
TooltipFadeSeconds = 0.15
PanelScale = 1
RightOffset = 48
TopOffset = 145
MaximumDeedRows = 9
MaximumWeaponRows = 7
MaximumMagicRows = 5
ShowCollapsedOtherRows = true
HidePointsAvailable = true
ShowBloodMagicStatistics = true

Troubleshooting:

If statistics track but no panel appears, install or update Grail Floating Text
to 1.11.1 or newer. Enable Diagnostics in the Deeds config and inspect
BepInEx/LogOutput.log for event binding, GFT API, or export messages.
