[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$InputFiles,

    [string]$OutputDirectory = "",
    [string]$Prefix = "killing_blow",
    [int]$StartIndex = 1,
    [double]$TargetPeakDb = -3.0,
    [int]$SampleRate = 44100,
    [switch]$Stereo,
    [string]$FfmpegPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-Ffmpeg {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($RequestedPath)
        }

        throw "ffmpeg was not found at: $RequestedPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:FFMPEG_PATH) -and (Test-Path -LiteralPath $env:FFMPEG_PATH -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:FFMPEG_PATH)
    }

    $command = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $commonCandidates = @(
        "$env:ProgramFiles\Topaz Labs LLC\Topaz Video AI\ffmpeg.exe",
        "$env:ProgramFiles\ffmpeg\bin\ffmpeg.exe",
        "${env:ProgramFiles(x86)}\ffmpeg\bin\ffmpeg.exe"
    )

    foreach ($candidate in $commonCandidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "ffmpeg was not found. Install ffmpeg, add it to PATH, set FFMPEG_PATH, or pass -FfmpegPath."
}

function Get-MaxVolumeDb {
    param(
        [Parameter(Mandatory = $true)][string]$Ffmpeg,
        [Parameter(Mandatory = $true)][string]$InputFile
    )

    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Ffmpeg -hide_banner -nostats -i $InputFile -af volumedetect -f null NUL 2>&1 |
            ForEach-Object { $_.ToString() }
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg volumedetect failed for $InputFile"
    }

    $maxVolumeLine = $output | Where-Object { $_ -match 'max_volume:\s*(-?(?:\d+(?:\.\d+)?|inf))\s*dB' } | Select-Object -Last 1
    if (-not $maxVolumeLine) {
        throw "Could not read max_volume for $InputFile"
    }

    $match = [regex]::Match($maxVolumeLine.ToString(), 'max_volume:\s*(-?(?:\d+(?:\.\d+)?|inf))\s*dB')
    if (-not $match.Success -or $match.Groups[1].Value -eq "-inf") {
        throw "Input appears silent or unreadable: $InputFile"
    }

    return [double]::Parse($match.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

if ($StartIndex -lt 1) {
    throw "StartIndex must be 1 or greater."
}

if ($SampleRate -lt 8000) {
    throw "SampleRate must be at least 8000."
}

if ([string]::IsNullOrWhiteSpace($Prefix) -or $Prefix -match '[\\/:*?"<>|]') {
    throw "Prefix must be a non-empty filename-safe value."
}

$ffmpeg = Find-Ffmpeg -RequestedPath $FfmpegPath

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $modRoot = Split-Path -Parent $PSScriptRoot
    $OutputDirectory = Join-Path $modRoot "audio"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$channels = if ($Stereo) { 2 } else { 1 }
$index = $StartIndex
$results = @()

foreach ($input in $InputFiles) {
    if (-not (Test-Path -LiteralPath $input -PathType Leaf)) {
        throw "Input file not found: $input"
    }

    $inputFull = [System.IO.Path]::GetFullPath($input)
    $outputFile = Join-Path $OutputDirectory ($Prefix + $index.ToString([System.Globalization.CultureInfo]::InvariantCulture) + ".wav")
    $maxVolumeDb = Get-MaxVolumeDb -Ffmpeg $ffmpeg -InputFile $inputFull
    $gainDb = $TargetPeakDb - $maxVolumeDb
    $gainText = $gainDb.ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture)

    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $ffmpeg `
            -hide_banner `
            -y `
            -i $inputFull `
            -vn `
            -ac $channels `
            -ar $SampleRate `
            -c:a pcm_s16le `
            -af "volume=${gainText}dB" `
            $outputFile 2>&1 | Out-Null
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed while converting $inputFull"
    }

    $results += [pscustomobject]@{
        Input = $inputFull
        Output = [System.IO.Path]::GetFullPath($outputFile)
        MaxVolumeDb = $maxVolumeDb
        GainDb = $gainDb
        TargetPeakDb = $TargetPeakDb
    }

    $index++
}

$results | Format-Table -AutoSize
