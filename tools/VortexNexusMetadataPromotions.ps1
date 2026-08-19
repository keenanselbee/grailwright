Set-StrictMode -Version Latest

$script:VortexNexusMetadataPromotionSchemaVersion = 1

function Get-VortexNexusMetadataBridgeRoot {
    param([string]$Candidate)

    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        return [System.IO.Path]::GetFullPath($Candidate)
    }

    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw "APPDATA is not available, so the Vortex metadata bridge root cannot be resolved."
    }

    return [System.IO.Path]::GetFullPath((Join-Path $env:APPDATA "Vortex\grailwright-nexus-metadata"))
}

function Get-VortexNexusMetadataStagingRoot {
    param([string]$Candidate, [string]$GameId)

    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        return [System.IO.Path]::GetFullPath($Candidate)
    }

    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw "APPDATA is not available, so the Vortex staging root cannot be resolved."
    }

    return [System.IO.Path]::GetFullPath((Join-Path $env:APPDATA "Vortex\$GameId\mods"))
}

function Convert-ToVortexPromotionSafeName {
    param([string]$Name)

    $safe = $Name -replace '[\\/:*?"<>|]', '-'
    $safe = $safe -replace "\s+", " "
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe Vortex staged mod name."
    }
    return $safe
}

function Get-VortexPromotionFileHash {
    param([string]$Path, [string]$Algorithm)
    return (Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash.ToLowerInvariant()
}

function Get-VortexPromotionStreamHash {
    param([System.IO.Stream]$Stream, [string]$Algorithm)

    $hasher = [System.Security.Cryptography.HashAlgorithm]::Create($Algorithm)
    if ($null -eq $hasher) {
        throw "Unsupported hash algorithm: $Algorithm"
    }
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
    }
}

function Get-VortexPromotionPayloadManifest {
    param(
        [string]$ArchivePath,
        [string]$PackageName,
        [string]$StagedPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $prefix = "$PackageName/"
        $archiveFiles = @{}
        foreach ($entry in $archive.Entries) {
            $entryPath = ([string]$entry.FullName).Replace('\', '/')
            if ($entryPath.EndsWith('/')) {
                continue
            }
            if (-not $entryPath.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
                throw "Archive entry '$entryPath' is outside the expected top-level package folder '$PackageName'."
            }

            $relativePath = $entryPath
            if ([string]::IsNullOrWhiteSpace($relativePath) -or $archiveFiles.ContainsKey($relativePath)) {
                throw "Archive contains an invalid or duplicate payload path: '$relativePath'."
            }

            $stream = $entry.Open()
            try {
                $archiveFiles[$relativePath] = [pscustomobject]@{
                    path = $relativePath
                    sizeBytes = [int64]$entry.Length
                    sha256 = Get-VortexPromotionStreamHash -Stream $stream -Algorithm 'SHA256'
                }
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $stagedRoot = [System.IO.Path]::GetFullPath($StagedPath).TrimEnd('\') + '\'
    $stagedFiles = @{}
    foreach ($file in Get-ChildItem -LiteralPath $StagedPath -Recurse -File -Force) {
        $relativePath = $file.FullName.Substring($stagedRoot.Length).Replace('\', '/')
        $stagedFiles[$relativePath] = $file
    }

    $archivePaths = @($archiveFiles.Keys | Sort-Object)
    $stagedPaths = @($stagedFiles.Keys | Sort-Object)
    if (($archivePaths -join "`n") -ne ($stagedPaths -join "`n")) {
        $missing = @($archivePaths | Where-Object { -not $stagedFiles.ContainsKey($_) })
        $extra = @($stagedPaths | Where-Object { -not $archiveFiles.ContainsKey($_) })
        throw "Staged payload does not match the uploaded archive. Missing: $($missing -join ', '); extra: $($extra -join ', ')."
    }

    foreach ($relativePath in $archivePaths) {
        $expected = $archiveFiles[$relativePath]
        $actual = $stagedFiles[$relativePath]
        if ([int64]$actual.Length -ne [int64]$expected.sizeBytes) {
            throw "Staged payload size differs from the uploaded archive for '$relativePath'."
        }
        $actualSha256 = Get-VortexPromotionFileHash -Path $actual.FullName -Algorithm 'SHA256'
        if ($actualSha256 -ne [string]$expected.sha256) {
            throw "Staged payload content differs from the uploaded archive for '$relativePath'."
        }
    }

    return @($archivePaths | ForEach-Object { $archiveFiles[$_] })
}

function Get-VortexPromotionRequestId {
    param([object]$Receipt)

    $identity = "$($Receipt.nexus.gameDomain)|$($Receipt.nexus.gameScopedModId)|$($Receipt.nexus.vortexFileId)|$($Receipt.archive.md5)"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($identity)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant().Substring(0, 24)
    }
    finally {
        $hasher.Dispose()
    }
}

function Get-VortexLocalGroupingIdentity {
    param(
        [object]$Manifest,
        [string]$ModRoot,
        [string]$DefaultGameId = 'taintedgrailthefallofavalon'
    )

    $settings = @{}
    if ($null -ne $Manifest -and $Manifest.PSObject.Properties.Name -contains 'nexus' -and $null -ne $Manifest.nexus) {
        foreach ($property in $Manifest.nexus.PSObject.Properties) {
            $settings[$property.Name] = $property.Value
        }
    }

    $apiPath = if ([string]::IsNullOrWhiteSpace($ModRoot)) { '' } else { Join-Path $ModRoot 'API.txt' }
    if (-not [string]::IsNullOrWhiteSpace($apiPath) -and (Test-Path -LiteralPath $apiPath -PathType Leaf)) {
        foreach ($line in Get-Content -LiteralPath $apiPath) {
            $trimmed = $line.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
                continue
            }
            $separator = $trimmed.IndexOf('=')
            if ($separator -lt 1) {
                continue
            }
            $key = $trimmed.Substring(0, $separator).Trim()
            if ($key -match '(?i)key|token|secret|password') {
                continue
            }
            if (-not $settings.ContainsKey($key)) {
                $settings[$key] = $trimmed.Substring($separator + 1).Trim()
            }
        }
    }

    function Get-GroupingSetting {
        param([string[]]$Names)
        foreach ($name in $Names) {
            foreach ($key in @($settings.Keys)) {
                if ([string]::Equals([string]$key, $name, [System.StringComparison]::OrdinalIgnoreCase) -and
                    -not [string]::IsNullOrWhiteSpace([string]$settings[$key])) {
                    return [string]$settings[$key]
                }
            }
        }
        return ''
    }

    $url = Get-GroupingSetting -Names @('url', 'NexusUrl')
    $gameId = Get-GroupingSetting -Names @('gameDomain')
    $modId = Get-GroupingSetting -Names @('gameScopedModId')
    if (-not [string]::IsNullOrWhiteSpace($url)) {
        $match = [regex]::Match($url, 'nexusmods\.com/(?<game>[^/?#]+)/mods/(?<mod>[0-9]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) {
            if ([string]::IsNullOrWhiteSpace($gameId)) {
                $gameId = $match.Groups['game'].Value
            }
            if ([string]::IsNullOrWhiteSpace($modId)) {
                $modId = $match.Groups['mod'].Value
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($gameId)) {
        $gameId = $DefaultGameId
    }

    $logicalFileName = Get-GroupingSetting -Names @('fileName', 'logicalFileName')
    if ([string]::IsNullOrWhiteSpace($logicalFileName)) {
        $logicalFileName = [string]$Manifest.displayName
    }

    return [pscustomobject]@{
        GameId = $gameId
        ModId = if ($modId -match '^[1-9][0-9]*$') { [int64]$modId } else { $null }
        LogicalFileName = $logicalFileName
        NexusUrl = $url
    }
}

function Get-VortexLocalGroupingRequestId {
    param(
        [string]$GameId,
        [string]$StagedModId,
        [string]$Version,
        [object]$ModId,
        [string]$LogicalFileName
    )

    $identity = "local-grouping|$GameId|$StagedModId|$Version|$ModId|$LogicalFileName"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($identity)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant().Substring(0, 24)
    }
    finally {
        $hasher.Dispose()
    }
}

function Write-VortexPromotionJsonAtomic {
    param([string]$Path, [object]$Value)

    $temporaryPath = "$Path.$([guid]::NewGuid().ToString('N')).tmp"
    try {
        [System.IO.File]::WriteAllText($temporaryPath, ($Value | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Write-VortexLocalGroupingCatalogRecord {
    param(
        [string]$BridgeRoot,
        [string]$GameId,
        [object]$Entry
    )

    foreach ($required in @('packageName', 'displayName', 'stagedNamePrefix', 'logicalFileName')) {
        if (-not ($Entry.PSObject.Properties.Name -contains $required) -or
            [string]::IsNullOrWhiteSpace([string]$Entry.$required)) {
            throw "Vortex grouping catalog entry is missing '$required'."
        }
    }

    $identity = "$GameId|$($Entry.packageName)"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($identity)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $recordId = ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant().Substring(0, 24)
    }
    finally {
        $hasher.Dispose()
    }

    $recordsRoot = Join-Path $BridgeRoot 'catalog-records'
    New-Item -ItemType Directory -Path $recordsRoot -Force | Out-Null
    $record = [pscustomobject]@{
        schemaVersion = $script:VortexNexusMetadataPromotionSchemaVersion
        gameId = $GameId
        packageName = [string]$Entry.packageName
        displayName = [string]$Entry.displayName
        stagedNamePrefix = [string]$Entry.stagedNamePrefix
        modId = $Entry.modId
        logicalFileName = [string]$Entry.logicalFileName
        nexusUrl = [string]$Entry.nexusUrl
    }
    $recordPath = Join-Path $recordsRoot "$recordId.json"
    Write-VortexPromotionJsonAtomic -Path $recordPath -Value $record
    return $recordPath
}

function Queue-VortexLocalMetadataGrouping {
    param(
        [object]$Manifest,
        [string]$ModRoot,
        [string]$StagedModId,
        [string]$StagedPath,
        [string]$VortexModsRoot = '',
        [string]$BridgeRoot = '',
        [switch]$Repair
    )

    if ($null -eq $Manifest) {
        throw 'A mod manifest is required for Vortex local metadata grouping.'
    }
    foreach ($required in @('displayName', 'packageName', 'version')) {
        if (-not ($Manifest.PSObject.Properties.Name -contains $required) -or
            [string]::IsNullOrWhiteSpace([string]$Manifest.$required)) {
            throw "Manifest is missing required Vortex grouping field '$required'."
        }
    }

    $identity = Get-VortexLocalGroupingIdentity -Manifest $Manifest -ModRoot $ModRoot
    if ([string]::IsNullOrWhiteSpace([string]$identity.LogicalFileName)) {
        throw "Could not resolve a stable Vortex logical filename for '$($Manifest.packageName)'."
    }

    $resolvedModsRoot = Get-VortexNexusMetadataStagingRoot -Candidate $VortexModsRoot -GameId ([string]$identity.GameId)
    $resolvedStagedPath = [System.IO.Path]::GetFullPath($StagedPath)
    $modsPrefix = $resolvedModsRoot.TrimEnd('\') + '\'
    if (-not $resolvedStagedPath.StartsWith($modsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to group a staged mod outside the Vortex staging root: $resolvedStagedPath"
    }
    if ([string]::IsNullOrWhiteSpace($StagedModId) -or
        -not [string]::Equals((Split-Path -Leaf $resolvedStagedPath), $StagedModId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Vortex grouping staged ID does not match its staging folder: $StagedModId"
    }
    if (-not (Test-Path -LiteralPath $resolvedStagedPath -PathType Container)) {
        return [pscustomobject]@{ Status = 'not-staged'; RequestId = ''; RequestPath = ''; StagedPath = $resolvedStagedPath }
    }

    $version = if ($StagedModId -match ' (?<version>[0-9]+\.[0-9]+\.[0-9]+)$') { $matches.version } else { [string]$Manifest.version }
    $requestId = Get-VortexLocalGroupingRequestId `
        -GameId ([string]$identity.GameId) `
        -StagedModId $StagedModId `
        -Version $version `
        -ModId $identity.ModId `
        -LogicalFileName ([string]$identity.LogicalFileName)
    $resolvedBridgeRoot = Get-VortexNexusMetadataBridgeRoot -Candidate $BridgeRoot
    $requestsRoot = Join-Path $resolvedBridgeRoot 'grouping-requests'
    $ackRoot = Join-Path $resolvedBridgeRoot 'acknowledgements'
    foreach ($path in @($requestsRoot, $ackRoot)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    Write-VortexLocalGroupingCatalogRecord `
        -BridgeRoot $resolvedBridgeRoot `
        -GameId ([string]$identity.GameId) `
        -Entry ([pscustomobject]@{
            packageName = [string]$Manifest.packageName
            displayName = [string]$Manifest.displayName
            stagedNamePrefix = Convert-ToVortexPromotionSafeName -Name ([string]$Manifest.displayName)
            modId = $identity.ModId
            logicalFileName = [string]$identity.LogicalFileName
            nexusUrl = [string]$identity.NexusUrl
        }) | Out-Null

    $ackPath = Join-Path $ackRoot "$requestId.json"
    if (-not $Repair -and (Test-Path -LiteralPath $ackPath -PathType Leaf)) {
        $acknowledgement = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json
        if ([string]$acknowledgement.status -in @('completed', 'local-grouped')) {
            return [pscustomobject]@{ Status = 'completed'; RequestId = $requestId; RequestPath = ''; AcknowledgementPath = $ackPath; StagedPath = $resolvedStagedPath }
        }
    }

    if ($Repair -and (Test-Path -LiteralPath $ackPath -PathType Leaf)) {
        Remove-Item -LiteralPath $ackPath -Force
    }

    $request = [pscustomobject]@{
        schemaVersion = $script:VortexNexusMetadataPromotionSchemaVersion
        requestType = 'local-grouping'
        requestId = $requestId
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
        gameId = [string]$identity.GameId
        stagedModId = $StagedModId
        stagingPath = $resolvedStagedPath
        packageName = [string]$Manifest.packageName
        displayName = [string]$Manifest.displayName
        version = $version
        grouping = [pscustomobject]@{
            source = 'grailwright-local'
            modId = $identity.ModId
            logicalFileName = [string]$identity.LogicalFileName
            nexusUrl = [string]$identity.NexusUrl
        }
    }

    $requestPath = Join-Path $requestsRoot "$requestId.json"
    Write-VortexPromotionJsonAtomic -Path $requestPath -Value $request
    return [pscustomobject]@{ Status = 'queued'; RequestId = $requestId; RequestPath = $requestPath; AcknowledgementPath = $ackPath; StagedPath = $resolvedStagedPath }
}

function Wait-VortexLocalGroupingAcknowledgement {
    param(
        [Parameter(Mandatory = $true)][string]$RequestId,
        [string]$BridgeRoot = '',
        [ValidateRange(0, 60)][int]$TimeoutSeconds = 15,
        [ValidateRange(50, 5000)][int]$PollMilliseconds = 250
    )

    if ([string]::IsNullOrWhiteSpace($RequestId)) {
        throw 'A Vortex local grouping request ID is required.'
    }

    $resolvedBridgeRoot = Get-VortexNexusMetadataBridgeRoot -Candidate $BridgeRoot
    $ackPath = Join-Path (Join-Path $resolvedBridgeRoot 'acknowledgements') "$RequestId.json"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $ackPath -PathType Leaf) {
            $acknowledgement = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json
            if (($acknowledgement.PSObject.Properties.Name -contains 'requestId') -and
                -not [string]::Equals([string]$acknowledgement.requestId, $RequestId, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Vortex grouping acknowledgement '$ackPath' does not match request '$RequestId'."
            }

            return [pscustomobject]@{
                Status = [string]$acknowledgement.status
                TimedOut = $false
                AcknowledgementPath = $ackPath
                Acknowledgement = $acknowledgement
            }
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            break
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    } while ($true)

    return [pscustomobject]@{
        Status = 'pending'
        TimedOut = $true
        AcknowledgementPath = $ackPath
        Acknowledgement = $null
    }
}

function Queue-VortexLocalGroupingCatalog {
    param(
        [object[]]$Entries,
        [string]$GameId = 'taintedgrailthefallofavalon',
        [string]$BridgeRoot = '',
        [switch]$Repair
    )

    $catalog = @($Entries | Sort-Object PackageName)
    if ($catalog.Count -eq 0) {
        throw 'At least one mod entry is required for a Vortex grouping catalog.'
    }
    $identity = 'local-grouping-catalog|' + $GameId + '|' + ($catalog | ConvertTo-Json -Depth 10 -Compress)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($identity)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $requestId = ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant().Substring(0, 24)
    }
    finally {
        $hasher.Dispose()
    }

    $resolvedBridgeRoot = Get-VortexNexusMetadataBridgeRoot -Candidate $BridgeRoot
    $requestsRoot = Join-Path $resolvedBridgeRoot 'grouping-requests'
    $ackRoot = Join-Path $resolvedBridgeRoot 'acknowledgements'
    foreach ($path in @($requestsRoot, $ackRoot)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
    foreach ($entry in $catalog) {
        Write-VortexLocalGroupingCatalogRecord -BridgeRoot $resolvedBridgeRoot -GameId $GameId -Entry $entry | Out-Null
    }
    Write-VortexPromotionJsonAtomic `
        -Path (Join-Path $resolvedBridgeRoot 'catalog-complete.json') `
        -Value ([pscustomobject]@{
            schemaVersion = $script:VortexNexusMetadataPromotionSchemaVersion
            gameId = $GameId
            recordedAt = (Get-Date).ToUniversalTime().ToString('o')
            packageNames = @($catalog | ForEach-Object { [string]$_.packageName })
        })
    $ackPath = Join-Path $ackRoot "$requestId.json"
    if (-not $Repair -and (Test-Path -LiteralPath $ackPath -PathType Leaf)) {
        $acknowledgement = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json
        if ([string]$acknowledgement.status -eq 'catalog-grouped') {
            return [pscustomobject]@{ Status = 'completed'; RequestId = $requestId; RequestPath = '' }
        }
    }

    $request = [pscustomobject]@{
        schemaVersion = $script:VortexNexusMetadataPromotionSchemaVersion
        requestType = 'local-grouping-catalog'
        requestId = $requestId
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
        gameId = $GameId
        mods = $catalog
    }
    $requestPath = Join-Path $requestsRoot "$requestId.json"
    Write-VortexPromotionJsonAtomic -Path $requestPath -Value $request
    return [pscustomobject]@{ Status = 'queued'; RequestId = $requestId; RequestPath = $requestPath }
}

function Queue-VortexNexusMetadataPromotion {
    param(
        [object]$Receipt,
        [string]$ArchivePath,
        [string]$VortexModsRoot = '',
        [string]$BridgeRoot = ''
    )

    if ($null -eq $Receipt) {
        throw "A Nexus release receipt is required for Vortex metadata promotion."
    }
    if ([string]::IsNullOrWhiteSpace([string]$Receipt.nexus.vortexFileId) -or
        [string]$Receipt.nexus.vortexFileId -notmatch '^[1-9][0-9]*$') {
        return [pscustomobject]@{ Status = 'pending-vortex-file-id'; RequestId = ''; RequestPath = ''; StagedPath = '' }
    }
    if ([string]$Receipt.verification.status -ne 'exact-version-id') {
        return [pscustomobject]@{ Status = 'pending-exact-verification'; RequestId = ''; RequestPath = ''; StagedPath = '' }
    }

    $archiveItem = Get-Item -LiteralPath ([System.IO.Path]::GetFullPath($ArchivePath)) -ErrorAction Stop
    if ([int64]$archiveItem.Length -ne [int64]$Receipt.archive.sizeBytes -or
        (Get-VortexPromotionFileHash -Path $archiveItem.FullName -Algorithm 'MD5') -ne [string]$Receipt.archive.md5 -or
        (Get-VortexPromotionFileHash -Path $archiveItem.FullName -Algorithm 'SHA256') -ne [string]$Receipt.archive.sha256) {
        throw "The promotion archive does not match the immutable Nexus release receipt."
    }

    $gameId = [string]$Receipt.nexus.gameDomain
    $resolvedModsRoot = Get-VortexNexusMetadataStagingRoot -Candidate $VortexModsRoot -GameId $gameId
    $stagedId = "$(Convert-ToVortexPromotionSafeName -Name ([string]$Receipt.displayName)) $($Receipt.version)"
    $stagedPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedModsRoot $stagedId))
    $modsPrefix = $resolvedModsRoot.TrimEnd('\') + '\'
    if (-not $stagedPath.StartsWith($modsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to promote a staged mod outside the Vortex staging root: $stagedPath"
    }
    if (-not (Test-Path -LiteralPath $stagedPath -PathType Container)) {
        return [pscustomobject]@{ Status = 'not-staged'; RequestId = ''; RequestPath = ''; StagedPath = $stagedPath }
    }

    $payload = @(Get-VortexPromotionPayloadManifest -ArchivePath $archiveItem.FullName -PackageName ([string]$Receipt.packageName) -StagedPath $stagedPath)
    $requestId = Get-VortexPromotionRequestId -Receipt $Receipt
    $resolvedBridgeRoot = Get-VortexNexusMetadataBridgeRoot -Candidate $BridgeRoot
    $requestsRoot = Join-Path $resolvedBridgeRoot 'requests'
    $archiveRoot = Join-Path (Join-Path $resolvedBridgeRoot 'archives') $requestId
    $ackRoot = Join-Path $resolvedBridgeRoot 'acknowledgements'
    foreach ($path in @($requestsRoot, $archiveRoot, $ackRoot)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    $ackPath = Join-Path $ackRoot "$requestId.json"
    if (Test-Path -LiteralPath $ackPath -PathType Leaf) {
        $acknowledgement = Get-Content -LiteralPath $ackPath -Raw | ConvertFrom-Json
        if ([string]$acknowledgement.status -eq 'completed') {
            return [pscustomobject]@{ Status = 'completed'; RequestId = $requestId; RequestPath = ''; StagedPath = $stagedPath }
        }
    }

    $queuedArchivePath = Join-Path $archiveRoot $archiveItem.Name
    if (Test-Path -LiteralPath $queuedArchivePath -PathType Leaf) {
        if ((Get-VortexPromotionFileHash -Path $queuedArchivePath -Algorithm 'SHA256') -ne [string]$Receipt.archive.sha256) {
            throw "A conflicting queued archive already exists for Vortex metadata promotion $requestId."
        }
    }
    else {
        $temporaryArchivePath = "$queuedArchivePath.$([guid]::NewGuid().ToString('N')).tmp"
        try {
            Copy-Item -LiteralPath $archiveItem.FullName -Destination $temporaryArchivePath
            Move-Item -LiteralPath $temporaryArchivePath -Destination $queuedArchivePath
        }
        finally {
            if (Test-Path -LiteralPath $temporaryArchivePath) {
                Remove-Item -LiteralPath $temporaryArchivePath -Force
            }
        }
    }

    $request = [pscustomobject]@{
        schemaVersion = $script:VortexNexusMetadataPromotionSchemaVersion
        requestType = 'nexus-promotion'
        requestId = $requestId
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
        gameId = $gameId
        stagedModId = $stagedId
        stagingPath = $stagedPath
        packageName = [string]$Receipt.packageName
        displayName = [string]$Receipt.displayName
        version = [string]$Receipt.version
        archivePath = $queuedArchivePath
        archive = [pscustomobject]@{
            fileName = [string]$Receipt.archive.fileName
            remoteFileName = [string]$Receipt.nexus.remoteArchiveName
            sizeBytes = [int64]$Receipt.archive.sizeBytes
            md5 = [string]$Receipt.archive.md5
            sha256 = [string]$Receipt.archive.sha256
        }
        nexus = [pscustomobject]@{
            source = 'nexus'
            url = [string]$Receipt.nexus.url
            modId = [int64]$Receipt.nexus.gameScopedModId
            fileId = [int64]$Receipt.nexus.vortexFileId
            logicalFileName = [string]$Receipt.nexus.logicalFileName
            category = [string]$Receipt.nexus.category
            isPrimary = [bool]$Receipt.nexus.isPrimary
            nxmUri = [string]$Receipt.nexus.nxmUri
            v3VersionId = [string]$Receipt.nexus.v3VersionId
        }
        payload = $payload
    }

    $requestPath = Join-Path $requestsRoot "$requestId.json"
    Write-VortexPromotionJsonAtomic -Path $requestPath -Value $request
    return [pscustomobject]@{ Status = 'queued'; RequestId = $requestId; RequestPath = $requestPath; StagedPath = $stagedPath }
}
