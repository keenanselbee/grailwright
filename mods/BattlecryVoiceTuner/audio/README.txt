Battlecry audio
================

Place up to 15 PCM WAV files in each gender folder. Files are loaded in
alphabetical order, and only the first 15 WAV files in each folder are used.

Recommended names:

male/battlecry_01.wav through male/battlecry_15.wav
female/battlecry_01.wav through female/battlecry_15.wav

The package includes 15 default male battlecries and 12 default female
battlecries. Female .wav.placeholder files reserve slots 13 through 15 but are
not loaded. Replace a placeholder by removing the .placeholder suffix and
supplying a real WAV file. Keep each battlecry short and free of music or other
voices.

MaleBattlecryPitchOffsetSemitones and FemaleBattlecryPitchOffsetSemitones are
added to the overall PitchSemitones setting for their respective battlecries.
Overall pitch and both gender offsets default to 0, so packaged male and female
battlecries retain their recorded pitch before random variation. Supported
native voice events use only the overall pitch and random variation.

Battlecries play through the game's SFX mixer category, so the in-game Master
and SFX volume controls apply. Their final per-sound scale is VolumeMultiplier
times BattlecryVolumeMultiplier. The defaults are 1.0 overall and 0.5 for
battlecries, producing a final per-sound scale of 0.5 before the game mixer.

Dynamic reverb uses two reusable battlecry-only paths beneath the SFX bus.
Outdoors, each cry performs one 24-direction geometry sample and uses the
result to shape its diffuse tail plus up to three distance-timed 3D reflections.
OutdoorBattlecryReverbAmount defaults to a light 0.15 and scales the complete
measured response. Interior scenes, caves, and the game's roof volumes use a
30-direction room probe with dedicated floor and ceiling measurements. The
IndoorBattlecryReverbAmount default of 0.70 scales geometry-driven room decay,
density, filtering, and only the long reflections suitable for larger spaces.
BattlecryReverbEnabled disables both effects without changing routing.

Command audio
=============

Keep command WAV files directly under audio/command. Soul and Service Attack,
Hold, Follow, Guard, Bulwark, and Hunt orders use separate gender-matched pools and recent histories.
The files use these exact filename patterns:

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

Battlecry Voice Tuner sorts each pool by filename and accepts up to 15 files per
pool. Command files use the overall pitch, random pitch, and volume settings,
then add their command-only gender pitch and 0.50 volume values. Male and female
command pitch offsets default to +5 and +1 semitones. RecentCommandVoiceMemory
defaults to 2 and avoids those successfully played clips within the same command
and gender pool when alternatives remain.

Command voices follow the game's SFX mixer. Their separate outdoor and indoor
reverb paths reuse the geometry-aware acoustic probes with lighter 0.10 and 0.45
defaults and schedule no more than one measured reflection. A command never
triggers the battlecry challenge or optional Wyrd Threat integration.
