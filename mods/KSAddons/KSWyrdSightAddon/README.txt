Wyrd Sight Addon
Version 1.2.6

Platforms: Windows and Linux through Proton.

Companion addon for Wyrd Sight 1.2.0.

This addon changes Wyrd Sight's own Highlight Key from a toggle into a pulse:
press the configured Wyrd Sight Highlight Key once, Wyrd Sight turns on briefly,
then the addon turns off only the pulse it started. Wyrd Sight's normal fade
handles the fade-out.

While Wyrd Sight is actively on, the addon also gives NPCs who have an untaken
quest the same golden outline style shown by Avalon Untold. Quest-giver discovery
is inspired by Avalon Untold. The needed functionality is integrated directly
into WyrdSightAddon.dll, runs read-only against the game's dialogue graphs and
your current save state, and does not require another mod.

QuestGiverMode defaults to Balanced:
- Thorough shows every untaken quest grant the scan found, including uncertain
  and story-locked grants.
- Balanced keeps uncertain grants visible but hides grants blocked by durable
  story progress such as flags, quest states, objectives, or journal entries.
  Temporary requirements such as time of day or carried items remain visible.
- Precise shows only grants confirmed available right now. It is the cleanest
  mode but may hide grants the detector cannot classify.

If an expected quest giver stays dark, switch QuestGiverMode to Thorough. The
detector must rescan after loading a save, so quest-giver outlines can take a
few seconds to become available. Archive parsing runs off the game thread, and
the remaining scan is spread over small frame budgets.
NPC reevaluation is event-driven, and outline rendering is fully suspended while
Wyrd Sight is off. Availability refreshes at the start of each pulse and every
15 seconds only when Wyrd Sight remains continuously active.

The addon does not edit or enforce Wyrd Sight's visual settings. For the intended
clean pulse look, Wyrd Sight works best with:

Enable Wyrd Sight Particles = false
Enable Glow Shape = true

Safety behavior:
- If Wyrd Sight was already on from outside the addon, the Highlight Key will not
  turn it off. The addon only turns off pulses it started.
- Pressing the Highlight Key during an addon-owned pulse extends the pulse timer.
- The addon suppresses Wyrd Sight's original Highlight Key toggle while enabled.
- If Wyrd Sight changes its private input/toggle methods in a future update, the
  addon logs a warning and lets Wyrd Sight's original input handling continue.

Config file: BepInEx/config/ks.tgfoa.wyrd-sight-addon.cfg

Defaults:

ConfigSchemaVersion = 2
Enabled = true
PulseDurationSeconds = 3
PulseStateCheckIntervalSeconds = 0.25
OffRetryDelaySeconds = 0.25
MaximumOffAttempts = 3
HighlightQuestGivers = true
QuestGiverMode = Balanced
QuestGiverMaxDistance = 20
QuestScanFrameBudgetMilliseconds = 5
QuestOutlineBakeFrameBudgetMilliseconds = 1.5
QuestOutlineRefreshRate = 30
QuestAvailabilityRefreshSeconds = 15
Diagnostics = false

Pulse timing lives in the addon's own config, not Wyrd Sight's config. Lower
PulseDurationSeconds for a shorter flash, or raise it for a longer scan. The
state-check and off-retry timing defaults are conservative; change them only if
the parent mod needs slower or faster pulse ownership handling.

Lower QuestGiverMaxDistance or QuestOutlineRefreshRate for less outline work.
The frame-budget settings are targets checked between work units, so one
unusually expensive operation can run longer. Lower values are usually smoother
but finish preparation more slowly.
Setting Enabled or HighlightQuestGivers to false immediately removes listeners,
cached outlines, and render resources. If archive parsing has already started,
that read may finish in the background and is retained so re-enabling cannot
start a duplicate parse.

Version 1.2.6 appears as Wyrd Sight Addon in BepInEx and Configuration
Manager while keeping the existing ks.tgfoa.wyrd-sight-addon.cfg config path.
It still uses ConfigSchemaVersion 2. Older configs are backed up and a fresh
default config is regenerated when the schema changes.

Requires BepInEx 5 Mono and Wyrd Sight 1.2.0. Quest-giver highlighting is
standalone inside WyrdSightAddon.dll; AvalonUntold.dll is not required or shipped.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
