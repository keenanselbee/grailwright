[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\DeedsOfAvalon.cs") -Raw

$requiredContracts = @(
    'facts.Set("blood.essence", Math.Max(0, Mathf.RoundToInt(bloodEssence)));',
    'facts.Set("blood.power", Mathf.Clamp(bloodPower, 0.0f, 200.0f));',
    'int bloodPower = Mathf.Clamp(Mathf.RoundToInt(facts.Get("blood.power", 0.0f)), 0, 200);',
    'int displayedBloodEssence = DisplayInteger("Blood Essence", bloodEssence);',
    'int displayedBloodPower = DisplayInteger("Blood Power", bloodPower);',
    'case "Blood Power": return 21;',
    '+ " (" + displayedBloodPower.ToString("N0", CultureInfo.InvariantCulture) + ")"',
    '"Fish caught", "fish", "Gold"',
    '"Food eaten", "food", "Gold"',
    '"Potions used", "potion", "Blue"',
    'Stat.Events.StatChangedBy(CurrencyStatType.Wealth)',
    'change.value <= 0.0f',
    'AddCounter(facts, "deeds.total_gold_earned", earned);',
    'GoldEarnedIcon(DisplayInteger("Total gold earned", totalGoldEarned))',
    'string corpseIcon = CorpseTierIcon(facts);',
    '"Meager corpses", "corpse_meager", "Red"',
    '"Worthy corpses", "corpse_worthy", "Red"',
    '"Potent corpses", "corpse_potent", "Red"',
    '"Prime corpses", "corpse_prime", "Red"',
    'facts.Get("blood.corpses_drained.quality_sum", 0.0f) / total',
    'private const int GoldEarnedLowMinimum = 1000;',
    'private const int GoldEarnedMediumMinimum = 5000;',
    'private const int GoldEarnedHighMinimum = 15000;',
    'private const int GoldEarnedVeryHighMinimum = 40000;',
    'return "gold_earned_very_low";',
    'return "gold_earned_low";',
    'return "gold_earned_medium";',
    'return "gold_earned_high";',
    'return "gold_earned_very_high";',
    'case "Total gold earned": return 12450;',
    '"Wyrdness", "wyrd", "Wyrd"',
    'case "wyrdness": return "Wyrd";',
    'availablePoints.Add("Attribute: "',
    'availablePoints.Add("Skill: "',
    'availablePoints.Add("Catalyst: "',
    'availablePoints.Add("Arthur: "',
    'private const float WhiteTextOutlineStrengthMultiplier = 1.1f;',
    '"PanelColumnWidth", 190.0f',
    '"ColumnGap", 30.0f',
    '"PanelBackgroundOpacity", 0.95f',
    '"PanelBackgroundPadding", 16.0f',
    '"TextOutlineOpacity", 0.5f',
    '"TextOutlineWidth", 5.0f',
    '"TextOutlineStrength", 2',
    '"TextShadowOpacity", 1.0f',
    '"TextShadowOffset", 4.0f',
    '"TextShadowSoftness", 0.5f',
    '"SortFoesByKillCount", true',
    'if (_sortFoesByKillCount.Value)',
    'RelabelOtherRows(foes, "combat", "Other Weapons");',
    'RelabelOtherRows(foes, "magic", "Other Magic");',
    'int byValue = right.Value.CompareTo(left.Value);',
    'args.Add(_textShadowSoftness.Value);',
    'args.Add(WhiteTextOutlineStrengthMultiplier);',
    '_panelColumnWidth.Value,',
    '_columnGap.Value,',
    '_panelBackgroundOpacity.Value,',
    '_panelBackgroundPadding.Value',
    'leftSummaryRowCount,',
    'GetRawConstantValue() < 15',
    'GetParameters().Length != 38',
    '[HarmonyPatch(typeof(VQuickUseWheelUI), "Appear")]',
    '[HarmonyPatch(typeof(VQuickUseWheelUI), "Disappear")]',
    '[HarmonyPatch(typeof(VMenuUI), "OnInitialize")]',
    '[HarmonyPatch(typeof(VMenuUI), "OnDiscard")]',
    '_showQuickWheelStatistics = Config.Bind("Quick Wheel", "ShowCharacterStatistics", true',
    '_showPauseMenuStatistics = Config.Bind("Pause Menu", "ShowCharacterStatistics", true',
    '&& _pauseMenuView.gameObject.activeInHierarchy;',
    'ShouldShowPanel(pauseMenuVisible)',
    'World.Events.ModelDiscarded<QuickUseWheelUI>()',
    '_nextPanelRefresh = now + 0.2f;',
    'new Category("foes.magic.damage.other", "Other", "magic", "White")',
    'LimitCountRows(weaponRows, _maximumWeaponRows.Value, "Other", "Red", "combat");',
    'LimitCountRows(magicRows, _maximumMagicRows.Value, "Other", "White", "magic");',
    'totalFoes = SaturatingAdd(totalFoes, Math.Max(0, foes[i].Value));',
    'if (item.IsPolearm) return "foes.weapon.one_handed_polearm";',
    'if (item.IsSickle) return "foes.weapon.one_handed_axe";',
    'value += facts.Get("foes.weapon.one_handed_sickle", 0);',
    '"One-Handed Spear", "one_handed_spear", "Red"',
    '"Two-Handed Spear", "two_handed_spear", "Red"',
    '"Throwables", "combat", "Red"',
    '"Other", "combat", "Red"',
    'LogFallbackKill(outcome.Damage, category);',
    'default: return "White";',
    'HeroPointsHelper.OwnedPoints(HeroStatType.CatalystTalentPoints)',
    'WyrdArthurUI.IsViewAvailable()',
    '"Wyrd Whispers: "',
    'IsWyrdWhispersReminderVisible()'
)
foreach ($contract in $requiredContracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Deeds panel presentation contract: $contract"
    }
}

if ($source.IndexOf('" | Blood Power: "', [StringComparison]::Ordinal) -ge 0) {
    throw "The obsolete long Blood Power label remains in the panel source."
}
if (($source.IndexOf('Mathf.Clamp(bloodPower, 0.0f, 120.0f)', [StringComparison]::Ordinal) -ge 0) -or
    ($source.IndexOf('Mathf.Clamp(Mathf.RoundToInt(facts.Get("blood.power", 0.0f)), 0, 120)', [StringComparison]::Ordinal) -ge 0)) {
    throw "Blood Power still uses the obsolete 120 cap."
}
if ($source.IndexOf('"Fishes Caught"', [StringComparison]::Ordinal) -ge 0) {
    throw "The obsolete Fishes Caught label remains in the panel source."
}
if (($source.IndexOf('"Attribute Points: "', [StringComparison]::Ordinal) -ge 0) -or ($source.IndexOf('"Skill Points: "', [StringComparison]::Ordinal) -ge 0)) {
    throw "The obsolete long contextual point labels remain in the panel source."
}
if ($source.IndexOf('World.Any<QuickUseWheelUI>()', [StringComparison]::Ordinal) -ge 0) {
    throw "Deeds still polls the world for the quick wheel every frame."
}
if ($source.IndexOf('"deeds-gold-earned"', [StringComparison]::Ordinal) -ge 0) {
    throw "Total Gold Earned still registers a Deeds-owned icon instead of using GFT's built-in icons."
}
if ($source.IndexOf('"corpse"', [StringComparison]::Ordinal) -ge 0) {
    throw "Deeds still requests the obsolete generic corpse icon."
}

$bloodEssenceRowStart = $source.IndexOf('rows.Add(new PanelRow(', $source.IndexOf('int displayedBloodEssence', [StringComparison]::Ordinal), [StringComparison]::Ordinal)
$bloodEssenceRowEnd = $source.IndexOf('displayedBloodEssence));', $bloodEssenceRowStart, [StringComparison]::Ordinal)
if ($bloodEssenceRowStart -lt 0 -or $bloodEssenceRowEnd -le $bloodEssenceRowStart) {
    throw "Could not locate the Deeds Blood Essence panel row."
}
$bloodEssenceRow = $source.Substring($bloodEssenceRowStart, $bloodEssenceRowEnd - $bloodEssenceRowStart)
if ($bloodEssenceRow.IndexOf('"magic_blood"', [StringComparison]::Ordinal) -lt 0) {
    throw "Blood Essence does not use Grail Floating Text's blood-magic icon."
}
if ($bloodEssenceRow.IndexOf('corpseIcon', [StringComparison]::Ordinal) -ge 0) {
    throw "Blood Essence still uses a corpse-quality icon."
}

Write-Output "Deeds panel presentation contracts passed."
