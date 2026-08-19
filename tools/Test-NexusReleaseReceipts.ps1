[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".codex-temp\tests")).TrimEnd("\") + "\"
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $testsRoot "nexus-release-receipts-repo"))
. (Join-Path $PSScriptRoot 'NexusReleaseReceipts.ps1')

if (-not $scratchRoot.StartsWith($testsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Scratch path escaped the repository test root: $scratchRoot"
}

function Assert-Contract {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Nexus release receipt contract failed: $Message" }
}

function New-TestReceipt {
    param(
        [string]$PackageName,
        [string]$Version,
        [string]$V3VersionId,
        [string]$VortexFileId,
        [string]$ArchiveMd5,
        [string]$ChangelogStatus = 'pending'
    )

    return [pscustomobject]@{
        receiptSchemaVersion = 1
        key = ''
        recordedAt = $null
        updatedAt = $null
        lifecycleStatus = 'version-uploaded'
        packageName = $PackageName
        displayName = $PackageName
        version = $Version
        nexus = [pscustomobject]@{
            source = 'nexus'
            url = 'https://www.nexusmods.com/taintedgrailthefallofavalon/mods/276'
            gameDomain = 'taintedgrailthefallofavalon'
            gameScopedModId = '276'
            v3ModId = '25280177504532'
            fileGroupId = '7790007'
            fileGroupName = 'Versatile Weapons - Dynamic Grip'
            v3VersionId = $V3VersionId
            vortexFileId = $VortexFileId
            vortexFileIdStatus = if ([string]::IsNullOrWhiteSpace($VortexFileId)) { 'pending-resolution' } else { 'resolved' }
            nxmUri = if ([string]::IsNullOrWhiteSpace($VortexFileId)) { '' } else { "nxm://taintedgrailthefallofavalon/mods/276/files/$VortexFileId" }
            logicalFileName = 'Versatile Weapons - Dynamic Grip'
            remoteArchiveName = ''
            uploadedAt = '2026-08-18T23:26:30Z'
            category = 'main'
            isPrimary = $true
        }
        archive = [pscustomobject]@{
            fileName = "$PackageName $Version.zip"
            sizeBytes = [int64]56117
            md5 = $ArchiveMd5
            sha256 = ('a' * 64)
        }
        changelog = [pscustomobject]@{
            requested = $true
            status = $ChangelogStatus
            error = ''
        }
        verification = [pscustomobject]@{
            status = 'exact-version-id'
            versionLabelMatches = $true
            fileDescriptionVerified = $true
        }
    }
}

try {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null

    $missing = Read-NexusReleaseReceiptStore -RepoRoot $scratchRoot -AllowMissing
    Assert-Contract ($missing.schemaVersion -eq 1 -and @($missing.receipts).Count -eq 0) "missing store did not initialize cleanly."

    $first = New-TestReceipt -PackageName 'VersatileWeapons' -Version '0.7.6' -V3VersionId '25280177505637' -VortexFileId '1381' -ArchiveMd5 ('1' * 32)
    $receiptPath = Set-NexusReleaseReceipt -RepoRoot $scratchRoot -Receipt $first
    Assert-Contract (Test-Path -LiteralPath $receiptPath -PathType Leaf) "receipt store was not created."

    $roundTrip = Read-NexusReleaseReceiptStore -RepoRoot $scratchRoot
    Assert-Contract (@($roundTrip.receipts).Count -eq 1) "round trip changed the receipt count."
    Assert-Contract ($roundTrip.receipts -is [array]) "single receipt was not serialized as an array."
    Assert-Contract ($roundTrip.receipts[0].nexus.vortexFileId -eq '1381') "Vortex file ID was not retained."
    Assert-Contract ($roundTrip.receipts[0].archive.md5 -eq ('1' * 32)) "archive MD5 was not retained."
    Assert-Contract ($roundTrip.receipts[0].archive.sha256 -eq ('a' * 64)) "archive SHA-256 was not retained."
    Assert-Contract ($roundTrip.receipts[0].archive.sizeBytes -eq 56117) "archive byte count was not retained."

    $replacement = New-TestReceipt -PackageName 'VersatileWeapons' -Version '0.7.6' -V3VersionId '25280177505637' -VortexFileId '1381' -ArchiveMd5 ('1' * 32) -ChangelogStatus 'posted'
    Set-NexusReleaseReceipt -RepoRoot $scratchRoot -Receipt $replacement | Out-Null
    $upserted = Read-NexusReleaseReceiptStore -RepoRoot $scratchRoot
    Assert-Contract (@($upserted.receipts).Count -eq 1) "same v3 version ID created a duplicate receipt."
    Assert-Contract ($upserted.receipts[0].changelog.status -eq 'posted') "same-ID receipt update was not retained."
    Assert-Contract (-not [string]::IsNullOrWhiteSpace([string]$upserted.receipts[0].recordedAt)) "initial receipt timestamp was lost during upsert."

    $noRegression = New-TestReceipt -PackageName 'VersatileWeapons' -Version '0.7.6' -V3VersionId '25280177505637' -VortexFileId '' -ArchiveMd5 ('1' * 32) -ChangelogStatus 'posted'
    Set-NexusReleaseReceipt -RepoRoot $scratchRoot -Receipt $noRegression | Out-Null
    $preserved = Read-NexusReleaseReceiptStore -RepoRoot $scratchRoot
    Assert-Contract ($preserved.receipts[0].nexus.vortexFileId -eq '1381') "resolved Vortex file ID regressed to pending."

    $pending = New-TestReceipt -PackageName 'SharedGroupPackage' -Version '1.0.0' -V3VersionId '25280177505638' -VortexFileId '' -ArchiveMd5 ('2' * 32)
    Set-NexusReleaseReceipt -RepoRoot $scratchRoot -Receipt $pending | Out-Null
    $shared = Read-NexusReleaseReceiptStore -RepoRoot $scratchRoot
    Assert-Contract (@($shared.receipts).Count -eq 2) "shared file-group receipts overwrote one another."
    $pendingRoundTrip = @($shared.receipts | Where-Object { $_.packageName -eq 'SharedGroupPackage' })[0]
    Assert-Contract ([string]::IsNullOrWhiteSpace([string]$pendingRoundTrip.nexus.vortexFileId)) "pending Vortex file ID was invented."
    Assert-Contract ($pendingRoundTrip.nexus.vortexFileIdStatus -eq 'pending-resolution') "pending Vortex file ID status was not retained."

    $conflict = New-TestReceipt -PackageName 'VersatileWeapons' -Version '0.7.6' -V3VersionId '25280177505637' -VortexFileId '1381' -ArchiveMd5 ('3' * 32)
    $conflictError = ''
    try {
        Set-NexusReleaseReceipt -RepoRoot $scratchRoot -Receipt $conflict | Out-Null
    }
    catch {
        $conflictError = $_.Exception.Message
    }
    Assert-Contract ($conflictError.Contains('immutable archive.md5 changed')) "conflicting immutable archive identity was not rejected."

    $temporaryFiles = @(Get-ChildItem -LiteralPath $scratchRoot -File -Filter '*.tmp' -Force -ErrorAction SilentlyContinue)
    Assert-Contract ($temporaryFiles.Count -eq 0) "atomic writer left temporary files behind."

    Write-Host "Nexus release receipt contracts passed: round trip, immutable upsert, shared groups, pending IDs, and atomic cleanup."
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
}
