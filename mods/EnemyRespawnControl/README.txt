Enemy Respawn Control

Version 2.0.2

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

Version 2.0.2 keeps the 2.0 classification model and tightens its runtime path. Hostility=true is still controlled immediately, but a false hostility read no longer excludes regular NPC-template mob spawners that have killed-state. Built-in terms cover enemy families seen in game templates and diagnostics, including Wyrdspirits, Dal Riata and Galahad guards, bandits, druids, undead, beasts, and constructs.

ControlFactionNeutralNpcSpawners catches regular mob spawners whose faction hostility is conditional, not restored yet, or otherwise unreadable at the moment ERC evaluates them. Set it to false for conservative behavior. AdditionalControlledSpawnerTerms and IgnoredSpawnerTerms accept semicolon-separated spawner/template text if a specific family needs to be forced in or out.

Resource, pickable, and passive non-enemy location spawners are ignored. ERC leaves the outer rest-ambush gate alone, but ordinary locked mob spawners remain controlled when the game considers them as ambush candidates. Ambush-only spawners, active Wyrd-night spawner state, and manual/story-triggered spawns are ignored so flagged special events can still happen.

Recommended use:

Disable or remove older RespawnTimer-style mods while testing this plugin. It is designed to replace simple global respawn timer edits with a spawner lock that reacts to cleared enemies.

Diagnostics:

Turn Diagnostics on if enemies still respawn too quickly. The log will show spawner keys, lock creation, blocked gate names, allowed spawn attempts, special-spawn bypasses, skipped spawners with classification reasons and template names, cleanup of locked spawned locations, and lock expiry decisions.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the package. Release zips contain only the runtime payload, README, and changelog.
