$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagePath = Join-Path $root "python"

if (-not (Test-Path -LiteralPath $packagePath -PathType Container)) {
    throw "Could not find local Python packages under '$packagePath'."
}

if ([string]::IsNullOrWhiteSpace($env:PYTHONPATH)) {
    $env:PYTHONPATH = $packagePath
} else {
    $env:PYTHONPATH = $packagePath + [IO.Path]::PathSeparator + $env:PYTHONPATH
}

& python @args
