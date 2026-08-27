[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sharedPath = Join-Path $PSScriptRoot "shared\CorpseQualityBuckets.cs"
$steelPath = Join-Path $repoRoot "mods\SteelAndBone\src\SteelAndBone.cs"
$bloodPath = Join-Path $repoRoot "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs"

function Assert-Contract {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw "Corpse-quality contract failed: $Message"
    }
}

function Get-QualityTier {
    param([double]$Quality)

    if ($Quality -le 0.25) { return "Meager" }
    if ($Quality -le 0.50) { return "Worthy" }
    if ($Quality -le 0.75) { return "Potent" }
    return "Prime"
}

function Apply-LevelAdjustment {
    param([double]$Quality, [double]$EnemyLevel, [double]$HeroLevel)

    $adjustment = [Math]::Max(-0.075, [Math]::Min(0.075, ($EnemyLevel - $HeroLevel) * 0.025))
    return [Math]::Max(0.0, [Math]::Min(1.0, $Quality + $adjustment))
}

$shared = Get-Content -LiteralPath $sharedPath -Raw
$steel = Get-Content -LiteralPath $steelPath -Raw
$blood = Get-Content -LiteralPath $bloodPath -Raw

Assert-Contract ($shared.Contains('DefaultReferenceKillXp = 700.0f')) "untagged XP reference is not 700."
Assert-Contract ($shared.Contains('DefaultReferenceMaxHealth = 3400.0f')) "untagged health reference is not 3400."
Assert-Contract ($shared.Contains('DefaultLevelQualityPerLevel = 0.025f')) "level adjustment is not 2.5% per level."
Assert-Contract ($shared.Contains('DefaultMaximumLevelQualityAdjustment = 0.075f')) "level adjustment cap is not 7.5%."
Assert-Contract ($shared.Contains('EliteQualityBonus = 0.10f')) "Elite bonus is not 10%."
Assert-Contract ($shared.Contains('MiniBossQualityBonus = 0.175f')) "MiniBoss bonus is not 17.5%."
Assert-Contract ($shared.Contains('BossMinimumQuality = 0.875f')) "Boss minimum is not Prime."
Assert-Contract ($shared.Contains('case 0:') -and $shared.Contains('quality01 = 0.05f;')) "Tier 0 anchor is not 0.05."
Assert-Contract ($shared.Contains('case 1:') -and $shared.Contains('quality01 = 0.125f;')) "Tier 1 anchor is not 0.125."
Assert-Contract ($shared.Contains('case 2:') -and $shared.Contains('quality01 = 0.23f;')) "Tier 2 anchor is not 0.23."
Assert-Contract ($shared.Contains('case 3:') -and $shared.Contains('quality01 = 0.425f;')) "Tier 3 anchor is not 0.425."
Assert-Contract ($shared.Contains('case 4:') -and $shared.Contains('quality01 = 0.625f;')) "Tier 4 anchor is not 0.625."
Assert-Contract ($shared.Contains('case 5:') -and $shared.Contains('quality01 = 0.80f;')) "Tier 5 anchor is not 0.80."
Assert-Contract ($shared.Contains('case 6:') -and $shared.Contains('quality01 = 0.90f;')) "Tier 6 anchor is not 0.90."
Assert-Contract ($shared.Contains('case 7:') -and $shared.Contains('quality01 = 1.0f;')) "Tier 7 anchor is not 1.0."

Assert-Contract ($steel.Contains('string.Equals(tag, "Tier:" + tier, StringComparison.Ordinal)')) "Steel and Bone does not use exact Tier tags."
Assert-Contract ($steel.Contains('CorpseQualityBuckets.CalculateIntrinsicQuality01(')) "Steel and Bone does not use shared intrinsic quality."
Assert-Contract ($steel.Contains('CorpseQualityBuckets.ApplyThreatClassAdjustment(')) "Steel and Bone does not use shared threat-class weighting."
Assert-Contract ($steel.Contains('CorpseQualityBuckets.ApplyBoundedRelativeLevelAdjustment(')) "Steel and Bone does not use shared bounded level weighting."
Assert-Contract ($steel.Contains('public const int ApiVersion = 7;')) "Steel and Bone hit-feedback API is not v7."
Assert-Contract ($steel.Contains('public static bool TryGetKillingBlowQuality(')) "Steel and Bone does not expose pre-death killing-blow quality."
Assert-Contract ($steel.Contains('TryGetKillingBlowQualityForInterop(')) "Steel and Bone does not share one killing-blow quality calculation between preview and death feedback."
Assert-Contract ($steel.Contains('TargetedKillingBlowResolved')) "Steel and Bone does not expose target-aware killing-blow feedback."

Assert-Contract ($blood.Contains('ConfigSchemaVersion = 25')) "Blood Magic Expansion schema is not 25."
Assert-Contract (-not $blood.Contains('"ReferenceKillXP"')) "Blood Magic Expansion still binds ReferenceKillXP."
Assert-Contract (-not $blood.Contains('"ReferenceMaxHealth"')) "Blood Magic Expansion still binds ReferenceMaxHealth."
Assert-Contract ($blood.Contains('TryResolveCorpseNativeTier(')) "Blood Magic Expansion does not resolve native tiers."
Assert-Contract ($blood.Contains('ResolveCorpseExpLevel(')) "Blood Magic Expansion does not resolve enemy XP level."
Assert-Contract ($blood.Contains('ResolveCorpseEffectiveKillXp(')) "Blood Magic Expansion lost its separate actual-XP path."
Assert-Contract ($blood.Contains('public const int ApiVersion = 10;')) "Blood Magic API changed unexpectedly."

Assert-Contract ((Get-QualityTier (Apply-LevelAdjustment 0.125 3 8)) -eq "Meager") "a level-8 hero's early wolf is not Meager."
Assert-Contract ((Get-QualityTier (Apply-LevelAdjustment 0.23 10 8)) -eq "Worthy") "a level-8 hero's early Lost Knight is not Worthy."
Assert-Contract ((Get-QualityTier (Apply-LevelAdjustment 0.23 5 8)) -eq "Meager") "a level-8 hero's early skeleton is not Meager."
Assert-Contract ([Math]::Abs((Apply-LevelAdjustment 0.90 1 100) - 0.825) -lt 0.000001) "downward level adjustment exceeds its 7.5% cap."
Assert-Contract ([Math]::Abs((Apply-LevelAdjustment 0.23 100 1) - 0.305) -lt 0.000001) "upward level adjustment exceeds its 7.5% cap."

Write-Host "Corpse-quality contracts passed."
