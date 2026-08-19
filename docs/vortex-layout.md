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

ZIP entry paths use the standard `/` separator even when packages are built on
Windows. This lets Linux-native mod managers extract the same folder structure
that Windows and Vortex see instead of creating flattened filenames containing
backslashes. The shared exporter validates this before accepting an archive.

Repository-only content such as `src/`, `tools/`, `mod.json`, and
`nexus-full-desc.txt` or `nexus-changelog.txt` is not included in release zips.
Nexus descriptions and consolidated release text are publishing source, not
runtime install payload.

Vortex should install the package as a BepInEx plugin mod so the payload lands
under:

```text
BepInEx/plugins/ModFolder/
```

Ordinary local stages immediately queue truthful grouping metadata for the
Grailwright Nexus Metadata Vortex extension. Every version receives the stable
logical filename and, when configured, the real Nexus page ID, so Vortex can
show the separate staging folders as versions beneath one visible mod row. Local
builds use `grailwright-local` as their source and receive no Nexus file ID.

After an exact archive is published, `Publish-NexusMod.ps1` queues a
receipt-backed promotion. The extension verifies the staged payload, imports or
reuses the exact archive through Vortex, and upgrades that version with its real
Nexus file ID, archive hash and size, and archive link. The folder name remains
the readable display name plus version; grouping and Nexus source identity live
in Vortex attributes instead of the folder name.

Run `tools/Update-VortexStagedModGrouping.ps1` once to queue grouping metadata
for existing Grailwright staging folders. Run
`tools/Test-VortexCollectionReadiness.ps1` before updating a collection; it
fails closed when the authored catalog is absent, an enabled Grailwright version
is missing grouping metadata, or an enabled managed version is still a local
test build. The extension registers new folders while Vortex is open and
reconciles catalogued records before refreshing collection readiness. After an
extension update, restart Vortex once. Use
`Update-VortexStagedModGrouping.ps1 -Repair` to explicitly requeue older
acknowledged records without removing or reinstalling any staged version.
