# Vortex Package Layout

Grailwright exports packages in a Vortex-friendly BepInEx plugin shape:

```text
ModFolder/
  ModFolder.dll
  README.txt
  CHANGELOG.txt
```

Asset folders such as `audio/` or reticle PNG files stay beside the DLL when
the runtime expects them there.

Archive filenames and staged Vortex variant folders use the readable mod
display name plus version, such as `Blood Magic Expansion 2.0.7`. The archive
payload keeps the compact plugin folder from `mod.json`, such as
`BloodMagicExpansion`.

Repository-only content such as `src/`, `tools/`, `mod.json`, and
`nexus-full-desc.txt` or `nexus-changelog.txt` is not included in release zips.
Nexus descriptions and consolidated release text are publishing source, not
runtime install payload.

Vortex should install the package as a BepInEx plugin mod so the payload lands
under:

```text
BepInEx/plugins/ModFolder/
```
