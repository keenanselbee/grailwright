[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.codex-temp\tests')).TrimEnd('\') + '\'
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $testsRoot 'vortex-unelevated-launcher'))
$extensionRoot = Join-Path $PSScriptRoot 'vortex-extension\grailwright-unelevated-launcher'
if (-not $scratchRoot.StartsWith($testsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Scratch path escaped the repository test root: $scratchRoot"
}

function Assert-Contract {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Vortex unelevated launcher contract failed: $Message"
    }
}

try {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null

    $info = Get-Content -LiteralPath (Join-Path $extensionRoot 'info.json') -Raw | ConvertFrom-Json
    Assert-Contract ($info.version -match '^\d+\.\d+\.\d+$') 'info.json does not contain a semantic version.'

    $brokerResult = & (Join-Path $extensionRoot 'launch-foa-unelevated.ps1') -ValidateOnly
    Assert-Contract ([bool]$brokerResult.Ready) 'the limited-task broker did not report ready.'
    Assert-Contract (Test-Path -LiteralPath ([string]$brokerResult.GameExecutable) -PathType Leaf) 'the broker did not resolve the game executable.'
    Assert-Contract ([string]$brokerResult.GameExecutable -eq 'G:\Steam\steamapps\common\Tainted Grail FoA\Fall of Avalon.exe') 'the broker targets the wrong game executable.'
    Assert-Contract ([string]$brokerResult.WorkingDirectory -eq 'G:\Steam\steamapps\common\Tainted Grail FoA') 'the broker targets the wrong working directory.'
    Assert-Contract ([string]$brokerResult.TaskName -eq 'Grailwright Launch Tainted Grail') 'the broker targets the wrong scheduled task.'
    Assert-Contract ([string]$brokerResult.RunLevel -eq 'Limited') 'the launch task is not configured for limited privileges.'

    $harnessPath = Join-Path $scratchRoot 'test-extension.js'
    [System.IO.File]::WriteAllText($harnessPath, @'
"use strict";
const Module = require("module");
const path = require("path");

const extensionPath = process.argv[2];
const calls = [];
const mockVortex = {
  selectors: { activeGameId: (state) => state.activeGameId },
  util: { steam: { id: "steam" } },
  log: (...args) => calls.push({ type: "log", args }),
};
const originalLoad = Module._load;
Module._load = function(request, parent, isMain) {
  return request === "vortex-api" ? mockVortex : originalLoad.call(this, request, parent, isMain);
};

const extension = require(path.join(extensionPath, "index.js"));
function assert(condition, message) {
  if (!condition) throw new Error(message);
}

(async () => {
  const gamePath = "G:\\Steam\\steamapps\\common\\Tainted Grail FoA";
  const state = {
    activeGameId: extension.GAME_ID,
    settings: {
      gameMode: {
        discovered: {
          [extension.GAME_ID]: { path: gamePath, store: "steam" },
        },
      },
    },
  };
  const api = { getState: () => state };
  let registeredHook;
  const context = {
    api,
    registerStartHook: (priority, id, hook) => { registeredHook = { priority, id, hook }; },
  };
  assert(extension.default(context) === true, "extension did not initialize");
  assert(registeredHook?.priority === 200 && registeredHook?.id === extension.HOOK_ID, "launch hook registration is incorrect");

  const original = {
    executable: path.join(gamePath, extension.GAME_EXECUTABLE),
    args: ["original"],
    options: { cwd: gamePath, env: { SteamAPPId: "1466060" }, suggestDeploy: true },
  };
  const rewritten = await registeredHook.hook(original);
  assert(rewritten !== original, "FOA launch was not rewritten");
  assert(rewritten.executable.toLowerCase().endsWith("schtasks.exe"), "rewritten launch does not use Task Scheduler directly");
  assert(rewritten.args.join("|") === `/Run|/TN|${extension.TASK_NAME}`, "rewritten launch does not target the limited task");
  assert(rewritten.options.shell === false && rewritten.options.detach === true, "rewritten launch has unsafe process options");
  assert(rewritten.options.suggestDeploy === true && rewritten.options.env.SteamAPPId === "1466060", "existing launch options were not preserved");

  state.activeGameId = "another-game";
  assert(await registeredHook.hook(original) === original, "inactive FOA launch was rewritten");
  state.activeGameId = extension.GAME_ID;
  state.settings.gameMode.discovered[extension.GAME_ID].store = "gog";
  assert(await registeredHook.hook(original) === original, "GOG launch was rewritten");
  state.settings.gameMode.discovered[extension.GAME_ID].store = "steam";
  const otherTool = { ...original, executable: "C:\\Tools\\OtherTool.exe" };
  assert(await registeredHook.hook(otherTool) === otherTool, "an alternate primary tool was rewritten");

  console.log("Vortex launcher JavaScript contracts passed.");
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
'@)
    & node $harnessPath $extensionRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Node launcher contracts failed with exit code $LASTEXITCODE."
    }

    $extensionBuild = & (Join-Path $PSScriptRoot 'Build-VortexUnelevatedLauncherExtension.ps1') -DestinationDirectory (Join-Path $scratchRoot 'extension-build') | Select-Object -Last 1
    Assert-Contract (Test-Path -LiteralPath $extensionBuild.ArchivePath -PathType Leaf) 'extension package was not created.'
    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $extensionArchive = [System.IO.Compression.ZipFile]::OpenRead([string]$extensionBuild.ArchivePath)
    try {
        $extensionEntries = @($extensionArchive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object FullName | Sort-Object)
    }
    finally {
        $extensionArchive.Dispose()
    }
    Assert-Contract (($extensionEntries -join ',') -eq 'index.js,info.json,launch-foa-unelevated.ps1') 'extension package contains unexpected files or layout.'

    $extensionInstall = & (Join-Path $PSScriptRoot 'Install-VortexUnelevatedLauncherExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') | Select-Object -Last 1
    Assert-Contract (Test-Path -LiteralPath (Join-Path $extensionInstall.InstalledPath 'index.js') -PathType Leaf) 'extension installer did not place index.js.'
    Assert-Contract (Test-Path -LiteralPath (Join-Path $extensionInstall.InstalledPath 'launch-foa-unelevated.ps1') -PathType Leaf) 'extension installer did not place the broker.'
    Assert-Contract ([bool]$extensionInstall.RestartVortex) 'extension installer did not report the required Vortex restart.'

    $existingInstallError = ''
    try {
        & (Join-Path $PSScriptRoot 'Install-VortexUnelevatedLauncherExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') | Out-Null
    }
    catch {
        $existingInstallError = $_.Exception.Message
    }
    Assert-Contract ($existingInstallError.Contains('-UpdateExisting')) 'extension installer did not guard an existing installation.'
    $updatedExtension = & (Join-Path $PSScriptRoot 'Install-VortexUnelevatedLauncherExtension.ps1') -VortexPluginsRoot (Join-Path $scratchRoot 'plugins') -UpdateExisting | Select-Object -Last 1
    Assert-Contract (@($updatedExtension.ReplacedVersions).Count -eq 1) 'extension update did not report the replaced installation.'

    Write-Host 'Vortex unelevated launcher contracts passed: limited task broker, FOA-only start hook, alternate-tool preservation, package layout, and guarded installation.'
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
}
