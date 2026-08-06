[CmdletBinding(DefaultParameterSetName = "Update")]
param(
    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [string]$Mod = "",

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [string]$ModRoot = "",

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [string]$NexusUrl = "",

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [string]$ProfileRoot = "",

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [ValidateSet("Chrome")]
    [string]$Browser = "Chrome",

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [int]$RemoteDebuggingPort = 9334,

    [Parameter(ParameterSetName = "Login")]
    [switch]$LoginOnly,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Revert")]
    [switch]$Save,

    [Parameter(ParameterSetName = "Update")]
    [switch]$UseTestContent,

    [Parameter(ParameterSetName = "Update")]
    [switch]$ForceSave,

    [Parameter(ParameterSetName = "Update")]
    [string]$ShortDescription = "",

    [Parameter(ParameterSetName = "Update")]
    [string]$FullDescriptionPath = "",

    [Parameter(ParameterSetName = "Revert")]
    [string]$BackupPath = "",

    [Parameter(ParameterSetName = "Revert")]
    [switch]$RevertLatest,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [switch]$KeepOpen,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [int]$TimeoutSeconds = 180,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [int]$LockWaitSeconds = 0,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [int]$LockStaleAfterMinutes = 720,

    [Parameter(ParameterSetName = "Update")]
    [Parameter(ParameterSetName = "Login")]
    [Parameter(ParameterSetName = "Revert")]
    [switch]$ForceStaleLock
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$NodeToolRoot = Join-Path $RepoRoot ".codex-temp\nexus-description-tool"
$RequestRoot = Join-Path $RepoRoot ".codex-temp\nexus-description-requests"
$BackupRoot = Join-Path $RepoRoot ".codex-temp\nexus-description-backups"
$ScriptPath = Join-Path $PSScriptRoot "nexus\update-nexus-description.mjs"
$LockScript = Join-Path $PSScriptRoot "Lock-Operation.ps1"
if (-not (Test-Path -LiteralPath $LockScript -PathType Leaf)) {
    throw "Missing lock helper: $LockScript"
}

. $LockScript

function Test-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object -ne $null -and $Object.PSObject.Properties.Name -contains $Name
}

function Resolve-ModRoot {
    param(
        [string]$RequestedMod,
        [string]$RequestedModRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedModRoot)) {
        $resolved = [System.IO.Path]::GetFullPath($RequestedModRoot)
        if (-not (Test-Path -LiteralPath (Join-Path $resolved "mod.json") -PathType Leaf)) {
            throw "Missing mod.json under ModRoot: $resolved"
        }

        return $resolved
    }

    if ([string]::IsNullOrWhiteSpace($RequestedMod)) {
        throw "Pass -Mod or -ModRoot."
    }

    $matches = New-Object "System.Collections.Generic.List[string]"
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepoRoot "mods") -Recurse -File -Filter "mod.json") {
        $root = Split-Path -Parent $file.FullName
        $manifest = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $names = @(
            $manifest.id,
            $manifest.displayName,
            $manifest.packageName,
            (Split-Path -Leaf $root)
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

        if ($names | Where-Object { $_ -ieq $RequestedMod }) {
            $matches.Add($root)
        }
    }

    if ($matches.Count -eq 0) {
        throw "Could not find mod manifest matching '$RequestedMod'."
    }

    if ($matches.Count -gt 1) {
        throw "Multiple mod manifests match '$RequestedMod': $($matches -join ', ')"
    }

    return [System.IO.Path]::GetFullPath($matches[0])
}

function Read-LocalApiSettings {
    param([string]$Root)

    $path = Join-Path $Root "API.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{}
    }

    $settings = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separator = $trimmed.IndexOf("=")
        if ($separator -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            $settings[$key] = $value
        }
    }

    foreach ($secretName in @("apiKey", "apikey", "token", "secret", "bearerToken", "password", "nexusApiKey")) {
        if ($settings.ContainsKey($secretName)) {
            throw "Refusing to read Nexus secret '$secretName' from $path. Store secrets in NEXUS_API_KEY only."
        }
    }

    return [pscustomobject]$settings
}

function Read-ModManifest {
    param([string]$Root)

    $path = Join-Path $Root "mod.json"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing mod manifest: $path"
    }

    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-NexusUrl {
    param(
        [string]$RequestedUrl,
        [object]$ApiSettings,
        [object]$Manifest
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedUrl)) {
        return $RequestedUrl
    }

    foreach ($name in @("NexusUrl", "nexusUrl", "url", "Url")) {
        if (Test-JsonProperty -Object $ApiSettings -Name $name -and -not [string]::IsNullOrWhiteSpace([string]$ApiSettings.$name)) {
            return [string]$ApiSettings.$name
        }
    }

    if (Test-JsonProperty -Object $Manifest -Name "nexus") {
        foreach ($name in @("NexusUrl", "nexusUrl", "url", "Url")) {
            if (Test-JsonProperty -Object $Manifest.nexus -Name $name -and -not [string]::IsNullOrWhiteSpace([string]$Manifest.nexus.$name)) {
                return [string]$Manifest.nexus.$name
            }
        }
    }

    throw "Could not resolve NexusUrl. Pass -NexusUrl or add NexusUrl to API.txt."
}

function Get-MetadataText {
    param(
        [string]$Root,
        [string]$FileName,
        [int]$MaximumLength = 0
    )

    $path = Join-Path $Root $FileName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing Nexus metadata file: $path"
    }

    $text = ([System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)).Trim()
    if ($MaximumLength -gt 0 -and $text.Length -gt $MaximumLength) {
        throw "$FileName is $($text.Length) characters, over limit $MaximumLength`: $path"
    }

    return $text
}

function Ensure-PlaywrightTooling {
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        throw "Node.js is required for the Nexus browser updater."
    }

    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw "npm is required for the Nexus browser updater."
    }

    if (-not (Test-Path -LiteralPath $NodeToolRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $NodeToolRoot | Out-Null
    }

    $packageJson = Join-Path $NodeToolRoot "package.json"
    if (-not (Test-Path -LiteralPath $packageJson -PathType Leaf)) {
        Push-Location $NodeToolRoot
        try {
            npm init -y | Out-Null
        }
        finally {
            Pop-Location
        }
    }

    $playwrightPackage = Join-Path $NodeToolRoot "node_modules\playwright\package.json"
    if (-not (Test-Path -LiteralPath $playwrightPackage -PathType Leaf)) {
        Push-Location $NodeToolRoot
        try {
            npm install playwright --no-audit --no-fund
        }
        finally {
            Pop-Location
        }
    }
}

function Find-BrowserExecutable {
    param([string]$RequestedBrowser)

    $candidates = New-Object "System.Collections.Generic.List[string]"
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Google\Chrome\Application\chrome.exe"),
        (Join-Path $env:LocalAppData "Google\Chrome\Application\chrome.exe")
    )) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $candidates.Add($candidate)
        }
    }

    $found = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($found)) {
        throw "Could not find Chrome. Install Google Chrome before running the Nexus description updater."
    }

    return $found
}

function Test-CdpEndpoint {
    param([int]$Port)

    try {
        $response = Invoke-RestMethod -Method Get -Uri ("http://127.0.0.1:{0}/json/version" -f $Port) -TimeoutSec 1
        return $response -ne $null
    }
    catch {
        return $false
    }
}

function Start-CdpBrowser {
    param(
        [string]$RequestedBrowser,
        [string]$UserDataDir,
        [int]$Port,
        [string]$StartUrl
    )

    if (Test-CdpEndpoint -Port $Port) {
        return
    }

    if (-not (Test-Path -LiteralPath $UserDataDir -PathType Container)) {
        New-Item -ItemType Directory -Path $UserDataDir | Out-Null
    }

    $browserExe = Find-BrowserExecutable -RequestedBrowser $RequestedBrowser
    $arguments = @(
        "--remote-debugging-address=127.0.0.1",
        "--remote-debugging-port=$Port",
        "--user-data-dir=$UserDataDir",
        "--no-first-run",
        "--disable-background-mode",
        "--hide-crash-restore-bubble",
        "--window-position=198,198",
        "--window-size=1300,1044",
        "--new-window",
        $StartUrl
    )

    Start-Process -FilePath $browserExe -ArgumentList $arguments | Out-Null

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        if (Test-CdpEndpoint -Port $Port) {
            return
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Started $RequestedBrowser, but its remote debugging endpoint did not appear on 127.0.0.1:$Port."
}

function Get-IsolatedCdpBrowserProcesses {
    param(
        [string]$ProcessName,
        [string]$UserDataDir,
        [int]$Port
    )

    $fullUserDataDir = [System.IO.Path]::GetFullPath($UserDataDir)
    $cimProcesses = Get-CimInstance Win32_Process -Filter "Name = '$ProcessName'" -ErrorAction SilentlyContinue |
        Where-Object {
            $commandLine = [string]$_.CommandLine
            $commandLine.IndexOf("--remote-debugging-port=$Port", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $commandLine.IndexOf($fullUserDataDir, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        }

    foreach ($cimProcess in $cimProcesses) {
        Get-Process -Id $cimProcess.ProcessId -ErrorAction SilentlyContinue
    }
}

function Stop-IsolatedCdpBrowser {
    param(
        [string]$RequestedBrowser,
        [string]$UserDataDir,
        [int]$Port
    )

    $processName = "chrome.exe"

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((Test-CdpEndpoint -Port $Port) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-CdpEndpoint -Port $Port)) {
        return
    }

    $processes = @(Get-IsolatedCdpBrowserProcesses -ProcessName $processName -UserDataDir $UserDataDir -Port $Port)
    foreach ($process in $processes) {
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $process.CloseMainWindow() | Out-Null
        }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((Test-CdpEndpoint -Port $Port) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-CdpEndpoint -Port $Port)) {
        return
    }

    $remaining = @(Get-IsolatedCdpBrowserProcesses -ProcessName $processName -UserDataDir $UserDataDir -Port $Port)
    if ($remaining.Count -gt 0) {
        Write-Warning "Chrome did not exit after DevTools and window-close requests; forcing the isolated browser process to stop."
        foreach ($process in $remaining) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-LatestBackupPath {
    param(
        [string]$Root,
        [string]$PackageName
    )

    $path = Join-Path $Root $PackageName
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "No backup directory exists for ${PackageName}: $path"
    }

    $backup = Get-ChildItem -LiteralPath $path -File -Filter "*.json" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($backup -eq $null) {
        throw "No backup JSON files found under $path"
    }

    return $backup.FullName
}

if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
    throw "Missing browser implementation: $ScriptPath"
}

$resolvedModRoot = Resolve-ModRoot -RequestedMod $Mod -RequestedModRoot $ModRoot
$nexusLockModName = if ([string]::IsNullOrWhiteSpace($Mod)) { Split-Path -Leaf $resolvedModRoot } else { $Mod }
$nexusLock = Enter-GrailwrightLock -Name "nexus" -Action "update-nexus-description" -Mod $nexusLockModName -RepoRoot $RepoRoot -TimeoutSeconds $LockWaitSeconds -StaleAfterMinutes $LockStaleAfterMinutes -ForceStaleLock:$ForceStaleLock

try {
$manifest = Read-ModManifest -Root $resolvedModRoot
$apiSettings = Read-LocalApiSettings -Root $resolvedModRoot
$resolvedNexusUrl = Get-NexusUrl -RequestedUrl $NexusUrl -ApiSettings $apiSettings -Manifest $manifest

if ([string]::IsNullOrWhiteSpace($ProfileRoot)) {
    $ProfileRoot = Join-Path $RepoRoot ".codex-temp\nexus-browser-profile-chrome"
}

if (-not (Test-Path -LiteralPath $RequestRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $RequestRoot | Out-Null
}

if (-not (Test-Path -LiteralPath $BackupRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $BackupRoot | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageName = if (Test-JsonProperty -Object $manifest -Name "packageName" -and -not [string]::IsNullOrWhiteSpace([string]$manifest.packageName)) {
    [string]$manifest.packageName
}
else {
    Split-Path -Leaf $resolvedModRoot
}

$action = "review"
$desiredShort = ""
$desiredFull = ""
$restoreBackupPath = ""

if ($LoginOnly) {
    $action = "login"
}
elseif ($RevertLatest -or -not [string]::IsNullOrWhiteSpace($BackupPath)) {
    $action = if ($Save) { "revert-save" } else { "revert-review" }
    if ([string]::IsNullOrWhiteSpace($BackupPath)) {
        $restoreBackupPath = Get-LatestBackupPath -Root $BackupRoot -PackageName $packageName
    }
    else {
        $restoreBackupPath = [System.IO.Path]::GetFullPath($BackupPath)
    }

    if (-not (Test-Path -LiteralPath $restoreBackupPath -PathType Leaf)) {
        throw "BackupPath not found: $restoreBackupPath"
    }
}
else {
    $action = if ($Save) { "save" } else { "review" }

    if ($UseTestContent) {
        $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
        $desiredShort = "TEST ONLY - Nexus description updater verification $stamp"
        $desiredFull = @"
[center][color=#C94A4A][size=6]TEST ONLY[/size][/color]
[i]Nexus description updater verification.[/i][/center]

[line]

This temporary description was written by the Grailwright Nexus description updater at $stamp.

It is safe to revert using the backup JSON path printed by the tool.
"@.Trim()
    }
    else {
        $desiredShort = if ([string]::IsNullOrWhiteSpace($ShortDescription)) {
            Get-MetadataText -Root $resolvedModRoot -FileName "nexus-short-desc.txt" -MaximumLength 350
        }
        else {
            $ShortDescription.Trim()
        }

        if ($desiredShort.Length -gt 350) {
            throw "ShortDescription is $($desiredShort.Length) characters, over Nexus limit 350."
        }

        $fullPath = if ([string]::IsNullOrWhiteSpace($FullDescriptionPath)) {
            Join-Path $resolvedModRoot "nexus-full-desc.txt"
        }
        else {
            [System.IO.Path]::GetFullPath($FullDescriptionPath)
        }

        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "FullDescriptionPath not found: $fullPath"
        }

        $desiredFull = ([System.IO.File]::ReadAllText($fullPath, [System.Text.Encoding]::UTF8)).Trim()
    }
}

$request = [pscustomobject]@{
    action = $action
    repoRoot = $RepoRoot
    modRoot = $resolvedModRoot
    packageName = $packageName
    displayName = [string]$manifest.displayName
    nexusUrl = $resolvedNexusUrl
    profileRoot = [System.IO.Path]::GetFullPath($ProfileRoot)
    backupRoot = [System.IO.Path]::GetFullPath($BackupRoot)
    browser = $Browser
    remoteDebuggingPort = $RemoteDebuggingPort
    desiredShortDescription = $desiredShort
    desiredFullDescription = $desiredFull
    forceSave = [bool]$ForceSave
    restoreBackupPath = $restoreBackupPath
    keepOpen = [bool]$KeepOpen
    timeoutSeconds = $TimeoutSeconds
    requestedAt = (Get-Date).ToString("o")
}

$requestPath = Join-Path $RequestRoot ("nexus-description-request-{0}.json" -f $timestamp)
$request | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $requestPath -Encoding UTF8

Ensure-PlaywrightTooling

$startUrl = if ($LoginOnly) { "https://www.nexusmods.com/" } else { $resolvedNexusUrl }
Start-CdpBrowser -RequestedBrowser $Browser -UserDataDir ([System.IO.Path]::GetFullPath($ProfileRoot)) -Port $RemoteDebuggingPort -StartUrl $startUrl

$oldToolRoot = $env:NEXUS_DESCRIPTION_TOOL_ROOT
$env:NEXUS_DESCRIPTION_TOOL_ROOT = $NodeToolRoot

try {
    & node $ScriptPath --request $requestPath
    if ($LASTEXITCODE -ne 0) {
        throw "Nexus description updater failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (-not $KeepOpen) {
        Stop-IsolatedCdpBrowser -RequestedBrowser $Browser -UserDataDir ([System.IO.Path]::GetFullPath($ProfileRoot)) -Port $RemoteDebuggingPort
    }

    if ($null -eq $oldToolRoot) {
        Remove-Item Env:NEXUS_DESCRIPTION_TOOL_ROOT -ErrorAction SilentlyContinue
    }
    else {
        $env:NEXUS_DESCRIPTION_TOOL_ROOT = $oldToolRoot
    }
}
} finally {
    Exit-GrailwrightLock -Lock $nexusLock
}
