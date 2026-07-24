Main Menu Music
===============

Version 2.0.6

Standalone BepInEx 5 Mono plugin for Tainted Grail: The Fall of Avalon.

What it does
------------

Main Menu Music hooks the game's title-screen music startup, mutes the original
title music emitters, and plays replacement title music through FMOD.

Version 2.0.0 adds a default layered mode:

  audio\menu_layer_01.ksaudio
  audio\menu_layer_02.ksaudio
  audio\menu_layer_03.ksaudio

The base title music layer receives the same approved pitch/demonic treatment
from KS Main Menu Pitch Shift 1.2.1. Fire and wind remain unpitched ambience
layers with their own toggles and volume controls.

Custom file mode
----------------

main_menu_music.wav is included as a small silent placeholder for the custom
option.

Set:

  MusicMode = CustomFile

CustomFile mode plays the configured custom WAV at CustomMusicVolume. It still
uses the shared title-loop controls, including loop start, end trim, and
crossfade. It is not affected by pitch, demonic DSP, fire ambience, wind
ambience, or layered title music controls.

Default mode
------------

  MusicMode = LayeredModifiedTaintedGrail
  BaseMusicVolume = 1.0
  EnableFireAmbience = true
  FireAmbienceVolume = 1.0
  EnableWindAmbience = true
  WindAmbienceVolume = 1.0

Default effects for the base title music layer:

  ApplyEffectsToBaseMusic = true
  Semitones = -9
  FFTSize = 4096
  Overlap = 32
  EnableHighFrequencyRestore = true
  HighFrequencyGainDb = 1.5
  HighFrequencyCrossoverHz = 5000
  DemonicMode = true
  EnableDistortion = true
  DistortionLevel = 0.1
  EnableLowpass = true
  LowpassCutoffHz = 5500
  EnableEcho = true
  EchoDelayMs = 100
  EchoFeedbackPercent = 10
  EchoWetLevelDb = -36

Loading transition
------------------

The plugin now patches real game loading operations and fades replacement title
music out when gameplay/loading starts.

  FadeOutOnGameLoad = true
  GameLoadFadeSeconds = 2

Muted original title emitters are stopped after the replacement fades out so
title music does not bleed into loading or gameplay.

Audio format
------------

The three built-in layered title files are stereo 44.1 kHz 16-bit PCM files
with WAV data and matching duration for synchronized playback.

main_menu_music.wav is a small silent 44.1 kHz 16-bit PCM placeholder. Replace
it with your own WAV if you use CustomFile mode.

Configuration
-------------

The config is generated after first launch:

  BepInEx\config\ks.tgfoa.main-menu-music.cfg

Version 2.0.6 uses ConfigSchemaVersion 13. Older configs are backed up and a
fresh config is generated once so the updated defaults apply cleanly.

Install shape
-------------

Vortex mod folder payload:

  KingsElegyMainMenuMusic\MainMenuMusic.dll
  KingsElegyMainMenuMusic\audio\menu_layer_01.ksaudio
  KingsElegyMainMenuMusic\audio\menu_layer_02.ksaudio
  KingsElegyMainMenuMusic\audio\menu_layer_03.ksaudio
  KingsElegyMainMenuMusic\main_menu_music.wav

When installed as a BepInEx plugin mod in Vortex, this payload is placed under:

  BepInEx\plugins\KingsElegyMainMenuMusic\

Music credit
------------

The default layered title music is based on "Ymir" by Danheim & Gealdyr:

  https://danheim.bandcamp.com/track/ymir

Please support the original artists on Bandcamp.

Notes
-----

- The plugin does not replace FMOD banks or edit game files.
- Use the repository-level tools/Build-Mod.ps1 script to rebuild and export the
  package.
- Release zips contain only the runtime payload, README, and changelog.
