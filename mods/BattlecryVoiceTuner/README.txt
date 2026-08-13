Battlecry Voice Tuner 1.1.3
===========================

Platforms: Windows and Linux through Proton.

Battlecry Voice Tuner tunes the player's supported nonverbal voice sounds and
adds a gender-aware custom battlecry that challenges nearby enemies.

Battlecry
---------

Hold the game's Take All Items action for 0.45 seconds to battlecry. Because
this uses the game's action, remapped keyboard and controller bindings remain
supported. An open container keeps the normal Take All Items behavior. An
optional separate BattlecryHotkey is also available.

Successful battlecries select one of up to 15 WAV files for the player's gender.
They use the overall pitch, random pitch, and volume tuning plus independent
gender pitch offsets and a battlecry-only volume multiplier. Overall pitch and
both gender offsets default to a neutral 0, before random variation. Custom cries play through the
game's SFX mixer category and follow its SFX volume and mute state. Reusable
the game classifies full interior scenes and already tracks roof volumes for
caves and covered structures. Interiors and roofed spaces sample horizontal,
upward, downward, floor, and ceiling geometry to distinguish small rooms,
corridors, halls, caves, and open-sided shelter. Small rooms receive dense
early reverb without a repeated full cry, while large spaces can return up to
three distance-timed reflections. Outdoors, each cry samples nearby terrain,
vegetation, walls, and overhead surfaces once, then derives a diffuse tail and
up to three distance-timed, directional reflections. Open terrain stays nearly
dry while forests, structures, cliffs, and partially enclosed spaces respond
according to their measured geometry. During the default three-second challenge,
hostile NPCs within three times their individual hearing range outdoors or four
times their hearing range in interiors and roofed spaces react once: unaware
enemies become strongly
alert, while enemies that were already alert attempt to enter combat with the
Hero. Walls and the game's normal combat restrictions remain authoritative.

Custom audio
------------

Place PCM WAV files beside the installed DLL under:

audio/battlecry/male
audio/battlecry/female

Use up to 15 files per folder. The recommended names are battlecry_01.wav
through battlecry_15.wav. The package includes 15 default male WAVs and 12
default female WAVs. Female .wav.placeholder files reserve slots 13 through 15
but are not played; replace their suffix with a real WAV file to fill those
slots. At least one real WAV is required for a gender. Immediate repeats are
avoided when a pool contains more than one file.

Config
------

Config file:
BepInEx/config/ks.tgfoa.battlecry-voice-tuner.cfg

Defaults:
Enabled = true
PitchSemitones = 0.0
RandomPitchSemitones = 0.15
VolumeMultiplier = 1.0
IncludeAttackGrunts = true
IncludeHurtGrunts = true
IncludeDeathGrunts = true
IncludeStatusPainGrunts = true
IncludePlayerHitFeedback = true
IncludeStaminaDepletedBreathing = false
BattlecryEnabled = true
BattlecryVolumeMultiplier = 0.5
BattlecryReverbEnabled = true
OutdoorBattlecryReverbAmount = 0.15
IndoorBattlecryReverbAmount = 0.70
MaleBattlecryPitchOffsetSemitones = 0.0
FemaleBattlecryPitchOffsetSemitones = 0.0
HoldTakeAllItemsForBattlecry = true
BattlecryHoldSeconds = 0.45
BattlecryHotkey = None
BattlecryCooldownSeconds = 1.5
BattlecryAggroRangeMultiplier = 3.0
IndoorBattlecryAggroRangeMultiplier = 4.0
BattlecryAggroDurationSeconds = 3.0
EyesInTheDarkThreat = 10.0
PlayRandomTestSound = false
Diagnostics = false

FoA Mod Manager presents these stable settings as General, Voice Tuning, Native
Voice Events, Battlecry Audio, Battlecry Input, Battlecry Challenge, Optional
Integrations, Testing, and Diagnostics. This display-only organization does not
change the stored config sections or keys.

PlayRandomTestSound is a pseudo-button for existing game voice events. It does
not select custom battlecry files.

Compatibility
-------------

Eyes in the Dark is optional. During an eligible exposed Wyrdnight, each
successful battlecry requests the configured Wyrd Threat and contributes to
Eyes' throttled pool of atmospheric Wyrdnight reactions. Repeated cries receive
diminishing threat returns inside Eyes. Eyes remains authoritative about when
threat and notifications are allowed.

Grail Floating Text is optional. It provides Eyes in the Dark's Wyrdnight
responses and can show a visible load error if this mod fails during startup.

The former Player Voice Tuner identity used a different plugin GUID and config
path. Version 1.0.0 generates a fresh Battlecry Voice Tuner config.

Troubleshooting
---------------

If holding Take All Items outside a container does not battlecry, confirm that a
real WAV exists for the current gender and that the battlecry cooldown has elapsed.
Enable Diagnostics and inspect BepInEx/LogOutput.log for file, FMOD, input, AI,
and Eyes in the Dark integration details.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab. Its one-shot
action imports compatible customized values from Battlecry Voice Tuner schema
backups and then turns itself off. Restart the game after importing.
