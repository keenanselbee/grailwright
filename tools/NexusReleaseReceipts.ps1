Set-StrictMode -Version Latest

$script:NexusReleaseReceiptSchemaVersion = 1

function Get-NexusReleaseReceiptsPath {
    param([string]$RepoRoot)
    return Join-Path ([System.IO.Path]::GetFullPath($RepoRoot)) "nexus-release-receipts.local.json"
}

function New-NexusReleaseReceiptStore {
    return [pscustomobject]@{
        schemaVersion = $script:NexusReleaseReceiptSchemaVersion
        updatedAt = $null
        receipts = @()
    }
}

function Test-NexusReleaseReceiptProperty {
    param([object]$Object, [string]$Name)
    return $null -ne $Object -and @($Object.PSObject.Properties | ForEach-Object Name) -contains $Name
}

function Get-NexusReleaseReceiptKey {
    param([object]$Receipt)

    foreach ($property in @('gameDomain', 'gameScopedModId', 'v3VersionId')) {
        if (-not (Test-NexusReleaseReceiptProperty -Object $Receipt.nexus -Name $property) -or
            [string]::IsNullOrWhiteSpace([string]$Receipt.nexus.$property)) {
            throw "Nexus release receipt is missing nexus.$property."
        }
    }

    return "$($Receipt.nexus.gameDomain)/mods/$($Receipt.nexus.gameScopedModId)/versions/$($Receipt.nexus.v3VersionId)"
}

function Read-NexusReleaseReceiptStore {
    param([string]$RepoRoot, [switch]$AllowMissing)

    $path = Get-NexusReleaseReceiptsPath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($AllowMissing) { return New-NexusReleaseReceiptStore }
        throw "Nexus release receipt store does not exist: $path"
    }

    $store = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    if ($store.schemaVersion -ne $script:NexusReleaseReceiptSchemaVersion) {
        throw "Unsupported Nexus release receipt schema '$($store.schemaVersion)' in $path."
    }
    if ($null -eq $store.receipts) {
        $store | Add-Member -NotePropertyName receipts -NotePropertyValue @()
    }
    return $store
}

function Write-NexusReleaseReceiptStore {
    param([string]$RepoRoot, [object]$Store)

    $path = Get-NexusReleaseReceiptsPath -RepoRoot $RepoRoot
    $Store.schemaVersion = $script:NexusReleaseReceiptSchemaVersion
    $Store.updatedAt = (Get-Date).ToUniversalTime().ToString('o')
    $temporaryPath = "$path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($temporaryPath, ($Store | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $temporaryPath -Destination $path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }

    return $path
}

function Assert-NexusReleaseReceiptCompatible {
    param([object]$Existing, [object]$Replacement)

    $checks = @(
        [pscustomobject]@{ Name = 'packageName'; Existing = [string]$Existing.packageName; Replacement = [string]$Replacement.packageName },
        [pscustomobject]@{ Name = 'version'; Existing = [string]$Existing.version; Replacement = [string]$Replacement.version },
        [pscustomobject]@{ Name = 'nexus.fileGroupId'; Existing = [string]$Existing.nexus.fileGroupId; Replacement = [string]$Replacement.nexus.fileGroupId },
        [pscustomobject]@{ Name = 'archive.md5'; Existing = [string]$Existing.archive.md5; Replacement = [string]$Replacement.archive.md5 },
        [pscustomobject]@{ Name = 'archive.sha256'; Existing = [string]$Existing.archive.sha256; Replacement = [string]$Replacement.archive.sha256 },
        [pscustomobject]@{ Name = 'archive.sizeBytes'; Existing = [string]$Existing.archive.sizeBytes; Replacement = [string]$Replacement.archive.sizeBytes }
    )
    foreach ($check in $checks) {
        if (-not [string]::Equals($check.Existing, $check.Replacement, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to replace Nexus release receipt $($Replacement.key): immutable $($check.Name) changed from '$($check.Existing)' to '$($check.Replacement)'."
        }
    }

    $existingVortexFileId = [string]$Existing.nexus.vortexFileId
    $replacementVortexFileId = [string]$Replacement.nexus.vortexFileId
    if (-not [string]::IsNullOrWhiteSpace($existingVortexFileId) -and
        -not [string]::IsNullOrWhiteSpace($replacementVortexFileId) -and
        $existingVortexFileId -ne $replacementVortexFileId) {
        throw "Refusing to replace Nexus release receipt $($Replacement.key): Vortex file ID changed from '$existingVortexFileId' to '$replacementVortexFileId'."
    }
}

function Set-NexusReleaseReceipt {
    param([string]$RepoRoot, [object]$Receipt)

    $receiptKey = Get-NexusReleaseReceiptKey -Receipt $Receipt
    if (Test-NexusReleaseReceiptProperty -Object $Receipt -Name 'key') {
        $Receipt.key = $receiptKey
    }
    else {
        $Receipt | Add-Member -NotePropertyName key -NotePropertyValue $receiptKey
    }
    $store = Read-NexusReleaseReceiptStore -RepoRoot $RepoRoot -AllowMissing
    $existing = @($store.receipts | Where-Object { [string]$_.key -eq [string]$Receipt.key } | Select-Object -First 1)
    if ($existing.Count -gt 0) {
        if (-not [string]::IsNullOrWhiteSpace([string]$existing[0].nexus.vortexFileId) -and
            [string]::IsNullOrWhiteSpace([string]$Receipt.nexus.vortexFileId)) {
            $Receipt.nexus.vortexFileId = [string]$existing[0].nexus.vortexFileId
            $Receipt.nexus.vortexFileIdStatus = [string]$existing[0].nexus.vortexFileIdStatus
            $Receipt.nexus.nxmUri = [string]$existing[0].nexus.nxmUri
        }
        Assert-NexusReleaseReceiptCompatible -Existing $existing[0] -Replacement $Receipt
        $recordedAt = [string]$existing[0].recordedAt
        if (Test-NexusReleaseReceiptProperty -Object $Receipt -Name 'recordedAt') {
            $Receipt.recordedAt = $recordedAt
        }
        else {
            $Receipt | Add-Member -NotePropertyName recordedAt -NotePropertyValue $recordedAt
        }
    }
    if (-not (Test-NexusReleaseReceiptProperty -Object $Receipt -Name 'recordedAt') -or
        [string]::IsNullOrWhiteSpace([string]$Receipt.recordedAt)) {
        $recordedAt = (Get-Date).ToUniversalTime().ToString('o')
        if (Test-NexusReleaseReceiptProperty -Object $Receipt -Name 'recordedAt') {
            $Receipt.recordedAt = $recordedAt
        }
        else {
            $Receipt | Add-Member -NotePropertyName recordedAt -NotePropertyValue $recordedAt
        }
    }
    $receiptUpdatedAt = (Get-Date).ToUniversalTime().ToString('o')
    if (Test-NexusReleaseReceiptProperty -Object $Receipt -Name 'updatedAt') {
        $Receipt.updatedAt = $receiptUpdatedAt
    }
    else {
        $Receipt | Add-Member -NotePropertyName updatedAt -NotePropertyValue $receiptUpdatedAt
    }

    $store.receipts = @(@(
            @($store.receipts | Where-Object { [string]$_.key -ne [string]$Receipt.key }) +
            @($Receipt)
        ) | Sort-Object { [string]$_.key })

    return Write-NexusReleaseReceiptStore -RepoRoot $RepoRoot -Store $store
}
