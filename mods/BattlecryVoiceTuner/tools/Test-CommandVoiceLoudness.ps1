$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
& (Join-Path $repositoryRoot "tools\audio\Normalize-CommandVoices.ps1") `
    -InputDirectory (Join-Path $modRoot "audio\command") `
    -VerifyOnly
