Set-StrictMode -Version Latest

function Test-GrailwrightJsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object -ne $null -and $Object.PSObject.Properties.Name -contains $Name
}

function Get-GrailwrightDefaultRepoRoot {
    $toolsRoot = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($toolsRoot)) {
        $toolsRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $toolsRoot ".."))
}

function ConvertTo-GrailwrightLockName {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name.ToLowerInvariant() -replace '[^a-z0-9._-]+', "-"
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe lock name from '$Name'."
    }

    return $safe
}

function Get-GrailwrightLockRoot {
    param([string]$RepoRoot = "")

    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        $RepoRoot = Get-GrailwrightDefaultRepoRoot
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ".codex-temp\locks"))
}

function Read-GrailwrightLockOwner {
    param([Parameter(Mandatory = $true)][string]$LockPath)

    $ownerPath = Join-Path $LockPath "owner.json"
    if (-not (Test-Path -LiteralPath $ownerPath -PathType Leaf)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Format-GrailwrightLockOwner {
    param(
        [object]$Owner,
        [string]$LockPath
    )

    if ($Owner -eq $null) {
        $item = Get-Item -LiteralPath $LockPath -ErrorAction SilentlyContinue
        if ($item -eq $null) {
            return "No owner metadata is available."
        }

        return "No owner metadata is available. Lock directory timestamp: $($item.LastWriteTime.ToString("o"))."
    }

    $parts = New-Object "System.Collections.Generic.List[string]"
    foreach ($name in @("Action", "Mod", "StartedAt", "UserName", "MachineName", "ProcessId")) {
        if (Test-GrailwrightJsonProperty -Object $Owner -Name $name -and -not [string]::IsNullOrWhiteSpace([string]$Owner.$name)) {
            $parts.Add(("{0}={1}" -f $name, [string]$Owner.$name))
        }
    }

    if ($parts.Count -eq 0) {
        return "Owner metadata exists but does not contain recognizable fields."
    }

    return ($parts -join ", ")
}

function Test-GrailwrightStaleLock {
    param(
        [Parameter(Mandatory = $true)][string]$LockPath,
        [int]$StaleAfterMinutes = 720
    )

    if ($StaleAfterMinutes -le 0) {
        return $false
    }

    $threshold = [DateTime]::UtcNow.AddMinutes(-1 * $StaleAfterMinutes)
    $owner = Read-GrailwrightLockOwner -LockPath $LockPath
    if ($owner -ne $null -and (Test-GrailwrightJsonProperty -Object $owner -Name "StartedAt")) {
        $startedAt = [DateTimeOffset]::MinValue
        if ([DateTimeOffset]::TryParse([string]$owner.StartedAt, [ref]$startedAt)) {
            return $startedAt.UtcDateTime -lt $threshold
        }
    }

    $item = Get-Item -LiteralPath $LockPath -ErrorAction SilentlyContinue
    return $item -ne $null -and $item.LastWriteTimeUtc -lt $threshold
}

function New-GrailwrightLockConflictMessage {
    param(
        [Parameter(Mandatory = $true)][string]$LockName,
        [Parameter(Mandatory = $true)][string]$LockPath
    )

    $owner = Read-GrailwrightLockOwner -LockPath $LockPath
    $ownerText = Format-GrailwrightLockOwner -Owner $owner -LockPath $LockPath
    return "Grailwright lock '$LockName' is already held at $LockPath. $ownerText Use -LockWaitSeconds to wait, or -ForceStaleLock only after confirming the owner is gone."
}

function Enter-GrailwrightLock {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Action = "",
        [string]$Mod = "",
        [string]$RepoRoot = "",
        [int]$TimeoutSeconds = 0,
        [int]$StaleAfterMinutes = 720,
        [switch]$ForceStaleLock
    )

    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        $RepoRoot = Get-GrailwrightDefaultRepoRoot
    }

    $lockRoot = Get-GrailwrightLockRoot -RepoRoot $RepoRoot
    New-Item -ItemType Directory -Force -Path $lockRoot | Out-Null

    $lockName = ConvertTo-GrailwrightLockName -Name $Name
    $lockPath = Join-Path $lockRoot "$lockName.lock"
    $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(0, $TimeoutSeconds))
    $ownerId = [System.Guid]::NewGuid().ToString("N")

    while ($true) {
        try {
            New-Item -ItemType Directory -Path $lockPath -ErrorAction Stop | Out-Null

            $owner = [pscustomobject]@{
                LockName = $lockName
                Action = $Action
                Mod = $Mod
                StartedAt = (Get-Date).ToString("o")
                OwnerId = $ownerId
                UserName = [Environment]::UserName
                MachineName = [Environment]::MachineName
                ProcessId = $PID
                RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
            }

            $owner | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $lockPath "owner.json") -Encoding UTF8

            return [pscustomobject]@{
                Name = $lockName
                Path = [System.IO.Path]::GetFullPath($lockPath)
                OwnerId = $ownerId
            }
        } catch {
            if (-not (Test-Path -LiteralPath $lockPath -PathType Container)) {
                throw
            }

            if ($ForceStaleLock -and (Test-GrailwrightStaleLock -LockPath $lockPath -StaleAfterMinutes $StaleAfterMinutes)) {
                Remove-Item -LiteralPath $lockPath -Recurse -Force
                continue
            }

            if ($TimeoutSeconds -gt 0 -and [DateTime]::UtcNow -lt $deadline) {
                Start-Sleep -Seconds 1
                continue
            }

            throw (New-GrailwrightLockConflictMessage -LockName $lockName -LockPath $lockPath)
        }
    }
}

function Exit-GrailwrightLock {
    param([object]$Lock)

    if ($Lock -eq $null -or -not (Test-GrailwrightJsonProperty -Object $Lock -Name "Path")) {
        return
    }

    $lockPath = [string]$Lock.Path
    if ([string]::IsNullOrWhiteSpace($lockPath) -or -not (Test-Path -LiteralPath $lockPath -PathType Container)) {
        return
    }

    $owner = Read-GrailwrightLockOwner -LockPath $lockPath
    if ($owner -ne $null -and
        (Test-GrailwrightJsonProperty -Object $owner -Name "OwnerId") -and
        (Test-GrailwrightJsonProperty -Object $Lock -Name "OwnerId") -and
        [string]$owner.OwnerId -ne [string]$Lock.OwnerId) {
        Write-Warning "Skipping release for Grailwright lock '$($Lock.Name)' because its owner metadata changed."
        return
    }

    Remove-Item -LiteralPath $lockPath -Recurse -Force
}
