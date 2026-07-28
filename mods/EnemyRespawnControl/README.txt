Enemy Respawn Control

Version 1.0.9

Enemy Respawn Control is a standalone BepInEx plugin for Tainted Grail: The Fall of Avalon. It remembers regular hostile enemy spawners after their spawned enemies are killed and blocks respawn checks until the configured delay has elapsed.

The generated default is the Default preset, which waits 24 in-game/weather hours. This is still a large increase over the game's usual 2-hour fallback while keeping the world from feeling permanently empty.

ERC measures respawn delays with the game's in-game/weather clock, not real time. Timescale or day-night mods can make these waits take more or less real-world time because the delay follows the world clock.

Configuration is created at:

BepInEx/config/ks.tgfoa.enemy-respawn-control.cfg

Main settings:

Enabled = true
RespawnMode = Default24Hours
CustomRespawnHours = 168
Diagnostics = false

RespawnMode values:

Vanilla = 2 in-game/weather hours
Fast6Hours = 6 in-game/weather hours
Default24Hours = 24 in-game/weather hours, generated default
Slow72Hours = 72 in-game/weather hours
VerySlow168Hours = 168 in-game/weather hours
Custom = CustomRespawnHours in in-game/weather hours
Disabled = block cleared hostile enemy spawners indefinitely

Version 1.0.9 keeps respawn blocking focused on hostile enemy and hostile NPC spawners. Resource, pickable, and passive non-enemy location spawners are ignored. ERC leaves the outer rest-ambush gate alone, but ordinary locked enemy spawners remain controlled when the game considers them as ambush candidates. Ambush-only spawners, Wyrd-night spawner state, and manual/story-triggered spawns are ignored so flagged special events can still happen.

Recommended use:

Disable or remove older RespawnTimer-style mods while testing this plugin. It is designed to replace simple global respawn timer edits with a spawner lock that reacts to cleared enemies.

Diagnostics:

Turn Diagnostics on if enemies still respawn too quickly. The log will show spawner keys, lock creation, blocked gate names, allowed spawn attempts, special-spawn bypasses, skipped non-enemy spawners, cleanup of locked spawned locations, and lock expiry decisions.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the package. Release zips contain only the runtime payload, README, and changelog.
