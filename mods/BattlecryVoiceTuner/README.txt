Battlecry Voice Tuner 1.0.0
===========================

Battlecry Voice Tuner tunes the player's supported nonverbal voice sounds and
adds a gender-aware custom battlecry that challenges nearby enemies.

Battlecry
---------

Tap the game's Toggle Weapon action to preserve its normal show/sheath
behavior. Hold it for 0.45 seconds to battlecry. Because this uses the game's
action, remapped keyboard and controller bindings remain supported. An optional
separate BattlecryHotkey is also available.

Successful battlecries select one of up to 10 WAV files for the player's gender
and apply the same pitch, random-pitch, and volume tuning used by supported game
voice events. During the default three-second challenge, hostile NPCs within
twice their individual hearing range react once: unaware enemies become strongly
alert, while enemies that were already alert attempt to enter combat with the
Hero. Walls and the game's normal combat restrictions remain authoritative.

Custom audio
------------

Place PCM WAV files beside the installed DLL under:

audio/battlecry/male
audio/battlecry/female

Use up to 10 files per folder. The recommended names are battlecry_01.wav
through battlecry_10.wav. Included .wav.placeholder files reserve every slot
but are not played; replace their suffix with a real WAV file when supplying
audio. At least one real WAV is required for that gender. Immediate repeats are
avoided when a pool contains more than one file.

Config
------

Config file:
BepInEx/config/ks.tgfoa.battlecry-voice-tuner.cfg

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
BattlecryEnabled = true
HoldToggleWeaponForBattlecry = true
BattlecryHoldSeconds = 0.45
BattlecryHotkey = None
BattlecryCooldownSeconds = 3.0
BattlecryAggroRangeMultiplier = 2.0
BattlecryAggroDurationSeconds = 3.0
EyesInTheDarkThreat = 20.0
PlayRandomTestSound = false
Diagnostics = false

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

If holding Toggle Weapon only performs the normal toggle, confirm that a real
WAV exists for the current gender and that the battlecry cooldown has elapsed.
Enable Diagnostics and inspect BepInEx/LogOutput.log for file, FMOD, input, AI,
and Eyes in the Dark integration details.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab. Its one-shot
action imports compatible customized values from Battlecry Voice Tuner schema
backups and then turns itself off. Restart the game after importing.
