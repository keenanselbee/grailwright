[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Config ordering contract failed: $Message"
    }
}

function Get-BalancedCall {
    param(
        [string]$Source,
        [int]$OpenParenthesis
    )

    $depth = 0
    $inString = $false
    $inCharacter = $false
    $escaped = $false
    for ($index = $OpenParenthesis; $index -lt $Source.Length; $index++) {
        $character = $Source[$index]
        if ($escaped) {
            $escaped = $false
            continue
        }

        if (($inString -or $inCharacter) -and $character -eq '\') {
            $escaped = $true
            continue
        }

        if (-not $inCharacter -and $character -eq '"') {
            $inString = -not $inString
            continue
        }

        if (-not $inString -and $character -eq "'") {
            $inCharacter = -not $inCharacter
            continue
        }

        if ($inString -or $inCharacter) {
            continue
        }

        if ($character -eq '(') {
            $depth++
        } elseif ($character -eq ')') {
            $depth--
            if ($depth -eq 0) {
                return $Source.Substring(
                    $OpenParenthesis,
                    $index + 1 - $OpenParenthesis)
            }
        }
    }

    return $null
}

function Split-TopLevelArguments {
    param([string]$Call)

    $arguments = [Collections.Generic.List[string]]::new()
    $start = 1
    $depth = 0
    $inString = $false
    $inCharacter = $false
    $escaped = $false
    for ($index = 1; $index -lt $Call.Length - 1; $index++) {
        $character = $Call[$index]
        if ($escaped) {
            $escaped = $false
            continue
        }

        if (($inString -or $inCharacter) -and $character -eq '\') {
            $escaped = $true
            continue
        }

        if (-not $inCharacter -and $character -eq '"') {
            $inString = -not $inString
            continue
        }

        if (-not $inString -and $character -eq "'") {
            $inCharacter = -not $inCharacter
            continue
        }

        if ($inString -or $inCharacter) {
            continue
        }

        if ($character -in @('(', '[', '{')) {
            $depth++
        } elseif ($character -in @(')', ']', '}')) {
            $depth--
        } elseif ($character -eq ',' -and $depth -eq 0) {
            $arguments.Add($Call.Substring($start, $index - $start).Trim())
            $start = $index + 1
        }
    }

    $arguments.Add(
        $Call.Substring($start, $Call.Length - 1 - $start).Trim())
    return $arguments.ToArray()
}

function Resolve-SectionLiteral {
    param(
        [string]$Source,
        [string]$Expression
    )

    if ($Expression -match '^"([^"]+)"$') {
        return $Matches[1]
    }

    if ($Expression -match '^[A-Za-z_][A-Za-z0-9_]*$') {
        $name = [regex]::Escape($Expression)
        $match = [regex]::Match(
            $Source,
            '(?m)(?:const\s+string|string)\s+' + $name + '\s*=\s*"([^"]+)"')
        if ($match.Success) {
            return $match.Groups[1].Value
        }
    }

    return $null
}

$modsRoot = Join-Path $RepositoryRoot 'mods'
$manifests = Get-ChildItem -LiteralPath $modsRoot -Filter 'mod.json' -File -Recurse
$configOwners = 0
$visibleBindings = 0

foreach ($manifestFile in $manifests) {
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw |
        ConvertFrom-Json
    $modRoot = $manifestFile.Directory.FullName
    $sourcePaths = @(
        foreach ($relativePath in @($manifest.sourceFiles)) {
            if ([IO.Path]::GetExtension($relativePath) -eq '.cs') {
                Join-Path $modRoot $relativePath
            }
        }
    )
    $sources = @(
        foreach ($sourcePath in $sourcePaths) {
            if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
                [pscustomobject]@{
                    Path = $sourcePath
                    Text = Get-Content -LiteralPath $sourcePath -Raw
                }
            }
        }
    )
    $combinedSource = ($sources.Text -join [Environment]::NewLine)
    $modHasConfig = $false

    foreach ($sourceFile in $sources) {
        $matches = [regex]::Matches(
            $sourceFile.Text,
            '(?<![A-Za-z0-9_])(?:(?:Config|config|_config)\.Bind|BindOrdered)\s*\(')
        foreach ($match in $matches) {
            $openParenthesis = $sourceFile.Text.IndexOf('(', $match.Index)
            $call = Get-BalancedCall $sourceFile.Text $openParenthesis
            $message = "$($manifest.packageName) has an unparseable Config.Bind call in " + $sourceFile.Path
            Assert-Contract ($null -ne $call) $message
            $arguments = Split-TopLevelArguments $call
            $message = "$($manifest.packageName) has a Config.Bind call with fewer than four arguments in " + $sourceFile.Path
            Assert-Contract ($arguments.Count -ge 4) $message

            $isOrderedWrapperImplementation =
                $match.Value -notmatch 'BindOrdered' -and
                $arguments[0].Trim() -eq 'section' -and
                $arguments[1].Trim() -eq 'key' -and
                $sourceFile.Text.Contains('ConfigEntry<T> BindOrdered<T>(')
            if ($isOrderedWrapperImplementation) {
                continue
            }

            $modHasConfig = $true
            $section = Resolve-SectionLiteral $sourceFile.Text $arguments[0]
            if ($null -ne $section) {
                $message = "$($manifest.packageName) still binds numbered raw section '$section'."
                Assert-Contract ($section -notmatch '^\d+\.\s') $message
            }

            $key = $arguments[1].Trim('"')
            if ($key -eq 'ConfigSchemaVersion') {
                $message = "$($manifest.packageName) exposes ConfigSchemaVersion."
                Assert-Contract ($call.Contains('BrowsableAttribute(false)')) $message
                continue
            }

            $visibleBindings++
            $description = $arguments[3]
            $usesOrderedWrapper =
                $match.Value -match 'BindOrdered' -and
                $combinedSource.Contains('ConfigUiDescription.Create(')
            $usesSharedMetadata =
                $description.Contains('ConfigUiDescription.Create(') -or
                $usesOrderedWrapper
            $usesInlineMetadata =
                $description.Contains('ConfigRecoveryUiMetadata')
            $usesLocalMetadataHelper =
                $description -match '(?:ConfigUi|UiDescription|OpacityDescription)\s*\('
            if ($usesLocalMetadataHelper) {
                $usesLocalMetadataHelper =
                    $combinedSource.Contains('ConfigRecoveryUiMetadata')
            }

            $hasMetadata = (
                $usesSharedMetadata -or
                $usesInlineMetadata -or
                $usesLocalMetadataHelper)
            $message = "$($manifest.packageName) setting '$key' lacks explicit ordering metadata in " + $sourceFile.Path
            Assert-Contract $hasMetadata $message
        }
    }

    if ($modHasConfig) {
        $configOwners++
    }
}

$helperPath = Join-Path $RepositoryRoot 'tools\shared\ConfigPreviousSettingsRecovery.cs'
$helperSource = Get-Content -LiteralPath $helperPath -Raw
Assert-Contract `
    ($helperSource.Contains('internal const string RecoverySection = "Import Previous Settings";')) `
    'The shared recovery section is not clean and unnumbered.'
Assert-Contract `
    ($helperSource.Contains('ImportSectionOrder = Int32.MaxValue')) `
    'Import Previous Settings is not reserved as the final section.'
$message = "Expected 26 config-owning mods, found $configOwners."
Assert-Contract ($configOwners -eq 26) $message

$message = "Config ordering contracts passed: $configOwners config-owning mods and " + "$visibleBindings visible bindings."
Write-Output $message
