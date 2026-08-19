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
    (Join-Path $testsRoot "nexus-publish")
)
$candidateRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repoRoot ".codex-temp\nexus-changelog-candidates")
)
$candidatePath = Join-Path $candidateRoot "NexusPublishFixture-1.2.0-from-1.0.0.txt"
$receiptPath = Join-Path $repoRoot "nexus-release-receipts.local.json"
$receiptExistedBefore = Test-Path -LiteralPath $receiptPath -PathType Leaf
$receiptHashBefore = if ($receiptExistedBefore) { (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash } else { '' }

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
        throw "Nexus publish contract failed: $Message"
    }
}

function Invoke-PublishDryRun {
    param(
        [string]$ModRoot,
        [string]$ArchivePath
    )

    $arguments = @{
        ModRoot = $ModRoot
        NexusUrl = "https://www.nexusmods.com/taintedgrailthefallofavalon/mods/1"
        ModId = "1"
        GroupId = "1"
        AddChangelog = $true
        DryRun = $true
        DryRunChangelogBaselineVersion = "1.0.0"
    }
    if (-not [string]::IsNullOrWhiteSpace($ArchivePath)) {
        $arguments.ArchivePath = $ArchivePath
        $arguments.SkipBuild = $true
    }

    return @(& $publishScript @arguments | Where-Object {
        $_ -ne $null -and
        $_.PSObject.Properties.Name -contains "ChangelogEntries"
    } | Select-Object -First 1)
}

try {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $candidatePath) {
        Remove-Item -LiteralPath $candidatePath -Force
    }

    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $archivePath = Join-Path $scratchRoot "fixture.zip"
    [System.IO.File]::WriteAllBytes($archivePath, [byte[]]@())
    Set-Content -LiteralPath (Join-Path $scratchRoot "mod.json") -Encoding UTF8 -Value @'
{
  "id": "NexusPublishFixture",
  "displayName": "Nexus Publish Fixture",
  "packageName": "NexusPublishFixture",
  "version": "1.2.0"
}
'@
    Set-Content -LiteralPath (Join-Path $scratchRoot "CHANGELOG.txt") -Encoding UTF8 -Value @'
Version 1.2.0
Finalized the fixture behavior.

Version 1.1.0
Added the fixture behavior.

Version 1.0.0
Established the published baseline.
'@

    $reviewedPath = Join-Path $scratchRoot "nexus-changelog.txt"
    Set-Content -LiteralPath $reviewedPath -Encoding UTF8 -Value @'
TargetVersion=1.2.0
BaselineVersion=1.0.0
Finalized the fixture behavior.
'@

    $plan = @(Invoke-PublishDryRun -ModRoot $scratchRoot -ArchivePath $archivePath)
    Assert-Contract ($plan.Count -eq 1) "valid reviewed changelog returned no publish plan."
    Assert-Contract ($plan[0].ChangelogSource -eq "reviewed-consolidation") "valid reviewed changelog was not selected."
    Assert-Contract ([string]::IsNullOrWhiteSpace([string]$plan[0].ChangelogCandidatePath)) "valid reviewed changelog still reported a raw candidate."
    Assert-Contract (-not (Test-Path -LiteralPath $candidatePath)) "valid reviewed changelog still created a raw candidate."
    Assert-Contract ($plan[0].Archive -eq [System.IO.Path]::GetFullPath($archivePath)) "explicit validated archive was not reused."

    Set-Content -LiteralPath $reviewedPath -Encoding UTF8 -Value @'
TargetVersion=1.2.0
BaselineVersion=0.9.0
Stale reviewed fixture entry.
'@
    $staleError = ""
    try {
        Invoke-PublishDryRun -ModRoot $scratchRoot -ArchivePath $archivePath | Out-Null
    } catch {
        $staleError = $_.Exception.Message
    }
    Assert-Contract (-not [string]::IsNullOrWhiteSpace($staleError)) "stale reviewed changelog did not stop publication."
    Assert-Contract ($staleError.Contains("Fresh raw candidate")) "stale reviewed changelog did not report its refreshed candidate."
    Assert-Contract (Test-Path -LiteralPath $candidatePath -PathType Leaf) "stale reviewed changelog did not create a fresh candidate."
    Remove-Item -LiteralPath $candidatePath -Force

    Remove-Item -LiteralPath $reviewedPath -Force
    $missingError = ""
    try {
        Invoke-PublishDryRun -ModRoot $scratchRoot -ArchivePath $archivePath | Out-Null
    } catch {
        $missingError = $_.Exception.Message
    }
    Assert-Contract (-not [string]::IsNullOrWhiteSpace($missingError)) "missing reviewed changelog did not stop publication."
    Assert-Contract ($missingError.Contains("Review and lightly consolidate")) "missing reviewed changelog did not request review."
    Assert-Contract (Test-Path -LiteralPath $candidatePath -PathType Leaf) "missing reviewed changelog did not create a fresh candidate."

    $skipBuildError = ""
    try {
        & $publishScript `
            -ModRoot $scratchRoot `
            -SkipBuild `
            -NexusUrl "https://www.nexusmods.com/taintedgrailthefallofavalon/mods/1" `
            -ModId "1" `
            -GroupId "1" `
            -DryRun | Out-Null
    } catch {
        $skipBuildError = $_.Exception.Message
    }
    Assert-Contract ($skipBuildError.Contains("Pass -ArchivePath when using -SkipBuild.")) "SkipBuild without ArchivePath was not rejected."

    $receiptExistsAfter = Test-Path -LiteralPath $receiptPath -PathType Leaf
    Assert-Contract ($receiptExistsAfter -eq $receiptExistedBefore) "dry-run changed release receipt store existence."
    if ($receiptExistedBefore) {
        $receiptHashAfter = (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash
        Assert-Contract ($receiptHashAfter -eq $receiptHashBefore) "dry-run changed the release receipt store."
    }

    Write-Host "Nexus publish contracts passed: validated archive reuse, lazy candidates, SkipBuild guard, and no dry-run receipts."
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $candidatePath) {
        Remove-Item -LiteralPath $candidatePath -Force
    }
}
