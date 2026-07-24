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

Repository-only content such as `src/`, `tools/`, `mod.json`, and
`nexus-desc.txt` is not included in release zips. Nexus descriptions are
publishing source, not runtime install payload.

Vortex should install the package as a BepInEx plugin mod so the payload lands
under:

```text
BepInEx/plugins/ModFolder/
```
