Player Voice Tuner 0.2.2
========================

Player Voice Tuner lowers or raises the player's nonverbal voice sounds in Tainted Grail: The Fall of Avalon. It targets the FMOD events used for player attack grunts, hurt grunts, death grunts, status pain grunts, and SFX_Player_Hit hit feedback.

Config
------

Config file:
BepInEx/config/ks.tgfoa.player-voice-tuner.cfg

Defaults:
Enabled = true
PitchSemitones = -3.0
RandomPitchSemitones = 0.25
VolumeMultiplier = 1.0
IncludeAttackGrunts = true
IncludeHurtGrunts = true
IncludeDeathGrunts = true
IncludeStatusPainGrunts = true
IncludePlayerHitFeedback = true
IncludeStaminaDepletedBreathing = false
PlayRandomTestSound = false
Diagnostics = false

PitchSemitones, RandomPitchSemitones, and VolumeMultiplier survive config
schema resets. Sound-category selection, testing controls, and diagnostics
receive fresh defaults.

PlayRandomTestSound is a pseudo-button for config panels. Toggle it on to play one random supported one-shot sample; the mod resets it to false after the attempt. The test button skips stamina-depleted breathing because those sounds are longer/looping.

Notes
-----

SFX_Player_Hit is included by default. The game uses it as player hit feedback when the player lands a hit.

Stamina-depleted breathing is supported but off by default because it is a longer/looping sound.

This mod only adjusts supported player voice FMOD events. It does not replace audio files, change NPC voices, edit weapon impacts, or alter footsteps.

Compatibility
-------------

Grail Floating Text is optional. If installed, it shows successful config
schema resets with the system icon and five-second System duration, and can show a
visible in-game load error if this mod fails during startup. Details still go
to BepInEx/LogOutput.log.

Troubleshooting
---------------

If a config edit behaves strangely after an update, delete the config file and let the mod regenerate it. The mod also backs up stale schema versions beside the active config before resetting defaults.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
