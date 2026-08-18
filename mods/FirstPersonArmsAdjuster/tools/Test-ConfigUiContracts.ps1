[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\FirstPersonArmsAdjuster.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$expectedSections = @(
    "General",
    "Position",
    "Equipment Depth",
    "Head Bob",
    "Advanced - Melee Guards",
    "Advanced - Effects",
    "Diagnostics"
)
$expectedLabels = @(
    "Enabled",
    "General / Unarmed Depth Offset (m)",
    "Horizontal Offset (m)",
    "Vertical Offset (m)",
    "Use Separate Equipment Depths",
    "Melee Depth Offset (m)",
    "Bow Depth Offset (m)",
    "Magic Depth Offset (m)",
    "Enable Head Bob",
    "Head Bob Strength",
    "Head Bob Smoothness",
    "Sprint Emphasis",
    "Prevent Body Intrusion",
    "Normal Offset Retained (0-1)",
    "Extra Depth Correction (m)",
    "Extra Vertical Correction (m)",
    "Keep Attached Effects Aligned",
    "Diagnostics"
)

$metadataCount = ([regex]::Matches(
    $source,
    "new Grailwright\.Shared\.ConfigRecoveryUiMetadata")).Count
if ($metadataCount -ne $expectedLabels.Count) {
    throw "Expected $($expectedLabels.Count) user-facing UI metadata entries; found $metadataCount."
}

foreach ($section in $expectedSections) {
    if (-not $source.Contains('DisplaySection = "' + $section + '"')) {
        throw "Missing FoA Mod Manager display section: $section"
    }
}

foreach ($label in $expectedLabels) {
    if (-not $source.Contains('DisplayName = "' + $label + '"')) {
        throw "Missing FoA Mod Manager display label: $label"
    }
}

if ($source -notmatch '(?s)"ConfigSchemaVersion",\s*ConfigSchemaVersion,.+BrowsableAttribute\(false\)' -or
    $source -notmatch 'private const int ConfigSchemaVersion = 14;') {
    throw "The schema marker must remain hidden and use schema 13 after removing and renaming the old camera-motion settings."
}

Write-Host "First Person Arms Adjuster config UI contracts passed."
