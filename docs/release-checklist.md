# Release Checklist

1. Update the mod source version, assembly version, README, Nexus description,
   and changelog.
2. Update the mod's `mod.json` version.
3. Build with `tools/Build-Mod.ps1 -Mod <id>`.
4. Confirm the zip was written to the Desktop.
5. Inspect the zip for one top-level folder and the expected DLL.
6. Confirm the zip does not contain `src`, `tools`, `mod.json`, or
   `nexus-desc.txt`.
7. Install into Vortex only after the package contents look correct.
