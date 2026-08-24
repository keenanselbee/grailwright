Soul Salvage Audio
==================

Successful light-cast corpse harvests and summon sacrifices choose one WAV from
the matching corpse-quality bank:

  Meager -> soul_salvage_low_0.wav through soul_salvage_low_9.wav
  Worthy -> soul_salvage_medium_0.wav through soul_salvage_medium_9.wav
  Potent -> soul_salvage_high_0.wav through soul_salvage_high_9.wav
  Prime  -> soul_salvage_max_0.wav through soul_salvage_max_9.wav

Files may be replaced with custom WAV audio while retaining these names. Missing
slots are skipped. If a full tier is missing, Soul and Service falls back through
nearby quality tiers. Sounds are loaded lazily and cached by FMOD after first use.

The packaged sounds intentionally retain their authored loudness differences.
Every successful ritual starts an independent FMOD channel and may overlap a
previous ritual. Playback volume, recent-repeat protection, repeat memory, random
pitch range, and echoes are configurable. Runtime female and male targets default
to +3 and -3 semitones. Recognized female and male monsters add -1 and -3 more,
for final defaults of +2 and -6. Gender-unknown monsters use the configurable
-6-semitone fallback; other unknown targets retain authored pitch. All pitch
values are configurable in the Audio section of ks.tgfoa.soul-and-service.cfg.
