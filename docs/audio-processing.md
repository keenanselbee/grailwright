# Audio Processing

Reusable audio tooling lives under `tools/audio`.

Current runtime WAV conventions for the audio-heavy mods:

- 44.1 kHz
- 16-bit PCM
- Mono unless a mod specifically needs stereo
- Conservative leading-silence trimming
- Peak headroom rather than clipping-heavy normalization

Publicly redistributed audio should have clear license and credit notes before
release.
