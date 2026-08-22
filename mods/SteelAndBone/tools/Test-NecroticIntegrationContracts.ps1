$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\SteelAndBone.cs") -Raw

function Assert-Contract([bool]$condition, [string]$message)
{
    if (!$condition) {
        throw $message
    }
}

function Get-MethodBlock([string]$name)
{
    $pattern = '(?s)private (?:static )?(?:bool|void|DamageClassification) ' + [regex]::Escape($name) + '\(.+?(?=\r?\n\s*private (?:static )?(?:bool|void|DamageClassification) |\r?\n\s*internal |\r?\n\s*public |\z)'
    return [regex]::Match(
        $source,
        $pattern)
}

foreach ($required in @(
    'private const string SoulAndServicePluginGuid',
    'private const string SoulAndServiceApiTypeName',
    '_soulAndServiceIsNecroticDamageMethod',
    'IsNecroticDamage(object damage)',
    'SoulAndServiceApi',
    'ApiVersion',
    'apiVersion < 5')) {
    Assert-Contract ($source.Contains($required)) "Missing Soul and Service Necrotic reflection contract: $required"
}
Assert-Contract (!$source.Contains('[BepInDependency(SoulAndServicePluginGuid')) "Steel and Bone must not declare Soul and Service as a BepIn dependency."
Assert-Contract (!$source.Contains('BepInDependency("ks.tgfoa.soul-and-service"')) "Steel and Bone must not create a Soul and Service load-order cycle."

foreach ($required in @(
    'Necrotic',
    'public bool IsNecrotic;',
    'IsNecroticDamage(damage)',
    'classification.Tags |= DamageTag.Necrotic;',
    'if (part.IsNecrotic) part.Tags |= DamageTag.Necrotic;')) {
    Assert-Contract ($source.Contains($required)) "Missing Necrotic damage classification contract: $required"
}

$partBlock = Get-MethodBlock 'PopulatePartDamageClassification'
Assert-Contract $partBlock.Success "Could not locate weighted part classification."
Assert-Contract ($partBlock.Value -match '(?s)part\.IsNecrotic\s*=\s*overall != null\s*&& overall\.IsNecrotic\s*&& part\.IsGenericMagical;') "Necrotic provenance must propagate only to GenericMagical weighted parts."

$necroticResolver = Get-MethodBlock 'TryResolveNecroticRule'
Assert-Contract $necroticResolver.Success "Dedicated Necrotic resolver is missing."
Assert-Contract ($necroticResolver.Value.Contains('damageClass.IsNecrotic')) "Necrotic resolver does not require the semantic Necrotic tag."
foreach ($rule in @(
    '(?s)targetClass.IsConstruct.+?baseMultiplier = 0.25f',
    '(?s)targetClass.IsConfirmedSkeleton.+?baseMultiplier = 0.40f',
    '(?s)targetClass.IsDrownedZombie.+?baseMultiplier = 0.60f',
    '(?s)targetClass.IsWyrd.+?baseMultiplier = 0.90f',
    '(?s)targetClass.IsInfectedFlesh.+?baseMultiplier = 0.85f',
    '(?s)targetClass.IsSpirit.+?baseMultiplier = 1.15f',
    '(?s)targetClass.IsFleshUndead.+?baseMultiplier = 0.60f',
    '(?s)targetClass.IsFlora\s*\|\|\s*targetClass.IsFungalBody.+?baseMultiplier = 1.15f',
    '(?s)targetClass.IsSeaFlesh.+?baseMultiplier = 1.10f',
    '(?s)targetClass.IsFlesh.+?baseMultiplier = 1.10f')) {
    Assert-Contract ($necroticResolver.Value -match $rule) "Missing Hardened Necrotic matchup: $rule"
}

$armoredSpellResolver = Get-MethodBlock 'TryResolveArmoredSpellRule'
Assert-Contract $armoredSpellResolver.Success "Could not locate armored-spell resolver."
Assert-Contract ($armoredSpellResolver.Value -match 'damageClass\.IsNecrotic') "Necrotic damage must be excluded from armored-spell bonuses."

$partResolver = Get-MethodBlock 'TryResolveDamagePartRule'
Assert-Contract $partResolver.Success "Could not locate weighted damage-part resolver."
Assert-Contract ($partResolver.Value -match '(?s)TryResolveNecroticRule\(.+?TryResolveArmoredSpellRule') "Weighted damage parts must resolve Necrotic before armored-spell bonuses."

$mainPipeline = [regex]::Match($source, '(?s)TargetClassification targetClass = GetTargetClassification\(.+?if \(matchedRule\)')
Assert-Contract $mainPipeline.Success "Could not locate direct damage rule pipeline."
Assert-Contract ($mainPipeline.Value -match '(?s)TryResolveNecroticRule\(.+?TryResolveArmoredSpellRule') "Direct damage must resolve Necrotic before armored-spell bonuses."

$exactClassification = Get-MethodBlock 'ApplyExactTargetClassification'
Assert-Contract $exactClassification.Success "Could not locate exact target-family classification."
foreach ($correction in @(
    'exact:RootambusherFlora',
    'exact:WightFlora',
    'exact:CorpseEaterFlesh',
    'exact:MistbearerSpirit',
    'exact:WyrdheirWyrd',
    'exact:StrawParentSpiritBoneBody',
    'exact:StagfatherSpiritBoneBody',
    'exact:GhostOfBrocMealaSpirit',
    'exact:SleepwalkerWyrdStoneBody',
    'exact:ElementalStagfatherGolemConstruct',
    'exact:DrownedSkeletonSailorBone',
    'exact:IceTrialWyrd',
    'exact:IceStatueConstruct',
    'exact:AncientBeholderFlesh',
    'exact:SingwormFlesh',
    'exact:LirTentacleFlesh',
    'exact:BloodAbominationSoftBody',
    'exact:WyrdSlimeSoftBody',
    'exact:TidewraithSeaFlesh',
    'exact:WailcapSeaCreature')) {
    Assert-Contract ($exactClassification.Value.Contains($correction)) "Missing exact Necrotic-family correction: $correction"
}
foreach ($required in @(
    'SetExclusiveTargetFamily(classification, TargetFamily.Spirit);',
    'SetExclusiveTargetFamily(classification, TargetFamily.Wyrd);',
    'SetExclusiveTargetFamily(classification, TargetFamily.Construct);',
    'SetExclusiveTargetFamily(classification, TargetFamily.BoneUndead);',
    'SetExclusiveTargetFamily(classification, TargetFamily.SeaFlesh);',
    'ClearTargetFamilies(classification);')) {
    Assert-Contract ($exactClassification.Value.Contains($required)) "Exact Necrotic family precedence is incomplete: $required"
}
Assert-Contract ($source.Contains('public bool IsFungalBody;')) "Target classification is missing the orthogonal fungal-body flag."
Assert-Contract ($exactClassification.Value -match '(?s)ContainsAnyTerm\(text, WailcapTerms\).+?SetExclusiveTargetFamily\(classification, TargetFamily\.SeaFlesh\);.+?classification\.IsFungalBody = true;') "Wailcaps must retain SeaFlesh while gaining exact fungal-body evidence."

Assert-Contract ($necroticResolver.Value -match '(?s)targetClass\.IsConfirmedSkeleton\s*\|\|\s*\(targetClass\.ExactTargets & ExactTarget\.DrownedSkeletonSailor\)') "Necrotic skeleton resistance must require confirmed skeleton evidence or an exact drowned-skeleton correction."
Assert-Contract ($necroticResolver.Value -notmatch 'targetClass\.IsBoneUndead') "Broad BoneUndead metadata must not grant the confirmed-skeleton Necrotic resistance."

Write-Output "Steel and Bone Necrotic integration contracts passed."
