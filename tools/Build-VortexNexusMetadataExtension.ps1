[CmdletBinding()]
param([string]$DestinationDirectory = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $PSScriptRoot 'vortex-extension\grailwright-nexus-metadata'
if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = Join-Path $repoRoot '.codex-temp\extensions'
}
$DestinationDirectory = [System.IO.Path]::GetFullPath($DestinationDirectory)

$info = Get-Content -LiteralPath (Join-Path $sourceRoot 'info.json') -Raw | ConvertFrom-Json
$archivePath = Join-Path $DestinationDirectory ("Grailwright Nexus Metadata {0}.zip" -f $info.version)
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -LiteralPath @(
    (Join-Path $sourceRoot 'info.json'),
    (Join-Path $sourceRoot 'index.js'),
    (Join-Path $sourceRoot 'promotion-core.js')
) -DestinationPath $archivePath -CompressionLevel Optimal

[pscustomobject]@{
    Name = [string]$info.name
    Version = [string]$info.version
    ArchivePath = $archivePath
}
