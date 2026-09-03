[CmdletBinding()]
param(
    [switch]$ValidateOnly,
    [switch]$PrepareOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$taskName = 'Grailwright Launch Tainted Grail'
$gameExecutable = 'G:\Steam\steamapps\common\Tainted Grail FoA\Fall of Avalon.exe'
if (-not (Test-Path -LiteralPath $gameExecutable -PathType Leaf)) {
    throw "The Tainted Grail executable does not exist: $gameExecutable"
}
$gameDirectory = Split-Path -Parent $gameExecutable
$identityName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $gameExecutable -WorkingDirectory $gameDirectory
$principal = New-ScheduledTaskPrincipal -UserId $identityName -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
$taskDefinition = New-ScheduledTask -Action $action -Principal $principal -Settings $settings

if ($ValidateOnly) {
    [pscustomobject]@{
        GameExecutable = $gameExecutable
        WorkingDirectory = $gameDirectory
        TaskName = $taskName
        TaskUser = $identityName
        RunLevel = [string]$principal.RunLevel
        Ready = $true
    }
    return
}

Register-ScheduledTask -TaskName $taskName -InputObject $taskDefinition -Force | Out-Null
if ($PrepareOnly) {
    Get-ScheduledTask -TaskName $taskName
    return
}

Start-ScheduledTask -TaskName $taskName
