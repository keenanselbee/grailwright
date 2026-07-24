[CmdletBinding()]
param(
    [string]$ModRoot = "",
    [string]$DestinationDirectory = "",
    [string]$PackageName = "",
    [string]$ArchiveName = "",
    [string]$Version = "",
    [switch]$KeepScratch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScriptPath {
    if ($PSCommandPath) {
        return [System.IO.Path]::GetFullPath($PSCommandPath)
    }

    if ($MyInvocation.MyCommand.Path) {
        return [System.IO.Path]::GetFullPath($MyInvocation.MyCommand.Path)
    }

    return ""
}

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd("\") + "\"
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (-not $pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected root: $pathFull"
    }

    return $pathFull.Substring($rootFull.Length)
}

function Convert-ToPackageFileName {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name -replace '[\\/:*?"<>|]', "-"
    $safe = $safe -replace "\s+", ""
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe package name."
    }

    return $safe
}

function Convert-ToArchiveFileNameStem {
    param([Parameter(Mandatory = $true)][string]$Name)

    $safe = $Name -replace '[\\/:*?"<>|]', "-"
    $safe = $safe -replace "\s+", " "
    $safe = $safe.Trim(".- ")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Could not infer a safe archive name."
    }

    return $safe
}

function Get-TextFileContent {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return Get-Content -LiteralPath $Path -Raw
    }

    return ""
}

function Find-ModVersion {
    param([Parameter(Mandatory = $true)][string]$Root)

    $candidateFiles = @(
        "CHANGELOG.txt",
        "CHANGELOG.md",
        "README.txt",
        "README.md",
        "nexus-desc.txt"
    ) | ForEach-Object { Join-Path $Root $_ }

    $candidateFiles += Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "^(README|CHANGELOG|nexus-desc).*\.(txt|md)$" } |
        Select-Object -ExpandProperty FullName

    foreach ($file in ($candidateFiles | Select-Object -Unique)) {
        $text = Get-TextFileContent $file
        if ([string]::IsNullOrWhiteSpace($text)) {
            continue
        }

        foreach ($pattern in @(
            '(?im)^\s*(?:PluginVersion|ModVersion|Version)\s*[:=]\s*"?([0-9]+(?:\.[0-9]+){1,3})"?',
            '(?im)\b(?:Plugin|Mod)?\s*Version\s+([0-9]+(?:\.[0-9]+){1,3})\b'
        )) {
            $match = [regex]::Match($text, $pattern)
            if ($match.Success) {
                return $match.Groups[1].Value
            }
        }
    }

    $dlls = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.dll" -ErrorAction SilentlyContinue |
        Where-Object { (Get-RelativePathCompat -Root $Root -Path $_.FullName) -notmatch '(^|[\\/])(bin|obj)([\\/]|$)' })
    foreach ($dll in $dlls) {
        try {
            $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dll.FullName).Version
            if ($assemblyVersion -and $assemblyVersion.ToString() -notin @("0.0.0.0", "1.0.0.0")) {
                if ($assemblyVersion.Revision -ge 0) {
                    return "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build).$($assemblyVersion.Revision)"
                }

                if ($assemblyVersion.Build -ge 0) {
                    return "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build)"
                }

                return "$($assemblyVersion.Major).$($assemblyVersion.Minor)"
            }
        } catch {
            continue
        }
    }

    foreach ($leaf in @((Split-Path -Leaf $Root), (Split-Path -Leaf (Split-Path -Parent $Root)))) {
        $folderMatch = [regex]::Match($leaf, '(?<!\d)([0-9]+(?:\.[0-9]+){1,3})(?!\d)')
        if ($folderMatch.Success) {
            return $folderMatch.Groups[1].Value
        }
    }

    foreach ($file in ($candidateFiles | Select-Object -Unique)) {
        $text = Get-TextFileContent $file
        if ([string]::IsNullOrWhiteSpace($text)) {
            continue
        }

        $match = [regex]::Match($text, '(?m)\b([0-9]+(?:\.[0-9]+){2,3})\b')
        if ($match.Success) {
            return $match.Groups[1].Value
        }
    }

    throw "Could not infer the mod version. Pass -Version explicitly."
}

function Test-DirectoryContainsDll {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [bool](Get-ChildItem -LiteralPath $Path -Recurse -File -Filter "*.dll" -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Find-PackageName {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [string]$FallbackName
    )

    $directDlls = @(Get-ChildItem -LiteralPath $Root -Filter "*.dll" -File -ErrorAction SilentlyContinue)
    if ($directDlls.Count -eq 1) {
        return Convert-ToPackageFileName $directDlls[0].BaseName
    }

    $pluginRoot = Join-Path $Root "BepInEx\plugins"
    if (Test-Path -LiteralPath $pluginRoot -PathType Container) {
        $pluginDirs = @(Get-ChildItem -LiteralPath $pluginRoot -Directory -ErrorAction SilentlyContinue)
        if ($pluginDirs.Count -eq 1) {
            return Convert-ToPackageFileName $pluginDirs[0].Name
        }

        $pluginDlls = @(Get-ChildItem -LiteralPath $pluginRoot -Filter "*.dll" -File -ErrorAction SilentlyContinue)
        if ($pluginDlls.Count -eq 1) {
            return Convert-ToPackageFileName $pluginDlls[0].BaseName
        }
    }

    $reserved = @("BepInEx", "Source", "src", "docs")
    $topLevelPluginDirs = @(Get-ChildItem -LiteralPath $Root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $reserved -and (Test-DirectoryContainsDll $_.FullName) })
    if ($topLevelPluginDirs.Count -eq 1) {
        return Convert-ToPackageFileName $topLevelPluginDirs[0].Name
    }

    foreach ($readmeName in @("README.txt", "README.md")) {
        $readme = Join-Path $Root $readmeName
        $text = Get-TextFileContent $readme
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            $firstLine = ($text -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
            if ($firstLine) {
                return Convert-ToPackageFileName ($firstLine.Trim("#= "))
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($FallbackName)) {
        return Convert-ToPackageFileName $FallbackName
    }

    return Convert-ToPackageFileName (Split-Path -Leaf $Root)
}

function Test-ShouldSkipExportFile {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $relativePath = Get-RelativePathCompat -Root $Root -Path $File.FullName
    $segments = $relativePath -split '[\\/]'
    if ($segments | Where-Object { $_ -in @(".git", ".svn", ".vs", "bin", "obj", "__pycache__", "src", "Source", "tools") }) {
        return $true
    }

    if ($File.Name -in @("mod.json", "mod.schema.json", "API.txt")) {
        return $true
    }

    if ($File.Name -match '^(?i:nexus-(?:desc|page-summary|file-summary))\.(txt|md)$') {
        return $true
    }

    if ($File.Extension -in @(".zip", ".7z", ".rar", ".nupkg", ".pdb", ".tmp")) {
        return $true
    }

    return $false
}

function Copy-FileIntoScratch {
    param(
        [Parameter(Mandatory = $true)][string]$SourceFile,
        [Parameter(Mandatory = $true)][string]$DestinationFile
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationFile) | Out-Null
    Copy-Item -LiteralPath $SourceFile -Destination $DestinationFile -Force
}

function Copy-DirectoryContentsToScratch {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationDirectory
    )

    $copied = 0
    foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File -Force -ErrorAction SilentlyContinue) {
        if (Test-ShouldSkipExportFile -File $file -Root $SourceDirectory) {
            continue
        }

        $relativePath = Get-RelativePathCompat -Root $SourceDirectory -Path $file.FullName
        Copy-FileIntoScratch -SourceFile $file.FullName -DestinationFile (Join-Path $DestinationDirectory $relativePath)
        $copied++
    }

    return $copied
}

function Copy-TopLevelCompanionsToPackage {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$PackageRoot
    )

    $copied = 0
    foreach ($name in @("README.txt", "README.md", "CHANGELOG.txt", "CHANGELOG.md")) {
        $path = Join-Path $Root $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Copy-FileIntoScratch -SourceFile $path -DestinationFile (Join-Path $PackageRoot $name)
            $copied++
        }
    }

    return $copied
}

function Assert-PackageScratchLayout {
    param(
        [Parameter(Mandatory = $true)][string]$ScratchRoot,
        [Parameter(Mandatory = $true)][string]$PackageName
    )

    $topLevelItems = @(Get-ChildItem -LiteralPath $ScratchRoot -Force)
    if ($topLevelItems.Count -ne 1 -or -not $topLevelItems[0].PSIsContainer) {
        throw "Export must contain exactly one top-level mod folder named '$PackageName'."
    }

    if ($topLevelItems[0].Name -ne $PackageName) {
        throw "Export top-level folder '$($topLevelItems[0].Name)' does not match package name '$PackageName'."
    }

    $nexusDescriptions = @(Get-ChildItem -LiteralPath $topLevelItems[0].FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^(?i:nexus-desc)\.(txt|md)$' })
    if ($nexusDescriptions.Count -gt 0) {
        throw "Nexus description files are publishing source and must not be included in release zips."
    }
}

function Remove-PreviousPackageArchives {
    param(
        [Parameter(Mandatory = $true)][string]$DestinationDirectory,
        [Parameter(Mandatory = $true)][string]$PackageName,
        [Parameter(Mandatory = $true)][string]$ArchiveName,
        [Parameter(Mandatory = $true)][string]$CurrentZipPath
    )

    $currentZipFull = [System.IO.Path]::GetFullPath($CurrentZipPath)
    $patterns = @(
        ("^" + [regex]::Escape($ArchiveName) + " [0-9][0-9A-Za-z.\-]*\.zip$")
        ("^" + [regex]::Escape($PackageName) + "-[0-9][0-9A-Za-z.\-]*\.zip$")
    )
    $removed = New-Object "System.Collections.Generic.List[string]"

    foreach ($filter in @("$ArchiveName *.zip", "$PackageName-*.zip")) {
        foreach ($archive in Get-ChildItem -LiteralPath $DestinationDirectory -File -Filter $filter -ErrorAction SilentlyContinue) {
            $archiveFull = [System.IO.Path]::GetFullPath($archive.FullName)
            if ($archiveFull -ieq $currentZipFull) {
                continue
            }

            $matched = $false
            foreach ($pattern in $patterns) {
                if ($archive.Name -match $pattern) {
                    $matched = $true
                    break
                }
            }

            if (-not $matched) {
                continue
            }

            Remove-Item -LiteralPath $archive.FullName -Force
            $removed.Add($archive.FullName)
        }
    }

    return @($removed)
}

$scriptPath = Get-ScriptPath
if ([string]::IsNullOrWhiteSpace($ModRoot)) {
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw "Pass -ModRoot when the script is not run from a file."
    }

    $ModRoot = Split-Path -Parent $scriptPath
}

$ModRoot = [System.IO.Path]::GetFullPath($ModRoot)
if (-not (Test-Path -LiteralPath $ModRoot -PathType Container)) {
    throw "Mod root does not exist: $ModRoot"
}

if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = [Environment]::GetFolderPath("DesktopDirectory")
    if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
        $DestinationDirectory = Join-Path $HOME "Desktop"
    }
}

New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Find-ModVersion -Root $ModRoot
}

if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = Find-PackageName -Root $ModRoot -FallbackName (Split-Path -Leaf $ModRoot)
} else {
    $PackageName = Convert-ToPackageFileName $PackageName
}

if ([string]::IsNullOrWhiteSpace($ArchiveName)) {
    $ArchiveName = $PackageName
} else {
    $ArchiveName = Convert-ToArchiveFileNameStem $ArchiveName
}

$zipName = "$ArchiveName $Version.zip"
$zipPath = Join-Path $DestinationDirectory $zipName
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("vortex-mod-export-" + [System.Guid]::NewGuid().ToString("N"))
$tempZipPath = Join-Path $DestinationDirectory (".$ArchiveName $Version " + [System.Guid]::NewGuid().ToString("N") + ".tmp.zip")
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

try {
    $packageRoot = Join-Path $scratch $PackageName
    $copiedFiles = 0
    $legacyPluginRoot = Join-Path $ModRoot "BepInEx\plugins"

    if (Test-Path -LiteralPath $legacyPluginRoot -PathType Container) {
        $pluginDirs = @(Get-ChildItem -LiteralPath $legacyPluginRoot -Directory -ErrorAction SilentlyContinue)
        $pluginFiles = @(Get-ChildItem -LiteralPath $legacyPluginRoot -File -ErrorAction SilentlyContinue)

        if ($pluginDirs.Count -eq 1) {
            $packageRoot = Join-Path $scratch (Convert-ToPackageFileName $pluginDirs[0].Name)
            $copiedFiles += Copy-DirectoryContentsToScratch -SourceDirectory $pluginDirs[0].FullName -DestinationDirectory $packageRoot
        } elseif ($pluginDirs.Count -gt 1) {
            foreach ($pluginDir in $pluginDirs) {
                $copiedFiles += Copy-DirectoryContentsToScratch -SourceDirectory $pluginDir.FullName -DestinationDirectory (Join-Path $scratch (Convert-ToPackageFileName $pluginDir.Name))
            }
        }

        foreach ($pluginFile in $pluginFiles) {
            Copy-FileIntoScratch -SourceFile $pluginFile.FullName -DestinationFile (Join-Path $packageRoot $pluginFile.Name)
            $copiedFiles++
        }

        $copiedFiles += Copy-TopLevelCompanionsToPackage -Root $ModRoot -PackageRoot $packageRoot
    } else {
        $directDlls = @(Get-ChildItem -LiteralPath $ModRoot -Filter "*.dll" -File -ErrorAction SilentlyContinue)
        $reserved = @("BepInEx", "Source", "src", "tools", "docs")
        $topLevelPluginDirs = @(Get-ChildItem -LiteralPath $ModRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notin $reserved -and (Test-DirectoryContainsDll $_.FullName) })

        if ($directDlls.Count -gt 0) {
            $copiedFiles += Copy-DirectoryContentsToScratch -SourceDirectory $ModRoot -DestinationDirectory $packageRoot
        } elseif ($topLevelPluginDirs.Count -eq 1) {
            $packageRoot = Join-Path $scratch (Convert-ToPackageFileName $topLevelPluginDirs[0].Name)
            $copiedFiles += Copy-DirectoryContentsToScratch -SourceDirectory $topLevelPluginDirs[0].FullName -DestinationDirectory $packageRoot
            $copiedFiles += Copy-TopLevelCompanionsToPackage -Root $ModRoot -PackageRoot $packageRoot
        } else {
            throw "Could not find a plugin payload in $ModRoot."
        }
    }

    if ($copiedFiles -eq 0) {
        throw "No package files were copied from $ModRoot."
    }

    Assert-PackageScratchLayout -ScratchRoot $scratch -PackageName $PackageName

    $items = @(Get-ChildItem -LiteralPath $scratch -Force)
    Compress-Archive -LiteralPath @($items.FullName) -DestinationPath $tempZipPath -CompressionLevel Optimal
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Move-Item -LiteralPath $tempZipPath -Destination $zipPath -Force
    $removedArchives = Remove-PreviousPackageArchives -DestinationDirectory $DestinationDirectory -PackageName $PackageName -ArchiveName $ArchiveName -CurrentZipPath $zipPath

    [pscustomobject]@{
        PackageName = $PackageName
        ArchiveName = $ArchiveName
        Version = $Version
        ZipPath = $zipPath
        Files = $copiedFiles
        RemovedArchives = $removedArchives
    }
} finally {
    if (Test-Path -LiteralPath $tempZipPath -PathType Leaf) {
        Remove-Item -LiteralPath $tempZipPath -Force
    }

    if (-not $KeepScratch -and (Test-Path -LiteralPath $scratch)) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}
