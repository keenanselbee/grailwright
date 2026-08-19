$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$api = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\API.md") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw

function Get-RequiredMatchText {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Description
    )

    $match = [regex]::Match($Text, $Pattern)
    if (!$match.Success) {
        throw "$Description was not found."
    }

    return $match.Value
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Required,
        [string]$Description
    )

    if (!$Text.Contains($Required)) {
        throw "$Description is missing: $Required"
    }
}

$notificationApiMethod = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)public static bool TryShowCompatibilityNotice\(\s*string sourceId,\s*string conflictId,\s*string text,\s*string diagnosticDetails\s*\)\s*\{.+?(?=\r?\n\s*public static bool TryClaimXpGain\()' `
    -Description "NotificationApi four-string compatibility method"
Assert-Contains $notificationApiMethod "plugin.TryShowCompatibilityNotice(" "NotificationApi compatibility method"

$featureProbe = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)internal static bool SupportsFeature\(string feature\).+?(?=\r?\n\s*internal static string\[\] GetBuiltInIconIds\()' `
    -Description "GFT feature-probe method"
foreach ($required in @('"ApiVersion13"', '"CompatibilityNotices"')) {
    Assert-Contains $featureProbe $required "GFT feature-probe method"
}

$instanceCompatibilityMethod = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)internal bool TryShowCompatibilityNotice\(.+?(?=\r?\n\s*internal bool TryClaimXpGain\()' `
    -Description "GFT instance compatibility method"
foreach ($required in @('_notifyModCompatibility == null', '!_notifyModCompatibility.Value')) {
    Assert-Contains $instanceCompatibilityMethod $required "GFT instance compatibility method"
}
if ($instanceCompatibilityMethod -notmatch '(?s)TryShowEvent\(\s*sourceId,\s*eventId,\s*text,\s*"Warning",\s*"System",\s*"High",\s*eventId,\s*"warning",\s*"System",\s*"OnMainMenu",\s*-1\.0f,\s*1\.0f\s*\)') {
    throw "GFT compatibility notices no longer fix the shared Warning/System/High/warning/System/OnMainMenu/-1.0f/1.0f presentation."
}

$compatibilityScan = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)private void ScanLoadedModCompatibility\(\).+?(?=\r?\n\s*private void ScanBepInExIncompatibilityErrors\()' `
    -Description "GFT loaded-mod compatibility scan"
Assert-Contains $compatibilityScan "ScanBepInExIncompatibilityErrors();" "GFT loaded-mod compatibility scan"

$bepInExScan = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)private void ScanBepInExIncompatibilityErrors\(\).+?(?=\r?\n\s*private void ScanEyesInTheDarkCompatibility\()' `
    -Description "generic BepInEx incompatibility scan"
foreach ($required in @(
    'Chainloader.DependencyErrors',
    'TryParseBepInExIncompatibilityError(',
    'Chainloader.PluginInfos.TryGetValue(',
    'pluginInfo.Metadata.Name',
    'Remove or disable one, then restart the game.')) {
    Assert-Contains $bepInExScan $required "generic BepInEx incompatibility scan"
}
foreach ($forbidden in @('SoulAndService', 'Avalon', 'BetterSummon')) {
    if ($bepInExScan.Contains($forbidden)) {
        throw "Generic BepInEx incompatibility scan hard-codes a mod identity: $forbidden"
    }
}

$incompatibilityParser = Get-RequiredMatchText `
    -Text $source `
    -Pattern '(?s)private static bool TryParseBepInExIncompatibilityError\(.+?(?=\r?\n\s*private static string RemoveTrailingPluginVersion\()' `
    -Description "BepInEx incompatibility-error parser"
foreach ($required in @(
    'const string rejectedPluginPrefix = "Could not load [";',
    'const string incompatibilityMarker =',
    '"] because it is incompatible with: ";',
    'string[] candidates = incompatibleList.Split(',
    'RemoveTrailingPluginVersion(')) {
    Assert-Contains $incompatibilityParser $required "BepInEx incompatibility-error parser"
}

foreach ($required in @(
    'NotificationApi v13 compatibility notice:',
    'TryShowCompatibilityNotice(sourceId, conflictId, text, diagnosticDetails)',
    '`ApiVersion13` and `CompatibilityNotices`',
    'GFT separately translates BepInEx''s verified incompatibility dependency errors',
    'remove or disable one mod before restarting')) {
    Assert-Contains $api $required "Packaged API guide"
}

foreach ($required in @(
    'NotificationApi is currently v13.',
    'TryShowCompatibilityNotice(PluginGuid, "incompatible-other-mod", conciseText, diagnosticDetails)',
    'Probe [b]CompatibilityNotices[/b]',
    'A plugin rejected by BepInEx cannot call any runtime API.',
    'without hard-coding those pairs into GFT')) {
    Assert-Contains $nexus $required "Nexus description"
}

Write-Host "GFT BepInEx compatibility bridge contracts passed."
