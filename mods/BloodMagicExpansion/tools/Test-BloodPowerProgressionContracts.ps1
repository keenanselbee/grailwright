[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Near {
    param(
        [Parameter(Mandatory = $true)][double]$Actual,
        [Parameter(Mandatory = $true)][double]$Expected,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([Math]::Abs($Actual - $Expected) -gt 0.0001) {
        throw "$Label expected $Expected but got $Actual."
    }
}

function Get-BloodPower {
    param([double]$Essence)

    $safeEssence = [Math]::Max(0.0, $Essence)
    if ($safeEssence -le 1000.0) {
        $mastery = [Math]::Min(1.0, $safeEssence / 1000.0)
        return (10.0 * $mastery * $mastery * $mastery) -
            (70.0 * $mastery * $mastery) +
            (160.0 * $mastery)
    }

    $overmastery = [Math]::Min(1.0, ($safeEssence - 1000.0) / 4000.0)
    return 100.0 + (100.0 * $overmastery)
}

function Get-SmoothStep {
    param([double]$Value)

    $progress = [Math]::Max(0.0, [Math]::Min(1.0, $Value))
    return $progress * $progress * (3.0 - (2.0 * $progress))
}

function Get-LightBrightnessMultiplier {
    param([double]$Power)

    if ($Power -le 100.0) {
        return 0.2 + ((2.0 - 0.2) * (Get-SmoothStep ($Power / 100.0)))
    }

    $overmastery = [Math]::Max(0.0, [Math]::Min(1.0, ($Power - 100.0) / 100.0))
    return 2.0 + ((3.0 - 2.0) * $overmastery)
}

function Get-LightRange {
    param([double]$Power)

    if ($Power -le 100.0) {
        return 1.5 + ((3.0 - 1.5) * (Get-SmoothStep ($Power / 100.0)))
    }

    $overmastery = [Math]::Max(0.0, [Math]::Min(1.0, ($Power - 100.0) / 100.0))
    return 3.0 + ((4.5 - 3.0) * $overmastery)
}

function Get-BleedDurationMultiplier {
    param([double]$Power)

    $progress = [Math]::Max(0.0, [Math]::Min(1.0, $Power / 200.0))
    return 1.0 + $progress
}

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\BloodMagicExpansion.cs")
$requiredContracts = @(
    'ConfigSchemaVersion = 23',
    'NormalMaximumBloodPower = 100.0f',
    'AbsoluteMaximumBloodPower = 200.0f',
    'BloodEssenceAtNormalMaximumPower = 1000.0f',
    'BloodEssenceAtAbsoluteMaximumPower = 5000.0f',
    'MaximumOvermasteryBonusFraction = 1.0f',
    '"MinimumPowerBrightnessMultiplier", 0.2f',
    '"MasteryBrightnessMultiplier", 2.0f',
    '"MaximumPowerBrightnessMultiplier", 3.0f',
    '"MinimumPowerRange", 1.5f',
    '"MasteryRange", 3.0f',
    '"MaximumPowerRange", 4.5f',
    '"MaximumBleedDurationMultiplier", 2.0f',
    '"ProjectileTravelBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:11;25:16;30:22;35:29;40:37;45:47;50:56"',
    '"AreaRadiusBloodPowerBonusCurve", "0:0;5:1;10:2;15:4;20:6;25:9;30:13;35:18;40:23;45:28;50:34"',
    '"BleedBuildupBloodPowerBonusCurve", "0:0;5:1;10:3;15:6;20:11;25:16;30:22;35:29;40:37;45:47;50:56"',
    '"TapCastSpeedBloodPowerBonusCurve", "0:0;5:0;10:1;15:2;20:4;25:6;30:8;35:11;40:14;45:18;50:21"',
    '"AbhartachRadiusMinimumMultiplier", 0.85f',
    '"AbhartachRadiusMaximumMultiplier", 1.15f',
    '"AbhartachHealingMinimumMultiplier", 0.75f',
    '"AbhartachHealingMaximumMultiplier", 1.25f',
    'BuildupStatusTypeName = "Awaken.TG.Main.Heroes.Statuses.BuildUp.BuildupStatus"',
    'if (patchMethodName == "Prefix")',
    'ConditionalWeakTable<object, BloodMagicBleedDurationState>',
    '[ThreadStatic]',
    'nameof(CharacterStatusesBuildupStatusPatch.Finalizer)',
    'nameof(BuildupStatusBuildupPatch.Postfix)',
    'nameof(SphereDamageRangePatch.Finalizer)',
    'nameof(ConeDamageRangePatch.Finalizer)',
    'sourceCharacter != null && IsSameModelOrOwner(sourceCharacter, GetHero())',
    'QualifyingPlayerBloodSpell = qualifying',
    'state.IsBleed = isBleed;',
    '__state.IsBleed);',
    'RecordBloodMagicBleedProc(__instance, __result)',
    '_bloodMagicBleedDurationStates.Remove(buildupStatus);',
    '_bloodMagicBleedDurationStates.TryGetValue(buildupStatus, out state)',
    'deltaTime /= state.Multiplier;',
    'GetBloodSpellProjectileTravelMultiplier()',
    'GetBloodSpellAreaRadiusMultiplier()',
    'GetAbhartachRadiusCorpseQualityMultiplier()',
    'GetAbhartachHealingCorpseQualityMultiplier()',
    '"HomingTargetSearchMaximumMultiplier", 2.1f',
    '"HeldTargetRangeMaximumMultiplier", 2.0f',
    '"CorpseSearchMaximumMultiplier", 2.0f',
    'GetBloodPowerOvermasteryProgress01(power)'
)
foreach ($contract in $requiredContracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Blood Power progression contract: $contract"
    }
}

foreach ($removedLegacyGrowthContract in @(
    '_bloodMagicGrowthSource',
    '_bloodSpellSpiritualityStatTerms',
    '_cachedSpiritualityTermsRaw',
    '_cachedSpiritualityTerms',
    '_cachedHeroSpiritualityValue',
    '_nextHeroSpiritualityRefreshTime',
    'BloodMagicGrowthSource',
    'GetSpiritualityStatTerms',
    'GetCachedHeroSpiritualityValue',
    'TryResolveHeroSpiritualityValue',
    'Spirituality')) {
    if ($source.IndexOf($removedLegacyGrowthContract, [StringComparison]::Ordinal) -ge 0) {
        throw "Blood Power progression source still contains removed Spirituality growth behavior: $removedLegacyGrowthContract"
    }
}

$growthMultiplierBlock = [regex]::Match(
    $source,
    '(?s)private float GetBloodMagicGrowthMultiplier\(.+?(?=\r?\n\s*private )')
if (!$growthMultiplierBlock.Success -or
    $growthMultiplierBlock.Value -notmatch 'float growthValue\s*=\s*GetBloodPower\(\);' -or
    $growthMultiplierBlock.Value -notmatch 'Math\.Min\(growthValue,\s*NormalMaximumBloodPower\)') {
    throw "Blood Magic curve scaling must unconditionally use Blood Power derived from Blood Essence and retain its mastery cap."
}

$tunedMultiplierBlock = [regex]::Match(
    $source,
    '(?s)private float GetBloodSpellTunedMultiplier\(float presetBase, float growthMultiplier, float maximum\).+?(?=\r?\n\s*private )')
if (!$tunedMultiplierBlock.Success -or
    $tunedMultiplierBlock.Value -notmatch 'float power\s*=\s*GetBloodPower\(\);' -or
    $tunedMultiplierBlock.Value -notmatch 'GetBloodPowerOvermasteryBonusFraction\(power\)') {
    throw "Blood Spell tuning must apply Blood Power mastery and overmastery scaling."
}

foreach ($required in @(
    'public const int ApiVersion = 10;',
    'GetBloodEssenceGainForQuality(',
    'bonusChance = 0.05f',
    '(0.0005f * Mathf.Clamp(GetBloodPower(), 0.0f, 200.0f))',
    'return nominal + bonus;',
    'public static bool IsBloodMagicDamage(object damage)')) {
    if (!$source.Contains($required)) {
        throw "Missing integer Blood Essence or exact damage provenance contract: $required"
    }
}

if ($source.IndexOf('BindOrdered("Blood Spell Inner Light", "MaximumPowerRangeMultiplier"', [StringComparison]::Ordinal) -ge 0 -or
    $source.IndexOf('BindOrdered("Blood Spell Inner Light", "Range"', [StringComparison]::Ordinal) -ge 0 -or
    $source.IndexOf('BindOrdered("Advanced - Blood Spell Growth", "RangeBleedTapBloodPowerBonusCurve"', [StringComparison]::Ordinal) -ge 0) {
    throw "An obsolete derived inner-light range setting is still bound."
}

if ($source.IndexOf('!GetBoolProperty(buildupStatus, "Active", false)', [StringComparison]::Ordinal) -ge 0) {
    throw "Tagged Bleed decay still performs the obsolete reflective Active check."
}

$areaOwnershipStart = $source.IndexOf('private bool IsPlayerBloodMagicAreaBuildup', [StringComparison]::Ordinal)
$applicationOwnershipStart = $source.IndexOf('private BloodMagicBuildupApplicationScopeState BeginBloodMagicBuildupApplicationScope', [StringComparison]::Ordinal)
$ownershipEnd = $source.IndexOf('private void EndBloodMagicBuildupApplicationScope', [StringComparison]::Ordinal)
if ($areaOwnershipStart -lt 0 -or $applicationOwnershipStart -lt 0 -or $ownershipEnd -le $applicationOwnershipStart) {
    throw "Could not locate the Blood Magic Bleed ownership methods."
}
$ownershipBlock = $source.Substring($areaOwnershipStart, $ownershipEnd - $areaOwnershipStart)
if ($ownershipBlock.IndexOf('_abhartachScaleExplosionBleed', [StringComparison]::Ordinal) -ge 0) {
    throw "Abhartach Bleed duration ownership still depends on explosion Bleed-buildup scaling."
}

$powerMilestones = @(
    @(0.0, 0.0),
    @(250.0, 35.78125),
    @(500.0, 63.75),
    @(750.0, 84.84375),
    @(1000.0, 100.0),
    @(1500.0, 112.5),
    @(2000.0, 125.0),
    @(3000.0, 150.0),
    @(4000.0, 175.0),
    @(5000.0, 200.0),
    @(6000.0, 200.0)
)
foreach ($milestone in $powerMilestones) {
    Assert-Near (Get-BloodPower $milestone[0]) $milestone[1] "Blood Power at $($milestone[0]) Essence"
}

$notificationMilestones = @(
    @{ Power = '25.0f'; EventId = 'blood-magic-power-25'; Text = 'Blood Power rises: Your blood arts gather strength.' },
    @{ Power = '50.0f'; EventId = 'blood-magic-power-50'; Text = 'Blood Power rises: Your command of blood magic deepens.' },
    @{ Power = '75.0f'; EventId = 'blood-magic-power-75'; Text = 'Blood Power rises: Your blood rites answer with growing force.' },
    @{ Power = '100.0f'; EventId = 'blood-magic-power-100'; Text = 'Blood Power rises: Your blood arts reach a new height.' },
    @{ Power = '125.0f'; EventId = 'blood-magic-power-125'; Text = 'Blood Power rises: Your blood arts surpass their former limits.' },
    @{ Power = '150.0f'; EventId = 'blood-magic-power-150'; Text = 'Blood Power rises: Your command of blood magic grows formidable.' },
    @{ Power = '175.0f'; EventId = 'blood-magic-power-175'; Text = 'Blood Power rises: Your blood arts approach their peak.' },
    @{ Power = '200.0f'; EventId = 'blood-magic-power-200'; Text = 'Blood Power rises: Your command of blood magic reaches its apex.' }
)
foreach ($milestone in $notificationMilestones) {
    $expected = 'new BloodPowerMilestone(' + $milestone.Power + ', "' + $milestone.EventId + '", "' + $milestone.Text + '")'
    if (!$source.Contains($expected)) {
        throw "Missing Blood Power notification milestone: $($milestone.Power)."
    }
}

$milestoneFeedbackBlock = [regex]::Match(
    $source,
    '(?s)private void ShowBloodPowerMilestonesAfterProgression\(.+?(?=\r?\n\s*private )')
if (!$milestoneFeedbackBlock.Success -or
    $milestoneFeedbackBlock.Value -notmatch '_overrideBloodEssence != null && _overrideBloodEssence\.Value' -or
    $milestoneFeedbackBlock.Value -notmatch 'GetBloodPowerFromEssence\(receipt\.BeforeEssence\)' -or
    $milestoneFeedbackBlock.Value -notmatch 'GetBloodPowerFromEssence\(\s*SaturatingAdd\(receipt\.BeforeEssence, receipt\.Award\)\)' -or
    $milestoneFeedbackBlock.Value -notmatch 'beforePower < milestone\.Power && afterPower >= milestone\.Power' -or
    $milestoneFeedbackBlock.Value -notmatch 'TryShowBloodPowerMilestone\(milestone\)') {
    throw 'Blood Power milestone feedback must compare saved Essence crossings and suppress diagnostic overrides.'
}

$presentationBlock = [regex]::Match(
    $source,
    '(?s)private bool TryShowBloodPowerMilestone\(.+?(?=\r?\n\s*private )')
if (!$presentationBlock.Success -or
    !$source.Contains('GrailFloatingTextBloodPowerStyle = "Red"') -or
    !$source.Contains('GrailFloatingTextBloodPowerIconId = "magic_blood"') -or
    !$source.Contains('GrailFloatingTextBloodPowerDurationBucket = "Medium"') -or
    $presentationBlock.Value -notmatch '_grailFloatingTextTryShowEventMethod\.Invoke' -or
    $presentationBlock.Value -notmatch '"Reward"' -or
    $presentationBlock.Value -notmatch '"High"' -or
    $presentationBlock.Value -notmatch 'string\.Empty') {
    throw 'Blood Power milestone feedback must use the approved GFT event presentation.'
}

$bridgeBlock = [regex]::Match(
    $source,
    '(?s)private bool TryResolveGrailFloatingTextBridge\(.+?(?=\r?\n\s*private )')
if (!$bridgeBlock.Success -or
    $bridgeBlock.Value -notmatch '_grailFloatingTextTryShowEventMethod = AccessTools\.Method\(' -or
    $bridgeBlock.Value -notmatch '"TryShowEvent"' -or
    $bridgeBlock.Value -notmatch 'typeof\(float\),\s*typeof\(float\)') {
    throw 'Blood Power milestone feedback must resolve the GFT TryShowEvent signature through the optional bridge.'
}

$corpsePaymentBlock = [regex]::Match(
    $source,
    '(?s)private void PayCorpseLeech\(.+?(?=\r?\n\s*private )')
$corpseRewardIndex = $corpsePaymentBlock.Value.IndexOf(
    'TryClaimGrailFloatingTextCorpseXp(',
    [StringComparison]::Ordinal)
$corpseXpCommittedIndex = $corpsePaymentBlock.Value.IndexOf(
    'state.XpAwarded = true;',
    [StringComparison]::Ordinal)
$corpseMilestoneIndex = $corpsePaymentBlock.Value.IndexOf(
    'ShowBloodPowerMilestonesAfterProgression(essenceReceipt);',
    [StringComparison]::Ordinal)
if (!$corpsePaymentBlock.Success -or
    $corpseRewardIndex -lt 0 -or
    $corpseXpCommittedIndex -lt 0 -or
    $corpseMilestoneIndex -lt 0 -or
    $corpseRewardIndex -ge $corpseMilestoneIndex -or
    $corpseXpCommittedIndex -ge $corpseMilestoneIndex) {
    throw 'Blood Power milestones must follow the separate reward line and irreversible XP transaction.'
}

$brightnessMilestones = @(
    @(0.0, 0.2),
    @(25.0, 0.48125),
    @(50.0, 1.1),
    @(75.0, 1.71875),
    @(100.0, 2.0),
    @(125.0, 2.25),
    @(150.0, 2.5),
    @(175.0, 2.75),
    @(200.0, 3.0)
)
foreach ($milestone in $brightnessMilestones) {
    Assert-Near (Get-LightBrightnessMultiplier $milestone[0]) $milestone[1] "Light brightness at Power $($milestone[0])"
}

$rangeMilestones = @(
    @(0.0, 1.5),
    @(50.0, 2.25),
    @(100.0, 3.0),
    @(150.0, 3.75),
    @(200.0, 4.5)
)
foreach ($milestone in $rangeMilestones) {
    Assert-Near (Get-LightRange $milestone[0]) $milestone[1] "Light range at Power $($milestone[0])"
}

$durationMilestones = @(
    @(0.0, 1.0),
    @(50.0, 1.25),
    @(100.0, 1.5),
    @(150.0, 1.75),
    @(200.0, 2.0)
)
foreach ($milestone in $durationMilestones) {
    Assert-Near (Get-BleedDurationMultiplier $milestone[0]) $milestone[1] "Bleed duration at Power $($milestone[0])"
}

Write-Output "Blood Power progression contracts passed."
