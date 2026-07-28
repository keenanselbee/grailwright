$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assetRipper = Join-Path $root "assetripper\AssetRipper.GUI.Free.exe"

if (-not (Test-Path -LiteralPath $assetRipper -PathType Leaf)) {
    throw "Could not find AssetRipper at '$assetRipper'."
}

& $assetRipper @args
