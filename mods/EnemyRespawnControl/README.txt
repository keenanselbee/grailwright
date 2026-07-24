Enemy Respawn Control

Version 1.0.2

Enemy Respawn Control is a standalone BepInEx plugin for Tainted Grail: The Fall of Avalon. It remembers spawners after their spawned enemies are killed and blocks respawn checks until the configured delay has elapsed.

The default Slow72Hours preset means 3 in-game/weather days when UseWeatherTime is enabled. This is intended to behave sensibly with TimeMod and other day-night speed changes while keeping the world from feeling permanently empty.

Configuration is created at:

BepInEx/config/ks.tgfoa.enemy-respawn-control.cfg

Main settings:

Enabled = true
RespawnMode = Slow72Hours
CustomRespawnHours = 168
UseWeatherTime = true
Diagnostics = false

RespawnMode values:

VeryFast2Hours = 2 hours
Fast6Hours = 6 hours
Default24Hours = 24 hours
Slow72Hours = 72 hours
VerySlow168Hours = 168 hours
Custom = CustomRespawnHours
Disabled = block cleared spawners indefinitely

Version 1.0.2 adds broader spawn gates and diagnostics for enemies that do not use the vanilla cooldown path. Locked spawners are now checked at spawn eligibility, valid-state, should-spawn, and final spawn-internal paths.

Recommended use:

Disable or remove older RespawnTimer-style mods while testing this plugin. It is designed to replace simple global respawn timer edits with a spawner lock that reacts to cleared enemies.

Diagnostics:

Turn Diagnostics on if enemies still respawn too quickly. The log will show spawner keys, lock creation, blocked gate names, allowed spawn attempts, cleanup of locked spawned locations, and lock expiry decisions.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the package. Release zips contain only the runtime payload, README, and changelog.
