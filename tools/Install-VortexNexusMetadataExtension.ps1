[CmdletBinding()]
param(
    [string]$VortexPluginsRoot = '',
    [switch]$UpdateExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourceRoot = Join-Path $PSScriptRoot 'vortex-extension\grailwright-nexus-metadata'
$info = Get-Content -LiteralPath (Join-Path $sourceRoot 'info.json') -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($VortexPluginsRoot)) {
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw 'APPDATA is not available, so the Vortex plugins directory cannot be resolved.'
    }
    $VortexPluginsRoot = Join-Path $env:APPDATA 'Vortex\plugins'
}
$VortexPluginsRoot = [System.IO.Path]::GetFullPath($VortexPluginsRoot)
$folderName = "$($info.name) $($info.version)"
$destination = [System.IO.Path]::GetFullPath((Join-Path $VortexPluginsRoot $folderName))
$pluginsPrefix = $VortexPluginsRoot.TrimEnd('\') + '\'
if (-not $destination.StartsWith($pluginsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install outside the Vortex plugins directory: $destination"
}
New-Item -ItemType Directory -Path $VortexPluginsRoot -Force | Out-Null
$existing = @(Get-ChildItem -LiteralPath $VortexPluginsRoot -Directory -Force | Where-Object {
    $_.Name -match ('^' + [regex]::Escape([string]$info.name) + ' [0-9]+\.[0-9]+\.[0-9]+$')
})
if ($existing.Count -gt 0 -and -not $UpdateExisting) {
    throw "Vortex extension already exists: $($existing.FullName -join ', '). Pass -UpdateExisting to replace it."
}

$temporary = Join-Path $VortexPluginsRoot (".grailwright-extension-" + [guid]::NewGuid().ToString('N'))
$backups = New-Object 'System.Collections.Generic.List[object]'
try {
    New-Item -ItemType Directory -Path $temporary | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'info.json') -Destination $temporary
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'index.js') -Destination $temporary
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'promotion-core.js') -Destination $temporary

    foreach ($item in $existing) {
        $backupPath = Join-Path $VortexPluginsRoot (".grailwright-extension-backup-" + [guid]::NewGuid().ToString('N'))
        Move-Item -LiteralPath $item.FullName -Destination $backupPath
        $backups.Add([pscustomobject]@{ Original = $item.FullName; Backup = $backupPath })
    }
    Move-Item -LiteralPath $temporary -Destination $destination

    foreach ($backup in $backups) {
        Remove-Item -LiteralPath $backup.Backup -Recurse -Force
    }
}
catch {
    foreach ($backup in $backups) {
        if ((Test-Path -LiteralPath $backup.Backup) -and -not (Test-Path -LiteralPath $backup.Original)) {
            Move-Item -LiteralPath $backup.Backup -Destination $backup.Original
        }
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Recurse -Force
    }
}

[pscustomobject]@{
    Name = [string]$info.name
    Version = [string]$info.version
    InstalledPath = $destination
    RestartVortex = $true
    ReplacedVersions = @($existing | ForEach-Object Name)
}
