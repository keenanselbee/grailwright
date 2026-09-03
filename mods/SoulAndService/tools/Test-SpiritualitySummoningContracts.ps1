$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulAndService.cs') -Raw
$summons = Get-Content -LiteralPath (Join-Path $modRoot 'src\SummonRuntime.cs') -Raw
$salvage = Get-Content -LiteralPath (Join-Path $modRoot 'src\SoulSalvageRuntime.cs') -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot 'mod.json') -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot 'README.txt') -Raw
$nexus = Get-Content -LiteralPath (Join-Path $modRoot 'nexus-full-desc.txt') -Raw
$matrix = Get-Content -LiteralPath (Join-Path $modRoot 'docs\TEST-MATRIX.md') -Raw

foreach ($required in @(
    'AssemblyInformationalVersion("3.3.0")',
    'public const string PluginVersion = "3.3.0"',
    '"version": "3.3.0"')) {
    if (!$plugin.Contains($required) -and !$manifest.Contains($required)) {
        throw "Spirituality integration release metadata is missing: $required"
    }
}

if ([regex]::Matches($salvage, 'SummonUtils\.InitializeSummon\(').Count -lt 2 -or
    $salvage -notmatch '(?s)TryRehydrateRaisedServant\(.*?SummonUtils\.InitializeSummon\(\s*raised,\s*hero,\s*null,' -or
    $salvage -notmatch '(?s)TryRaiseCorpse\(.*?SummonUtils\.InitializeSummon\(\s*raised,\s*hero,\s*sourceItem,') {
    throw 'Fresh and persistently restored servants must enter through native NpcHeroSummon initialization.'
}
if ($summons.Contains('INpcSummon.Events.SummonSpawned') -or
    $salvage.Contains('INpcSummon.Events.SummonSpawned')) {
    throw 'Soul and Service must not replay the native summon-spawned event and double-apply talent effects.'
}

if (($summons -notmatch '(?s)BeforeAddLimitedLocation\(.*?emptyCount != 0.*?oldestIndex.*?RepairInvocationScaling\(') -or
    ($summons -notmatch 'InvocationsOfMightTalentGuid\s*=\s*\r?\n?\s*"a3c1f159efbec4647ace8fafcba7da14"') -or
    ($summons -notmatch '(?s)RepairInvocationScaling\(.*?HasLearnedInvocationsOfMight\(hero\).*?1\.0f \+ spirituality \* 0\.05f') -or
    ($summons -notmatch '(?s)HasLearnedInvocationsOfMight\(Hero hero\).*?hero\.Talents\.Elements<TalentTable>\(\).*?talent\.Level > 0.*?talent\.Template\.GUID.*?InvocationsOfMightTalentGuid') -or
    ($summons -match '(?s)RepairInvocationScaling\(.*?GetReanimatedHealthMultiplier\(outgoing\).*?private static bool HasExpectedMultiplier') -or
    ($summons -notmatch '(?s)AddMissingInvocationMultiplier\(.*?HasExpectedMultiplier\(stat, invocationMultiplier\).*?HasExpectedMultiplier\(stat, composedMultiplier\).*?StatTweak\.Multi\(\s*stat,\s*invocationMultiplier') -or
    ($summons -notmatch '(?s)float healthFraction =.*?target\.Health\.Percentage.*?tweaks\.Health.*?target\.Health\.SetToFull\(\).*?1\.0f - healthFraction') -or
    ($salvage -notmatch '(?s)GetReanimatedHealthMultiplier\(.*?record\.IsMiniboss\s*\? 0\.50f\s*:\s*SoulProgressionRuntime\.GetQualityHealthMultiplier')) {
    throw 'Replacement-only Invocations of Might repair no longer verifies learned talent ownership, composes, or preserves raised-servant Health correctly.'
}

if ($summons -notmatch '(?s)AfterGetSummonLimit\(.*?__result \+= SoulProgressionRuntime\s*\.GetProgressionSummonLimitBonus\(\)\s*\+ plugin\.SummonLimitBonus\.Value') {
    throw 'Soul and Service capacity must remain additive to the native Astral Bonds summon limit.'
}
if (($summons -notmatch '(?s)UpdateServantUpkeep\(.*?npc\.HealthElement\.Kill\(\)') -or
    ($summons -notmatch '(?s)AfterRestTimeSkipped\(.*?npc\.HealthElement\.Kill\(\)') -or
    ($salvage -notmatch '(?s)CompleteLightSummonHarvest\(.*?HealthElement\.Kill\(\)') -or
    ($salvage -notmatch '(?s)RestoreSourceCorpse\(.*?PendingRaisedDiscards\.Add\(record\.RaisedLocation\)') -or
    ($salvage -notmatch '(?s)PendingRaisedDiscards\.ToArray\(\).*?location\.Discard\(\)')) {
    throw 'Real servant deaths and administrative raised-copy cleanup no longer use their distinct native Kill and Discard paths.'
}

$talents = @(
    'Astral Bonds',
    "Summoner's Battery",
    'Feeding Frenzy',
    'Hold The Line',
    'Invocations of Might',
    'Mana-Infused Blood',
    'Power Conduit',
    'Prepared Rituals',
    'Worthy Sacrifice'
)
foreach ($document in @($readme, $nexus, $matrix)) {
    $normalizedDocument = $document -replace '\s+', ' '
    foreach ($talent in $talents) {
        if (!$normalizedDocument.Contains($talent)) {
            throw "Spirituality integration documentation is missing: $talent"
        }
    }
}
foreach ($required in @(
    'ordinary summon',
    'fresh raised servant',
    'persistently restored servant',
    'replacement servant',
    'administrative cleanup',
    'SAS-SMOKE-53',
    'Version under test: 3.3.0')) {
    if (!$matrix.Contains($required)) {
        throw "Spirituality integration smoke coverage is missing: $required"
    }
}

Write-Host 'Soul and Service Spirituality Summoning integration contracts passed.'
