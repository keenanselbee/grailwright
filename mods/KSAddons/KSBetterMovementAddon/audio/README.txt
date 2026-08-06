Custom Slide Audio

Each surface folder accepts mono or stereo PCM WAV files. Mono 48 kHz files are preferred for positional playback.

Filenames must contain one marker:

_loop_   Seamless sustained slide sound. At least one is required.
_start_  Optional transient layered over the loop when this surface begins.
_stop_   Optional transient played when a slide ends on this surface.

Multiple files with the same marker are selected randomly. Files are read only from the top level of each surface folder.

Surface folders:

grass
gravel
ground
mud
puddle
snow
stone
sand
wood
cloth
metal

The included set provides two loop, start, and stop variants for every surface. Runtime files are mono 48 kHz 16-bit PCM WAVs. They were prepared from Footsteps: Full Bundle by Sound Armoury / Matt J Hart.

When editing, begin from the 32-bit float review masters when available. Preserve the filename marker, export mono 48 kHz PCM WAV, leave at least 3 dB of peak headroom, and verify loop files across the end-to-start boundary. Keep at least one _loop_ file in every surface folder you want to support. A missing surface falls back to ground.
