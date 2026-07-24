[CmdletBinding()]
param(
    [string]$Mod = "",
    [string]$ModRoot = "",
    [string]$ArchivePath = "",
    [string]$DestinationDirectory = "",
    [switch]$SkipBuild,
    [switch]$SkipCompile,

    [string]$NexusUrl = "",
    [string]$GameDomain = "",
    [string]$GameScopedModId = "",
    [string]$ModId = "",
    [Alias("GroupId")]
    [string]$ModFileId = "",

    [string]$FileName = "",
    [string]$FileVersion = "",
    [string]$FileDescription = "",
    [ValidateSet("main", "optional", "miscellaneous")]
    [string]$FileCategory = "main",
    [bool]$PrimaryModManagerDownload = $true,
    [bool]$AllowModManagerDownload = $true,
    [bool]$ShowRequirementsPopUp = $false,
    [bool]$UpdateModVersion = $true,
    [bool]$ArchiveExistingFile = $true,
    [string]$PreviousVersionId = "",

    [switch]$ListFiles,
    [switch]$AddChangelog,
    [switch]$DryRun,
    [string]$ApiBaseUrl = "https://api.nexusmods.com/v3"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http | Out-Null

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiKey = $env:NEXUS_API_KEY

function Test-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object -ne $null -and $Object.PSObject.Properties.Name -contains $Name
}

function Read-ModManifest {
    param([string]$Root)

    $manifestPath = Join-Path $Root "mod.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing mod manifest: $manifestPath"
    }

    return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

function Resolve-ModRoot {
    param(
        [string]$RequestedMod,
        [string]$RequestedModRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedModRoot)) {
        return [System.IO.Path]::GetFullPath($RequestedModRoot)
    }

    if ([string]::IsNullOrWhiteSpace($RequestedMod)) {
        return ""
    }

    $manifests = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "mods") -Recurse -File -Filter "mod.json"
    $matches = New-Object "System.Collections.Generic.List[string]"

    foreach ($file in $manifests) {
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

function Read-NexusSettings {
    param([object]$Manifest)

    if ($Manifest -ne $null -and (Test-JsonProperty -Object $Manifest -Name "nexus")) {
        Assert-NoNexusSecrets -Settings $Manifest.nexus -Source "mod.json"
        return $Manifest.nexus
    }

    return $null
}

function Read-LocalApiSettings {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        return $null
    }

    $path = Join-Path $Root "API.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return $null
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

    $object = [pscustomobject]$settings
    Assert-NoNexusSecrets -Settings $object -Source $path
    return $object
}

function Assert-NoNexusSecrets {
    param(
        [object]$Settings,
        [string]$Source
    )

    if ($Settings -eq $null) {
        return
    }

    foreach ($secretName in @("apiKey", "apikey", "token", "secret", "bearerToken", "password", "nexusApiKey")) {
        if (Test-JsonProperty -Object $Settings -Name $secretName) {
            throw "Refusing to read Nexus secret '$secretName' from $Source. Store secrets in the NEXUS_API_KEY environment variable only."
        }
    }
}

function Convert-ToSettingBool {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($Value -is [bool]) {
        return [bool]$Value
    }

    $text = ([string]$Value).Trim()
    if ($text -match '^(?i:true|1|yes|y|on)$') {
        return $true
    }

    if ($text -match '^(?i:false|0|no|n|off)$') {
        return $false
    }

    throw "Invalid boolean value for ${Name}: $Value"
}

function Apply-NexusManifestDefaults {
    param([object]$Settings)

    if ($Settings -eq $null) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "NexusUrl")) {
        $script:NexusUrl = [string]$Settings.NexusUrl
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "url")) {
        $script:NexusUrl = [string]$Settings.url
    }

    if ([string]::IsNullOrWhiteSpace($script:NexusUrl) -and (Test-JsonProperty -Object $Settings -Name "Url")) {
        $script:NexusUrl = [string]$Settings.Url
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -and (Test-JsonProperty -Object $Settings -Name "gameDomain")) {
        $script:GameDomain = [string]$Settings.gameDomain
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -and (Test-JsonProperty -Object $Settings -Name "GameDomain")) {
        $script:GameDomain = [string]$Settings.GameDomain
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId) -and (Test-JsonProperty -Object $Settings -Name "gameScopedModId")) {
        $script:GameScopedModId = [string]$Settings.gameScopedModId
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId) -and (Test-JsonProperty -Object $Settings -Name "GameScopedModId")) {
        $script:GameScopedModId = [string]$Settings.GameScopedModId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModId) -and (Test-JsonProperty -Object $Settings -Name "modId")) {
        $script:ModId = [string]$Settings.modId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModId) -and (Test-JsonProperty -Object $Settings -Name "ModId")) {
        $script:ModId = [string]$Settings.ModId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "modFileId")) {
        $script:ModFileId = [string]$Settings.modFileId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "ModFileId")) {
        $script:ModFileId = [string]$Settings.ModFileId
    }

    if ([string]::IsNullOrWhiteSpace($script:ModFileId) -and (Test-JsonProperty -Object $Settings -Name "GroupId")) {
        $script:ModFileId = [string]$Settings.GroupId
    }

    if ([string]::IsNullOrWhiteSpace($script:FileName) -and (Test-JsonProperty -Object $Settings -Name "fileName")) {
        $script:FileName = [string]$Settings.fileName
    }

    if ([string]::IsNullOrWhiteSpace($script:FileName) -and (Test-JsonProperty -Object $Settings -Name "FileName")) {
        $script:FileName = [string]$Settings.FileName
    }

    if ((Test-JsonProperty -Object $Settings -Name "fileCategory") -and [string]::IsNullOrWhiteSpace($script:FileCategory)) {
        $script:FileCategory = [string]$Settings.fileCategory
    }

    if ((Test-JsonProperty -Object $Settings -Name "FileCategory") -and -not $PSBoundParameters.ContainsKey("FileCategory")) {
        $script:FileCategory = [string]$Settings.FileCategory
    }

    foreach ($property in @(
        "primaryModManagerDownload",
        "allowModManagerDownload",
        "showRequirementsPopUp",
        "updateModVersion",
        "archiveExistingFile"
    )) {
        if (Test-JsonProperty -Object $Settings -Name $property) {
            Set-Variable -Scope Script -Name ($property.Substring(0, 1).ToUpperInvariant() + $property.Substring(1)) -Value (Convert-ToSettingBool -Value $Settings.$property -Name $property)
        }
    }

    foreach ($property in @(
        "PrimaryModManagerDownload",
        "AllowModManagerDownload",
        "ShowRequirementsPopUp",
        "UpdateModVersion",
        "ArchiveExistingFile"
    )) {
        if (Test-JsonProperty -Object $Settings -Name $property) {
            Set-Variable -Scope Script -Name $property -Value (Convert-ToSettingBool -Value $Settings.$property -Name $property)
        }
    }
}

function Resolve-NexusUrl {
    if ([string]::IsNullOrWhiteSpace($script:NexusUrl)) {
        return
    }

    $match = [regex]::Match($script:NexusUrl, 'nexusmods\.com/(?<game>[^/?#]+)/mods/(?<mod>[0-9]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (!$match.Success) {
        throw "Could not parse Nexus URL: $script:NexusUrl"
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain)) {
        $script:GameDomain = $match.Groups["game"].Value
    }

    if ([string]::IsNullOrWhiteSpace($script:GameScopedModId)) {
        $script:GameScopedModId = $match.Groups["mod"].Value
    }
}

function Invoke-NexusApi {
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    if ([string]::IsNullOrWhiteSpace($script:ApiKey)) {
        throw "Missing Nexus API key. Set NEXUS_API_KEY in your environment. Do not store it in repo files or pass it on the command line."
    }

    $headers = @{
        apikey = $script:ApiKey
        accept = "application/json"
    }

    $uri = $script:ApiBaseUrl.TrimEnd("/") + $Path
    if ($Body -eq $null) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 20
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
}

function Resolve-RemoteModId {
    if (-not [string]::IsNullOrWhiteSpace($script:ModId)) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:GameDomain) -or [string]::IsNullOrWhiteSpace($script:GameScopedModId)) {
        return
    }

    if ($script:DryRun -and [string]::IsNullOrWhiteSpace($script:ApiKey)) {
        return
    }

    $response = Invoke-NexusApi -Method GET -Path ("/games/{0}/mods/{1}" -f $script:GameDomain, $script:GameScopedModId)
    if ($response -ne $null -and $response.data -ne $null -and (Test-JsonProperty -Object $response.data -Name "id")) {
        $script:ModId = [string]$response.data.id
    }
}

function Get-CurrentChangelogEntries {
    param(
        [string]$Root,
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Root) -or [string]::IsNullOrWhiteSpace($Version)) {
        return @()
    }

    $path = Join-Path $Root "CHANGELOG.txt"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return @()
    }

    $lines = Get-Content -LiteralPath $path
    $entries = New-Object "System.Collections.Generic.List[string]"
    $inSection = $false

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        $isHeader = $trimmed -match '^(?:Version\s+)?[A-Za-z0-9 ''().:_-]*\b[0-9]+(?:\.[0-9]+){1,3}\b\s*$'
        if ($isHeader) {
            if ($inSection) {
                break
            }

            if ($trimmed -match [regex]::Escape($Version)) {
                $inSection = $true
            }

            continue
        }

        if (!$inSection -or [string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $entries.Add(($trimmed -replace '^\s*[-*]\s*', ''))
    }

    return @($entries)
}

function Get-NexusMetadataText {
    param(
        [string]$Root,
        [string]$FileName,
        [int]$MaximumLength,
        [switch]$SearchParents,
        [switch]$Required
    )

    if ([string]::IsNullOrWhiteSpace($Root)) {
        if ($Required) {
            throw "Cannot read $FileName without a mod root."
        }

        return ""
    }

    $path = Join-Path $Root $FileName
    if ($SearchParents -and -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $current = [System.IO.DirectoryInfo]::new($Root)
        $repoRootFull = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd("\")
        while ($current.Parent -ne $null -and $current.FullName.StartsWith($repoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            $candidate = Join-Path $current.Parent.FullName $FileName
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $path = $candidate
                break
            }

            $current = $current.Parent
        }
    }

    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($Required) {
            throw "Missing Nexus metadata file: $path"
        }

        return ""
    }

    $text = (Get-Content -LiteralPath $path -Raw).Trim()
    if ($text.Length -gt $MaximumLength) {
        throw "$FileName is $($text.Length) characters, over Nexus limit $MaximumLength`: $path"
    }

    return $text
}

function Build-Archive {
    param(
        [string]$Root,
        [switch]$NoBuild,
        [switch]$CompileSkip,
        [string]$Destination
    )

    if (-not [string]::IsNullOrWhiteSpace($script:ArchivePath)) {
        $script:ArchivePath = [System.IO.Path]::GetFullPath($script:ArchivePath)
        if (-not (Test-Path -LiteralPath $script:ArchivePath -PathType Leaf)) {
            throw "ArchivePath not found: $script:ArchivePath"
        }

        return
    }

    if ([string]::IsNullOrWhiteSpace($Root)) {
        if ($NoBuild) {
            return
        }

        throw "Pass -Mod/-ModRoot or -ArchivePath."
    }

    if ($NoBuild) {
        throw "Pass -ArchivePath when using -SkipBuild."
    }

    $buildScript = Join-Path $PSScriptRoot "Build-Mod.ps1"
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw "Missing build script: $buildScript"
    }

    $buildArgs = @{
        ModRoot = $Root
    }

    if ($CompileSkip) {
        $buildArgs.SkipCompile = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($Destination)) {
        $buildArgs.DestinationDirectory = $Destination
    }

    $result = & $buildScript @buildArgs | Select-Object -Last 1
    if ($result -eq $null -or [string]::IsNullOrWhiteSpace([string]$result.ZipPath)) {
        throw "Build did not return a zip path."
    }

    $script:ArchivePath = [string]$result.ZipPath
}

function Wait-NexusUploadAvailable {
    param(
        [string]$UploadId,
        [int]$TimeoutSeconds = 180
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $response = Invoke-NexusApi -Method GET -Path ("/uploads/{0}" -f $UploadId)
        if ($response -ne $null -and $response.data -ne $null -and [string]$response.data.state -eq "available") {
            return
        }

        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Upload $UploadId did not become available within $TimeoutSeconds seconds."
}

function Send-PresignedUpload {
    param(
        [string]$PresignedUrl,
        [string]$Path
    )

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl -ne $null) {
        & $curl.Source `
            --fail `
            --silent `
            --show-error `
            --request PUT `
            --header "Content-Type: application/octet-stream" `
            --header "Content-Disposition;" `
            --upload-file $Path `
            $PresignedUrl

        if ($LASTEXITCODE -ne 0) {
            throw "Presigned upload failed with curl exit code $LASTEXITCODE."
        }

        return
    }

    Invoke-WebRequest `
        -Method Put `
        -Uri $PresignedUrl `
        -InFile $Path `
        -ContentType "application/octet-stream" `
        -Headers @{ "Content-Disposition" = "" } |
        Out-Null
}

function Send-MultipartUpload {
    param([string]$Path)

    $archiveItem = Get-Item -LiteralPath $Path
    $uploadRequest = @{
        size_bytes = [string]$archiveItem.Length
        filename = $archiveItem.Name
    }

    $upload = Invoke-NexusApi -Method POST -Path "/uploads/multipart" -Body $uploadRequest
    $uploadId = [string]$upload.data.id
    $partUrls = @($upload.data.part_presigned_urls)
    $partSize = [int64]$upload.data.part_size_bytes
    $completeUrl = [string]$upload.data.complete_presigned_url

    if ([string]::IsNullOrWhiteSpace($uploadId) -or $partUrls.Count -eq 0 -or $partSize -le 0 -or [string]::IsNullOrWhiteSpace($completeUrl)) {
        throw "Nexus did not return a complete multipart upload session."
    }

    $client = [System.Net.Http.HttpClient]::new()
    $stream = [System.IO.File]::OpenRead($archiveItem.FullName)
    $parts = New-Object "System.Collections.Generic.List[object]"

    try {
        for ($index = 0; $index -lt $partUrls.Count; $index++) {
            $offset = [int64]$index * $partSize
            $remaining = $stream.Length - $offset
            $bytesToRead = [int64][Math]::Min($partSize, $remaining)
            if ($bytesToRead -lt 0) {
                $bytesToRead = 0
            }

            if ($bytesToRead -gt [int]::MaxValue) {
                throw "Multipart part is too large for the local uploader buffer: $bytesToRead bytes."
            }

            $buffer = New-Object byte[] ([int]$bytesToRead)
            $stream.Seek($offset, [System.IO.SeekOrigin]::Begin) | Out-Null
            $totalRead = 0
            while ($totalRead -lt $bytesToRead) {
                $read = $stream.Read($buffer, $totalRead, ([int]$bytesToRead - $totalRead))
                if ($read -le 0) {
                    break
                }

                $totalRead += $read
            }

            if ($totalRead -ne $bytesToRead) {
                throw "Could not read multipart part $($index + 1). Expected $bytesToRead bytes, read $totalRead bytes."
            }

            $content = [System.Net.Http.ByteArrayContent]::new($buffer)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")
            $response = $client.PutAsync([string]$partUrls[$index], $content).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (-not $response.IsSuccessStatusCode) {
                throw "Failed to upload multipart part $($index + 1): $([int]$response.StatusCode) $responseBody"
            }

            $etag = ""
            if ($response.Headers.ETag -ne $null) {
                $etag = $response.Headers.ETag.Tag
            } elseif ($response.Headers.Contains("ETag")) {
                $etag = @($response.Headers.GetValues("ETag"))[0]
            }

            if ([string]::IsNullOrWhiteSpace($etag)) {
                throw "No ETag returned for multipart part $($index + 1)."
            }

            $parts.Add([pscustomobject]@{
                PartNumber = $index + 1
                ETag = $etag.Trim('"')
            })
        }
    } finally {
        $stream.Dispose()
        $client.Dispose()
    }

    $partXml = ($parts | ForEach-Object {
        "  <Part>`n    <PartNumber>$($_.PartNumber)</PartNumber>`n    <ETag>$([System.Security.SecurityElement]::Escape($_.ETag))</ETag>`n  </Part>"
    }) -join "`n"
    $xml = "<CompleteMultipartUpload>`n$partXml`n</CompleteMultipartUpload>"

    $completeClient = [System.Net.Http.HttpClient]::new()
    try {
        $completeContent = [System.Net.Http.StringContent]::new($xml, [System.Text.Encoding]::UTF8, "application/xml")
        $completeResponse = $completeClient.PostAsync($completeUrl, $completeContent).GetAwaiter().GetResult()
        $completeBody = $completeResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $completeResponse.IsSuccessStatusCode) {
            throw "Failed to complete multipart upload: $([int]$completeResponse.StatusCode) $completeBody"
        }
    } finally {
        $completeClient.Dispose()
    }

    return $uploadId
}

function Write-PublishPlan {
    param(
        [object]$Manifest,
        [string[]]$ChangelogEntries,
        [string]$PageSummary,
        [string]$FileSummary
    )

    [pscustomobject]@{
        Package = if ($Manifest -ne $null) { [string]$Manifest.packageName } else { "" }
        Version = $script:FileVersion
        Archive = $script:ArchivePath
        NexusUrl = $script:NexusUrl
        GameDomain = $script:GameDomain
        GameScopedModId = $script:GameScopedModId
        ModId = $script:ModId
        ModFileId = $script:ModFileId
        FileName = $script:FileName
        FileCategory = $script:FileCategory
        PrimaryModManagerDownload = $script:PrimaryModManagerDownload
        AllowModManagerDownload = $script:AllowModManagerDownload
        ShowRequirementsPopUp = $script:ShowRequirementsPopUp
        UpdateModVersion = $script:UpdateModVersion
        ArchiveExistingFile = $script:ArchiveExistingFile
        FileDescription = $script:FileDescription
        FileDescriptionLength = $script:FileDescription.Length
        FileSummary = $FileSummary
        FileSummaryLength = $FileSummary.Length
        PageSummary = $PageSummary
        PageSummaryLength = $PageSummary.Length
        AddChangelog = [bool]$script:AddChangelog
        ChangelogEntries = $ChangelogEntries
        DescriptionUpdate = "Manual/browser step: v3 API does not expose a main mod description update endpoint."
    }
}

$resolvedModRoot = Resolve-ModRoot -RequestedMod $Mod -RequestedModRoot $ModRoot
$manifest = $null
if (-not [string]::IsNullOrWhiteSpace($resolvedModRoot)) {
    $manifest = Read-ModManifest -Root $resolvedModRoot
    Apply-NexusManifestDefaults -Settings (Read-NexusSettings -Manifest $manifest)
    Apply-NexusManifestDefaults -Settings (Read-LocalApiSettings -Root $resolvedModRoot)
}

Resolve-NexusUrl

if ($manifest -ne $null) {
    if ([string]::IsNullOrWhiteSpace($FileName)) {
        $FileName = [string]$manifest.displayName
    }

    if ([string]::IsNullOrWhiteSpace($FileVersion)) {
        $FileVersion = [string]$manifest.version
    }
}

if ([string]::IsNullOrWhiteSpace($FileName) -and -not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    $FileName = [System.IO.Path]::GetFileNameWithoutExtension($ArchivePath)
}

Resolve-RemoteModId

if ($ListFiles) {
    if ([string]::IsNullOrWhiteSpace($ModId)) {
        throw "Could not resolve Nexus mod id. Provide -ModId or -NexusUrl with a valid API key."
    }

    $files = Invoke-NexusApi -Method GET -Path ("/mods/{0}/files" -f $ModId)
    $rows = @()
    $fileItems = if (Test-JsonProperty -Object $files.data -Name "files") { @($files.data.files) } else { @($files.data.mod_files) }
    foreach ($file in $fileItems) {
        $rows += [pscustomobject]@{
            Id = [string]$file.id
            GameScopedId = if (Test-JsonProperty -Object $file -Name "game_scoped_id") { [string]$file.game_scoped_id } else { "" }
            Name = [string]$file.name
            Category = if (Test-JsonProperty -Object $file -Name "category") { [string]$file.category } else { "" }
        }
    }

    $rows
    return
}

Build-Archive -Root $resolvedModRoot -NoBuild:$SkipBuild -CompileSkip:$SkipCompile -Destination $DestinationDirectory

if ([string]::IsNullOrWhiteSpace($FileVersion)) {
    throw "Could not infer FileVersion. Pass -FileVersion or use a mod manifest."
}

if ([string]::IsNullOrWhiteSpace($FileName)) {
    throw "Could not infer FileName. Pass -FileName or use a mod manifest."
}

$changelogEntries = @(Get-CurrentChangelogEntries -Root $resolvedModRoot -Version $FileVersion)
$pageSummary = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-page-summary.txt" -MaximumLength 350 -SearchParents
$fileSummary = Get-NexusMetadataText -Root $resolvedModRoot -FileName "nexus-file-summary.txt" -MaximumLength 255
if ([string]::IsNullOrWhiteSpace($FileDescription) -and -not [string]::IsNullOrWhiteSpace($fileSummary)) {
    $FileDescription = $fileSummary
}

if ($FileDescription.Length -gt 255) {
    throw "FileDescription is $($FileDescription.Length) characters, over Nexus file description limit 255."
}

if ($DryRun) {
    Write-PublishPlan -Manifest $manifest -ChangelogEntries $changelogEntries -PageSummary $pageSummary -FileSummary $fileSummary
    return
}

if ([string]::IsNullOrWhiteSpace($FileDescription)) {
    throw "Missing Nexus file upload description. Add nexus-file-summary.txt beside the mod or pass -FileDescription."
}

if ([string]::IsNullOrWhiteSpace($ModFileId)) {
    throw "Pass -ModFileId/-GroupId for new versions of an existing Nexus file. Use -ListFiles to inspect candidates."
}

$archiveItem = Get-Item -LiteralPath $ArchivePath
$uploadId = Send-MultipartUpload -Path $archiveItem.FullName
Invoke-NexusApi -Method POST -Path ("/uploads/{0}/finalise" -f $uploadId) | Out-Null
Wait-NexusUploadAvailable -UploadId $uploadId

$versionRequest = @{
    upload_id = $uploadId
    name = $FileName
    version = $FileVersion
    description = $FileDescription
    file_category = $FileCategory
    primary_mod_manager_download = $PrimaryModManagerDownload
    allow_mod_manager_download = $AllowModManagerDownload
    show_requirements_pop_up = $ShowRequirementsPopUp
    update_mod_version = $UpdateModVersion
    archive_existing_file = $ArchiveExistingFile
}

if (-not [string]::IsNullOrWhiteSpace($PreviousVersionId)) {
    $versionRequest.previous_version_id = $PreviousVersionId
}

$createdVersion = Invoke-NexusApi -Method POST -Path ("/mod-files/{0}/versions" -f $ModFileId) -Body $versionRequest

if ($AddChangelog) {
    if ([string]::IsNullOrWhiteSpace($ModId)) {
        throw "Cannot add changelog without ModId. Provide -ModId or -NexusUrl so it can be resolved."
    }

    if ($changelogEntries.Count -eq 0) {
        throw "Cannot add changelog: no entries found for version $FileVersion."
    }

    $changelogRequest = @{
        version = $FileVersion
        entries = @($changelogEntries)
    }

    Invoke-NexusApi -Method POST -Path ("/mods/{0}/changelogs" -f $ModId) -Body $changelogRequest | Out-Null
}

[pscustomobject]@{
    UploadedArchive = $archiveItem.FullName
    UploadId = $uploadId
    ModFileId = $ModFileId
    CreatedFileId = if ($createdVersion.data.file -ne $null) { [string]$createdVersion.data.file.id } else { "" }
    CreatedVersionId = if ($createdVersion.data.version -ne $null) { [string]$createdVersion.data.version.id } else { "" }
    Version = $FileVersion
    ChangelogAdded = [bool]$AddChangelog
    DescriptionUpdate = "Manual/browser step: v3 API does not expose a main mod description update endpoint."
}
