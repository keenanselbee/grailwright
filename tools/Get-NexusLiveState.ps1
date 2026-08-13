[CmdletBinding()]
param(
    [string]$Mod = '',
    [double]$MaxAgeHours = 168
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'NexusLiveState.ps1')

Get-NexusLiveStateReport -RepoRoot $repoRoot -Mod $Mod -MaxAgeHours $MaxAgeHours |
    Sort-Object Package, Surface
