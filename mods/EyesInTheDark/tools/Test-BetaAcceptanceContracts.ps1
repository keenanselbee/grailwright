[CmdletBinding()]
param(
    [string]$PackagePath = ""
)

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (!$Condition) {
        throw "Eyes in the Dark beta acceptance contract failed: $Message"
    }
}

$modJsonPath = Join-Path $modRoot "mod.json"
$modJson = Get-Content -LiteralPath $modJsonPath -Raw | ConvertFrom-Json
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$catalogSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\HunterCatalog.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\FirstHunterRuntime.cs") -Raw
$ambientDirectorSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\AmbientStalkerDirector.cs") -Raw
$ambientRuntimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\AmbientStalkerRuntime.cs") -Raw
$worldTimescaleSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\WorldTimescale.cs") -Raw
$meterSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatMeter.cs") -Raw
$boundarySource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\BoundaryController.cs") -Raw
$layeredBoundarySource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\LayeredBoundaryPass.cs") -Raw
$wyrdVisualSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\WyrdVisualRuntime.cs") -Raw
$atmosphereSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\Atmosphere.cs") -Raw
$gftBridgeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingTextBridge.cs") -Raw
$readme = Get-Content -LiteralPath (
    Join-Path $modRoot "README.txt") -Raw
$changelog = Get-Content -LiteralPath (
    Join-Path $modRoot "CHANGELOG.txt") -Raw
$design = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\DESIGN.md") -Raw
$roadmap = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\ROADMAP.md") -Raw
$nexusShort = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-short-desc.txt") -Raw
$nexusFile = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-file-desc.txt") -Raw
$nexusFull = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$gloriousSource = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\GloriousUI\src\GloriousUI.cs") -Raw
$gftSource = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\GrailFloatingText\src\GrailFloatingText.cs") -Raw
$gftModJson = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\GrailFloatingText\mod.json") -Raw |
    ConvertFrom-Json
$repoReadme = Get-Content -LiteralPath (
    Join-Path $repoRoot "README.md") -Raw

Assert-Contract ($modJson.version -eq "1.3.1") "mod.json version is not 1.3.1."
Assert-Contract ($modJson.displayName -eq "Eyes in the Dark - Wyrdnight Overhaul") "mod.json display name is stale."
Assert-Contract ($pluginSource.Contains('public const string PluginName = "Eyes in the Dark";')) "config/plugin title is not Eyes in the Dark."
Assert-Contract ($pluginSource.Contains('public const string PluginVersion = "1.3.1";')) "plugin version is not 1.3.1."
Assert-Contract ($pluginSource.Contains('[assembly: AssemblyVersion("1.3.1.0")]')) "assembly version is not 1.3.1.0."
Assert-Contract ($pluginSource.Contains('[assembly: AssemblyTitle("Eyes in the Dark - Wyrdnight Overhaul")]')) "assembly title is stale."
Assert-Contract ($pluginSource.Contains('private const int ConfigSchemaVersion = 21;')) "config schema is not 21."
Assert-Contract ($pluginSource -match '(?s)"AllowUnprotectedWyrdnightRest",\s*true,\s*UiDescription') "unprotected Wyrdnight rest does not default to enabled for Watchful tuning."
Assert-Contract ($pluginSource -match '(?s)"ShowWyrdnightRestAvailability",\s*true,\s*UiDescription') "Wyrdnight rest availability does not default to enabled."
Assert-Contract ($pluginSource.Contains('"The new visual baseline deliberately replaces prior customized pulse amounts with the tested 0.8 default."')) "schema-7 pulse recovery rule is missing."
Assert-Contract ($pluginSource.Contains('"The new visual baseline deliberately returns diagnostics to its safe off default after regeneration."')) "schema-7 diagnostics recovery rule is missing."
Assert-Contract ($pluginSource.Contains('"The recalibrated brightness control gives 1.0 a new 3x RGB meaning, so older same-name values are unsafe to preserve."')) "schema-17 meter brightness recovery rules are missing."
Assert-Contract ($readme.Contains('Version: 1.3.1')) "installed README version is stale."
Assert-Contract ($readme.StartsWith('Eyes in the Dark - Wyrdnight Overhaul')) "installed README title is stale."
Assert-Contract ($changelog.StartsWith('Version 1.3.1')) "changelog does not start with 1.3.1."
Assert-Contract ($repoReadme.Contains('| [Eyes in the Dark - Wyrdnight Overhaul](mods/EyesInTheDark) | 1.3.1 |')) "top-level README row is stale."
Assert-Contract ($gftModJson.version -eq "1.10.0") "GFT mod.json version is not 1.10.0."
Assert-Contract ($gftSource.Contains('private const int ConfigSchemaVersion = 24;')) "GFT config schema is not 24."
Assert-Contract (!(Test-Path -LiteralPath (Join-Path $repoRoot 'mods\PurpleWyrdness'))) "retired Purple Wyrdness package remains in the repository."
Assert-Contract (!$repoReadme.Contains('(mods/PurpleWyrdness)')) "top-level README still lists Purple Wyrdness."
Assert-Contract (!(Test-Path -LiteralPath (Join-Path $repoRoot 'mods\PurpleMoonTest'))) "retired PurpleMoonTest package remains in the repository."
Assert-Contract (!$repoReadme.Contains('(mods/PurpleMoonTest)')) "top-level README still lists Purple Moon Test."

$sourcePaths = @($modJson.sourceFiles)
foreach ($requiredSource in @(
    'src/EyesInTheDark.cs',
    'src/NightState.cs',
    'src/ThreatState.cs',
    'src/ThreatMeter.cs',
    'src/PacingState.cs',
    'src/WorldTimescale.cs',
    'src/RestRisk.cs',
    'src/Atmosphere.cs',
    'src/GrailFloatingTextBridge.cs',
    'src/BoundaryController.cs',
    'src/LayeredBoundaryPass.cs',
    'src/WyrdVisualRuntime.cs',
    'src/HuntDirector.cs',
    'src/HunterCatalog.cs',
    'src/AmbientStalkerDirector.cs',
    'src/AmbientStalkerRuntime.cs',
    'src/FirstHunterRuntime.cs')) {
    Assert-Contract ($sourcePaths -contains $requiredSource) "mod.json omits $requiredSource."
}

$allEyesText = $pluginSource + $catalogSource + $runtimeSource + $ambientDirectorSource + $ambientRuntimeSource + $worldTimescaleSource + $meterSource `
    + $boundarySource + $wyrdVisualSource + $atmosphereSource + $gftBridgeSource + $readme `
    + $design + $nexusShort + $nexusFile + $nexusFull
$retiredThreatTerm = 'Wyrd' + ' ' + 'Scent'
Assert-Contract (!$allEyesText.Contains($retiredThreatTerm)) "retired threat terminology remains in Eyes surfaces."
Assert-Contract (!$allEyesText.Contains($retiredThreatTerm.ToLowerInvariant())) "lowercase retired threat terminology remains in Eyes surfaces."
Assert-Contract (!$pluginSource.Contains('kane.tgfoa.wyrd-hunt')) "Eyes implements its own Wyrd Hunt scanner."
Assert-Contract (!$runtimeSource.Contains('kane.tgfoa.wyrd-hunt')) "the encounter runtime implements Wyrd Hunt compatibility behavior."

foreach ($required in @(
    '"CampaignMap_HOS"',
    '"CampaignMap_Cuanacht"',
    '"CampaignMap_Forlorn"',
    '"CampaignMap_Sarras"',
    'HunterSafetyFlags.ReviewedUniversal',
    '"wyrdspirit-contact"',
    '"redcap-hos"',
    '"corpse-eater-hos"',
    '"mistling-hos"',
    '"sharg-hos"',
    '"ogre-hos"',
    '"corpse-eater-cuanacht"',
    '"lost-knight-cuanacht"',
    '"barnaclator-cuanacht"',
    '"mistling-cuanacht"',
    '"sharg-cuanacht"',
    '"ogre-cuanacht"',
    '"redcap-forlorn"',
    '"bonemask-mage-forlorn"',
    '"mistling-forlorn"',
    '"corpse-eater-forlorn"',
    '"elite-skeleton-forlorn"',
    '"drowner-sarras"',
    '"finbled-heavy-sarras"',
    '"tidewraith-sarras"',
    '"drowned-knight-sarras"',
    'HunterSafetyFlags.Elite',
    'EliteThreatThreshold = 75f',
    'FailureRejectionCount = 3',
    'SafetyPackCap',
    'PrimaryWeight',
    'BuildSidecarPool')) {
    Assert-Contract ($catalogSource.Contains($required)) "curated selector is missing $required."
}
Assert-Contract (!$catalogSource.Contains('ProgressionTier')) "dormant progression-tier selector plumbing remains."
Assert-Contract (!$pluginSource.Contains('ProgressionTier')) "dormant progression-tier integration remains."

foreach ($required in @(
    'BaseLocationSpawner.VerifyPosition(',
    'template.SpawnLocation(',
    'location.MarkedNotSaved = true',
    'MinimumMemberSeparationMeters',
    'HasExactHeroTarget(',
    'ReacquisitionIntervalSeconds = 2f',
    'ReacquisitionDistanceMeters = 60f',
    'MaximumReacquisitionAttemptsPerMember = 3',
    'EnterCombatWith(hero, true)',
    'ReleaseReferences(false);',
    'ReferenceEquals(npc, _members[0].Npc)')) {
    Assert-Contract ($runtimeSource.Contains($required)) "atomic exact-primary runtime is missing $required."
}

foreach ($required in @(
    '"2. World Timescale"',
    '"EnableDynamicTimescale"',
    '"DayMinutes"',
    '"BaseNightMinutes"',
    '"MaximumThreatNightMinutes"',
    '"AllowEliteEnemies"',
    '_allowEliteEnemies.Value = false;',
    '_allowEliteEnemies.Value = true;',
    'DefaultDayMinutes = 60f',
    'DefaultBaseNightMinutes = 6f',
    'DefaultMaximumThreatNightMinutes = 12f',
    '"EnableThreatOverride"',
    '"ThreatOverrideValue"',
    '"EnableTimescaleOverride"',
    '"TimescaleOverrideMultiplier"',
    'ListenerRetryBackoffSeconds = 30.0f',
    'Continuous threat summary: passive=')) {
    Assert-Contract ($pluginSource.Contains($required)) "clock or runtime hardening is missing $required."
}
Assert-Contract ($worldTimescaleSource.Contains('SetWeatherDayDuration(')) "world clock does not use the native weather-duration setter."
Assert-Contract (!$pluginSource.Contains('Time.timeScale =')) "Eyes writes gameplay Time.timeScale."

foreach ($required in @(
    'public static class EyesInTheDarkHudApi',
    'RequestBelowVanillaBars(',
    '_placeMeterBelowResourceBars',
    'ShouldShowThreatMeter(',
    '_meterFailureLogged',
    '_boundaryFailureLogged')) {
    Assert-Contract ($pluginSource.Contains($required)) "HUD ownership or failure isolation is missing $required."
}
Assert-Contract ($meterSource.Contains('TryMirrorVisuals(')) "meter mirroring is missing."
Assert-Contract ($meterSource -match '_root,\s*true,\s*true\)') "horizontal and vertical meter mirroring are not both enabled."
foreach ($required in @(
    'ICharacter.Events.OnAttackStart',
    'ICharacter.Events.HitEnvironment',
    '_environmentImpactSeenThisAttack',
    'maximum * 0.5f',
    '"CombatResponseSeconds"',
    '"PurpleThreatMeterColor"',
    '"OrangeThreatMeterColor"',
    '"PurpleThreatMeterRedColor"',
    '"OrangeThreatMeterRedColor"',
    '"PurpleThreatMeterBrightness"',
    '"OrangeThreatMeterBrightness"',
    '"PurpleThreatMeterColorShift"',
    '"OrangeThreatMeterColorShift"',
    '"MinimumThreatMeterBrightnessScale"',
    '"MaximumThreatMeterBrightnessScale"',
    '"MinimumWorldThreatBrightnessScale"',
    '"MaximumWorldThreatBrightnessScale"',
    '"WorldThreatTargetColor"',
    '"MaximumWorldThreatColorShift"')) {
    Assert-Contract ($pluginSource.Contains($required)) "environment-impact threat or meter color config is missing $required."
}
foreach ($required in @(
    'public const string DefaultPurpleColorText = "#8032FF";',
    'public const string DefaultOrangeColorText = "#FFB87A";',
    'ColorUtility.TryParseHtmlString(',
    'WyrdVisualMath.ShiftTowardRed(')) {
    Assert-Contract ($meterSource.Contains($required)) "configurable meter color is missing $required."
}
Assert-Contract (!$boundarySource.Contains('_maskIntensityField.SetValue')) "boundary controller modifies native mask intensity."
Assert-Contract ($layeredBoundarySource.Contains('material.SetFloat(MaskIntensityId, _maskIntensity);')) "layered boundary does not preserve the native visual mask value."
Assert-Contract ($boundarySource.Contains('Mathf.Clamp(_settings.PulseAmount, 0f, 1f)')) "runtime pulse amount is not bounded to 0-1."
Assert-Contract ($boundarySource.Contains('_settings.OuterIntensityMultiplier')) "outer ring brightness is not applied independently."
Assert-Contract ($pluginSource.Contains('new AcceptableValueRange<float>(0f, 1f)')) "config pulse amount is not exposed across 0-1."
foreach ($required in @(
    'private const string DefaultBoundaryColor = "#B878FF";',
    'private const float BoundaryVanillaHdrBaseline = 271.529f;',
    'private const float DefaultBoundaryBrightness = 1.0f;',
    '"BoundaryBrightness"',
    'private const float DefaultBoundaryNearRadius = 10.0f;',
    'private const float DefaultBoundaryNearIntensity = 0.05f;',
    'private const float DefaultBoundaryNearThickness = 0.25f;',
    'private const float DefaultBoundaryMiddleRadius = 20.0f;',
    'private const float DefaultBoundaryMiddleIntensity = 0.05f;',
    'private const float DefaultBoundaryMiddleThickness = 0.25f;',
    'private const float DefaultBoundaryOuterRadius = 30.0f;',
    'private const float DefaultBoundaryOuterIntensity = 0.05f;',
    'private const float DefaultBoundaryPulseAmount = 0.8f;')) {
    Assert-Contract ($pluginSource.Contains($required)) "vanilla-adjacent purple boundary defaults are missing $required."
}
Assert-Contract (!$pluginSource.Contains('"BoundaryHdrIntensity"')) "retired raw boundary HDR setting remains."
Assert-Contract ($pluginSource -match '(?s)HdrIntensity\s*=\s*ValueOrDefault\(\s*_boundaryBrightness,\s*DefaultBoundaryBrightness\)\s*\*\s*BoundaryVanillaHdrBaseline') "normalized boundary brightness is not converted through the vanilla HDR baseline."

foreach ($required in @(
    '"Apply Gameplay Preset Once"',
    '"Quiet Wyrdnight Length (Minutes)"',
    '"Maximum-Threat Wyrdnight Length (Minutes)"',
    '"Advanced - Threat Tuning"',
    '"Advanced - Hunt Pacing"',
    '"Advanced - Stalker Tuning"',
    '"Advanced - Boundary Tuning"',
    '"Advanced - Visual Layers"',
    '"Advanced - Diagnostics"')) {
    Assert-Contract ($pluginSource.Contains($required)) "config UX contract is missing $required."
}
Assert-Contract ($pluginSource -match '(?s)"Enable Ambient Stalkers",\s*0,\s*30') "ambient-stalker toggle is not in the primary General flow."
Assert-Contract ($pluginSource -match '(?s)"Allow Elite Enemies",\s*0,\s*40') "elite toggle is not in the primary General flow."
Assert-Contract ($pluginSource -match '(?s)"Show Wyrdnight Rest Availability",\s*0,\s*45') "Wyrdnight rest availability is not ordered in the primary General flow."
Assert-Contract ($pluginSource -match '(?s)"Allow Unprotected Wyrdnight Rest",\s*0,\s*50') "rest toggle is not ordered in the primary General flow."

foreach ($required in @(
    'GftNotificationPreset.Minimal',
    'GftNotificationPreset.Atmospheric',
    'AtmosphereEventKind.HuntCommitted',
    'AtmosphereEventKind.HunterKilled',
    'AtmosphereEventKind.HunterEscaped',
    '_lastIndices')) {
    Assert-Contract ($atmosphereSource.Contains($required)) "atmospheric routing is missing $required."
}
foreach ($required in @(
    '"System"',
    '"Low"',
    '"eyes-in-the-dark-diagnostics"',
    '"Immediate"')) {
    Assert-Contract (($gftBridgeSource + $pluginSource).Contains($required)) "diagnostic GFT convention is missing $required."
}
foreach ($required in @(
    'selection.FilterSummary',
    'selection.WeightSummary',
    'plan.DescribeComposition()',
    'confirmedPlan.DangerCost',
    '_pacing.RemainingBudget',
    'HuntResolution.HunterKilled',
    'HuntResolution.Escaped')) {
    Assert-Contract ($pluginSource.Contains($required)) "diagnostics or resolution data is missing $required."
}
foreach ($required in @(
    'PatchRest();',
    'AccessTools.PropertyGetter(',
    'nameof(HeroDevelopment.CanRest)',
    'AfterCanRest',
    'CanUseNativeRest',
    'ApplyRestInterruptionRisk',
    'ShouldSuppressNativeWyrdnightSurprise',
    'ShowWyrdnightRestAvailability',
    'RestInterruptionChanceAtZeroThreat',
    'RestInterruptionChanceAtMaximumThreat',
    '"AllowUnprotectedWyrdnightRest"',
    'wyrdnessService.IsInRepeller(hero.Coords)',
    'restPopup.IsSafelyResting',
    'NightStateEvaluator.CanBeginRest(',
    '_restAtmosphereReconciliationPending',
    'slept-through transitions suppressed')) {
    Assert-Contract ($pluginSource.Contains($required)) "safe rest or post-sleep GFT reconciliation is missing $required."
}
foreach ($retired in @(
    'NotifyRestBlockedOnce',
    '_restBlockNoticeShown',
    'FancyPanelType.Custom.Spawn',
    'You can rest during a Wyrdnight only within a protective boundary.')) {
    Assert-Contract (!$pluginSource.Contains($retired)) "retired blocked-rest warning behavior remains: $retired."
}
foreach ($required in @(
    'DefaultWyrdVisualTransitionSeconds = 60.0f;',
    'LoadThreatVisualTransitionSeconds = 10.0f;',
    'DefaultDiagnosticGftCooldownSeconds = 1.0f;',
    '"WyrdVisualTransitionSeconds"',
    'beginNaturalTransition',
    'canContinueTransition',
    'WorldTimescalePolicy.RemainingNightRealSeconds(',
    'WorldTimescalePolicy.RemainingDaylightRealSeconds(',
    'WorldTimescalePolicy.ElapsedNightRealSeconds(',
    'WyrdVisualMath.PreDawnBlendLimit(',
    'WyrdVisualMath.CenteredDuskBlend(',
    'phaseBlendLimit')) {
    Assert-Contract ($pluginSource.Contains($required)) "natural Wyrd visual transition is missing $required."
}
foreach ($required in @(
    'WyrdVisualMath.AdvanceBlend(',
    '_visualBlend',
    'TransitionSeconds',
    'WyrdnightBrightness',
    'exposure.compensation.value * multiplier + compensation;',
    'exposure.fixedExposure.value * multiplier - compensation;',
    'return brightness * 1.75f;',
    '2f) * 0.35f;')) {
    Assert-Contract ($wyrdVisualSource.Contains($required)) "visual runtime transition is missing $required."
}
Assert-Contract ($pluginSource.Contains('DefaultWyrdnightBrightness = 1.0f;')) "Wyrdnight brightness default is missing."
Assert-Contract ($pluginSource.Contains('"WyrdnightBrightness"')) "Wyrdnight brightness is not configurable."
Assert-Contract (!$wyrdVisualSource.Contains('HandleIndirectLighting')) "Eyes still patches native indirect lighting."
Assert-Contract (!$wyrdVisualSource.Contains('indirectDiffuseLightingMultiplier')) "Eyes still writes native indirect diffuse lighting."
Assert-Contract (!$wyrdVisualSource.Contains('postExposure')) "Eyes still modifies HDRP post-exposure."
foreach ($required in @(
    'ClosePursuitAggressionDistance = 8f',
    'AmbientStalkerEscalationCause.ClosePursuit')) {
    Assert-Contract (($ambientDirectorSource + $ambientRuntimeSource).Contains($required)) "close-pursuit escalation is missing $required."
}

foreach ($required in @(
    'ICharacter.Events.OnFiredProjectile',
    'ICharacter.Events.CastingEnded',
    'QueueRangedActionThreat(',
    'sourceWeapon.IsMagic',
    '"ranged-action:" + ModelId(item)')) {
    Assert-Contract ($pluginSource.Contains($required)) "ranged or spell-use threat is missing $required."
}

foreach ($required in @(
    'GameplayTuningPreset.UneasyNight',
    'GameplayTuningPreset.WatchfulNight',
    'GameplayTuningPreset.CursedNight',
    '_gameplayPreset.Value = GameplayTuningPreset.Custom;',
    '"2. Gameplay Preset"',
    '"ApplyPreset"')) {
    Assert-Contract ($pluginSource.Contains($required)) "one-shot gameplay presets are missing $required."
}

Assert-Contract ($gloriousSource.Contains('"EyesInTheDark.EyesInTheDarkHudApi"')) "Glorious does not use the Eyes HUD contract."
Assert-Contract (!$gloriousSource.Contains('ResolveWyrdHuntIntegration();')) "Glorious still activates its retired Wyrd Hunt meter path."
Assert-Contract ($gftSource.Contains('"Wyrd Hunt is flagged as incompatible with Eyes in the Dark."')) "GFT exact incompatibility notice is missing."
Assert-Contract ($gftSource.Contains('"DeathWrench.TimeMod"')) "GFT Custom Timescale GUID detection is missing."
Assert-Contract ($gftSource.Contains('"TimeMod"')) "GFT Custom Timescale assembly fallback is missing."
Assert-Contract ($gftSource.Contains('"Custom Timescale is flagged as incompatible with Eyes in the Dark."')) "GFT Custom Timescale notice text is missing."
Assert-Contract ($gftSource.Contains('"OnMainMenu"')) "GFT incompatibility notice is not main-menu scoped."
Assert-Contract ($gftBridgeSource.Contains('WyrdnessPalette.NativeOrange')) "Eyes GFT messages do not follow the Wyrdness palette."
Assert-Contract ($gftSource.Contains('ResolveEyesWyrdStyle()')) "GFT vanilla Wyrd messages do not follow the Eyes palette."

Assert-Contract ($nexusShort.Trim().Length -le 350) "Nexus short description exceeds 350 characters."
Assert-Contract ($nexusFile.Trim().Length -lt $nexusShort.Trim().Length) "Nexus file description is not shorter than the short description."
Assert-Contract ($nexusFull.Contains('mods/201]Wyrd Hunt[/url] is incompatible with Eyes in the Dark')) "Nexus compatibility copy is incomplete."
Assert-Contract ($nexusFull.Contains('mods/76]Custom Timescale[/url] is incompatible with Eyes in the Dark')) "Nexus Custom Timescale incompatibility copy is incomplete."
Assert-Contract ($nexusFull.Contains('DayMinutes 60')) "Nexus 60-minute day target is missing."
Assert-Contract ($nexusFull.Contains('BaseNightMinutes 6')) "Nexus zero-threat night target is missing."
Assert-Contract ($nexusFull.Contains('MaximumThreatNightMinutes 12')) "Nexus maximum-threat night target is missing."
Assert-Contract ($nexusFull.Contains('inspired by [url=https://www.nexusmods.com/taintedgrailthefallofavalon/mods/201]Wyrd Hunt[/url]')) "Nexus copy omits the inspiration statement."
Assert-Contract ($nexusFull.Contains('Diagnostics is enabled')) "Nexus copy omits diagnostic GFT behavior."
Assert-Contract ($roadmap.Contains('after the implementation is complete')) "roadmap does not defer consolidated in-game testing until implementation completion."

if (![string]::IsNullOrWhiteSpace($PackagePath)) {
    Assert-Contract (Test-Path -LiteralPath $PackagePath -PathType Leaf) "package path does not exist."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entries = @($archive.Entries | ForEach-Object {
            $_.FullName.Replace('\', '/')
        })
        $expected = @(
            'EyesInTheDark/CHANGELOG.txt',
            'EyesInTheDark/EyesInTheDark.dll',
            'EyesInTheDark/README.txt'
        )
        Assert-Contract ($entries.Count -eq $expected.Count) "package contains unexpected files."
        foreach ($entry in $expected) {
            Assert-Contract ($entries -contains $entry) "package is missing $entry."
        }
    } finally {
        $archive.Dispose()
    }
}

Write-Host "Eyes in the Dark 1.3.1 acceptance contracts passed."
