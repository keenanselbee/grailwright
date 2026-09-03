$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BloodMagicExpansion.cs")

foreach ($required in @(
    'typeof(ItemStats)',
    '"OnInitialize"',
    'ItemStatsInitializePatch',
    'typeof(MagicItemTemplateInfo)',
    '"get_MagicDescription"',
    'MagicDescriptionPatch')) {
    if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic tooltip hook is missing: $required"
    }
}

$trackedDescriptions = @(
    '_bloodTransfusionLightCastInfos',
    '_bloodTransfusionHeavyCastInfos',
    '_abhartachLightCastInfos',
    '_abhartachHeavyCastInfos')
foreach ($trackName in $trackedDescriptions) {
    if ($source -notmatch (
            'HashSet<MagicItemTemplateInfo>\s+' + [regex]::Escape($trackName))) {
        throw "Blood Magic tooltips must track $trackName."
    }
    if ($source -notmatch ([regex]::Escape($trackName) + '\.Add\(')) {
        throw "Blood Magic tooltip tracking never records $trackName."
    }
}

$tooltipBlock = [regex]::Match(
    $source,
    '(?s)private bool BeforeGetMagicDescription\(.+?(?=\r?\n\s*private string BuildBloodTransfusionLightDescription\()')
if (!$tooltipBlock.Success) {
    throw "Could not locate the Blood Magic description override."
}
foreach ($trackName in $trackedDescriptions) {
    if ($tooltipBlock.Value.IndexOf($trackName, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic description override does not use $trackName."
    }
}

foreach ($required in @(
    'Enemies: Fire a damaging projectile that applies Bleed.',
    'Current tap damage bonus:',
    'Living enemies: Drain Health to heal yourself; held damage grants limited XP.',
    'Corpse quality: Improves corpse healing.',
    'Corpses: Detonate a nearby corpse to damage and Bleed enemies in the area.',
    'Corpses: Drain a nearby corpse continuously to heal yourself.',
    'Blood Essence:')) {
    if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood Magic tooltip wording is missing: $required"
    }
}
foreach ($removedLegacyGrowthContract in @(
    '_bloodMagicGrowthSource',
    '_bloodSpellSpiritualityStatTerms',
    'BloodMagicGrowthSource',
    'GetSpellGrowthTooltipLabel',
    'Spirituality')) {
    if ($source.IndexOf($removedLegacyGrowthContract, [StringComparison]::Ordinal) -ge 0) {
        throw "Blood Magic tooltip source still contains removed Spirituality growth behavior: $removedLegacyGrowthContract"
    }
}

if ($tooltipBlock.Value -notmatch '(?s)return\s+true;') {
    throw "Blood Magic descriptions must preserve native text for disabled or unmatched spells."
}
if ($tooltipBlock.Value -notmatch '(?s)result\s*=.*?return\s+false;') {
    throw "Blood Magic description override does not replace matched spell text."
}

$bloodLightBlock = [regex]::Match(
    $source,
    '(?s)private string BuildBloodTransfusionLightDescription\(.+?(?=\r?\n\s*private string BuildBloodTransfusionHeavyDescription\()')
if (!$bloodLightBlock.Success -or
    $bloodLightBlock.Value.IndexOf('_bloodSpellScaleProjectileTravel', [StringComparison]::Ordinal) -lt 0 -or
    $bloodLightBlock.Value.IndexOf('GetBloodSpellTapDamageMultiplier()', [StringComparison]::Ordinal) -lt 0 -or
    $bloodLightBlock.Value.IndexOf('FormatMultiplierBonus', [StringComparison]::Ordinal) -lt 0) {
    throw "Blood/Life light tooltip does not account for projectile travel scaling."
}
if ($bloodLightBlock.Value.IndexOf('damage radius', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
    throw "Blood/Life light tooltip still promises a nonexistent damage radius."
}
if ($bloodLightBlock.Value.IndexOf('_bloodSpellScaleHomingTargetSearch', [StringComparison]::Ordinal) -ge 0) {
    throw "Blood/Life light tooltip must omit homing target-search tuning."
}

$bloodTuningBlock = [regex]::Match(
    $source,
    '(?s)private bool ShouldTuneBloodSpells\(.+?(?=\r?\n\s*private bool ShouldTuneAbhartach\()')
$abhartachTuningBlock = [regex]::Match(
    $source,
    '(?s)private bool ShouldTuneAbhartach\(.+?(?=\r?\n\s*private void OnItemStatsInitialize\()')
if (!$bloodTuningBlock.Success -or
    $bloodTuningBlock.Value.IndexOf('_enabled', [StringComparison]::Ordinal) -lt 0 -or
    $bloodTuningBlock.Value.IndexOf('_bloodSpellTuningEnabled', [StringComparison]::Ordinal) -lt 0 -or
    !$abhartachTuningBlock.Success -or
    $abhartachTuningBlock.Value.IndexOf('_enabled', [StringComparison]::Ordinal) -lt 0 -or
    $abhartachTuningBlock.Value.IndexOf('_abhartachTuningEnabled', [StringComparison]::Ordinal) -lt 0) {
    throw "Blood Magic tooltip tuning does not honor its master and per-spell settings."
}

$bloodHeavyBlock = [regex]::Match(
    $source,
    '(?s)private string BuildBloodTransfusionHeavyDescription\(.+?(?=\r?\n\s*private string BuildAbhartachLightDescription\()')
if (!$bloodHeavyBlock.Success) {
    throw "Could not locate the Blood/Life heavy tooltip builder."
}
foreach ($required in @(
    '_awardCharacterXp',
    '_rawCharacterXpPerCorpseXp',
    '_healCharacter',
    '_healMaxHealthPercentPerXpPercent',
    '_liveDrainEnabled',
    '_liveDrainAwardCharacterXp',
    '_liveDrainRawCharacterXpMultiplier',
    'GetPayoutPercentOfKillXp()',
    'GetLiveDrainXpPercentPerTick()',
    'GetLiveDrainMaximumXpPercentPerTarget()',
    'if (corpseXpEnabled || corpseHealingEnabled)',
    '_corpseQualityScaleTransfusionHealing',
    '_handRequirement',
    '_singleHandPayoutMultiplier')) {
    if ($bloodHeavyBlock.Value.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Blood/Life heavy tooltip does not account for: $required"
    }
}

$abhartachBlock = [regex]::Match(
    $source,
    '(?s)private string BuildAbhartachLightDescription\(.+?(?=\r?\n\s*private static string JoinTooltipFeatures\()')
if (!$abhartachBlock.Success -or
    $abhartachBlock.Value.IndexOf('_corpseQualityScaleAbhartachEffects', [StringComparison]::Ordinal) -lt 0) {
    throw "Abhartach tooltips do not account for corpse-quality scaling."
}

Write-Host "Blood Magic light/heavy tooltip contracts passed."
