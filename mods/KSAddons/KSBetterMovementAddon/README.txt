KS Better Movement Addon 0.2.2

Adds terrain-aware sliding sounds and optional combat speed normalization to Better Movement 1.3.0. Slide audio begins with the game's slide, follows long downhill slides, changes when the player crosses onto another surface, and fades when sliding ends or is cancelled.

Requirements

Tainted Grail: The Fall of Avalon Mono branch
BepInEx 5 Mono
Better Movement 1.3.0 or newer

Configuration

BepInEx/config/ks.tgfoa.better-movement-addon.cfg

Version 0.1.4 advances ConfigSchemaVersion to 2 for the lower 0.40 volume
default. The old config is backed up, untouched old defaults regenerate at
0.40, and compatible customized settings are restored conservatively.
FoA Mod Manager keeps a final Import Previous Settings tab for supported
backups.

Version 0.2.1 advanced ConfigSchemaVersion to 4 because the old binary combat
speed setting was replaced by the progressive Combat Override Mode. The old
config is backed up, the new mode defaults to Off, and compatible customized
audio settings are restored conservatively.

FoA Mod Manager section order:

General
Combat
Audio
Terrain Detection
Diagnostics
Import Previous Settings

Combat Override Mode

Off is the default and preserves Better Movement completely.

HalfSpeedBonuses retains half of each positive Better Movement walk, jog, sprint, and swim bonus during combat. For example, 1.20x becomes 1.10x. Entering, leaving, or switching to this tier takes one full second.

SpeedBonuses smoothly removes Better Movement's positive walk, jog, sprint, and swim multipliers over one second when combat begins, then restores them over one second when combat ends. Native movement speed, speed penalties, and modifiers from other mods remain intact.

MobilityAdvantages includes SpeedBonuses and restores vanilla sprint directions, walking direction penalties, dash permissions, encumbrance restrictions, and dash limits during combat. An air dash that began before combat is allowed to finish.

FullVanilla includes both earlier tiers and suppresses every Better Movement patch during combat. Sprint stamina and sprint FOV are restored separately because Better Movement applies those changes even when its own Enabled setting is off. The mode waits for an active dash, jump, slide, or bow draw to finish before switching, and it never writes to Better Movement's config.

Default audio behavior

The addon is enabled with 40 percent overall volume, speed-responsive volume and pitch, 0.10-second surface crossfades, and terrain checks every 0.15 seconds. Audio is positional and normally routes through the game's SFX mixer.

Included audio

Version 0.1.4 contains an edited Foley set for grass, gravel, ground, mud, puddles, snow, stone, sand, wood, cloth, and metal. Each surface has two loop, start, and stop variants. Puddle sounds blend 70 percent ground friction with 30 percent water. The sounds were prepared from the licensed Footsteps: Full Bundle by Sound Armoury / Matt J Hart.

Replace files under:

BetterMovementAddon/audio/slide/<surface>/

Use mono PCM WAV files when possible. Filenames must contain exactly one of these markers:

_loop_   Seamless sustained sliding audio
_start_  Optional slide-start transient
_stop_   Optional slide-stop transient

Multiple files of each kind are supported and selected randomly. At least one _loop_ WAV is required in a surface folder. Missing surfaces fall back to the ground set.

Compatibility

The addon does not patch Immersive Footsteps' FMOD footstep method or reuse its embedded audio. Both mods can run together: Immersive Footsteps handles walking and running steps while this addon supplies the continuous surface layer during Better Movement slides.

Troubleshooting

Enable Diagnostics in the addon config and check BepInEx/LogOutput.log for detected surfaces, fallbacks, and FMOD errors. If blended terrain sampling is unavailable, explicitly tagged meshes still work and blended terrain temporarily uses the ground sound.
