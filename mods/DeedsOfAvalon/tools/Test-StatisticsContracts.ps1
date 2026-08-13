[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\DeedsOfAvalon.cs") -Raw
$bloodMagicSource = Get-Content -LiteralPath (
    Join-Path (Split-Path -Parent $modRoot) "BloodMagicExpansion\src\BloodMagicExpansion.cs") -Raw

$contracts = @(
    'HealthElement.Events.OnHeroSummonKill',
    'target.NpcType == NpcType.HeroSummon',
    'Increment(facts, "foes.summon")',
    'AddRow(foes, facts.Get("foes.summon", 0), "Summons", "summon", "Pink")',
    'Awaken.TG.Main.Crafting.Crafting.Events.Created, OnItemCrafted, this',
    'Math.Max(1, created.Item.Quantity)',
    'Math.Max(1, item.Quantity)',
    'ReconcileFoeTotal(facts)',
    'ReconcileCorpseDrainTotal(facts)',
    'TryGetCorpseDrainStatistics(',
    'ReconcileActiveBounty(facts)',
    'entry.Key.StartsWith("Bounty: ", StringComparison.Ordinal)',
    'SaturatingAdd(Math.Max(0, facts.Get(key, 0)), amount)',
    'ResolveSpellKey(facts, spellName)',
    '_pendingLoadedExportAt = ExportCurrentSavedStatistics("load")',
    '!entry.Key.StartsWith("bounty.faction.", StringComparison.Ordinal)',
    '[HarmonyPatch(typeof(CrimeUtils), nameof(CrimeUtils.TryCommitCrime))]',
    'typeof(CommitCrime)'
)
foreach ($contract in $contracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Deeds statistic contract: $contract"
    }
}

$fireIndex = $source.IndexOf('new Category("foes.magic.damage.fire"', [StringComparison]::Ordinal)
$coldIndex = $source.IndexOf('new Category("foes.magic.damage.cold"', [StringComparison]::Ordinal)
$wetIndex = $source.IndexOf('new Category("foes.magic.damage.wet"', [StringComparison]::Ordinal)
$electricIndex = $source.IndexOf('new Category("foes.magic.damage.electric"', [StringComparison]::Ordinal)
$poisonIndex = $source.IndexOf('new Category("foes.magic.damage.poison"', [StringComparison]::Ordinal)
$bloodIndex = $source.IndexOf('new Category("foes.magic.damage.blood_magic"', [StringComparison]::Ordinal)
$pureIndex = $source.IndexOf('new Category("foes.magic.damage.pure"', [StringComparison]::Ordinal)
$wyrdnessIndex = $source.IndexOf('new Category("foes.magic.damage.wyrdness"', [StringComparison]::Ordinal)
$otherIndex = $source.IndexOf('new Category("foes.magic.damage.other"', [StringComparison]::Ordinal)
if (-not (0 -le $fireIndex -and $fireIndex -lt $coldIndex -and $coldIndex -lt $wetIndex -and
    $wetIndex -lt $electricIndex -and $electricIndex -lt $poisonIndex -and
    $poisonIndex -lt $bloodIndex -and $bloodIndex -lt $pureIndex -and
    $pureIndex -lt $wyrdnessIndex -and $wyrdnessIndex -lt $otherIndex)) {
    throw "Deeds magic categories are not in the authored display order."
}

$magicRowsIndex = $source.IndexOf('foes.AddRange(magicRows);', [StringComparison]::Ordinal)
$summonsIndex = $source.IndexOf('AddRow(foes, facts.Get("foes.summon", 0), "Summons", "summon", "Pink");', [StringComparison]::Ordinal)
$trailingOtherIndex = $source.IndexOf('foes.Add(trailingOtherMagicRow);', [StringComparison]::Ordinal)
if (-not (0 -le $magicRowsIndex -and $magicRowsIndex -lt $summonsIndex -and $summonsIndex -lt $trailingOtherIndex)) {
    throw "Summons must appear after the ordered magic rows and before their trailing Other row."
}

$bloodMagicContracts = @(
    'TrySetCorpseDrainStatistics',
    'TryGetCorpseDrainStatistics',
    'BloodProgressionCorpseStatisticsInitializedKey',
    'ReportCorpseStatisticsToDeeds()',
    'BeforeTierCount',
    'BeforeQualitySum'
)
foreach ($contract in $bloodMagicContracts) {
    if ($bloodMagicSource.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Blood Magic statistic synchronization contract: $contract"
    }
}

Write-Output "Deeds statistic hardening contracts passed."
