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
| [Blood Magic Expansion](mods/BloodMagicExpansion) | 2.0.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/224) |
| [Dishonored Dynamic Crosshair](mods/DishonoredDynamicCrosshair) | 2.8.3 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/223) |
| [Enemy Respawn Control](mods/EnemyRespawnControl) | 1.0.0 | Unpublished |
| [Killing Blow Mastery](mods/KillingBlowMastery) | 1.3.1 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/234) |
| [King's Elegy - Main Menu Music](mods/KingsElegyMainMenuMusic) | 2.0.5 | [Nexus](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/230) |
| [More Weapon Loadouts Addon](mods/KSAddons/MoreWeaponLoadoutsAddon) | 0.1.2 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [Owrocc HUD Tweaks / More Weapon Loadouts](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/192) |
| [No Player Light](mods/NoPlayerLight) | 1.0.0 | Unpublished |
| [Steel and Bone](mods/SteelAndBone) | 0.2.0 | Unpublished |
| [TG All Lights Cast Shadows Addon](mods/KSAddons/TGAllLightsCastShadowsAddon) | 1.1.0 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [TG All Lights Cast Shadows](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/133) |
| [Ultrawide Fixes](mods/UltrawideFixes) | 1.0.0 | Unpublished |
| [Wyrd Hunt Addon](mods/KSAddons/WyrdHuntAddon) | 1.2.1 | [KS Addons](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225); targets [Wyrd Hunt](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/201) |

## Layout

```text
mods/
  BloodMagicExpansion/
  DishonoredDynamicCrosshair/
  KillingBlowMastery/
  KingsElegyMainMenuMusic/
  EnemyRespawnControl/
  NoPlayerLight/
  SteelAndBone/
  UltrawideFixes/
  KSAddons/
    WyrdHuntAddon/
    TGAllLightsCastShadowsAddon/
    MoreWeaponLoadoutsAddon/

tools/
  Build-Mod.ps1
  Build-All.ps1
  Export-VortexPackage.ps1
  Publish-NexusMod.ps1
  audio/
    Convert-RewardSounds.ps1

docs/
```

## Release Output

Release zips are exported to the Windows Desktop by default. The repo does not
keep a `dist` folder. When a new version is exported, older same-package zips
in the destination folder are removed so the Desktop stays latest-only.
Archive filenames use the readable display name plus version, such as
`No Player Light 1.0.2.zip`. The zip payload still contains one compact
top-level mod folder, such as `NoPlayerLight`, so plugin folder identity and
Vortex staging checks stay stable.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod KillingBlowMastery
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-All.ps1
```

Use `-SkipCompile` to repackage the current checked-in DLL and assets without
running a mod compile script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod NoPlayerLight -SkipCompile
```

Use `-StageToVortex` when you want the exported zip copied into Vortex's mod
staging folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Mod BloodMagicExpansion -StageToVortex
```

This creates a folder such as
`%APPDATA%\Vortex\taintedgrailthefallofavalon\mods\BloodMagicExpansion-2.0.6`
from the zip payload. It does not edit Vortex metadata, deploy, enable, disable,
or change the active profile selection. If that exact version folder already
exists, the script stops instead of overwriting it.

## Nexus Publishing

`tools/Publish-NexusMod.ps1` automates the Nexus upload/version/changelog path
that is exposed by the Nexus Mods v3 API. The main mod-page description is still
a manual or browser-automation step because the current v3 OpenAPI schema does
not expose a mod description update endpoint.

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
include `src`, `tools`, `mod.json`, `API.txt`, `nexus-desc.txt`,
`nexus-page-summary.txt`, `nexus-file-summary.txt`, or other repository-only
build and publishing scaffolding.

## Documentation Standards

Top-level `README.txt` files are packaged installed-user quick references. Keep
them focused on what the mod does, version, config path, default behavior,
custom asset notes, compatibility, and troubleshooting when relevant. Put full
Nexus page copy in `nexus-desc.txt` instead.

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
nexus-page-summary.txt   # Nexus page short description, 350 characters max
nexus-file-summary.txt   # Nexus file-row description, 255 characters max
nexus-desc.txt           # full Nexus description, manual/browser update
```

`Publish-NexusMod.ps1` uses `nexus-file-summary.txt` as the file upload
description unless `-FileDescription` is passed. Keep that file to one or two
sentences describing what the mod does. Do not put version-specific changelog
notes there; changelog entries remain separate and are posted only with
`-AddChangelog`.

Validate Nexus metadata before publishing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-NexusMetadata.ps1
```

Use `-RequireApi` when checking only mods that already have Nexus pages.
