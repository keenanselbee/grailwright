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
OutdoorBattlecryReverbAmount defaults to a light 0.15. Interior scenes, caves,
and the game's roof volumes use the heavier IndoorBattlecryReverbAmount default
of 0.70. BattlecryReverbEnabled disables both effects without changing routing.
