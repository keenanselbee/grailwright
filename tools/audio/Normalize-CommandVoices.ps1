[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$InputDirectory = "",
    [string]$OutputDirectory = "",
    [string]$FfmpegPath = "",
    [double]$TargetLufs = -15.0,
    [double]$TruePeakCeilingDb = -2.0,
    [double]$CompressorThresholdDb = -18.0,
    [double]$CompressionRatio = 1.75,
    [double]$MaximumPoolSpreadDb = 2.0,
    [switch]$AnalyzeOnly,
    [switch]$VerifyOnly,
    [switch]$ReplaceOriginals
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-FfmpegPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($RequestedPath)
        }

        throw "ffmpeg was not found at: $RequestedPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:FFMPEG_PATH) -and
        (Test-Path -LiteralPath $env:FFMPEG_PATH -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:FFMPEG_PATH)
    }

    $command = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($candidate in @(
        "$env:ProgramFiles\Topaz Labs LLC\Topaz Video AI\ffmpeg.exe",
        "$env:ProgramFiles\ffmpeg\bin\ffmpeg.exe",
        "${env:ProgramFiles(x86)}\ffmpeg\bin\ffmpeg.exe")) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "ffmpeg was not found. Install ffmpeg, add it to PATH, set FFMPEG_PATH, or pass -FfmpegPath."
}

function ConvertTo-InvariantText {
    param([double]$Value)

    return $Value.ToString(
        "0.###",
        [System.Globalization.CultureInfo]::InvariantCulture)
}

function ConvertFrom-InvariantText {
    param([string]$Value)

    return [double]::Parse(
        $Value,
        [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-LoudnessJson {
    param(
        [Parameter(Mandatory = $true)][string[]]$Output,
        [Parameter(Mandatory = $true)][string]$InputFile
    )

    $match = [regex]::Match(
        [string]::Join("`n", $Output),
        '\{\s*"input_i".*?\}',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (!$match.Success) {
        throw "Could not parse ffmpeg loudness output for $InputFile"
    }

    return $match.Value | ConvertFrom-Json
}

function Get-LoudnessMeasurement {
    param(
        [Parameter(Mandatory = $true)][string]$Ffmpeg,
        [Parameter(Mandatory = $true)][string]$InputFile,
        [Parameter(Mandatory = $true)][double]$TargetLufs,
        [Parameter(Mandatory = $true)][double]$TruePeakCeiling
    )

    $targetText = ConvertTo-InvariantText $TargetLufs
    $peakText = ConvertTo-InvariantText $TruePeakCeiling
    $filter = "loudnorm=I=${targetText}:LRA=7:TP=${peakText}:print_format=json"
    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Ffmpeg `
            -hide_banner `
            -nostats `
            -i $InputFile `
            -af $filter `
            -f null `
            NUL 2>&1 | ForEach-Object { $_.ToString() }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "ffmpeg loudness analysis failed for $InputFile"
    }

    $data = Get-LoudnessJson -Output $output -InputFile $InputFile
    return [pscustomobject]@{
        IntegratedLufs = ConvertFrom-InvariantText $data.input_i
        LoudnessRange = ConvertFrom-InvariantText $data.input_lra
        TruePeakDb = ConvertFrom-InvariantText $data.input_tp
        ThresholdLufs = ConvertFrom-InvariantText $data.input_thresh
        TargetOffsetDb = ConvertFrom-InvariantText $data.target_offset
    }
}

function Write-NormalizedFile {
    param(
        [Parameter(Mandatory = $true)][string]$Ffmpeg,
        [Parameter(Mandatory = $true)][string]$InputFile,
        [Parameter(Mandatory = $true)][string]$OutputFile,
        [Parameter(Mandatory = $true)][double]$TargetLufs,
        [Parameter(Mandatory = $true)][double]$TruePeakCeiling,
        [Parameter(Mandatory = $true)][double]$CompressorThreshold,
        [Parameter(Mandatory = $true)][double]$CompressorRatio
    )

    $targetText = ConvertTo-InvariantText $TargetLufs
    $peakText = ConvertTo-InvariantText $TruePeakCeiling
    $thresholdAmplitude = [Math]::Pow(10.0, $CompressorThreshold / 20.0)
    $thresholdAmplitudeText = ConvertTo-InvariantText $thresholdAmplitude
    $ratioText = ConvertTo-InvariantText $CompressorRatio
    $filter = "acompressor=threshold=${thresholdAmplitudeText}:" +
        "ratio=${ratioText}:attack=10:release=100:makeup=1," +
        "loudnorm=I=${targetText}:LRA=7:TP=${peakText}:print_format=summary"
    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $Ffmpeg `
            -hide_banner `
            -nostats `
            -y `
            -i $InputFile `
            -vn `
            -ac 2 `
            -ar 48000 `
            -c:a pcm_s16le `
            -af $filter `
            $OutputFile 2>&1 | Out-Null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "ffmpeg normalization failed for $InputFile"
    }
}

if ($TargetLufs -gt -5.0 -or $TargetLufs -lt -70.0) {
    throw "TargetLufs must be between -70.0 and -5.0 LUFS."
}
if ($TruePeakCeilingDb -gt -0.1 -or $TruePeakCeilingDb -lt -9.0) {
    throw "TruePeakCeilingDb must be between -9.0 and -0.1 dBTP."
}
if ($MaximumPoolSpreadDb -le 0.0) {
    throw "MaximumPoolSpreadDb must be greater than zero."
}
if ($CompressorThresholdDb -gt -1.0 -or $CompressorThresholdDb -lt -60.0) {
    throw "CompressorThresholdDb must be between -60.0 and -1.0 dBFS."
}
if ($CompressionRatio -lt 1.0 -or $CompressionRatio -gt 5.0) {
    throw "CompressionRatio must be between 1.0 and 5.0."
}
$selectedModes = @(@($AnalyzeOnly, $VerifyOnly, $ReplaceOriginals) |
    Where-Object { [bool]$_ })
if ($selectedModes.Count -gt 1) {
    throw "AnalyzeOnly, VerifyOnly, and ReplaceOriginals are mutually exclusive."
}

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($InputDirectory)) {
    $InputDirectory = Join-Path `
        $repositoryRoot `
        "mods\BattlecryVoiceTuner\audio\command"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        $repositoryRoot `
        ".codex-temp\audio-normalized\BattlecryVoiceTuner\command"
}

$inputFull = [System.IO.Path]::GetFullPath($InputDirectory)
$outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath $inputFull -PathType Container)) {
    throw "Command voice input directory was not found: $inputFull"
}
if ($inputFull.Equals($outputFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must differ from InputDirectory."
}

$ffmpeg = Get-FfmpegPath -RequestedPath $FfmpegPath
$files = @(Get-ChildItem -LiteralPath $inputFull -File -Filter "*.wav" |
    Where-Object {
        $_.Name -match '^summon_(male|female)_(attack|hold|follow|recall|raiseall|guard|bulwark|hunt)_\d+\.wav$'
    } |
    Sort-Object Name)
if ($files.Count -eq 0) {
    throw "No recognized command WAV files were found under $inputFull"
}

$sourceMeasurements = foreach ($file in $files) {
    if ($file.Name -notmatch '^summon_(male|female)_([a-z]+)_') {
        throw "Unexpected command voice filename: $($file.Name)"
    }

    $gender = $Matches[1]
    $command = $Matches[2]
    $measurement = Get-LoudnessMeasurement `
        -Ffmpeg $ffmpeg `
        -InputFile $file.FullName `
        -TargetLufs -18.0 `
        -TruePeakCeiling $TruePeakCeilingDb
    [pscustomobject]@{
        File = $file
        Gender = $gender
        Command = $command
        Pool = $gender + ":" + $command
        IntegratedLufs = $measurement.IntegratedLufs
        TruePeakDb = $measurement.TruePeakDb
    }
}

$sourcePoolSummary = @($sourceMeasurements |
    Group-Object Pool |
    ForEach-Object {
        $minimum = ($_.Group.IntegratedLufs | Measure-Object -Minimum).Minimum
        $maximum = ($_.Group.IntegratedLufs | Measure-Object -Maximum).Maximum
        [pscustomobject]@{
            Pool = $_.Name
            Count = $_.Count
            MinimumLufs = $minimum
            MaximumLufs = $maximum
            SpreadDb = [Math]::Round($maximum - $minimum, 2)
            MaximumTruePeakDb =
                ($_.Group.TruePeakDb | Measure-Object -Maximum).Maximum
        }
    } |
    Sort-Object Pool)
if ($VerifyOnly) {
    $sourcePoolSummary | Format-Table -AutoSize
    $spreadViolations = @($sourcePoolSummary |
        Where-Object { $_.SpreadDb -gt $MaximumPoolSpreadDb })
    $peakViolations = @($sourceMeasurements |
        Where-Object {
            $_.TruePeakDb -gt ($TruePeakCeilingDb + 0.15)
        })
    if ($spreadViolations.Count -gt 0 -or $peakViolations.Count -gt 0) {
        throw "Command voices do not satisfy the configured loudness contract."
    }

    Write-Host "Command voice loudness contract passed: $($files.Count) WAV files, maximum pool spread $MaximumPoolSpreadDb dB, true-peak ceiling $TruePeakCeilingDb dBTP."
    return
}

$planned = foreach ($measurement in $sourceMeasurements) {
    [pscustomobject]@{
        File = $measurement.File
        Gender = $measurement.Gender
        Command = $measurement.Command
        Pool = $measurement.Pool
        InputLufs = $measurement.IntegratedLufs
        InputTruePeakDb = $measurement.TruePeakDb
        TargetLufs = $TargetLufs
        AdjustmentDb = $TargetLufs - $measurement.IntegratedLufs
    }
}

if ($AnalyzeOnly) {
    $planned |
        Select-Object `
            @{ Name = "File"; Expression = { $_.File.Name } },
            Pool,
            InputLufs,
            InputTruePeakDb,
            TargetLufs,
            AdjustmentDb |
        Format-Table -AutoSize
    return
}

New-Item -ItemType Directory -Force -Path $outputFull | Out-Null
foreach ($item in $planned) {
    Write-NormalizedFile `
        -Ffmpeg $ffmpeg `
        -InputFile $item.File.FullName `
        -OutputFile (Join-Path $outputFull $item.File.Name) `
        -TargetLufs $item.TargetLufs `
        -TruePeakCeiling $TruePeakCeilingDb `
        -CompressorThreshold $CompressorThresholdDb `
        -CompressorRatio $CompressionRatio
}

$normalizedMeasurements = foreach ($item in $planned) {
    $outputFile = Join-Path $outputFull $item.File.Name
    $measurement = Get-LoudnessMeasurement `
        -Ffmpeg $ffmpeg `
        -InputFile $outputFile `
        -TargetLufs $item.TargetLufs `
        -TruePeakCeiling $TruePeakCeilingDb
    [pscustomobject]@{
        File = $item.File.Name
        Pool = $item.Pool
        InputLufs = $item.InputLufs
        OutputLufs = $measurement.IntegratedLufs
        OutputTruePeakDb = $measurement.TruePeakDb
        AppliedChangeDb = $measurement.IntegratedLufs - $item.InputLufs
    }
}

$poolSummary = @($normalizedMeasurements |
    Group-Object Pool |
    ForEach-Object {
        $minimum = ($_.Group.OutputLufs | Measure-Object -Minimum).Minimum
        $maximum = ($_.Group.OutputLufs | Measure-Object -Maximum).Maximum
        [pscustomobject]@{
            Pool = $_.Name
            Count = $_.Count
            MinimumLufs = $minimum
            MaximumLufs = $maximum
            SpreadDb = [Math]::Round($maximum - $minimum, 2)
        }
    } |
    Sort-Object Pool)
$spreadViolations = @($poolSummary |
    Where-Object { $_.SpreadDb -gt $MaximumPoolSpreadDb })
$peakViolations = @($normalizedMeasurements |
    Where-Object {
        $_.OutputTruePeakDb -gt ($TruePeakCeilingDb + 0.15)
    })
if ($spreadViolations.Count -gt 0 -or $peakViolations.Count -gt 0) {
    $poolSummary | Format-Table -AutoSize
    if ($peakViolations.Count -gt 0) {
        $peakViolations |
            Select-Object File, OutputTruePeakDb |
            Format-Table -AutoSize
    }
    throw "Normalized command voices did not satisfy the configured loudness contract; originals were not replaced."
}

$backupFull = $null
if ($ReplaceOriginals -and
    $PSCmdlet.ShouldProcess($inputFull, "Replace command WAVs with validated normalized files")) {
    $backupName = "BattlecryVoiceTuner-command-" +
        [DateTime]::Now.ToString("yyyyMMdd-HHmmss-fff")
    $backupFull = Join-Path `
        (Join-Path $repositoryRoot ".codex-temp\audio-backups") `
        $backupName
    New-Item -ItemType Directory -Force -Path $backupFull | Out-Null
    foreach ($file in $files) {
        Copy-Item `
            -LiteralPath $file.FullName `
            -Destination (Join-Path $backupFull $file.Name)
    }
    foreach ($file in $files) {
        Copy-Item `
            -LiteralPath (Join-Path $outputFull $file.Name) `
            -Destination $file.FullName `
            -Force
    }
}

$normalizedMeasurements |
    Select-Object File, Pool, InputLufs, OutputLufs, OutputTruePeakDb, AppliedChangeDb |
    Format-Table -AutoSize
$poolSummary | Format-Table -AutoSize
Write-Host "Normalized command voices passed: $($files.Count) WAV files, maximum pool spread $MaximumPoolSpreadDb dB, true-peak ceiling $TruePeakCeilingDb dBTP."
Write-Host "Preview directory: $outputFull"
if ($backupFull) {
    Write-Host "Original backup: $backupFull"
}
