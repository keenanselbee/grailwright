[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "Publish-NexusMod.ps1"
$testsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repoRoot ".codex-temp\tests")
).TrimEnd("\") + "\"
$scratchRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $testsRoot "nexus-changelog-heading")
)

if (-not $scratchRoot.StartsWith(
    $testsRoot,
    [System.StringComparison]::OrdinalIgnoreCase
)) {
    throw "Scratch path escaped the repository test root: $scratchRoot"
}

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Nexus changelog heading contract failed: $Message"
    }
}

function Invoke-PublishDryRun {
    param(
        [string]$ModRoot,
        [string]$ArchivePath,
        [string]$BaselineVersion
    )

    $outputs = @(& $publishScript `
        -ModRoot $ModRoot `
        -ArchivePath $ArchivePath `
        -NexusUrl "https://www.nexusmods.com/taintedgrailthefallofavalon/mods/225" `
        -ModId "1" `
        -GroupId "1" `
        -AddChangelog `
        -DryRun `
        -DryRunChangelogBaselineVersion $BaselineVersion)
    return @($outputs | Where-Object {
        $_ -ne $null -and
        $_.PSObject.Properties.Name -contains "ChangelogEntries"
    } | Select-Object -First 1)
}

try {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $archivePath = Join-Path $scratchRoot "fixture.zip"
    [System.IO.File]::WriteAllBytes($archivePath, [byte[]]@())

    $ksPlan = @(Invoke-PublishDryRun `
        -ModRoot (Join-Path $repoRoot "mods\KSAddons\KSPersistentCorpsesAddon") `
        -ArchivePath $archivePath `
        -BaselineVersion "1.1.0")
    Assert-Contract ($ksPlan.Count -eq 1) "KS Addons dry run returned no publish plan."
    $ksEntries = @($ksPlan[0].ChangelogEntries)
    Assert-Contract (
        $ksEntries[0] -eq "KS Persistent Corpses Addon"
    ) "KS Addons payload did not use the manifest display name."
    Assert-Contract (
        @($ksEntries | Where-Object {
            $_ -eq "KS Persistent Corpses Addon"
        }).Count -eq 1
    ) "KS Addons payload did not add exactly one heading line."

    $ordinaryPlan = @(Invoke-PublishDryRun `
        -ModRoot (Join-Path $repoRoot "mods\VersatileWeapons") `
        -ArchivePath $archivePath `
        -BaselineVersion "0.7.6")
    Assert-Contract ($ordinaryPlan.Count -eq 1) "Ordinary mod dry run returned no publish plan."
    $ordinaryEntries = @($ordinaryPlan[0].ChangelogEntries)
    Assert-Contract (
        $ordinaryEntries.Count -eq $ordinaryPlan[0].RawChangelogEntryCount
    ) "Ordinary mod payload received an unexpected heading line."
    Assert-Contract (
        $ordinaryEntries[0] -ne [string]$ordinaryPlan[0].Package
    ) "Ordinary mod payload was prefixed with a package heading."

    Write-Host "Nexus changelog heading contracts passed: KS Addons prefixed; ordinary mods unchanged."
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
}
