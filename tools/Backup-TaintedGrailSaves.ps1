[CmdletBinding()]
param(
    [string]$Source = "$env:USERPROFILE\AppData\LocalLow\Questline\Fall of Avalon\77198420\Saved",
    [string]$DestinationRoot = "Z:\Backup\Tainted Grail",
    [string]$VortexGameRoot = "$env:APPDATA\Vortex\taintedgrailthefallofavalon",
    [string]$BepInExConfigRoot = "G:\Steam\steamapps\common\Tainted Grail FoA\BepInEx\config",
    [string]$GrailwrightRoot = "C:\Repositories\Grailwright",
    [string]$Reason = "Manual backup",
    [switch]$NoMods,
    [switch]$NoBepInExConfig,
    [switch]$NoGrailwright,
    [switch]$AllowRunning,
    [switch]$OpenDestination,
    [switch]$NoPauseOnError,
    [switch]$NoPauseOnSuccess
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BackupPrefix = "Tainted Grail Save Backup"
$VortexSubfolders = @("mods", "profiles", "snapshots")
$script:SkippedItems = @()

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

function Get-NextBackupNumber {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $highest = 0

    if (Test-Path -LiteralPath $Root) {
        foreach ($directory in Get-ChildItem -LiteralPath $Root -Directory) {
            if ($directory.Name -match "^$([regex]::Escape($BackupPrefix)) (?<number>\d+) - \d{1,2}-\d{1,2}-\d{4}$") {
                $highest = [Math]::Max($highest, [int]$Matches["number"])
            }
        }
    }

    return $highest + 1
}

function New-BackupTarget {
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
        $finalPath = Join-Path $Root $folderName
        if (Test-Path -LiteralPath $finalPath) {
            $number++
        }
    } while (Test-Path -LiteralPath $finalPath)

    $stagingPath = Join-Path $Root (".backup-in-progress-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $stagingPath | Out-Null

    return [pscustomobject]@{
        FinalPath = $finalPath
        StagingPath = $stagingPath
    }
}

function Complete-BackupTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StagingPath,

        [Parameter(Mandatory = $true)]
        [string]$FinalPath
    )

    if (Test-Path -LiteralPath $FinalPath) {
        throw "Final backup folder already exists: $FinalPath"
    }

    Move-Item -LiteralPath $StagingPath -Destination $FinalPath
    return $FinalPath
}

function Assert-SourceReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Save source was not found: $Path"
    }

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force)
    if ($files.Count -eq 0) {
        throw "Save source exists but contains no files: $Path"
    }
}

function Assert-VortexReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Vortex Tainted Grail folder was not found: $Path. Rerun with -NoMods to back up saves only."
    }

    $existingSections = @()
    foreach ($subfolder in $VortexSubfolders) {
        $sectionPath = Join-Path $Path $subfolder
        if (Test-Path -LiteralPath $sectionPath) {
            $existingSections += $subfolder
        }
    }

    if ($existingSections.Count -eq 0) {
        throw "Vortex folder exists but none of these sections were found: $($VortexSubfolders -join ', '). Rerun with -NoMods to back up saves only."
    }
}

function Assert-BepInExConfigReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "BepInEx config folder was not found: $Path. Rerun with -NoBepInExConfig to back up saves and mods only."
    }

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse -Force)
    if ($files.Count -eq 0) {
        throw "BepInEx config folder exists but contains no files: $Path. Rerun with -NoBepInExConfig to back up saves and mods only."
    }
}

function Assert-GrailwrightReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Grailwright repository was not found: $Path. Rerun with -NoGrailwright to back up saves, mods, and config only."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Path ".git"))) {
        throw "Grailwright path exists but does not look like a git repository: $Path. Rerun with -NoGrailwright to back up saves, mods, and config only."
    }
}

function Assert-GameNotRunning {
    if ($AllowRunning) {
        return
    }

    $process = Get-Process -Name "Fall of Avalon" -ErrorAction SilentlyContinue
    if ($process) {
        $ids = ($process | ForEach-Object { $_.Id }) -join ", "
        throw "Fall of Avalon appears to be running (PID $ids). Quit or pause away from saving, or rerun with -AllowRunning."
    }
}

function Assert-VortexNotRunning {
    if ($AllowRunning -or $NoMods) {
        return
    }

    $process = Get-Process -Name "Vortex" -ErrorAction SilentlyContinue
    if ($process) {
        $ids = ($process | ForEach-Object { $_.Id }) -join ", "
        throw "Vortex appears to be running (PID $ids). Quit Vortex, or rerun with -AllowRunning or -NoMods."
    }
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
        Copy-ItemTree -Item $item -DestinationRoot $ToRoot
    }

    return $ToRoot
}

function Test-ReparsePointTargetAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo]$Item
    )

    if (-not (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq [System.IO.FileAttributes]::ReparsePoint)) {
        return $true
    }

    if ($Item.PSIsContainer) {
        $script:SkippedItems += "Skipped reparse-point directory: $($Item.FullName)"
        return $false
    }

    $targets = @($Item.Target)
    if ($targets.Count -eq 0) {
        return $true
    }

    foreach ($target in $targets) {
        if ([string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        $targetPath = $target
        if (-not [System.IO.Path]::IsPathRooted($targetPath)) {
            $targetPath = Join-Path $Item.DirectoryName $targetPath
        }

        if (Test-Path -LiteralPath $targetPath) {
            return $true
        }
    }

    $script:SkippedItems += "Skipped broken link: $($Item.FullName) -> $($targets -join ', ')"
    return $false
}

function Copy-ItemTree {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo]$Item,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    if (-not (Test-ReparsePointTargetAvailable -Item $Item)) {
        return
    }

    $destinationPath = Join-Path $DestinationRoot $Item.Name

    if ($Item.PSIsContainer) {
        New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
        foreach ($child in Get-ChildItem -LiteralPath $Item.FullName -Force) {
            Copy-ItemTree -Item $child -DestinationRoot $destinationPath
        }
        return
    }

    Copy-Item -LiteralPath $Item.FullName -Destination $destinationPath -Force
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
            Copy-ItemTree -Item (Get-Item -LiteralPath $sectionPath -Force) -DestinationRoot $vortexBackupPath
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

function Copy-GrailwrightState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FromRoot,

        [Parameter(Mandatory = $true)]
        [string]$BackupPath
    )

    $repoBackupPath = Join-Path $BackupPath "Grailwright"
    Copy-Item -LiteralPath $FromRoot -Destination $repoBackupPath -Recurse -Force
    return $repoBackupPath
}

function Write-BackupInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupPath,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [string]$VortexSourcePath,

        [string]$VortexBackupPath,

        [string]$BepInExConfigSourcePath,

        [string]$BepInExConfigBackupPath,

        [string]$GrailwrightSourcePath,

        [string]$GrailwrightBackupPath,

        [string]$DestinationPath
    )

    if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
        $DestinationPath = $BackupPath
    }

    $saveStats = Get-PathStats -Path $SourcePath
    $vortexStats = Get-PathStats -Path $VortexBackupPath
    $bepInExConfigStats = Get-PathStats -Path $BepInExConfigBackupPath
    $grailwrightStats = Get-PathStats -Path $GrailwrightBackupPath

    $info = @(
        "Tainted Grail: The Fall of Avalon save backup"
        "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
        "SaveSource: $SourcePath"
        "VortexSource: $VortexSourcePath"
        "BepInExConfigSource: $BepInExConfigSourcePath"
        "Destination: $DestinationPath"
        "Reason: $Reason"
        "SavedFiles: $($saveStats.Files)"
        "SavedBytes: $($saveStats.Bytes)"
        "VortexIncluded: $(-not $NoMods)"
        "VortexFiles: $($vortexStats.Files)"
        "VortexBytes: $($vortexStats.Bytes)"
        "BepInExConfigIncluded: $([bool]$BepInExConfigBackupPath)"
        "BepInExConfigFiles: $($bepInExConfigStats.Files)"
        "BepInExConfigBytes: $($bepInExConfigStats.Bytes)"
        "GrailwrightIncluded: $([bool]$GrailwrightBackupPath)"
        "GrailwrightSource: $GrailwrightSourcePath"
        "GrailwrightFiles: $($grailwrightStats.Files)"
        "GrailwrightBytes: $($grailwrightStats.Bytes)"
        "SkippedItems: $($script:SkippedItems.Count)"
        "AllowRunning: $AllowRunning"
    )

    foreach ($skippedItem in $script:SkippedItems) {
        $info += "SkippedItem: $skippedItem"
    }

    Set-Content -LiteralPath (Join-Path $BackupPath "backup-info.txt") -Value $info -Encoding UTF8
}

function Wait-BeforeErrorExit {
    if ($NoPauseOnError) {
        return
    }

    if (-not [Environment]::UserInteractive) {
        return
    }

    try {
        if ([Console]::IsInputRedirected) {
            return
        }
    }
    catch {
        return
    }

    Write-Host ""
    [void](Read-Host "Press Enter to exit")
}

function Wait-BeforeSuccessExit {
    if ($NoPauseOnSuccess) {
        return
    }

    if (-not [Environment]::UserInteractive) {
        return
    }

    try {
        if ([Console]::IsInputRedirected) {
            return
        }
    }
    catch {
        return
    }

    Write-Host ""
    [void](Read-Host "Press Enter to close")
}

try {
$resolvedSource = (Resolve-Path -LiteralPath $Source).Path
$resolvedVortexGameRoot = $null
$resolvedBepInExConfigRoot = $null
$resolvedGrailwrightRoot = $null

Assert-SourceReady -Path $resolvedSource
Assert-GameNotRunning
Assert-VortexNotRunning

if (-not $NoMods) {
    Assert-VortexReady -Path $VortexGameRoot
    $resolvedVortexGameRoot = (Resolve-Path -LiteralPath $VortexGameRoot).Path
}

if (-not $NoBepInExConfig) {
    Assert-BepInExConfigReady -Path $BepInExConfigRoot
    $resolvedBepInExConfigRoot = (Resolve-Path -LiteralPath $BepInExConfigRoot).Path
}

if (-not $NoGrailwright) {
    Assert-GrailwrightReady -Path $GrailwrightRoot
    $resolvedGrailwrightRoot = (Resolve-Path -LiteralPath $GrailwrightRoot).Path
}

$backupTarget = $null
$bepInExConfigBackupPath = $null
$grailwrightBackupPath = $null
try {
    $backupTarget = New-BackupTarget -Root $DestinationRoot
    Copy-Item -LiteralPath $resolvedSource -Destination $backupTarget.StagingPath -Recurse -Force
    $vortexBackupPath = $null
    if (-not $NoMods) {
        $vortexBackupPath = Copy-VortexState -FromRoot $resolvedVortexGameRoot -BackupPath $backupTarget.StagingPath
    }

    if (-not $NoBepInExConfig) {
        $bepInExConfigBackupPath = Copy-BepInExConfigState -FromRoot $resolvedBepInExConfigRoot -BackupPath $backupTarget.StagingPath
    }

    if (-not $NoGrailwright) {
        $grailwrightBackupPath = Copy-GrailwrightState -FromRoot $resolvedGrailwrightRoot -BackupPath $backupTarget.StagingPath
    }

    Write-BackupInfo -BackupPath $backupTarget.StagingPath -SourcePath $resolvedSource -VortexSourcePath $resolvedVortexGameRoot -VortexBackupPath $vortexBackupPath -BepInExConfigSourcePath $resolvedBepInExConfigRoot -BepInExConfigBackupPath $bepInExConfigBackupPath -GrailwrightSourcePath $resolvedGrailwrightRoot -GrailwrightBackupPath $grailwrightBackupPath -DestinationPath $backupTarget.FinalPath
    Set-Content -LiteralPath (Join-Path $backupTarget.StagingPath ".backup-complete") -Value @("Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')") -Encoding UTF8

    $backupPath = Complete-BackupTarget -StagingPath $backupTarget.StagingPath -FinalPath $backupTarget.FinalPath
}
catch {
    if ($backupTarget -and (Test-Path -LiteralPath $backupTarget.StagingPath)) {
        Remove-Item -LiteralPath $backupTarget.StagingPath -Recurse -Force
    }

    throw
}

Write-Host "Created backup: $backupPath"
if ($vortexBackupPath) {
    Write-Host "Included Vortex mods: $(Join-Path $backupPath 'Vortex')"
}
if ($bepInExConfigBackupPath) {
    Write-Host "Included BepInEx config: $(Join-Path $backupPath 'BepInExConfig')"
}
if ($grailwrightBackupPath) {
    Write-Host "Included Grailwright repo: $(Join-Path $backupPath 'Grailwright')"
}

if ($OpenDestination) {
    Invoke-Item -LiteralPath $backupPath
}

Write-Host ""
Write-Host "Backup completed successfully." -ForegroundColor Green
Wait-BeforeSuccessExit
}
catch {
    Write-Host ""
    Write-Host "Backup failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.InvocationInfo -and $_.InvocationInfo.PositionMessage) {
        Write-Host $_.InvocationInfo.PositionMessage -ForegroundColor DarkRed
    }
    Wait-BeforeErrorExit
    exit 1
}
