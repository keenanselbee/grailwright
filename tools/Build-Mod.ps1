[CmdletBinding()]
param(
    [string]$Mod = "",
    [string]$ModRoot = "",
    [string]$GameRoot = "",
    [string]$BepInExRoot = "",
    [string]$VortexModsRoot = "",
    [string]$DestinationDirectory = "",
    [switch]$SkipCompile,
    [switch]$StageToVortex,
    [switch]$DesktopOnly,
    [switch]$PackageOnly,
    [switch]$KeepScratch,
    [int]$LockWaitSeconds = 0,
    [int]$LockStaleAfterMinutes = 720,
    [switch]$ForceStaleLock
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$outputModes = @($StageToVortex.IsPresent, $DesktopOnly.IsPresent, $PackageOnly.IsPresent)
$outputModeCount = @($outputModes | Where-Object { $_ }).Count
if ($outputModeCount -gt 1) {
    throw "Use only one output mode: -StageToVortex, -DesktopOnly, or -PackageOnly."
}
if ($DesktopOnly -and -not [string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    throw "-DesktopOnly cannot be combined with -DestinationDirectory; it always exports to the Windows Desktop."
}
if ($PackageOnly -and [string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    throw "-PackageOnly requires an explicit -DestinationDirectory."
}

$shouldStageToVortex = -not $DesktopOnly -and -not $PackageOnly

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LockScript = Join-Path $PSScriptRoot "Lock-Operation.ps1"
if (-not (Test-Path -LiteralPath $LockScript -PathType Leaf)) {
    throw "Missing lock helper: $LockScript"
}

. $LockScript

function Get-SteamRootCandidates {
    $roots = New-Object "System.Collections.Generic.List[string]"

    foreach ($key in @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )) {
        try {
            $props = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
            foreach ($name in @("SteamPath", "InstallPath")) {
                $value = $props.$name
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $roots.Add(($value -replace "/", "\"))
                }
            }
        } catch {
        }
    }

    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $roots.Add((Join-Path $programFilesX86 "Steam"))
    }

    foreach ($root in @($roots | Select-Object -Unique)) {
        $root
        $libraryFile = Join-Path $root "steamapps\libraryfolders.vdf"
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $libraryFile) {
            if ($line -match '^\s*"(?:path|\d+)"\s+"([^"]+)"') {
                $libraryRoot = $matches[1] -replace "\\\\", "\"
                if (-not [string]::IsNullOrWhiteSpace($libraryRoot)) {
                    $libraryRoot
                }
            }
        }
    }
}

function Resolve-GameRoot {
    param([string]$Candidate)

    $candidates = New-Object "System.Collections.Generic.List[string]"
    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        $candidates.Add($Candidate)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:TAINTED_GRAIL_FOA_DIR)) {
        $candidates.Add($env:TAINTED_GRAIL_FOA_DIR)
    }

    $candidates.Add("G:\Steam\steamapps\common\Tainted Grail FoA")

    foreach ($steamRoot in Get-SteamRootCandidates) {
        $candidates.Add((Join-Path $steamRoot "steamapps\common\Tainted Grail FoA"))
    }

    $resolved = $candidates | Select-Object -Unique | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ "Fall of Avalon_Data\Managed\UnityEngine.dll") -PathType Leaf
    } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($resolved)) {
        throw "Could not find Tainted Grail FoA. Pass -GameRoot."
    }

    return [System.IO.Path]::GetFullPath($resolved)
}

function Resolve-VortexModsRoot {
    param([string]$Candidate)

    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        return [System.IO.Path]::GetFullPath($Candidate)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $env:APPDATA "Vortex\taintedgrailthefallofavalon\mods"))
}

function Resolve-BepInExRoot {
    param(
        [string]$Candidate,
        [string]$ResolvedGameRoot,
        [string]$ResolvedVortexModsRoot
    )

    $candidates = New-Object "System.Collections.Generic.List[string]"
    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        $candidates.Add($Candidate)
    }

    $candidates.Add($ResolvedGameRoot)

    if (Test-Path -LiteralPath $ResolvedVortexModsRoot -PathType Container) {
        Get-ChildItem -LiteralPath $ResolvedVortexModsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "BepInEx Mono Windows x64*" } |
            Sort-Object LastWriteTime -Descending |
            ForEach-Object { $candidates.Add($_.FullName) }
    }

    $resolved = $candidates | Select-Object -Unique | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ "BepInEx\core\BepInEx.dll") -PathType Leaf
    } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($resolved)) {
        throw "Could not find BepInEx. Pass -BepInExRoot."
    }

    return [System.IO.Path]::GetFullPath($resolved)
}

function Read-ModManifest {
    param([string]$Root)

    $manifestPath = Join-Path $Root "mod.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing mod manifest: $manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $version = [string]$manifest.version
    if ($version -notmatch '^[0-9]+\.[0-9]\.[0-9]$') {
        throw "Invalid authored mod version '$version' in $manifestPath. Use MAJOR.MINOR.PATCH with single-digit MINOR and PATCH components."
    }

    return $manifest
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
        throw "Pass -Mod or -ModRoot."
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

function Expand-TokenPath {
    param(
        [string]$Value,
        [string]$ResolvedModRoot,
        [string]$ResolvedGameRoot,
        [string]$ResolvedBepInExRoot,
        [string]$ResolvedVortexModsRoot
    )

    $expanded = $Value
    $expanded = $expanded.Replace("%MOD%", $ResolvedModRoot)
    $expanded = $expanded.Replace("%REPO%", $RepoRoot)
    $expanded = $expanded.Replace("%GAME%", $ResolvedGameRoot)
    $expanded = $expanded.Replace("%BEPINEX%", $ResolvedBepInExRoot)
    $expanded = $expanded.Replace("%VORTEX_MODS%", $ResolvedVortexModsRoot)
    $expanded = $expanded -replace "/", "\"
    return [System.IO.Path]::GetFullPath($expanded)
}

function Test-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
    )

    return $Object.PSObject.Properties.Name -contains $Name
}

function Resolve-Reference {
    param(
        [object]$Reference,
        [string]$ResolvedModRoot,
        [string]$ResolvedGameRoot,
        [string]$ResolvedBepInExRoot,
        [string]$ResolvedVortexModsRoot
    )

    $optional = $false
    if ($Reference -is [string]) {
        $path = Expand-TokenPath -Value $Reference -ResolvedModRoot $ResolvedModRoot -ResolvedGameRoot $ResolvedGameRoot -ResolvedBepInExRoot $ResolvedBepInExRoot -ResolvedVortexModsRoot $ResolvedVortexModsRoot
    } elseif (Test-JsonProperty -Object $Reference -Name "path") {
        if (Test-JsonProperty -Object $Reference -Name "optional") {
            $optional = [bool]$Reference.optional
        }

        $path = Expand-TokenPath -Value ([string]$Reference.path) -ResolvedModRoot $ResolvedModRoot -ResolvedGameRoot $ResolvedGameRoot -ResolvedBepInExRoot $ResolvedBepInExRoot -ResolvedVortexModsRoot $ResolvedVortexModsRoot
    } elseif (Test-JsonProperty -Object $Reference -Name "vortexLatest") {
        if (Test-JsonProperty -Object $Reference -Name "optional") {
            $optional = [bool]$Reference.optional
        }

        $relativePath = ([string]$Reference.relativePath) -replace "/", "\"
        $candidates = Get-ChildItem -LiteralPath $ResolvedVortexModsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like ([string]$Reference.vortexLatest) } |
            Sort-Object LastWriteTime -Descending

        $path = ""
        foreach ($candidate in $candidates) {
            $candidatePath = Join-Path $candidate.FullName $relativePath
            if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
                $path = [System.IO.Path]::GetFullPath($candidatePath)
                break
            }
        }
    } else {
        throw "Unsupported reference entry in manifest."
    }

    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($optional) {
            return $null
        }

        throw "Reference not found: $path"
    }

    return $path
}

function Resolve-RoslynCompiler {
    $dotnetHosts = New-Object "System.Collections.Generic.List[string]"
    $programFiles = [Environment]::GetFolderPath("ProgramFiles")
    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        $dotnetHosts.Add((Join-Path $programFiles "dotnet\dotnet.exe"))
    }

    $pathDotNet = Get-Command "dotnet.exe" -ErrorAction SilentlyContinue
    if ($null -ne $pathDotNet) {
        $dotnetHosts.Add($pathDotNet.Source)
    }

    foreach ($dotnetHost in @($dotnetHosts | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $dotnetHost -PathType Leaf)) {
            continue
        }

        $installedSdks = @(& $dotnetHost --list-sdks 2>$null)
        if ($LASTEXITCODE -ne 0) {
            continue
        }

        for ($index = $installedSdks.Count - 1; $index -ge 0; $index--) {
            if ($installedSdks[$index] -notmatch '^(\S+)\s+\[(.+)\]$') {
                continue
            }

            $sdkVersion = $matches[1]
            $sdkRoot = $matches[2]
            $compiler = Join-Path $sdkRoot "$sdkVersion\Roslyn\bincore\csc.dll"
            if (Test-Path -LiteralPath $compiler -PathType Leaf) {
                return [pscustomobject]@{
                    Host = $dotnetHost
                    Compiler = $compiler
                    SdkVersion = $sdkVersion
                    LanguageVersion = "7.3"
                }
            }
        }
    }

    return $null
}

function Resolve-CSharpCompiler {
    $roslyn = Resolve-RoslynCompiler
    if ($null -ne $roslyn) {
        return $roslyn
    }

    throw "No .NET SDK Roslyn compiler was found. Install a .NET SDK with Roslyn\bincore\csc.dll. Searched the Program Files dotnet host and dotnet.exe on PATH."
}

function Get-DesktopDirectory {
    $desktop = [Environment]::GetFolderPath("DesktopDirectory")
    if ([string]::IsNullOrWhiteSpace($desktop)) {
        $desktop = Join-Path $HOME "Desktop"
    }

    return $desktop
}

$ResolvedModRoot = Resolve-ModRoot -RequestedMod $Mod -RequestedModRoot $ModRoot
$Manifest = Read-ModManifest -Root $ResolvedModRoot
$lockModName = Split-Path -Leaf $ResolvedModRoot
if ((Test-JsonProperty -Object $Manifest -Name "packageName") -and -not [string]::IsNullOrWhiteSpace([string]$Manifest.packageName)) {
    $lockModName = [string]$Manifest.packageName
} elseif ((Test-JsonProperty -Object $Manifest -Name "id") -and -not [string]::IsNullOrWhiteSpace([string]$Manifest.id)) {
    $lockModName = [string]$Manifest.id
}

$buildAction = if ($shouldStageToVortex) {
    "build-stage-mod"
} elseif ($DesktopOnly) {
    "build-desktop-mod"
} else {
    "build-package-mod"
}
$modLock = Enter-GrailwrightLock -Name "mod-$lockModName" -Action $buildAction -Mod $lockModName -RepoRoot $RepoRoot -TimeoutSeconds $LockWaitSeconds -StaleAfterMinutes $LockStaleAfterMinutes -ForceStaleLock:$ForceStaleLock

try {
$Manifest = Read-ModManifest -Root $ResolvedModRoot
$ResolvedGameRoot = Resolve-GameRoot -Candidate $GameRoot
$ResolvedVortexModsRoot = Resolve-VortexModsRoot -Candidate $VortexModsRoot
$ResolvedBepInExRoot = Resolve-BepInExRoot -Candidate $BepInExRoot -ResolvedGameRoot $ResolvedGameRoot -ResolvedVortexModsRoot $ResolvedVortexModsRoot

if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = if ($DesktopOnly) {
        Get-DesktopDirectory
    } else {
        Join-Path $RepoRoot ".codex-temp\packages"
    }
}

if (-not $SkipCompile) {
    $compiler = Resolve-CSharpCompiler

    $sourceFiles = @($Manifest.sourceFiles)
    if ($sourceFiles.Count -eq 0) {
        throw "Manifest $($Manifest.id) has no sourceFiles."
    }

    $resolvedSources = @()
    foreach ($sourceFile in $sourceFiles) {
        $path = Expand-TokenPath -Value (Join-Path $ResolvedModRoot ([string]$sourceFile)) -ResolvedModRoot $ResolvedModRoot -ResolvedGameRoot $ResolvedGameRoot -ResolvedBepInExRoot $ResolvedBepInExRoot -ResolvedVortexModsRoot $ResolvedVortexModsRoot
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Source file not found: $path"
        }

        $resolvedSources += $path
    }

    $managedRoot = Join-Path $ResolvedGameRoot "Fall of Avalon_Data\Managed"
    $coreReferenceNames = @(
        "mscorlib.dll",
        "System.dll",
        "System.Core.dll"
    )

    $references = @()
    foreach ($coreReferenceName in $coreReferenceNames) {
        $coreReference = Join-Path $managedRoot $coreReferenceName
        if (-not (Test-Path -LiteralPath $coreReference -PathType Leaf)) {
            throw "Game framework reference not found: $coreReference"
        }

        $references += $coreReference
    }

    foreach ($reference in @($Manifest.references)) {
        $resolvedReference = Resolve-Reference -Reference $reference -ResolvedModRoot $ResolvedModRoot -ResolvedGameRoot $ResolvedGameRoot -ResolvedBepInExRoot $ResolvedBepInExRoot -ResolvedVortexModsRoot $ResolvedVortexModsRoot
        if (
            -not [string]::IsNullOrWhiteSpace($resolvedReference) -and
            $coreReferenceNames -inotcontains (Split-Path -Leaf $resolvedReference) -and
            $references -inotcontains $resolvedReference
        ) {
            $references += $resolvedReference
        }
    }

    $output = Join-Path $ResolvedModRoot ([string]$Manifest.dll)
    $compilerArgs = @(
        "/nologo",
        "/noconfig",
        "/nostdlib+",
        "/target:library",
        "/optimize+",
        "/out:$output"
    )

    $compilerArgs += "/langversion:$($compiler.LanguageVersion)"
    if ($Manifest.PSObject.Properties.Name -contains "allowUnsafe" -and [bool]$Manifest.allowUnsafe) {
        $compilerArgs += "/unsafe+"
    }
    Write-Host "Compiler: .NET SDK $($compiler.SdkVersion) Roslyn (C# $($compiler.LanguageVersion))"
    Write-Host "Compiler path: $($compiler.Compiler)"

    foreach ($reference in $references) {
        $compilerArgs += "/reference:$reference"
    }

    foreach ($source in $resolvedSources) {
        $compilerArgs += $source
    }

    & $compiler.Host $compiler.Compiler @compilerArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Compiler failed for $($Manifest.id) with exit code $LASTEXITCODE"
    }
}

$exportScript = Join-Path $PSScriptRoot "Export-VortexPackage.ps1"
if (-not (Test-Path -LiteralPath $exportScript -PathType Leaf)) {
    throw "Missing export script: $exportScript"
}

$exportArgs = @{
    ModRoot = $ResolvedModRoot
    PackageName = [string]$Manifest.packageName
    ArchiveName = [string]$Manifest.displayName
    Version = [string]$Manifest.version
    DestinationDirectory = $DestinationDirectory
}

if ($KeepScratch) {
    $exportArgs.KeepScratch = $true
}

$exportResult = & $exportScript @exportArgs

if ($shouldStageToVortex) {
    $stageScript = Join-Path $PSScriptRoot "Stage-VortexMod.ps1"
    if (-not (Test-Path -LiteralPath $stageScript -PathType Leaf)) {
        throw "Missing Vortex staging script: $stageScript"
    }

    $zipPath = ($exportResult | Select-Object -Last 1).ZipPath
    if ([string]::IsNullOrWhiteSpace($zipPath)) {
        throw "Export did not return a zip path for $($Manifest.id)."
    }

    $stageArgs = @{
        ModRoot = $ResolvedModRoot
        PackageArchive = [string]$zipPath
        VortexModsRoot = $ResolvedVortexModsRoot
    }

    if ($KeepScratch) {
        $stageArgs.KeepScratch = $true
    }

    & $stageScript @stageArgs
} else {
    $exportResult
}
} finally {
    Exit-GrailwrightLock -Lock $modLock
}
