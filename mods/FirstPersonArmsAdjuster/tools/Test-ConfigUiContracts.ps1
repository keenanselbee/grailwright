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
    "Advanced - Retraction Profile",
    "Head Bob",
    "Advanced - Animation Guards",
    "Advanced - Effects",
    "Diagnostics"
)
$expectedLabels = @(
    "Enabled",
    "General / Unarmed Depth Offset (m)",
    "Horizontal Offset (m)",
    "Vertical Offset (m)",
    "Shoulder Retraction (m)",
    "Spine Retraction (%)",
    "Spine1 Retraction (%)",
    "Spine2 Retraction (%)",
    "Left Shoulder Retraction (%)",
    "Right Shoulder Retraction (%)",
    "Upper-Arm Retraction (%)",
    "Forearm Retraction (%)",
    "Lower Torso Retraction (%)",
    "Chest Helper Retraction (%)",
    "Shoulder-Fix Retraction (%)",
    "Native Cloth Retraction (%)",
    "Torso Renderer Retraction (%)",
    "Test Retraction Bone Name",
    "Test Bone Retraction (%)",
    "Use Separate Equipment Depths",
    "Melee Depth Offset (m)",
    "Bow Depth Offset (m)",
    "Magic Depth Offset (m)",
    "Enable Head Bob",
    "Head Bob Strength",
    "Head Bob Smoothness",
    "Sprint Emphasis",
    "Head Bob Speed (%)",
    "Stabilize Viewmodel During Head Bob",
    "Viewmodel Head-Bob Follow (%)",
    "Suppress Motion Blur During Head Bob",
    "Temporal-Safe Head Bob Timing (Test)",
    "Enable Animation Guards",
    "Enable Attack Guards",
    "Enable Dodge Guard",
    "Enable Sheathing Guard",
    "Enable Bow Draw Guard",
    "Bow Draw Maximum Offset (%)",
    "Use Shared Guard Target",
    "Shared Move Toward Vanilla (%)",
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
    $source -notmatch 'private const int ConfigSchemaVersion = 20;') {
    throw "The schema marker must remain hidden and use schema 19 after consolidating animation guards and adopting the new defaults."
}

Write-Host "First Person Arms Adjuster config UI contracts passed."
