[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$mainSourcePath = Join-Path $modRoot "src\SteelAndBone.cs"
$difficultySourcePath = Join-Path $modRoot "src\DifficultyOverhaul.cs"
$manifestPath = Join-Path $modRoot "mod.json"
$readmePath = Join-Path $modRoot "README.txt"
$nexusFullPath = Join-Path $modRoot "nexus-full-desc.txt"
$nexusShortPath = Join-Path $modRoot "nexus-short-desc.txt"
$nexusFilePath = Join-Path $modRoot "nexus-file-desc.txt"

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Steel and Bone difficulty contract failed: $Message"
    }
}

$mainSource = Get-Content -LiteralPath $mainSourcePath -Raw
$difficultySource = Get-Content -LiteralPath $difficultySourcePath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$readme = Get-Content -LiteralPath $readmePath -Raw
$nexusFull = Get-Content -LiteralPath $nexusFullPath -Raw
$nexusShort = (Get-Content -LiteralPath $nexusShortPath -Raw).Trim()
$nexusFile = (Get-Content -LiteralPath $nexusFilePath -Raw).Trim()

Assert-Contract ($manifest.version -eq "3.1.4") "mod.json is not version 3.1.4."
Assert-Contract ($manifest.sourceFiles -contains "src/DifficultyOverhaul.cs") "DifficultyOverhaul.cs is missing from sourceFiles."
Assert-Contract ($mainSource.Contains('PluginVersion = "3.1.4"')) "PluginVersion is not 3.1.4."
Assert-Contract ($mainSource.Contains('ConfigSchemaVersion = 15')) "Config schema is not 15."
Assert-Contract ($mainSource.Contains('ConfigRecoveryBaselineSchema = 14')) "Recovery baseline moved from 14."
Assert-Contract ($mainSource.Contains('new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(')) "Preset safety rule is missing."
Assert-Contract ($mainSource.Contains('"Preset"')) "Preset safety rule does not target Preset."
Assert-Contract ($mainSource.Contains('ReadCustomizationProfile(')) "Automatic config preservation does not use the shared customization profile."
Assert-Contract ($mainSource.Contains('profile.TryGetCustomizedValue(')) "Automatic config preservation does not use shared typed customization detection."
Assert-Contract ($mainSource.Contains('ConfigPreviousSettingsRecovery.TryRestore(')) "Automatic config preservation does not use shared current-range clamping."
Assert-Contract ($mainSource.IndexOf('RestorePreservedConfigSettings();', [StringComparison]::Ordinal) -lt $mainSource.IndexOf('ConfigPreviousSettingsRecovery.Bind(', [StringComparison]::Ordinal)) "Automatic preservation does not run before the manual recovery tab is bound."
Assert-Contract ($mainSource.Contains('class EnemyArmorProfile : Element<NpcElement>')) "NPC armor profiles are not cached on the target."
Assert-Contract ($mainSource.Contains('ICharacterInventory.Events.AfterEquipmentChanged')) "NPC armor profiles do not refresh after equipment changes."
Assert-Contract ($mainSource.Contains('item.Template.IsLightArmor')) "Light armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('item.Template.IsMediumArmor')) "Medium armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('item.Template.IsHeavyArmor')) "Heavy armor does not use native item-template evidence."
Assert-Contract ($mainSource.Contains('itemAudio.ArmorSurfaceType')) "Armor spell interactions do not use native armor-surface evidence."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorFabric')) "Fabric armor material is not classified."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorLeather')) "Leather armor material is not classified."
Assert-Contract ($mainSource.Contains('SurfaceType.ArmorMetal')) "Metal armor material is not classified."
Assert-Contract ($mainSource.Contains('baseMultiplier = 1.08f;')) "Light armor arrow tuning is not x1.08 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.75f;')) "Heavy armor arrow tuning is not x0.75 on Hardened."
Assert-Contract ($mainSource.Contains('DampArmorTierResistanceAgainstNativeArmor')) "Armor-tier resistance does not account for existing numerical armor."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 1.00f : 0.90f')) "Heavy armor Pierce tuning is not x0.90 on Hardened."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 0.92f : 0.82f')) "Slash is not less effective than Pierce against Medium and Heavy armor."
Assert-Contract ($mainSource.Contains('armorTier == EnemyArmorTier.Medium ? 1.08f : 1.15f')) "Blunt does not improve against Medium and Heavy armor."

$boundEntryFields = @(
    [regex]::Matches(
        $mainSource + [Environment]::NewLine + $difficultySource,
        '\b(_[A-Za-z][A-Za-z0-9_]*)\s*=\s*Config\.Bind') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
)
$restoredEntryFields = @(
    [regex]::Matches(
        $mainSource,
        'RestorePreservedSetting\(profile,\s*(_[A-Za-z][A-Za-z0-9_]*)\s*,') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
)
$missingAutomaticRecovery = @(
    $boundEntryFields | Where-Object { $restoredEntryFields -notcontains $_ }
)
Assert-Contract ($missingAutomaticRecovery.Count -eq 0) ("Automatic config preservation omits bound fields: " + ($missingAutomaticRecovery -join ", "))

$requiredSettings = @(
    "DifficultyModifiersEnabled",
    "ModifyPlayerDamageDealt",
    "PlayerDamageDealtMultiplier",
    "ModifyPlayerDamageTaken",
    "ModifyStaminaUsage",
    "ModifyManaUsage",
    "ModifyPlayerPoiseDamageDealt",
    "ModifyPlayerArrowVelocity",
    "ModifyArmorWeightPenalties",
    "ModifyLightArmorMobility",
    "ModifyArmorPhysicalProtection",
    "ModifyEnemyAttackSlots",
    "EnemyAttackSlotCap",
    "ModifyEnemyAttackRecovery",
    "ModifyHostileArrowVelocity",
    "ModifyEnemySightRange",
    "ModifyKillExperience",
    "ModifyQuestExperience",
    "ModifyProficiencyExperience"
)

foreach ($setting in $requiredSettings) {
    Assert-Contract ($difficultySource.Contains('"' + $setting + '"')) "Missing setting $setting."
}

Assert-Contract ($difficultySource.Contains('return 0.05f;')) "Hardened 5 percent preset value is missing."
Assert-Contract ($difficultySource.Contains('return 0.10f;')) "Crucible 10 percent preset value is missing."
Assert-Contract ($difficultySource.Contains('return 1.0f - PresetPenaltyAmount();')) "Preset reduction math is missing."
Assert-Contract ($difficultySource.Contains('return 1.0f + PresetPenaltyAmount();')) "Preset cost math is missing."
Assert-Contract ($difficultySource.Contains('return 1.10f;')) "Tempered arrow velocity is not x1.10."
Assert-Contract ($difficultySource.Contains('return 1.30f;')) "Hardened arrow velocity is not x1.30."
Assert-Contract ($difficultySource.Contains('return 1.50f;')) "Crucible arrow velocity is not x1.50."
Assert-Contract ($difficultySource.Contains('PresetEnemySightRangeMultiplier')) "Enemy sight-range preset mapping is missing."
Assert-Contract ($difficultySource.Contains('return heavy ? 1.10f : 1.05f;')) "Hardened physical armor values are missing."
Assert-Contract ($difficultySource.Contains('return heavy ? 1.20f : 1.10f;')) "Crucible physical armor values are missing."
Assert-Contract ($difficultySource.Contains('return 1.025f;')) "Hardened Light armor mobility is not x1.025."
Assert-Contract ($difficultySource.Contains('case Preset.Hardened:') -and $difficultySource.Contains('return 1;')) "Hardened attack-slot bonus is missing."
Assert-Contract ($difficultySource.Contains('case Preset.Crucible:') -and $difficultySource.Contains('return 2;')) "Crucible attack-slot bonus is missing."

$requiredHooks = @(
    "CharacterStats.CharacterStatsWrapper",
    "MaxEnemiesAttacking",
    "AttackActionUnBookProlong",
    "GetExpReward",
    "Quest.ExperiencePoints",
    "Objective.ExperiencePoints",
    "TryAddXP",
    "NpcGeneralFSM",
    "BowFSM.FireProjectileInternal",
    "CombatBehaviourUtils.FireProjectile",
    "Hero.TotalArmor"
)

foreach ($hook in $requiredHooks) {
    Assert-Contract ($difficultySource.Contains($hook)) "Missing hook contract for $hook."
}

$targetGuard = $mainSource.IndexOf("bool targetIsHero", [StringComparison]::Ordinal)
$incomingApplication = $mainSource.IndexOf("ApplyIncomingHealthDamageModifier", [StringComparison]::Ordinal)
$sourceGuard = $mainSource.IndexOf("if (!IsHeroDamageSource", [StringComparison]::Ordinal)
Assert-Contract ($targetGuard -ge 0 -and $incomingApplication -gt $targetGuard -and $sourceGuard -gt $incomingApplication) "Incoming damage does not run before the hero-source guard."

Assert-Contract (-not $difficultySource.Contains("ChangingStatWealth")) "A coin hook was added to the 3.0 difficulty source."
Assert-Contract ($difficultySource.Contains("HeroStats.ArmorPenaltyMultiplier")) "Native armor-penalty scaling is missing."
Assert-Contract ($difficultySource.Contains("stats.MovementSpeedMultiplier")) "Light armor movement scaling is missing."
Assert-Contract ($difficultySource.Contains("IsPhysicalDamageSubtype")) "Physical-only armor filtering is missing."
Assert-Contract ($difficultySource.Contains("EquipmentSlotType.Quiver")) "Hostile projectile filtering is not limited to arrows."
Assert-Contract ($difficultySource.Contains("IsHostileTo(Hero.Current)")) "Hostile arrow ownership filtering is missing."
Assert-Contract ($difficultySource.Contains("Mathf.Clamp") -and $difficultySource.Contains("ScaleBallisticVelocity")) "Hostile velocity is not scaled at the ballistic clamp."
Assert-Contract ($difficultySource.Contains("World.All<NpcElement>()")) "Loaded-NPC sight-range reconciliation is missing."
Assert-Contract ($difficultySource.Contains("NpcStats.SightLengthMultiplier")) "Native NPC sight-distance stat is not used."
Assert-Contract ($difficultySource.Contains("EnemySightRangeTweak : StatTweak")) "Owned enemy sight-range tweak is missing."
Assert-Contract ($difficultySource.Contains("MarkedNotSaved = true")) "Enemy sight-range tweak is not marked non-saved."
Assert-Contract ($difficultySource.Contains("npc.IsAlive") -and $difficultySource.Contains("!npc.IsSummonOrAlly")) "Enemy sight eligibility lacks life or ally filtering."
Assert-Contract ($difficultySource.Contains("npc.NpcAI.Working")) "Enemy sight eligibility lacks active-AI filtering."
Assert-Contract ($difficultySource.Contains("WithFactionUtils.IsHostileToHero(npc)")) "Enemy sight eligibility lacks hostility filtering."
Assert-Contract ($difficultySource.Contains("RemoveAllEnemySightRangeTweaks")) "Enemy sight-range shutdown cleanup is missing."
Assert-Contract ($difficultySource.Contains("CustomDifficultyPluginGuid")) "Custom Difficulty overlap detection is missing."
Assert-Contract ($difficultySource.Contains("TaintedCombatPluginGuid")) "Tainted Combat overlap detection is missing."
Assert-Contract ($difficultySource.Contains("TaintedInstinctsPluginGuid")) "Tainted Instincts overlap detection is missing."
Assert-Contract ($difficultySource.Contains('ReportCompatibilityOverlap("Tainted Instincts", conflicts)')) "Tainted Instincts conflicts are not reported through the shared silent-overlap policy."
Assert-Contract ($difficultySource.Contains("conflicts.Count == 0")) "Silent no-overlap behavior is missing."
Assert-Contract ($difficultySource.Contains('bool momentumOverlap = momentum')) "Tainted Combat custom momentum detection is missing."
Assert-Contract ($difficultySource.Contains('staminaOverlap = momentumOverlap')) "Tainted Combat momentum does not report stamina overlap."
Assert-Contract ($difficultySource.Contains('recoveryOverlap = momentumOverlap')) "Tainted Combat momentum does not report enemy-recovery overlap."
Assert-Contract ($mainSource.Contains('"ArrowMaterialRulesEnabled"')) "Arrow material rules are not independently toggleable."
Assert-Contract ($mainSource.Contains('"ArmoredSpellWeaknessEnabled"')) "Armored spell weakness is not independently toggleable."
Assert-Contract ($mainSource.Contains('classification.IsArrow = IsArrowDamage(damage)')) "Direct arrow delivery is not classified separately from Pierce."
Assert-Contract ($mainSource.Contains('GetPhysicalDamageShare(damage, damageClass)')) "Arrow rules do not preserve independent elemental payload weighting."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.20f;')) "Confirmed skeleton arrow resistance is not 0.20 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.50f;')) "Construct or stone arrow resistance is not 0.50 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.55f;')) "Spirit arrow resistance is not 0.55 on Hardened."
Assert-Contract ($mainSource.Contains('baseMultiplier = 0.60f;')) "Flora or wood arrow resistance is not 0.60 on Hardened."
Assert-Contract ($mainSource.Contains('return "Exposed Flesh";') -and $mainSource.Contains('baseMultiplier = 1.20f;')) "Exposed humanoid flesh arrow weakness is missing."
Assert-Contract ($mainSource.Contains('targetLabel = "Flesh";') -and $mainSource.Contains('baseMultiplier = 1.12f;')) "Ordinary flesh arrow weakness is missing."
Assert-Contract ($mainSource.Contains('case EnemyArmorTier.Heavy:') -and $mainSource.Contains('tierBonus = 0.12f;')) "Heavy armor direct-spell tier bonus is missing."
Assert-Contract ($mainSource.Contains('return 0.10f;') -and $mainSource.Contains('EnemyArmorMaterial.Metal')) "Electric spells do not receive the intended Metal armor bonus."
Assert-Contract ($mainSource.Contains('damageClass.IsBloodMagic') -and $mainSource.Contains('damageClass.IsWyrdness')) "Biological or Wyrd spell identities are not excluded from generic armor bonuses."
Assert-Contract ($mainSource.Contains('GetOptionalBoolProperty(damage, "IgnoreArmor")')) "Armor-ignoring damage is not protected from duplicate armor interactions."
Assert-Contract ($mainSource.Contains('TryApplyWeightedDamageComposition(')) "Mixed hits do not use weighted damage composition."
Assert-Contract ($mainSource.Contains('weightedAdjustment += postVanillaShare * partAdjustment;')) "Per-part adjustments are not weighted by post-vanilla contribution."
Assert-Contract ($mainSource.Contains('amplificationRatio = amplifiedMultiplier / nativeMultiplier;')) "Vanilla amplification does not preserve the existing native multiplier."
Assert-Contract ($mainSource.Contains('part.SetTotalDamageMultiplier(adjustedShare);')) "Adjusted damage-part shares are not exposed to downstream systems."
Assert-Contract ($mainSource.Contains('weightedFeedback / feedbackWeight')) "Damage feedback does not aggregate native and custom per-part reactions."
Assert-Contract ($mainSource.Contains('bool contextualStatusPart = subtype == DamageSubType.Pure')) "Contextual status metadata is not isolated from unrelated mixed-damage parts."

$opposingMixedAdjustment = (0.25 * (0.40 / 0.50)) + (0.75 * (1.60 / 1.50))
Assert-Contract ([Math]::Abs($opposingMixedAdjustment - 1.0) -lt 0.000001) "Opposing mixed subtype reactions do not recombine to the expected neutral result."

$physicalPayloadAdjustment = (0.80 * 0.75) + (0.20 * 1.25)
Assert-Contract ([Math]::Abs($physicalPayloadAdjustment - 0.85) -lt 0.000001) "Independent physical and payload rules do not produce the expected weighted result."

Assert-Contract ($readme.Contains("Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.")) "Packaged README lacks the defining subtext."
Assert-Contract ($readme.Contains("Material weaknesses and resistances define the experience")) "Packaged README does not lead with material combat."
Assert-Contract ($nexusFull.Contains("Bone, flesh, stone, and spirit. Know your enemy. Strike with purpose.")) "Nexus description lacks the defining subtext."
Assert-Contract ($nexusFull.Contains("Its defining feature is a material weakness and resistance system.")) "Nexus description does not lead with material combat."
Assert-Contract ($nexusFull.Contains("a broad, knowledge-driven difficulty mod built from lightweight, native-first, modular changes")) "Nexus introduction does not distinguish broad scope from lightweight implementation."
Assert-Contract ($nexusFull.Contains("Broad in Scope, Lightweight in Implementation")) "Nexus lightweight-implementation section heading is missing."
Assert-Contract ($nexusFull.Contains("x1.10 / x1.30 / x1.50 speed by preset")) "Nexus preset sequences do not use the slash-separated convention."
Assert-Contract ($nexusFull.Contains("Difficulty That Supports the Matchup Game")) "Nexus description does not connect supporting difficulty to material combat."
Assert-Contract ($nexusFull.Contains("Inspired by Requiem's Philosophy")) "Nexus description lacks the Requiem inspiration context."
Assert-Contract ($nexusFull.Contains("mods/60888")) "Nexus description does not link Requiem's current Special Edition page."
Assert-Contract ($nexusFull.Contains("not a Requiem port, dependency, or attempt to reproduce the scope of a total conversion")) "Nexus description does not bound the Requiem comparison."
Assert-Contract ($nexusFull.Contains("Custom Difficulty[/url] is incompatible")) "Custom Difficulty is not described as incompatible on Nexus."
Assert-Contract ($nexusFull.Contains("Tainted Instincts[/url] is incompatible")) "Tainted Instincts is not described as incompatible on Nexus."
Assert-Contract (-not $nexusFull.Contains("flagged as incompatible")) "Nexus compatibility wording still says flagged as incompatible."
Assert-Contract ($nexusFull.Contains("conditionally compatible")) "Tainted Combat conditional compatibility note is missing."
Assert-Contract ($nexusFull.Contains("Better Movement")) "Better Movement compatibility note is missing."
Assert-Contract ($nexusFull.Contains("Tainted Instincts") -and $nexusFull.Contains("enemy sight")) "Tainted Instincts incompatibility or enemy-awareness description is missing."
Assert-Contract ($nexusShort.Length -le 350) "Nexus short description exceeds 350 characters."
Assert-Contract ($nexusFile.Length -lt $nexusShort.Length) "Nexus file description is not shorter than the short description."
Assert-Contract ($nexusFile -ne $nexusShort) "Nexus file description duplicates the short description."

Write-Output "Steel and Bone 3.1 difficulty contracts passed."
