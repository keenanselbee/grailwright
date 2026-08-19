[CmdletBinding()]
param(
    [string]$SaveRoot = "$env:USERPROFILE\AppData\LocalLow\Questline\Fall of Avalon\77198420\Saved",
    [string]$BackupRoot = "Z:\Backup\Tainted Grail",
    [string]$VortexGameRoot = "$env:APPDATA\Vortex\taintedgrailthefallofavalon",
    [string]$BepInExConfigRoot = "G:\Steam\steamapps\common\Tainted Grail FoA\BepInEx\config",
    [int]$BackupNumber,
    [switch]$Latest,
    [switch]$List,
    [switch]$RestoreMods,
    [switch]$SkipMods,
    [switch]$RestoreBepInExConfig,
    [switch]$SkipBepInExConfig,
    [switch]$SavesOnly,
    [switch]$AllowRunning,
    [switch]$OpenDestination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BackupPrefix = "Tainted Grail Save Backup"
$VortexSubfolders = @("mods", "profiles", "snapshots")
$PathTrimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

function Get-PathStats {
    param(
        [AllowNull()]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{
            Files = 0
            Bytes = 0
        }
    }

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force)
    $totalBytes = 0
    foreach ($file in $files) {
        $totalBytes += $file.Length
    }

    return [pscustomobject]@{
        Files = $files.Count
        Bytes = $totalBytes
    }
}

function Get-BackupDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    if (-not (Test-Path -LiteralPath $Root)) {
        return @()
    }

    $backups = @()
    foreach ($directory in Get-ChildItem -LiteralPath $Root -Directory) {
        if ($directory.Name -match "^$([regex]::Escape($BackupPrefix)) (?<number>\d+) - (?<date>\d{1,2}-\d{1,2}-\d{4})$") {
            $manifestPath = Join-Path $directory.FullName "backup-info.txt"
            if (-not (Test-Path -LiteralPath $manifestPath)) {
                continue
            }

            $savedPath = Join-Path $directory.FullName "Saved"
            $emptySavedStateMarkerPath = Join-Path $directory.FullName ".saved-state-empty"
            $fileCount = 0
            if (Test-Path -LiteralPath $savedPath) {
                $fileCount = @(Get-ChildItem -LiteralPath $savedPath -File -Recurse -Force).Count
            }

            $vortexPath = Join-Path $directory.FullName "Vortex"
            $vortexFileCount = 0
            $hasVortexMods = Test-Path -LiteralPath $vortexPath
            if ($hasVortexMods) {
                $vortexFileCount = @(Get-ChildItem -LiteralPath $vortexPath -File -Recurse -Force).Count
            }

            $bepInExConfigPath = Join-Path $directory.FullName "BepInExConfig"
            $bepInExConfigFileCount = 0
            $hasBepInExConfig = Test-Path -LiteralPath $bepInExConfigPath
            if ($hasBepInExConfig) {
                $bepInExConfigFileCount = @(Get-ChildItem -LiteralPath $bepInExConfigPath -File -Recurse -Force).Count
            }

            $backups += [pscustomobject]@{
                Number = [int]$Matches["number"]
                Date = $Matches["date"]
                Name = $directory.Name
                FullName = $directory.FullName
                SavedPath = $savedPath
                FileCount = $fileCount
                HasEmptySavedStateMarker = Test-Path -LiteralPath $emptySavedStateMarkerPath -PathType Leaf
                VortexPath = $vortexPath
                HasVortexMods = $hasVortexMods
                VortexFileCount = $vortexFileCount
                BepInExConfigPath = $bepInExConfigPath
                HasBepInExConfig = $hasBepInExConfig
                BepInExConfigFileCount = $bepInExConfigFileCount
                ManifestPath = $manifestPath
                LastWriteTime = $directory.LastWriteTime
            }
        }
    }

    return @($backups | Sort-Object Number)
}

function Get-NextBackupNumber {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $backups = @(Get-BackupDirectories -Root $Root)
    if ($backups.Count -eq 0) {
        return 1
    }

    return (($backups | Measure-Object -Property Number -Maximum).Maximum + 1)
}

function New-BackupDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    if (-not (Test-Path -LiteralPath $Root)) {
        New-Item -ItemType Directory -Path $Root | Out-Null
    }

    $number = Get-NextBackupNumber -Root $Root
    $date = Get-Date -Format "M-d-yyyy"

    do {
        $folderName = "$BackupPrefix $number - $date"
        $backupPath = Join-Path $Root $folderName
        if (Test-Path -LiteralPath $backupPath) {
            $number++
        }
    } while (Test-Path -LiteralPath $backupPath)

    New-Item -ItemType Directory -Path $backupPath | Out-Null
    return $backupPath
}

function Assert-GameNotRunning {
    if ($AllowRunning) {
        return
    }

    $process = Get-Process -Name "Fall of Avalon" -ErrorAction SilentlyContinue
    if ($process) {
        $ids = ($process | ForEach-Object { $_.Id }) -join ", "
        throw "Fall of Avalon appears to be running (PID $ids). Quit the game, or rerun with -AllowRunning."
    }
}

function Assert-VortexNotRunning {
    if ($AllowRunning) {
        return
    }

    $process = Get-Process -Name "Vortex" -ErrorAction SilentlyContinue
    if ($process) {
        $ids = ($process | ForEach-Object { $_.Id }) -join ", "
        throw "Vortex appears to be running (PID $ids). Quit Vortex before restoring mods, or rerun with -AllowRunning."
    }
}

function Assert-SavedFolderReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label folder was not found: $Path"
    }

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force)
    if ($files.Count -eq 0) {
        throw "$Label folder exists but contains no files: $Path"
    }
}

function Assert-SelectedBackupSavedFolderReady {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Backup
    )

    if (-not (Test-Path -LiteralPath $Backup.SavedPath -PathType Container)) {
        throw "Selected backup Saved folder was not found: $($Backup.SavedPath)"
    }

    $files = @(Get-ChildItem -LiteralPath $Backup.SavedPath -File -Recurse -Force)
    if ($files.Count -eq 0 -and -not $Backup.HasEmptySavedStateMarker) {
        throw "Selected backup Saved folder exists but contains no files and is not marked as an intentional empty Saved state: $($Backup.SavedPath)"
    }
}

function Assert-VortexFolderReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label Vortex folder was not found: $Path"
    }

    $foundSection = $false
    foreach ($subfolder in $VortexSubfolders) {
        $sectionPath = Join-Path $Path $subfolder
        if (Test-Path -LiteralPath $sectionPath) {
            $foundSection = $true
        }
    }

    if (-not $foundSection) {
        throw "$Label Vortex folder exists but none of these sections were found: $($VortexSubfolders -join ', ')."
    }
}

function Assert-BepInExConfigFolderReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label BepInEx config folder was not found: $Path"
    }

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force)
    if ($files.Count -eq 0) {
        throw "$Label BepInEx config folder exists but contains no files: $Path"
    }
}

function Write-BackupInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupPath,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$Reason,

        [string]$VortexSourcePath,

        [string]$VortexBackupPath,

        [string]$BepInExConfigSourcePath,

        [string]$BepInExConfigBackupPath
    )

    $saveStats = Get-PathStats -Path $SourcePath
    $vortexStats = Get-PathStats -Path $VortexBackupPath
    $bepInExConfigStats = Get-PathStats -Path $BepInExConfigBackupPath

    $info = @(
        "Tainted Grail: The Fall of Avalon save backup"
        "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
        "SaveSource: $SourcePath"
        "VortexSource: $VortexSourcePath"
        "BepInExConfigSource: $BepInExConfigSourcePath"
        "Destination: $BackupPath"
        "Reason: $Reason"
        "SavedFiles: $($saveStats.Files)"
        "SavedBytes: $($saveStats.Bytes)"
        "VortexIncluded: $([bool]$VortexBackupPath)"
        "VortexFiles: $($vortexStats.Files)"
        "VortexBytes: $($vortexStats.Bytes)"
        "BepInExConfigIncluded: $([bool]$BepInExConfigBackupPath)"
        "BepInExConfigFiles: $($bepInExConfigStats.Files)"
        "BepInExConfigBytes: $($bepInExConfigStats.Bytes)"
        "AllowRunning: $AllowRunning"
    )

    Set-Content -LiteralPath (Join-Path $BackupPath "backup-info.txt") -Value $info -Encoding UTF8
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromRoot,

        [Parameter(Mandatory = $true)]
        [string]$ToRoot
    )

    New-Item -ItemType Directory -Path $ToRoot | Out-Null

    foreach ($item in Get-ChildItem -LiteralPath $FromRoot -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $ToRoot -Recurse -Force
    }

    return $ToRoot
}

function Copy-VortexState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromRoot,

        [Parameter(Mandatory = $true)]
        [string]$BackupPath
    )

    $vortexBackupPath = Join-Path $BackupPath "Vortex"
    New-Item -ItemType Directory -Path $vortexBackupPath | Out-Null

    foreach ($file in Get-ChildItem -LiteralPath $FromRoot -File -Force) {
        Copy-Item -LiteralPath $file.FullName -Destination $vortexBackupPath -Force
    }

    foreach ($subfolder in $VortexSubfolders) {
        $sectionPath = Join-Path $FromRoot $subfolder
        if (Test-Path -LiteralPath $sectionPath) {
            Copy-Item -LiteralPath $sectionPath -Destination $vortexBackupPath -Recurse -Force
        }
    }

    return $vortexBackupPath
}

function Copy-BepInExConfigState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromRoot,

        [Parameter(Mandatory = $true)]
        [string]$BackupPath
    )

    $configBackupPath = Join-Path $BackupPath "BepInExConfig"
    Copy-DirectoryContents -FromRoot $FromRoot -ToRoot $configBackupPath | Out-Null
    return $configBackupPath
}

function New-SafetyBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentSavePath,

        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$RestoreSource,

        [string]$CurrentVortexPath,

        [switch]$IncludeMods,

        [string]$CurrentBepInExConfigPath,

        [switch]$IncludeBepInExConfig
    )

    $backupPath = New-BackupDirectory -Root $Root
    $currentSaveStats = Get-PathStats -Path $CurrentSavePath
    if ((Test-Path -LiteralPath $CurrentSavePath -PathType Container) -and $currentSaveStats.Files -gt 0) {
        Copy-Item -LiteralPath $CurrentSavePath -Destination $backupPath -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path (Join-Path $backupPath "Saved") | Out-Null
        New-Item -ItemType File -Path (Join-Path $backupPath ".saved-state-empty") | Out-Null
    }
    $vortexBackupPath = $null
    if ($IncludeMods -and $CurrentVortexPath -and (Test-Path -LiteralPath $CurrentVortexPath)) {
        $vortexBackupPath = Copy-VortexState -FromRoot $CurrentVortexPath -BackupPath $backupPath
    }

    $bepInExConfigBackupPath = $null
    if ($IncludeBepInExConfig -and $CurrentBepInExConfigPath -and (Test-Path -LiteralPath $CurrentBepInExConfigPath)) {
        $bepInExConfigBackupPath = Copy-BepInExConfigState -FromRoot $CurrentBepInExConfigPath -BackupPath $backupPath
    }

    Write-BackupInfo -BackupPath $backupPath -SourcePath $CurrentSavePath -Reason "Pre-restore safety backup before restoring: $RestoreSource" -VortexSourcePath $CurrentVortexPath -VortexBackupPath $vortexBackupPath -BepInExConfigSourcePath $CurrentBepInExConfigPath -BepInExConfigBackupPath $bepInExConfigBackupPath
    return $backupPath
}

function Resolve-BackupToRestore {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Backups
    )

    if ($BackupNumber -gt 0 -and $Latest) {
        throw "Use either -BackupNumber or -Latest, not both."
    }

    if ($BackupNumber -gt 0) {
        $match = @($Backups | Where-Object { $_.Number -eq $BackupNumber })
        if ($match.Count -eq 0) {
            throw "Backup number $BackupNumber was not found under $BackupRoot."
        }
        return $match[0]
    }

    if ($Latest) {
        if ($Backups.Count -eq 0) {
            throw "No backups were found under $BackupRoot."
        }
        return @($Backups | Sort-Object Number -Descending)[0]
    }

    throw "Choose a backup with -BackupNumber <number>, use -Latest, or use -List."
}

function Get-ShouldRestoreMods {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Backup
    )

    if ($RestoreMods -and ($SkipMods -or $SavesOnly)) {
        throw "Use either -RestoreMods, -SkipMods, or -SavesOnly, not more than one of those."
    }

    if (-not $Backup.HasVortexMods) {
        if ($RestoreMods) {
            throw "Backup '$($Backup.Name)' does not contain Vortex mod state. Choose a backup with HasVortexMods=True, or rerun without -RestoreMods."
        }

        return $false
    }

    if ($RestoreMods) {
        return $true
    }

    if ($SkipMods -or $SavesOnly) {
        return $false
    }

    $answer = Read-Host "Backup includes Vortex mods. Restore mods too? [y/N]"
    $normalized = $answer.Trim().ToLowerInvariant()
    return ($normalized -eq "y" -or $normalized -eq "yes")
}

function Get-ShouldRestoreBepInExConfig {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Backup
    )

    if ($RestoreBepInExConfig -and ($SkipBepInExConfig -or $SavesOnly)) {
        throw "Use either -RestoreBepInExConfig, -SkipBepInExConfig, or -SavesOnly, not more than one of those."
    }

    if (-not $Backup.HasBepInExConfig) {
        if ($RestoreBepInExConfig) {
            throw "Backup '$($Backup.Name)' does not contain BepInEx config state. Choose a backup with HasBepInExConfig=True, or rerun without -RestoreBepInExConfig."
        }

        return $false
    }

    if ($RestoreBepInExConfig) {
        return $true
    }

    if ($SkipBepInExConfig -or $SavesOnly) {
        return $false
    }

    $answer = Read-Host "Backup includes BepInEx config and FoA Mod Manager profiles. Restore them too? [y/N]"
    $normalized = $answer.Trim().ToLowerInvariant()
    return ($normalized -eq "y" -or $normalized -eq "yes")
}

function Restore-SavedFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromSavedPath,

        [Parameter(Mandatory = $true)]
        [string]$ToSavedPath
    )

    $targetPath = $ToSavedPath.TrimEnd($PathTrimChars)
    $parent = [System.IO.Path]::GetDirectoryName($targetPath)
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    $tempPath = Join-Path $parent ("Saved.pre-restore-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

    if (Test-Path -LiteralPath $targetPath) {
        Move-Item -LiteralPath $targetPath -Destination $tempPath
    }

    try {
        Copy-Item -LiteralPath $FromSavedPath -Destination $parent -Recurse -Force
        if (-not (Test-Path -LiteralPath $targetPath -PathType Container)) {
            throw "Restored live save folder was not created: $targetPath"
        }

        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Recurse -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $targetPath) {
            Remove-Item -LiteralPath $targetPath -Recurse -Force
        }

        if (Test-Path -LiteralPath $tempPath) {
            Move-Item -LiteralPath $tempPath -Destination $targetPath
        }

        throw
    }
}

function Restore-BepInExConfigState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromConfigPath,

        [Parameter(Mandatory = $true)]
        [string]$ToConfigPath
    )

    Assert-BepInExConfigFolderReady -Path $FromConfigPath -Label "Selected backup"

    $targetPath = $ToConfigPath.TrimEnd($PathTrimChars)
    $parent = [System.IO.Path]::GetDirectoryName($targetPath)
    $leaf = [System.IO.Path]::GetFileName($targetPath)
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    $tempPath = Join-Path $parent ($leaf + ".pre-restore-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

    if (Test-Path -LiteralPath $targetPath) {
        Move-Item -LiteralPath $targetPath -Destination $tempPath
    }

    try {
        Copy-DirectoryContents -FromRoot $FromConfigPath -ToRoot $targetPath | Out-Null
        Assert-BepInExConfigFolderReady -Path $targetPath -Label "Restored live"

        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Recurse -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $targetPath) {
            Remove-Item -LiteralPath $targetPath -Recurse -Force
        }

        if (Test-Path -LiteralPath $tempPath) {
            Move-Item -LiteralPath $tempPath -Destination $targetPath
        }

        throw
    }
}

function Restore-VortexState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromVortexPath,

        [Parameter(Mandatory = $true)]
        [string]$ToVortexPath
    )

    Assert-VortexFolderReady -Path $FromVortexPath -Label "Selected backup"

    $targetPath = $ToVortexPath.TrimEnd($PathTrimChars)
    $parent = [System.IO.Path]::GetDirectoryName($targetPath)
    $leaf = [System.IO.Path]::GetFileName($targetPath)
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }

    $tempPath = Join-Path $parent ($leaf + ".pre-restore-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

    if (Test-Path -LiteralPath $targetPath) {
        Move-Item -LiteralPath $targetPath -Destination $tempPath
    }

    try {
        New-Item -ItemType Directory -Path $targetPath | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $FromVortexPath -Force) {
            Copy-Item -LiteralPath $item.FullName -Destination $targetPath -Recurse -Force
        }

        Assert-VortexFolderReady -Path $targetPath -Label "Restored live"

        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Recurse -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $targetPath) {
            Remove-Item -LiteralPath $targetPath -Recurse -Force
        }

        if (Test-Path -LiteralPath $tempPath) {
            Move-Item -LiteralPath $tempPath -Destination $targetPath
        }
        throw
    }
}

$backups = @(Get-BackupDirectories -Root $BackupRoot)

if ($List) {
    if ($backups.Count -eq 0) {
        Write-Host "No Tainted Grail backups found under $BackupRoot."
        return
    }

    $backups | Select-Object Number, Date, FileCount, HasEmptySavedStateMarker, HasVortexMods, VortexFileCount, HasBepInExConfig, BepInExConfigFileCount, LastWriteTime, FullName | Format-Table -AutoSize
    return
}

Assert-GameNotRunning

$selectedBackup = Resolve-BackupToRestore -Backups $backups
Assert-SelectedBackupSavedFolderReady -Backup $selectedBackup
$shouldRestoreMods = Get-ShouldRestoreMods -Backup $selectedBackup
$shouldRestoreBepInExConfig = Get-ShouldRestoreBepInExConfig -Backup $selectedBackup
if ($shouldRestoreMods) {
    Assert-VortexNotRunning
    Assert-VortexFolderReady -Path $selectedBackup.VortexPath -Label "Selected backup"
}
if ($shouldRestoreBepInExConfig) {
    Assert-BepInExConfigFolderReady -Path $selectedBackup.BepInExConfigPath -Label "Selected backup"
}

$resolvedSaveRoot = $SaveRoot
if (Test-Path -LiteralPath $SaveRoot) {
    $resolvedSaveRoot = (Resolve-Path -LiteralPath $SaveRoot).Path
}

$resolvedVortexGameRoot = $VortexGameRoot
if (Test-Path -LiteralPath $VortexGameRoot) {
    $resolvedVortexGameRoot = (Resolve-Path -LiteralPath $VortexGameRoot).Path
}

$resolvedBepInExConfigRoot = $BepInExConfigRoot
if (Test-Path -LiteralPath $BepInExConfigRoot) {
    $resolvedBepInExConfigRoot = (Resolve-Path -LiteralPath $BepInExConfigRoot).Path
}

$safetyBackup = $null
$currentSaveStats = Get-PathStats -Path $resolvedSaveRoot
if ($currentSaveStats.Files -gt 0 -or $shouldRestoreMods -or $shouldRestoreBepInExConfig) {
    $safetyBackup = New-SafetyBackup -CurrentSavePath $resolvedSaveRoot -Root $BackupRoot -RestoreSource $selectedBackup.Name -CurrentVortexPath $resolvedVortexGameRoot -IncludeMods:$shouldRestoreMods -CurrentBepInExConfigPath $resolvedBepInExConfigRoot -IncludeBepInExConfig:$shouldRestoreBepInExConfig
}
Restore-SavedFolder -FromSavedPath $selectedBackup.SavedPath -ToSavedPath $resolvedSaveRoot
if ($shouldRestoreMods) {
    Restore-VortexState -FromVortexPath $selectedBackup.VortexPath -ToVortexPath $resolvedVortexGameRoot
}
if ($shouldRestoreBepInExConfig) {
    Restore-BepInExConfigState -FromConfigPath $selectedBackup.BepInExConfigPath -ToConfigPath $resolvedBepInExConfigRoot
}

$safetyBackupLog = "Skipped (no current live save files)"
if ($safetyBackup) {
    $safetyBackupLog = $safetyBackup
}

$restoreLog = @(
    "Tainted Grail: The Fall of Avalon save restore"
    "Restored: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
    "RestoredFrom: $($selectedBackup.FullName)"
    "LiveSavePath: $resolvedSaveRoot"
    "VortexRestoreRequested: $shouldRestoreMods"
    "LiveVortexPath: $resolvedVortexGameRoot"
    "BepInExConfigRestoreRequested: $shouldRestoreBepInExConfig"
    "LiveBepInExConfigPath: $resolvedBepInExConfigRoot"
    "PreRestoreSafetyBackup: $safetyBackupLog"
    "PreRestoreSafetyBackupSavedFiles: $($currentSaveStats.Files)"
    "AllowRunning: $AllowRunning"
)

Set-Content -LiteralPath (Join-Path $BackupRoot ("restore-log-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".txt")) -Value $restoreLog -Encoding UTF8

Write-Host "Restored from: $($selectedBackup.FullName)"
if ($safetyBackup) {
    if ($currentSaveStats.Files -gt 0) {
        Write-Host "Safety backup: $safetyBackup"
    }
    else {
        Write-Host "Safety backup: $safetyBackup (no current live save files; created to preserve Vortex or BepInEx config state)."
    }
}
else {
    Write-Host "Safety backup: skipped because the current live Saved folder had no files and no Vortex or BepInEx config state was restored."
}
if ($shouldRestoreMods) {
    Write-Host "Restored Vortex mods: $resolvedVortexGameRoot"
}
if ($shouldRestoreBepInExConfig) {
    Write-Host "Restored BepInEx config: $resolvedBepInExConfigRoot"
}

if ($OpenDestination) {
    Invoke-Item -LiteralPath $BackupRoot
}
