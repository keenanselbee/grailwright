Full Enemy XP - No Overlevel Penalty
Version 1.0.6

Full Enemy XP is a standalone BepInEx plugin for Tainted Grail: The Fall of
Avalon. It removes the vanilla kill XP falloff that reduces enemy XP when the
player is above the enemy XP level.

Default behavior
----------------

By default, lower-level enemies keep their full normal enemy kill XP.

The mod preserves the rest of the vanilla reward path:

- enemy base XP and XP tier
- New Game Plus enemy XP bonuses
- hero kill XP multiplier
- global hero XP multiplier
- XP-blocked enemies
- summons and non-player kills

Config path
-----------

BepInEx/config/ks.tgfoa.full-enemy-xp.cfg

Important settings
------------------

Enabled = true
Master switch.

ConfigSchemaVersion = 1
Configuration layout version. Do not edit this manually. If the schema changes
in a future release, Full Enemy XP backs up the old config beside the active
.cfg file, clears stale settings, reloads the config, and saves regenerated
defaults.

MinimumOverlevelXPMultiplier = 1
Minimum enemy-level XP multiplier when the player is above the enemy XP level.
The default of 1 gives full enemy XP. Lower values allow partial vanilla
falloff.

DryRun = false
Logs the adjustment that would happen without changing the vanilla XP reward.
Use this while testing if you want before/after diagnostics without altering XP.

Diagnostics = false
Enables diagnostic logging.

LogAdjustedKills = true
Logs each kill whose overlevel XP multiplier is raised, or would be raised in
DryRun.

LogUnchangedEligibleKills = false
Logs eligible kill XP awards that did not need adjustment.

LogSkippedDeathChecks = false
Logs skipped adjustment checks. This is usually only needed while diagnosing a
patch or reflection issue.

SummaryEveryAdjustedKills = 10
Logs a summary after this many adjusted or dry-run adjusted kills. Set to 0 to
disable periodic summaries.

Diagnostics testing
-------------------

For first-run testing, set:

Diagnostics = true
DryRun = true

Kill an enemy below your level. The log should show the enemy name, hero level,
enemy XP level, base XP, vanilla level multiplier, applied level multiplier, and
estimated reward before the game's global XP multiplier.

Then set:

DryRun = false

Kill another lower-level enemy. The log should show the actual adjusted
multiplier.

Compatibility
-------------

Full Enemy XP patches the enemy death XP calculation. It may overlap with mods
that replace or heavily transpile enemy death XP rewards. It should be
compatible with mods that only add separate bonus XP after enemy death.

Build notes
-----------

Use the repository-level tools/Build-Mod.ps1 script to compile and export the
package. Release zips contain only the runtime payload, README, and changelog.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
