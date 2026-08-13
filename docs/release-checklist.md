# Release Checklist

1. Update the mod source version, assembly version, README, Nexus description,
   and changelog.
2. Update the mod's `mod.json` version.
3. Do not use patch components of 10 or higher for Grailwright authored mods.
   Roll `X.Y.9` to `X.(Y+1).0`, such as `2.0.9` to `2.1.0`.
4. Build with `tools/Build-Mod.ps1 -Mod <id> -DestinationDirectory .codex-temp\builds -StageToVortex`.
   If a `.codex-temp\locks\mod-<package>.lock` conflict appears, wait or confirm the recorded owner is stale before forcing it.
5. Confirm the zip was written under `.codex-temp` and the new version was staged into Vortex.
   Both names should use the readable display name plus version, such as `Blood Magic Expansion 2.0.7`.
6. Inspect the zip or staged folder for one compact top-level plugin folder and the expected DLL.
   ZIP entry paths must use `/`, never `\`; the shared exporter validates this before publishing or staging.
7. Confirm the zip does not contain `src`, `tools`, `mod.json`, or
   Nexus publishing metadata such as `nexus-full-desc.txt` or `nexus-changelog.txt`.
8. For a Desktop-only zip, omit `-StageToVortex` only when the user explicitly asks to send the build to Desktop.
9. Publish and update Nexus only through `tools/Publish-NexusMod.ps1` and
   `tools/Update-NexusDescription.ps1` so `.codex-temp\locks\nexus.lock` serializes remote updates.
   Reuse the exact validated build with `-ArchivePath <zip> -SkipBuild`; otherwise let the publisher build into `.codex-temp\builds` by default.
10. Let `Publish-NexusMod.ps1 -AddChangelog` resolve the current Nexus file-group version and collect every newer local changelog block through the upload target.
11. When more than one local version is included and the reviewed consolidation is missing or stale, review the generated candidate and save the final text as the mod's `nexus-changelog.txt`. A valid reviewed consolidation is reused without generating another candidate. Merge repeated work on the same feature into one final-state change while retaining distinct changes and still-relevant compatibility, config-reset, or migration effects.
12. Confirm the Nexus payload contains one target-version entry, no embedded version headings, no duplicate or work-in-progress lines, and no content from the already-published baseline block.
