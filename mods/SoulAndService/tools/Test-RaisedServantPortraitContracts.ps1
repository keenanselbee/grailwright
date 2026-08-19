$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw

$fallbackKey = "759a3e6e96ddae742ab8cde19fae42f0"
foreach ($required in @(
    "using Awaken.TG.Assets;",
    "private const string GenericRaisedServantPortraitKey =",
    "`"$fallbackKey`";")) {
    if (!$source.Contains($required)) {
        throw "Raised-servant portrait source is missing: $required"
    }
}

$helperMatch = [regex]::Match(
    $source,
    '(?s)private static bool EnsureRaisedServantPortrait\(.+?(?=\r?\n\s*private static bool TryFindEligibleCorpse\()')
if (!$helperMatch.Success) {
    throw "EnsureRaisedServantPortrait was not found."
}

$helper = $helperMatch.Value
foreach ($required in @(
    "npc.NpcIcon",
    "portrait.IsSet",
    "portrait.arSpriteReference =",
    "new ARAssetReference(GenericRaisedServantPortraitKey)")) {
    if (!$helper.Contains($required)) {
        throw "Raised-servant portrait helper is missing: $required"
    }
}

$spawnIndex = $source.IndexOf(
    "raised = source.Template.SpawnLocation",
    [StringComparison]::Ordinal)
$portraitIndex = $source.IndexOf(
    "bool usedFallbackPortrait = EnsureRaisedServantPortrait(raisedNpc);",
    [StringComparison]::Ordinal)
$summonIndex = $source.IndexOf(
    "NpcElement npc = SummonUtils.InitializeSummon(",
    [StringComparison]::Ordinal)
if ($spawnIndex -lt 0 -or $portraitIndex -le $spawnIndex -or
    $summonIndex -le $portraitIndex) {
    throw "Raised-servant portrait fallback must run after the instance spawns and before hero-summon initialization."
}

if ($source.Contains("NpcAttachment.NpcIcon") -or
    $source.Contains("source.Template.NpcIcon")) {
    throw "Raised-servant portrait fallback must not mutate shared NPC attachments or templates."
}

foreach ($required in @(
    "native NPC portrait",
    "vanilla skeleton-summon portrait")) {
    if (!$readme.Contains($required)) {
        throw "Installed README is missing portrait behavior: $required"
    }
}

foreach ($required in @(
    "native NPC portrait",
    "vanilla skeleton-summon portrait",
    "summon HUD always has a valid image")) {
    if (!$nexus.Contains($required)) {
        throw "Nexus description is missing portrait behavior: $required"
    }
}

Write-Host "Soul and Service raised-servant portrait contracts passed."
