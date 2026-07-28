# Game Inspection Tools

Local tools for inspecting Tainted Grail: The Fall of Avalon game files while working on Steel and Bone.

Installed tools:

| Tool | Location | Use |
|---|---|---|
| ILSpy command line `10.1.1.8388` | `tools/game-inspection/ilspycmd` | Decompile and inspect managed assemblies such as `TG.Main.dll`. |
| UnityPy `1.25.2` and dependencies | `tools/game-inspection/python` | Parse Unity assets, asset bundles, addressable catalogs, `TextAsset`s, and serialized object names from Python. |
| dnfile `0.18.0` and pefile `2024.8.26` | `tools/game-inspection/python` | Scan .NET assembly metadata without a full decompile. Useful for type, method, enum, and user-string discovery. |
| AssetRipper `1.3.14` x64 | `tools/game-inspection/assetripper` | Browse or export Unity assets through AssetRipper's local UI/server. |

Wrappers:

```powershell
.\tools\game-inspection\Invoke-ILSpy.ps1 --version
.\tools\game-inspection\Invoke-ILSpy.ps1 -l c "G:\Steam\steamapps\common\Tainted Grail FoA\Fall of Avalon_Data\Managed\TG.Main.dll"

.\tools\game-inspection\Invoke-UnityPython.ps1 -c "import UnityPy, dnfile; print(UnityPy.__version__)"

.\tools\game-inspection\Invoke-AssetRipper.ps1 --help
.\tools\game-inspection\Invoke-AssetRipper.ps1 --headless --port 0
```

Decompile the main game assembly into the ignored cache:

```powershell
.\tools\game-inspection\Decompile-TGMain.ps1
rg -n "DamageType|StatusDamage|NpcTemplate|Bestiary|Weakspot|ApplyDamageModifiers" .\.codex-temp\decompiled\TG.Main-1.25
```

For Steel and Bone enemy research, start with ILSpy against `TG.Main.dll` for damage types and model names, then use UnityPy or AssetRipper against `Fall of Avalon_Data/StreamingAssets/aa` when enemy definitions or localized names are stored in addressable bundles.
