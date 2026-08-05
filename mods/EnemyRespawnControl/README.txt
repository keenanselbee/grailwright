Enemy Respawn Control

Version 2.1.2

Enemy Respawn Control is a standalone BepInEx plugin for Tainted Grail: The Fall of Avalon. It remembers regular world mob spawners after their spawned NPCs or creatures are killed and blocks respawn checks until the configured delay has elapsed.

The generated default is the Default preset, which waits 24 in-game/weather hours. This is still a large increase over the game's usual 2-hour fallback while keeping the world from feeling permanently empty.

ERC measures respawn delays with the game's in-game/weather clock, not real time. Timescale or day-night mods can make these waits take more or less real-world time because the delay follows the world clock.

Configuration is created at:

BepInEx/config/ks.tgfoa.enemy-respawn-control.cfg

Main settings:

Enabled = true
RespawnMode = Default24Hours
CustomRespawnHours = 168
ControlFactionNeutralNpcSpawners = true
AdditionalControlledSpawnerTerms =
IgnoredSpawnerTerms =
Diagnostics = false

RespawnMode values:

Vanilla = 2 in-game/weather hours
Fast6Hours = 6 in-game/weather hours
Default24Hours = 24 in-game/weather hours, generated default
Slow72Hours = 72 in-game/weather hours
VerySlow168Hours = 168 in-game/weather hours
Custom = CustomRespawnHours in in-game/weather hours
Disabled = block cleared controlled mob spawners indefinitely

Version 2.1.2 keeps the 2.0 classification model and tightens its ambiguous runtime path. Manual ignored terms are authoritative even when current hostility is true, while manual controlled terms can opt NPC-template spawners back in before built-in exclusions. Without a manual override, hostility=true is controlled immediately, faction-neutral spawners are ignored when every NPC template is prey or summon-derived, and mixed-template spawners remain eligible for normal control. Built-in terms cover enemy families seen in game templates and diagnostics, including Wyrdspirits, Dal Riata and Galahad guards, bandits, druids, undead, beasts, and constructs.

ControlFactionNeutralNpcSpawners catches regular mob spawners whose faction hostility is conditional, not restored yet, or otherwise unreadable at the moment ERC evaluates them. Set it to false for conservative behavior. AdditionalControlledSpawnerTerms and IgnoredSpawnerTerms accept semicolon-separated spawner/template text if a specific family needs to be forced in or out.

Those two manual spawner override lists are preserved by exact current setting
name across future config schema resets. Other gameplay, timing, and diagnostic
settings still regenerate from fresh defaults.

Resource, pickable, passive prey, summon-derived, and other passive non-enemy location spawners are ignored when they are not currently hostile. Known Friendly Stronghold variants are also ignored while faction-neutral. ERC leaves the outer rest-ambush gate alone, but ordinary locked mob spawners remain controlled when the game considers them as ambush candidates. Ambush-only spawners, active Wyrd-night spawner state, and manual/story-triggered spawns are ignored so flagged special events can still happen.

Recommended use:

Disable or remove older RespawnTimer-style mods while testing this plugin. It is designed to replace simple global respawn timer edits with a spawner lock that reacts to cleared enemies.

Diagnostics:

Turn Diagnostics on if enemies still respawn too quickly. The log will show spawner keys, lock creation, lifecycle and classification reasons, compact NPC type/prey/summon signals, blocked gate names, allowed spawn attempts, special-spawn bypasses, skipped spawners, cleanup of locked spawned locations, and lock expiry decisions.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the package. Release zips contain only the runtime payload, README, and changelog.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
