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
    '"Fish caught", "fish", "Cyan"',
    '"Food eaten", "food", "Orange"',
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
    'facts.Get("soul.soul_vigor", 0.0f)',
    'facts.Get("soul.necromantic_power", 0.0f)',
    '"Soul Vigor: "',
    '"necro"',
    '"Necrotic"',
    '_showSoulAndServiceStatistics = Config.Bind("Integrations", "ShowSoulAndServiceStatistics", true',
    '_soulAndServiceStatisticsMode = Config.Bind("Integrations", "SoulAndServiceStatisticsMode", SoulAndServiceStatisticsMode.Detailed',
    '"Corpses Harvested",',
    '"Meager harvests", "corpse_meager", "Necrotic"',
    '"Worthy harvests", "corpse_worthy", "Necrotic"',
    '"Potent harvests", "corpse_potent", "Necrotic"',
    '"Prime harvests", "corpse_prime", "Necrotic"',
    'case "Soul Vigor": return 116;',
    'case "Necromantic Power": return 18;',
    'case "Corpses Harvested": return 23;',
    'case "Meager harvests": return 12;',
    'case "Worthy harvests": return 7;',
    'case "Potent harvests": return 3;',
    'case "Prime harvests": return 1;',
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
    'MidpointRounding.AwayFromZero);',
    'DisplayInteger("Hours rested", hours).ToString("N0", CultureInfo.InvariantCulture)',
    'case "Hours rested": return 44;',
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
    '_showLoadingScreenStatistics = Config.Bind("Loading Screen", "ShowCharacterStatistics", false',
    'bool loadingScreenStatisticsEnabled = LoadingScreenStatisticsEnabled();',
    'bool loadingScreenVisible = IsLoadingScreenVisible();',
    'private bool LoadingScreenStatisticsEnabled()',
    'if (loadingScreenVisible)',
    'return LoadingScreenStatisticsEnabled();',
    '_pendingSavePanelContent = LoadingScreenStatisticsEnabled()',
    'return LoadingScreenUI.IsLoading',
    '|| World.HasAny<LoadingScreenUI>();',
    'ShouldShowPanel(pauseMenuVisible, loadingScreenVisible)',
    'IsLoadingScreenVisible()))',
    '&& _pauseMenuView.gameObject.activeInHierarchy;',
    '_pauseMenuView = null;',
    'ClearGftPanel();',
    'private PanelContent _loadingPanelContent;',
    'private string _loadingPanelSlotId;',
    '&& !_loadingGameplayDeserialized)',
    '_loadingGameplayDeserialized = true;',
    'JsonUtility.FromJson<SavedPanelCache>',
    'JsonUtility.ToJson(cache, true)',
    'FormatVersion = 2,',
    'PanelContent panelContent = PanelContentFromCache(cache);',
    'SavedPanelCache roundTrip = JsonUtility.FromJson<SavedPanelCache>(json);',
    'PanelContent roundTripPanel = PanelContentFromCache(roundTrip);',
    'The serialized cache failed round-trip validation.',
    'string.Equals(cache.SlotId, saveSlot.ID, StringComparison.Ordinal)',
    'WritePanelCache(slotId, content);',
    'PublishSuccessfulSaveSnapshot(__instance.SlotId);',
    '[HarmonyPatch(typeof(LoadSave), nameof(LoadSave.Load))]',
    'DeedsOfAvalonPlugin.Instance.PrepareLoadingPanel(saveSlot);',
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

if ($source.IndexOf('public PanelContent Panel;', [StringComparison]::Ordinal) -ge 0) {
    throw "Loading-screen panel cache must not rely on a nested JsonUtility payload."
}

if ($source.IndexOf('_showLoadingScreenStatistics = Config.Bind("Loading Screen", "ShowCharacterStatistics", true', [StringComparison]::Ordinal) -ge 0) {
    throw "Loading-screen statistics must remain opt-in by default."
}

$cacheGateCount = [regex]::Matches($source, 'LoadingScreenStatisticsEnabled\(\)').Count
if ($cacheGateCount -lt 8) {
    throw "Loading-screen cache reads, generation, writes, and presentation are not fully gated by the opt-in setting."
}

$itemsCraftedIndex = $source.IndexOf('AddRow(rows, facts.Get("deeds.items_crafted"', [StringComparison]::Ordinal)
$totalGoldIndex = $source.IndexOf('int totalGoldEarned =', [StringComparison]::Ordinal)
$foodEatenIndex = $source.IndexOf('AddRow(rows, facts.Get("deeds.food_eaten"', [StringComparison]::Ordinal)
if ($itemsCraftedIndex -lt 0 -or $totalGoldIndex -le $itemsCraftedIndex -or $foodEatenIndex -le $totalGoldIndex) {
    throw "Total gold earned must appear after Items crafted and before Food eaten."
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

$cachedPanelIndex = $source.IndexOf('content = _loadingPanelContent;', [StringComparison]::Ordinal)
$livePanelIndex = $source.IndexOf('content = CreateLivePanelContent();', [StringComparison]::Ordinal)
if ($cachedPanelIndex -lt 0 -or $livePanelIndex -le $cachedPanelIndex) {
    throw "Cached save-slot panel content must be preferred until live gameplay data is restored."
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

$bloodSectionIndex = $source.IndexOf('if (_showBloodMagicStatistics.Value)', [StringComparison]::Ordinal)
$soulsBoundIndex = $source.IndexOf('if (_showSoulAndServiceStatistics.Value)', [StringComparison]::Ordinal)
$crimesIndex = $source.IndexOf('AddRow(rows, facts.Get("deeds.crimes_committed"', [StringComparison]::Ordinal)
if (-not (0 -le $bloodSectionIndex -and $bloodSectionIndex -lt $soulsBoundIndex -and $soulsBoundIndex -lt $crimesIndex)) {
    throw "Soul Vigor must appear immediately after the complete Blood Magic section."
}

Write-Output "Deeds panel presentation contracts passed."
