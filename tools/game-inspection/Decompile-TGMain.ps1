param(
    [string] $GameRoot = "G:\Steam\steamapps\common\Tainted Grail FoA",
    [string] $OutputDirectory = "",
    [string] $GameVersionLabel = "1.25",
    [switch] $Force
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$cacheRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".codex-temp\decompiled"))

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $cacheRoot "TG.Main-$GameVersionLabel"
}

$gameRootFull = [System.IO.Path]::GetFullPath($GameRoot)
$managedDir = Join-Path $gameRootFull "Fall of Avalon_Data\Managed"
$assemblyPath = Join-Path $managedDir "TG.Main.dll"
$outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Could not find TG.Main.dll at '$assemblyPath'. Pass -GameRoot if the game moved."
}

if ((Test-Path -LiteralPath $outputFull) -and -not $Force) {
    throw "Output directory already exists: '$outputFull'. Re-run with -Force to replace it."
}

if ($Force -and (Test-Path -LiteralPath $outputFull)) {
    if (-not $outputFull.StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete output outside the decompile cache: '$outputFull'."
    }

    Remove-Item -LiteralPath $outputFull -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $outputFull | Out-Null

$ilspyWrapper = Join-Path $PSScriptRoot "Invoke-ILSpy.ps1"
$ilspyArgs = @(
    "--nested-directories",
    "-p",
    "-r",
    $managedDir,
    "-o",
    $outputFull,
    $assemblyPath
)

& powershell -NoProfile -ExecutionPolicy Bypass -File $ilspyWrapper @ilspyArgs
if ($LASTEXITCODE -ne 0) {
    throw "ILSpy failed with exit code $LASTEXITCODE."
}

$stampPath = Join-Path $outputFull "_decompile-info.txt"
$assemblyFile = Get-Item -LiteralPath $assemblyPath
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
@(
    "SourceAssembly=$assemblyPath",
    "AssemblyFullName=$($assemblyName.FullName)",
    "AssemblyLastWriteTime=$($assemblyFile.LastWriteTime.ToString("o"))",
    "AssemblyLength=$($assemblyFile.Length)",
    "GameRoot=$gameRootFull",
    "GameVersionLabel=$GameVersionLabel",
    "GeneratedAt=$([DateTimeOffset]::Now.ToString("o"))"
) | Set-Content -LiteralPath $stampPath -Encoding UTF8

Write-Host "Decompiled TG.Main.dll to '$outputFull'."
Write-Host "Wrote '$stampPath'."
