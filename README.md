# Grailwright

Grailwright is the source-of-truth workspace for Keenan's Tainted Grail: The
Fall of Avalon mods.

The repo keeps mod source, docs, audio, reticles, and release metadata in one
place. Vortex install folders and Desktop zips are treated as outputs, not the
main development home.

## Current Mods

Versions come from each mod's `mod.json`. Nexus links point to the public mod
page when one is known; addon rows also include the parent Nexus mod they patch.

| Mod | Version | Nexus |
| --- | --- | --- |
| [Glorious UI](mods/GloriousUI) | 1.7.1 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/248) |
| [Blood Magic Expansion](mods/BloodMagicExpansion) | 2.4.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/224) |
| [Dishonored Dynamic Crosshair](mods/DishonoredDynamicCrosshair) | 2.9.2 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/223) |
| [Enemy Respawn Control](mods/EnemyRespawnControl) | 2.1.3 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/240) |
| [Eyes in the Dark - Wyrdnight Overhaul](mods/EyesInTheDark) | 1.2.8 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/254) |
| [First Person Arms Adjuster](mods/FirstPersonArmsAdjuster) | 0.3.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/263) |
| [Full Enemy XP](mods/FullEnemyXP) | 1.0.8 | Unpublished |
| [Grail Floating Text](mods/GrailFloatingText) | 1.10.0 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/247) |
| [Killing Blow Mastery](mods/KillingBlowMastery) | 1.5.7 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/234) |
| [King's Elegy - Main Menu Music](mods/KingsElegyMainMenuMusic) | 2.2.1 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/230) |
| [KS Better Movement Addon](mods/KSAddons/KSBetterMovementAddon) | 0.1.4 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [Better Movement](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/102) |
| [KS Persistent Corpses Addon](mods/KSAddons/KSPersistentCorpsesAddon) | 1.0.8 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [Persistent Corpses](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/228) |
| [KS TG All Lights Cast Shadows Addon](mods/KSAddons/KSTGAllLightsCastShadowsAddon) | 1.2.0 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [TG All Lights Cast Shadows](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/133) |
| [KS Wyrd Sight Addon](mods/KSAddons/KSWyrdSightAddon) | 1.2.6 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [Wyrd Sight](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/94) |
| [No Player Light](mods/NoPlayerLight) | 1.3.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/238) |
| [Battlecry Voice Tuner](mods/BattlecryVoiceTuner) | 1.1.0 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/252) |
| [Steel and Bone](mods/SteelAndBone) | 3.2.1 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/241) |
| [Ultrawide Fixes](mods/UltrawideFixes) | 1.0.9 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/239) |
| [Wyrdsoul Reserve](mods/WyrdsoulReserve) | 1.0.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/264) |

## Layout

```text
mods/
  GloriousUI/
  BloodMagicExpansion/
  DishonoredDynamicCrosshair/
  KillingBlowMastery/
  KingsElegyMainMenuMusic/
  EnemyRespawnControl/
  EyesInTheDark/
  FullEnemyXP/
  NoPlayerLight/
  BattlecryVoiceTuner/
  SteelAndBone/
  UltrawideFixes/
  WyrdsoulReserve/
  KSAddons/
    KSBetterMovementAddon/
    KSPersistentCorpsesAddon/
    KSWyrdSightAddon/
    KSTGAllLightsCastShadowsAddon/

tools/
  Build-Mod.ps1
  Build-All.ps1
  Export-VortexPackage.ps1
  Lock-Operation.ps1
  Publish-NexusMod.ps1
  audio/
    Convert-RewardSounds.ps1

docs/
```

## Config Recovery

Every config-owning mod compiles the shared
`tools/shared/ConfigPreviousSettingsRecovery.cs` helper. FoA Mod Manager always
shows its explicit final `Import Previous Settings` tab with the current and
newest compatible backup schemas. The one-shot action imports only compatible
values that the user changed from the backup's recorded defaults, clamps current
ranges, keeps new defaults, and creates a transactional pre-import backup.

Each mod fixes `ConfigRecoveryBaselineSchema` at the schema where recovery was
introduced. Future schema changes leave that baseline alone and add exact
`ConfigRecoveryKeepCurrentDefaultRule` entries only when an old customized
same-name value must not return. Automatic durable-setting preservation uses
the same typed customization profile, transition rules, and current
`AcceptableValues` validation/clamping as manual import, so equivalent
serialized values are not mistaken for customization and mods do not maintain
parallel stale-value parsers. Each mod also declares permanent exclusions for
one-shot actions, preset triggers, and derived status entries that must never
be restored.

Run both contracts after config or schema work:

```powershell
.\tools\Test-ConfigRecoveryContracts.ps1
.\tools\Test-ConfigPreservationContracts.ps1
```

## Release Output

Release zips are not kept in a repo-local `dist` folder. For normal agent-led
test builds, export the intermediate zip under `.codex-temp` and stage the new
version into Vortex. When a Desktop zip is explicitly requested, export only to
the Windows Desktop and do not stage to Vortex. Older same-package zips in the
destination folder are removed so a Desktop export stays latest-only.
Archive filenames use the readable display name plus version, such as
`No Player Light 1.0.2.zip`. The zip payload still contains one compact
top-level mod folder, such as `NoPlayerLight`, so plugin folder identity and
Vortex staging checks stay stable.

`Build-Mod.ps1` and `Build-All.ps1` acquire a per-mod lock under
`.codex-temp\locks\mod-<package>.lock` while compiling, exporting, and staging.
If another thread already owns that mod lock, the command stops and prints the
owner details from `owner.json`. Use `-LockWaitSeconds` when waiting is desired,
or `-ForceStaleLock` only after confirming the recorded owner is gone.

Compilation prefers Roslyn from the newest installed .NET SDK and pins
`/langversion:7.3` for compatibility with the game's Mono/.NET Framework
runtime. It uses the game's `mscorlib.dll`, `System.dll`, and `System.Core.dll`
plus each mod manifest's references. A .NET SDK is required because the
authored sources use language features that the legacy .NET Framework compiler
cannot compile.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod BloodMagicExpansion -DestinationDirectory .\.codex-temp\builds -StageToVortex
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-All.ps1 -DestinationDirectory .\.codex-temp\builds -StageToVortex
```

Use `-SkipCompile` to repackage the current checked-in DLL and assets without
running a mod compile script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod NoPlayerLight -DestinationDirectory .\.codex-temp\builds -StageToVortex -SkipCompile
```

This creates a readable archive and staged Vortex variant such as
`Blood Magic Expansion 2.0.7.zip` and
`%APPDATA%\Vortex\taintedgrailthefallofavalon\mods\Blood Magic Expansion 2.0.7`.
The payload inside both remains the compact plugin folder, such as
`BloodMagicExpansion`. Staging does not edit Vortex metadata, deploy, enable,
disable, or change the active profile selection. If that exact version folder
already exists, the script stops instead of overwriting it.

For a Desktop-only zip, omit `-StageToVortex` and use the Desktop destination:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod BloodMagicExpansion
```

## Nexus Publishing

`tools/Publish-NexusMod.ps1` automates the Nexus upload/version/changelog path
that is exposed by the Nexus Mods v3 API. The main mod-page description is still
a manual or browser-automation step because the current v3 OpenAPI schema does
not expose a mod description update endpoint.

`tools/Update-NexusDescription.ps1` handles that browser step through the
Chrome profile under `.codex-temp\nexus-browser-profile-chrome`. Chrome is the
only supported browser path; run it with `-LoginOnly` when the profile needs a
fresh Nexus login, then rerun with `-Save`.

Nexus updates are serialized by a global `.codex-temp\locks\nexus.lock`.
`Publish-NexusMod.ps1` builds the archive before taking the global lock, then
rereads the mod manifest, changelog, and Nexus metadata while it owns the lock
before uploading. `Update-NexusDescription.ps1` takes the same lock before
reading description files or launching browser automation. The same
`-LockWaitSeconds` and `-ForceStaleLock` options are available for explicit
waiting or stale-lock recovery.

Keep the Nexus API key out of repo files. Set it only in your shell environment:

```powershell
$env:NEXUS_API_KEY = "..."
```

Do not put API keys in `mod.json`, `README`, changelogs, `.ps1` files, or
Nexus description files. The publish script refuses obvious secret fields inside
`mod.json`, and `.gitignore` excludes common local secret file names.

Per-mod local Nexus metadata can live in `mods/<ModName>/API.txt`. These files
are ignored by git and can store non-secret values such as `NexusUrl`, `ModId`,
`GameDomain`, `GroupId`, and `FileName`. Do not put the personal Nexus API key
there.

Example `API.txt`:

```text
GameDomain=taintedgrailthefallofavalon
ModId=25280177504494
GroupId=7703316
FileName=No Player Light
FileCategory=main
PrimaryModManagerDownload=true
AllowModManagerDownload=true
ShowRequirementsPopUp=false
UpdateModVersion=true
ArchiveExistingFile=true
```

Dry-run a publish plan without uploading:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-NexusMod.ps1 `
  -Mod EnemyRespawnControl `
  -NexusUrl "https://www.nexusmods.com/skyrimspecialedition/mods/27633" `
  -GroupId 4970635 `
  -DryRun
```

List existing Nexus file/update groups for a mod:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-NexusMod.ps1 `
  -NexusUrl "https://www.nexusmods.com/skyrimspecialedition/mods/27633" `
  -ListFiles
```

Publish a new version to a known Group ID:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-NexusMod.ps1 `
  -Mod EnemyRespawnControl `
  -NexusUrl "https://www.nexusmods.com/skyrimspecialedition/mods/27633" `
  -GroupId 4970635 `
  -AddChangelog
```

## Mod Manifests

Each mod has a `mod.json` file describing the package name, plugin version,
DLL, source file, runtime assets, and compile references. Shared build/export
logic lives in `tools/`; individual mod folders should not carry copies of
build or export scripts.

Exported zips are runtime payloads only. They include one top-level mod folder
with the DLL, runtime assets, README, and changelog inside it. They do not
include `src`, `tools`, `mod.json`, `API.txt`, `nexus-full-desc.txt`,
`nexus-short-desc.txt`, `nexus-file-desc.txt`, `nexus-changelog.txt`, or other repository-only
build and publishing scaffolding.

## Documentation Standards

Top-level `README.txt` files are packaged installed-user quick references. Keep
them focused on what the mod does, version, config path, default behavior,
custom asset notes, compatibility, and troubleshooting when relevant. Put full
Nexus page copy in `nexus-full-desc.txt` instead.

Keep `CHANGELOG.txt` plain text, newest first, and ready to paste into Nexus.
Use this exact shape:

```text
Version 1.3.2
Added a concise change line.
Changed another concise behavior.
Fixed a specific issue.

Version 1.3.1
Refreshed an earlier change.
Kept compatible settings unchanged.
```

Keep Nexus release metadata beside each mod:

```text
nexus-short-desc.txt   # Nexus page short description, 350 characters max
nexus-file-desc.txt    # Nexus file-row description, 255 characters max
nexus-full-desc.txt    # full Nexus description, manual/browser update
nexus-changelog.txt    # reviewed consolidation for a multi-version Nexus upload
```

In `nexus-full-desc.txt`, link the first nearby mention of each published mod
name to its Nexus page with BBCode `[url=...]Name[/url]`. Leave repeated
mentions in the same paragraph, list, or nearby section unlinked or bold. Put
the concise Grail Floating Text diagnostics note under Compatibility or
Optional Integrations when present.

`Publish-NexusMod.ps1` uses `nexus-file-desc.txt` as the file upload
description unless `-FileDescription` is passed. Keep that file to one or two
sentences describing what the mod does. Do not put version-specific changelog
notes there; changelog entries remain separate and are posted only with
`-AddChangelog`.

When a Nexus upload spans several local versions, `Publish-NexusMod.ps1`
generates a flattened candidate under `.codex-temp` and requires a reviewed
`nexus-changelog.txt`. Its first two nonblank lines identify the target and
current Nexus baseline; the remaining lines describe the final cumulative
release without intermediate version headings or superseded repeated changes.
The complete per-version history remains in `CHANGELOG.txt`.

Validate Nexus metadata before publishing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-NexusMetadata.ps1
```

Use `-RequireApi` when checking only mods that already have Nexus pages.

Validate stale-config preservation source ordering, exact setting contracts,
and use of shared typed recovery and current-range clamping with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-ConfigPreservationContracts.ps1
```
