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
$worldTimescaleSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\WorldTimescale.cs") -Raw
$meterSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatMeter.cs") -Raw
$boundarySource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\BoundaryController.cs") -Raw
$layeredBoundarySource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\LayeredBoundaryPass.cs") -Raw
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

Assert-Contract ($modJson.version -eq "0.9.1") "mod.json version is not 0.9.1."
Assert-Contract ($modJson.displayName -eq "Eyes in the Dark - Wyrdnight Encounters") "mod.json display name is stale."
Assert-Contract ($pluginSource.Contains('public const string PluginName = "Eyes in the Dark";')) "config/plugin title is not Eyes in the Dark."
Assert-Contract ($pluginSource.Contains('public const string PluginVersion = "0.9.1";')) "plugin version is not 0.9.1."
Assert-Contract ($pluginSource.Contains('[assembly: AssemblyVersion("0.9.1.0")]')) "assembly version is not 0.9.1.0."
Assert-Contract ($pluginSource.Contains('[assembly: AssemblyTitle("Eyes in the Dark - Wyrdnight Encounters")]')) "assembly title is stale."
Assert-Contract ($pluginSource.Contains('private const int ConfigSchemaVersion = 6;')) "config schema is not 6."
Assert-Contract ($readme.Contains('Version: 0.9.1')) "installed README version is stale."
Assert-Contract ($readme.StartsWith('Eyes in the Dark - Wyrdnight Encounters')) "installed README title is stale."
Assert-Contract ($changelog.StartsWith('Version 0.9.1')) "changelog does not start with 0.9.1."
Assert-Contract ($repoReadme.Contains('| [Eyes in the Dark - Wyrdnight Encounters](mods/EyesInTheDark) | 0.9.1 |')) "top-level README row is stale."
Assert-Contract ($gftModJson.version -eq "1.9.8") "GFT mod.json version is not 1.9.8."
Assert-Contract ($gftSource.Contains('private const int ConfigSchemaVersion = 24;')) "GFT config schema is not 24."
Assert-Contract (!(Test-Path -LiteralPath (Join-Path $repoRoot 'mods\PurpleWyrdness'))) "retired Purple Wyrdness package remains in the repository."
Assert-Contract (!$repoReadme.Contains('(mods/PurpleWyrdness)')) "top-level README still lists Purple Wyrdness."

$sourcePaths = @($modJson.sourceFiles)
foreach ($requiredSource in @(
    'src/EyesInTheDark.cs',
    'src/NightState.cs',
    'src/ThreatState.cs',
    'src/ThreatMeter.cs',
    'src/PacingState.cs',
    'src/WorldTimescale.cs',
    'src/Atmosphere.cs',
    'src/GrailFloatingTextBridge.cs',
    'src/BoundaryController.cs',
    'src/LayeredBoundaryPass.cs',
    'src/HuntDirector.cs',
    'src/HunterCatalog.cs',
    'src/FirstHunterRuntime.cs')) {
    Assert-Contract ($sourcePaths -contains $requiredSource) "mod.json omits $requiredSource."
}

$allEyesText = $pluginSource + $catalogSource + $runtimeSource + $worldTimescaleSource + $meterSource `
    + $boundarySource + $atmosphereSource + $gftBridgeSource + $readme `
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
    '"DayTimescale"',
    '"NightTimescale"',
    '"AllowEliteEnemies"',
    '_allowEliteEnemies.Value = false;',
    '_allowEliteEnemies.Value = true;',
    'DefaultDayTimescale = 0.23f',
    'DefaultNightTimescale = 0.413f',
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
    '"ThreatMeterColor"')) {
    Assert-Contract ($pluginSource.Contains($required)) "environment-impact threat or meter color config is missing $required."
}
foreach ($required in @(
    'public const string DefaultColorText = "#B878FF";',
    'ColorUtility.TryParseHtmlString(',
    'ApplyColor(colorText);')) {
    Assert-Contract ($meterSource.Contains($required)) "configurable meter color is missing $required."
}
Assert-Contract (!$boundarySource.Contains('_maskIntensityField.SetValue')) "boundary controller modifies native mask intensity."
Assert-Contract ($layeredBoundarySource.Contains('material.SetFloat(MaskIntensityId, _maskIntensity);')) "layered boundary does not preserve the native visual mask value."
Assert-Contract ($boundarySource.Contains('Mathf.Clamp(_settings.PulseAmount, 0f, 1f)')) "runtime pulse amount is not bounded to 0-1."
Assert-Contract ($boundarySource.Contains('_settings.OuterIntensityMultiplier')) "outer ring brightness is not applied independently."
Assert-Contract ($pluginSource.Contains('new AcceptableValueRange<float>(0f, 1f)')) "config pulse amount is not exposed across 0-1."
foreach ($required in @(
    'private const string DefaultBoundaryColor = "#B878FF";',
    'private const float DefaultBoundaryHdrIntensity = 271.529f;',
    'private const float DefaultBoundaryNearRadius = 12.0f;',
    'private const float DefaultBoundaryMiddleRadius = 22.0f;',
    'private const float DefaultBoundaryOuterRadius = 32.0f;',
    'private const float DefaultBoundaryOuterIntensity = 1.0f;',
    'BoundaryThreatReactivity.Subtle')) {
    Assert-Contract ($pluginSource.Contains($required)) "vanilla-adjacent purple boundary defaults are missing $required."
}

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

Assert-Contract ($nexusShort.Trim().Length -le 350) "Nexus short description exceeds 350 characters."
Assert-Contract ($nexusFile.Trim().Length -lt $nexusShort.Trim().Length) "Nexus file description is not shorter than the short description."
Assert-Contract ($nexusFull.Contains('mods/201]Wyrd Hunt[/url] is incompatible with Eyes in the Dark')) "Nexus compatibility copy is incomplete."
Assert-Contract ($nexusFull.Contains('mods/76]Custom Timescale[/url] is incompatible with Eyes in the Dark')) "Nexus Custom Timescale incompatibility copy is incomplete."
Assert-Contract ($nexusFull.Contains('DayTimescale 0.23')) "Nexus 60-minute day target is missing."
Assert-Contract ($nexusFull.Contains('NightTimescale 0.413')) "Nexus 15-minute night target is missing."
Assert-Contract ($nexusFull.Contains('inspired by [url=https://www.nexusmods.com/taintedgrailthefallofavalon/mods/201]Wyrd Hunt[/url]')) "Nexus copy omits the inspiration statement."
Assert-Contract ($nexusFull.Contains('Diagnostics is enabled')) "Nexus copy omits diagnostic GFT behavior."
Assert-Contract ($roadmap.Contains('Begin this pass only after the `0.9.0` implementation')) "roadmap does not defer consolidated in-game testing until implementation completion."

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

Write-Host "Eyes in the Dark 0.9.1 beta acceptance contracts passed."
