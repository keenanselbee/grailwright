# Linux and Proton Portability

Grailwright release packages support Linux-native extraction while retaining
the existing Windows and Vortex layout. The Windows game, Unity, BepInEx, and
FMOD still run through Proton; Grailwright does not add a parallel native-Linux
runtime or per-frame compatibility layer.

## Packaging contract

- Every ZIP contains exactly one top-level folder matching `packageName`.
- ZIP entry paths use `/`, as required for portable extraction, and never `\`.
- The expected DLL and runtime assets retain their authored filename casing.
- Source, tools, manifests, and Nexus publishing files stay outside the package.
- `tools/Export-VortexPackage.ps1` enforces these rules before an archive can be
  staged or published.

Previously published archives are not repaired retroactively. Rebuild and
upload each release that should gain Linux-native extraction support.

## Source review

The 2026-08-05 review covered all 37 authored C# files referenced by the 18 mod
manifests, plus the two shared helpers compiled into config-owning mods. No
Win32 imports, registry access, platform-specific native libraries, or manual
runtime path concatenation requiring an OS branch were found.

| Mod | Filesystem or dependency surface | Result |
| --- | --- | --- |
| Battlecry Voice Tuner | Nested WAV discovery and FMOD file loading | Uses `Path.Combine`; packaged names and casing match |
| Blood Magic Expansion | Packaged corpse-leech WAV loading | Uses `Path.Combine`; packaged names and casing match |
| Dishonored Dynamic Crosshair | Packaged and configurable reticle PNG paths | Uses `Path.Combine`; packaged defaults match |
| Enemy Respawn Control | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Eyes in the Dark | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| First Person Arms Adjuster | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Full Enemy XP | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Glorious UI | Config-local cache files and managed game types | Uses `Path.Combine` under the BepInEx config path |
| Grail Floating Text | Packaged icon PNG loading | Uses `Path.Combine`; packaged names and casing match |
| Killing Blow Mastery | Packaged WAV loading through FMOD and Unity URI APIs | Uses `Path.Combine` and `Uri`; packaged names and casing match |
| King's Elegy | Packaged and configurable audio paths through FMOD | Uses `Path.Combine`; packaged defaults match |
| KS Persistent Corpses Addon | Managed hard dependency and BepInEx config | No direct dependency-file path assumption |
| KS TG All Lights Cast Shadows Addon | Managed hard dependency and BepInEx config | No direct dependency-file path assumption |
| KS Wyrd Sight Addon | Managed hard dependency and BepInEx config | No direct dependency-file path assumption |
| No Player Light | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Steel and Bone | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Ultrawide Fixes | BepInEx config and managed game types | No mod-owned runtime asset path issue |
| Wyrdsoul Reserve | Packaged HUD icon PNG loading | Uses `Path.Combine`; packaged names and casing match |

The shared config-recovery helper derives backup paths with `System.IO.Path`
and the active BepInEx config path. The shared Grail Floating Text notifier uses
managed plugin metadata and has no filesystem dependency.

## Verification and residual risk

`tools/Build-All.ps1 -SkipCompile` successfully packaged all 18 mods after the
portable exporter change. Raw inspection confirmed that every archive had its
exact package root and DLL, contained no backslash entry names, and excluded
repository-only content. Asset-heavy extraction was also checked with Blood
Magic Expansion.

Static review and Windows-side archive verification cannot replace an actual
Proton launch. Before advertising full support, smoke-test representative
DLL-only, asset-heavy, audio-heavy, and hard-dependency mods with the supported
BepInEx setup. User-configured absolute Windows paths are intentionally not
portable, and custom asset names must match case on case-sensitive filesystems.
