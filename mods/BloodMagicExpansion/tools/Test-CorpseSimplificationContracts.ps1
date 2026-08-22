$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BloodMagicExpansion.cs")
$readme = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "README.txt")
$nexusDescription = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt")

$requiredSourceContracts = @(
    'private ConfigEntry<bool> _simplifyDrainedCorpses;',
    'BindOrdered("Main Loop", "SimplifyDrainedCorpses", true',
    'Corpse corpse = state.Corpse as Corpse;',
    'NpcDummy dummy = corpse.ParentModel.TryGetElement<NpcDummy>();',
    'dummy == null || dummy.HasBeenDiscarded || !dummy.HasDied',
    'bool simplified = dummy.TryReplaceWithSimplifiedLocation();',
    'CorpseLeechSoundRangeVolume", 1.0f',
    'CorpseLeechMaximumRangeDistance = 30.0f',
    'CorpseLeechMinimumRangeVolume = 0.10f',
    'state.HasPosition,',
    'state.LastKnownPosition);',
    'GetCorpseLeechRangeVolumeMultiplier(',
    'Vector3.Distance(heroPosition, corpsePosition)'
)

foreach ($contract in $requiredSourceContracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing corpse simplification source contract: $contract"
    }
}

$exhaustedIndex = $source.IndexOf(
    'state.Exhausted = true;',
    [StringComparison]::Ordinal)
$reportIndex = $source.IndexOf(
    'ReportCorpseDrained(corpseQuality);',
    $exhaustedIndex,
    [StringComparison]::Ordinal)
$simplifyIndex = $source.IndexOf(
    'TrySimplifyDrainedCorpse(state);',
    $reportIndex,
    [StringComparison]::Ordinal)

if ($exhaustedIndex -lt 0 -or
    $reportIndex -le $exhaustedIndex -or
    $simplifyIndex -le $reportIndex) {
    throw (
        "Corpse simplification must run only after the ritual is exhausted " +
        "and its completed-drain integrations have been reported.")
}

foreach ($document in @($readme, $nexusDescription)) {
    if ($document.IndexOf(
            'SimplifyDrainedCorpses',
            [StringComparison]::Ordinal) -lt 0) {
        throw "Player-facing documentation is missing SimplifyDrainedCorpses."
    }
    if ($document.IndexOf(
            'CorpseLeechSoundRangeVolume',
            [StringComparison]::Ordinal) -lt 0) {
        throw "Player-facing documentation is missing corpse-audio distance fading."
    }
}

Write-Host "Blood Magic Expansion corpse simplification contracts passed."
