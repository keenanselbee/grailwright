Battlecry Voice Tuner 1.2.4
===========================

Platforms: Windows and Linux through Proton.

Battlecry Voice Tuner tunes the player's supported nonverbal voice sounds,
adds a gender-aware custom battlecry that challenges nearby enemies, and can
voice explicit commands issued through supported mods.

Attribute voice progression
---------------------------

The Hero's voice now deepens as the attributes that define the selected
archetype grow. Warrior is the enabled-by-default preset and uses 75% permanent
Strength plus 25% permanent Endurance. Rogue uses Dexterity and Perception,
Mage uses Spirituality and Perception, Warden uses Endurance and Spirituality,
Artisan uses Practicality and Dexterity, and Adventurer averages all six
attributes. Custom lets the player choose both attributes and their weighting;
Disabled or VoiceGrowthEnabled = false retains manual tuning only.

Progress starts at the game's innate attribute values, remains subtle early,
and follows a smooth late-weighted curve toward full depth at attribute value
40. The default maximum is -6 semitones. Permanent BaseValue attributes are
used by default, so equipment, consumables, and temporary effects do not make
the voice fluctuate. Respeccing permanent attributes immediately reshapes the
next supported sound.

Overall Pitch remains a manual baseline. Attribute growth, gender or pool
offsets, and random variation are added to it. Balanced pitch processing then
splits the final shift evenly between natural playback-rate pitch and FMOD's
tempo-preserving pitch DSP. A final -6 semitone voice therefore plays about 19%
longer instead of a full natural rate shift's 41%. Natural and TempoPreserving
processing modes remain available, and DSP failure safely falls back to the
full natural shift. Supported native events wait briefly for their hybrid DSP
path before playback begins, preventing a full-rate onset before the split.

Battlecry
---------

Hold the game's Take All Items action for 0.45 seconds to battlecry. Because
this uses the game's action, remapped keyboard and controller bindings remain
supported. An open container keeps the normal Take All Items behavior. An
optional separate BattlecryHotkey is also available.

Successful battlecries select one of up to 15 WAV files for the player's gender.
They use the overall pitch, random pitch, and volume tuning plus independent
gender pitch offsets and a battlecry-only volume multiplier. Overall pitch and
both gender offsets default to a neutral 0 before attribute growth and random
variation. Custom cries
play through the game's SFX mixer category and follow its SFX volume and mute
state. The game classifies full interior scenes and already tracks roof volumes for
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

Command voices
--------------

Soul and Service can request one spoken command after an explicit Attack, Hold,
Follow, Guard, Bulwark, or Hunt order succeeds. Each command type and gender has its own matching pool
and recent-history memory. Automatic targeting, retaliation, and failed commands
remain silent. The package includes five recordings in each male Attack, Hold,
and Follow pool and four in each matching female pool. Male commands default to
+5 semitones, female commands to +1,
and both retain the overall voice pitch and random variation. Guard, Bulwark,
and Hunt include two recordings per gender. Commands do not
challenge enemies or request Wyrd Threat.

Command voices use lighter geometry-aware acoustics than battlecries: 0.10
outdoors, 0.45 indoors, and no more than one measured reflection. Separate
reusable FMOD paths prevent a command from changing an active battlecry's reverb.
While Soul and Service owns Take All Items for a summon formation command,
Battlecry Voice Tuner yields that hold action. Its separate battlecry hotkey
remains available.

Custom audio
------------

Place PCM WAV files beside the installed DLL under:

audio/battlecry/male
audio/battlecry/female

Use up to 15 files per folder. The recommended names are battlecry_01.wav
through battlecry_15.wav. The package includes 15 default male WAVs and 12
default female WAVs. Female .wav.placeholder files reserve slots 13 through 15
but are not played; replace their suffix with a real WAV file to fill those
slots. At least one real WAV is required for a gender. A configurable recent
history avoids the previous two successful clips by default.

Summon command audio stays directly under:

audio/command

Use these flat filename pools:

  summon_male_attack_0.wav through summon_male_attack_4.wav
  summon_male_hold_0.wav through summon_male_hold_4.wav
  summon_male_follow_0.wav through summon_male_follow_4.wav
  summon_male_guard_0.wav through summon_male_guard_1.wav
  summon_male_bulwark_0.wav through summon_male_bulwark_1.wav
  summon_male_hunt_0.wav through summon_male_hunt_1.wav
  summon_female_attack_0.wav through summon_female_attack_3.wav
  summon_female_hold_0.wav through summon_female_hold_3.wav
  summon_female_follow_0.wav through summon_female_follow_3.wav
  summon_female_guard_0.wav through summon_female_guard_1.wav
  summon_female_bulwark_0.wav through summon_female_bulwark_1.wav
  summon_female_hunt_0.wav through summon_female_hunt_1.wav

The packaged files preserve their authored loudness. Each matching command and
gender pool avoids its last two successfully played clips by default.

Config
------

Config file:
BepInEx/config/ks.tgfoa.battlecry-voice-tuner.cfg

Defaults:
Enabled = true
PitchSemitones = 0.0
RandomPitchSemitones = 0.15
VolumeMultiplier = 1.0
PitchProcessingMode = Balanced
VoiceGrowthEnabled = true
VoiceGrowthPreset = Warrior
VoiceGrowthMaximumSemitones = -6.0
UseTemporaryAttributeModifiers = false
CustomPrimaryAttribute = Strength
CustomSecondaryAttribute = Endurance
CustomPrimaryAttributeWeight = 0.75
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
RecentBattlecryMemory = 2
CommandVoiceEnabled = true
CommandVoiceVolumeMultiplier = 0.50
CommandVoiceReverbEnabled = true
OutdoorCommandVoiceReverbAmount = 0.10
IndoorCommandVoiceReverbAmount = 0.45
MaleCommandVoicePitchOffsetSemitones = 5.0
FemaleCommandVoicePitchOffsetSemitones = 1.0
RecentCommandVoiceMemory = 2
CommandVoiceCooldownSeconds = 0.75
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

FoA Mod Manager and the generated config use these task-oriented sections:
General, Voice Tuning, Native Voice Events, Battlecry Audio, Command Voice,
Battlecry Input, Battlecry Challenge, Optional Integrations, Testing,
Diagnostics, and the final Import Previous Settings section.

PlayRandomTestSound is a pseudo-button for existing game voice events. It does
not select custom battlecry files.

Compatibility
-------------

Eyes in the Dark is optional. During an eligible exposed Wyrdnight, each
successful battlecry requests the configured Wyrd Threat and contributes to
Eyes' throttled pool of atmospheric Wyrdnight reactions. Repeated cries receive
diminishing threat returns inside Eyes. Eyes remains authoritative about when
threat and notifications are allowed.

Soul and Service is optional. Its successful Attack, Hold, Follow, Guard,
Bulwark, and Hunt orders can
request a gender-matched command voice through the public API. While summons are
active, Soul and Service owns the Take All Items hold for formation commands;
the separate battlecry hotkey remains available. Either mod continues
working normally when the other is absent or its relevant feature is disabled.

Grail Floating Text is optional. It provides Eyes in the Dark's Wyrdnight
responses and can show a visible load error if this mod fails during startup.

The former Player Voice Tuner identity used a different plugin GUID and config
path. Version 1.0.0 generates a fresh Battlecry Voice Tuner config.

Troubleshooting
---------------

If holding Take All Items outside a container does not battlecry, confirm that a
real WAV exists for the current gender and that the battlecry cooldown has elapsed.
If a Soul and Service order is silent, confirm that its matching command-type and
gender pool exists and that the command cooldown has elapsed.
Enable Diagnostics and inspect BepInEx/LogOutput.log for file, FMOD, input, AI,
and Eyes in the Dark integration details.

Previous settings
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab. Its one-shot
action imports compatible customized values from Battlecry Voice Tuner schema
backups and then turns itself off. Restart the game after importing.
