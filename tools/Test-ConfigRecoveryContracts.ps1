[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$GameRoot = $(if ($env:TAINTED_GRAIL_FOA_DIR) {
        $env:TAINTED_GRAIL_FOA_DIR
    } else {
        'G:\Steam\steamapps\common\Tainted Grail FoA'
    })
)

$ErrorActionPreference = 'Stop'
$scratchRoot = Join-Path $RepositoryRoot '.codex-temp\config-recovery-contracts'
$helperPath = Join-Path $RepositoryRoot 'tools\shared\ConfigPreviousSettingsRecovery.cs'
$bepInExPath = Join-Path $GameRoot 'BepInEx\core\BepInEx.dll'

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Config recovery contract failed: $Message"
    }
}

function Get-ConfigOwningModPaths {
    param(
        [string]$ModsRoot
    )

    $modsRootFull = [IO.Path]::GetFullPath($ModsRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
    return @(
        Get-ChildItem -LiteralPath $modsRootFull -Filter 'mod.json' -File -Recurse |
            ForEach-Object {
                $manifestPath = $_.FullName
                $modRoot = $_.Directory.FullName
                $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                $modRootPrefix = $modRoot.TrimEnd(
                    [IO.Path]::DirectorySeparatorChar,
                    [IO.Path]::AltDirectorySeparatorChar
                ) + [IO.Path]::DirectorySeparatorChar
                $ownsConfig = $false
                foreach ($sourceFile in @($manifest.sourceFiles)) {
                    $sourcePath = [IO.Path]::GetFullPath((Join-Path $modRoot $sourceFile))
                    if (-not $sourcePath.StartsWith(
                        $modRootPrefix,
                        [StringComparison]::OrdinalIgnoreCase
                    ) -or [IO.Path]::GetExtension($sourcePath) -ne '.cs') {
                        continue
                    }

                    $source = Get-Content -LiteralPath $sourcePath -Raw
                    if ($source -match '\b[A-Za-z_][A-Za-z0-9_]*\.Bind\s*\(') {
                        $ownsConfig = $true
                        break
                    }
                }

                if ($ownsConfig) {
                    $modRoot.Substring($modsRootFull.Length + 1)
                }
            } |
            Sort-Object -Unique
    )
}

function Get-SourceCall {
    param(
        [string]$Source,
        [string]$Token
    )

    $start = $Source.IndexOf($Token, [StringComparison]::Ordinal)
    if ($start -lt 0) {
        return $null
    }

    $end = $Source.IndexOf(');', $start, [StringComparison]::Ordinal)
    if ($end -lt 0) {
        return $null
    }

    return $Source.Substring($start, $end + 2 - $start)
}

function Get-ArrayInitializer {
    param(
        [string]$Source,
        [string]$Name
    )

    $start = $Source.IndexOf($Name, [StringComparison]::Ordinal)
    if ($start -lt 0) {
        return $null
    }

    $end = $Source.IndexOf(';', $start)
    if ($end -lt 0) {
        return $null
    }

    return $Source.Substring($start, $end + 1 - $start)
}

$modContracts = @(
    @{ Mod = 'GloriousUI'; Source = 'src\GloriousUI.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'BloodMagicExpansion'; Source = 'src\BloodMagicExpansion.cs'; Schema = 10; AutoPreserves = $true },
    @{ Mod = 'DeedsOfAvalon'; Source = 'src\DeedsOfAvalon.cs'; Schema = 1; AutoPreserves = $false },
    @{ Mod = 'DishonoredDynamicCrosshair'; Source = 'src\Plugin.cs'; Schema = 3; AutoPreserves = $true },
    @{ Mod = 'EnemyRespawnControl'; Source = 'src\EnemyRespawnControl.cs'; Schema = 4; AutoPreserves = $true },
    @{ Mod = 'EyesInTheDark'; Source = 'src\EyesInTheDark.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'FirstPersonArmsAdjuster'; Source = 'src\FirstPersonArmsAdjuster.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'FullEnemyXP'; Source = 'src\FullEnemyXP.cs'; Schema = 1; AutoPreserves = $false },
    @{ Mod = 'GrailFloatingText'; Source = 'src\GrailFloatingText.cs'; Schema = 15; AutoPreserves = $true },
    @{ Mod = 'KillingBlowMastery'; Source = 'src\KillingBlowMastery.cs'; Schema = 13; AutoPreserves = $true },
    @{ Mod = 'KingsElegyMainMenuMusic'; Source = 'src\MainMenuMusic.cs'; Schema = 16; AutoPreserves = $false },
    @{ Mod = 'KSAddons\KSBetterMovementAddon'; Source = 'src\BetterMovementAddon.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'KSAddons\KSPersistentCorpsesAddon'; Source = 'src\PersistentCorpsesAddon.cs'; Schema = 1; AutoPreserves = $false },
    @{ Mod = 'KSAddons\KSTGAllLightsCastShadowsAddon'; Source = 'src\TGAllLightsCastShadowsAddon.cs'; Schema = 2; AutoPreserves = $true },
    @{ Mod = 'KSAddons\KSWyrdSightAddon'; Source = 'src\WyrdSightAddon.cs'; Schema = 2; AutoPreserves = $false },
    @{ Mod = 'BattlecryVoiceTuner'; Source = 'src\BattlecryVoiceTuner.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'SteelAndBone'; Source = 'src\SteelAndBone.cs'; Schema = 14; AutoPreserves = $true },
    @{ Mod = 'TorchlightRekindled'; Source = 'src\TorchlightRekindled.cs'; Schema = 1; AutoPreserves = $false },
    @{ Mod = 'UltrawideFixes'; Source = 'src\UltrawideFixes.cs'; Schema = 1; AutoPreserves = $true },
    @{ Mod = 'WyrdsoulReserve'; Source = 'src\WyrdsoulReserve.cs'; Schema = 1; AutoPreserves = $true }
)

$expectedPermanentExclusions = @{
    'GloriousUI' = @(
        @{ Section = 'Diagnostics'; Key = 'BuffDebuffLayoutTestMode' }
    )
    'EyesInTheDark' = @(
        @{ Section = '2. Gameplay Preset'; Key = 'ApplyPreset' },
        @{ Section = '10. Diagnostics'; Key = 'EnableThreatOverride' },
        @{ Section = '10. Diagnostics'; Key = 'ThreatOverrideValue' },
        @{ Section = '10. Diagnostics'; Key = 'EnableTimescaleOverride' },
        @{ Section = '10. Diagnostics'; Key = 'TimescaleOverrideMultiplier' }
    )
    'BattlecryVoiceTuner' = @(
        @{ Section = '4. Testing'; Key = 'PlayRandomTestSound' }
    )
}

$harnessSource = @'
namespace Grailwright.Shared
{
    public static class ConfigRecoveryContractHarness
    {
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.InvalidOperationException(message);
            }
        }

        private static string Entry(
            string type,
            string defaultValue,
            string key,
            string value)
        {
            return "# Setting type: " + type + System.Environment.NewLine
                + "# Default value: " + defaultValue + System.Environment.NewLine
                + key + " = " + value + System.Environment.NewLine;
        }

        public static void Run(string scratchRoot)
        {
            if (System.IO.Directory.Exists(scratchRoot))
            {
                System.IO.Directory.Delete(scratchRoot, true);
            }
            System.IO.Directory.CreateDirectory(scratchRoot);

            string configPath = System.IO.Path.Combine(scratchRoot, "recovery.cfg");
            BepInEx.Configuration.ConfigFile config =
                new BepInEx.Configuration.ConfigFile(configPath, true);
            config.Bind("1. Core", "ConfigSchemaVersion", 2, "");
            BepInEx.Configuration.ConfigEntry<int> customized =
                config.Bind(
                    "2. Values",
                    "Customized",
                    2,
                    new BepInEx.Configuration.ConfigDescription(
                        "",
                        new BepInEx.Configuration.AcceptableValueRange<int>(0, 5)));
            BepInEx.Configuration.ConfigEntry<int> untouchedOldDefault =
                config.Bind("2. Values", "UntouchedOldDefault", 9, "");
            BepInEx.Configuration.ConfigEntry<float> numericEquivalent =
                config.Bind("2. Values", "NumericEquivalent", 9.0f, "");
            BepInEx.Configuration.ConfigEntry<string> text =
                config.Bind("2. Values", "Text", "current", "");
            BepInEx.Configuration.ConfigEntry<int> typeChanged =
                config.Bind("2. Values", "TypeChanged", 4, "");
            BepInEx.Configuration.ConfigEntry<int> invalid =
                config.Bind("2. Values", "Invalid", 4, "");
            BepInEx.Configuration.ConfigEntry<int> missingDefault =
                config.Bind("2. Values", "MissingDefault", 4, "");
            BepInEx.Configuration.ConfigEntry<int> blocked =
                config.Bind("2. Values", "Blocked", 11, "");
            BepInEx.Configuration.ConfigEntry<int> permanent =
                config.Bind("2. Values", "Permanent", 12, "");
            BepInEx.Configuration.ConfigEntry<bool> permanentAction =
                config.Bind("2. Values", "PermanentAction", false, "");
            int permanentActionTriggers = 0;
            permanentAction.SettingChanged += delegate
            {
                if (permanentAction.Value)
                {
                    permanentActionTriggers++;
                }
            };
            BepInEx.Configuration.ConfigEntry<int> newSetting =
                config.Bind("2. Values", "NewSetting", 13, "");
            BepInEx.Configuration.ConfigEntry<int> sharedRestore =
                config.Bind(
                    "2. Values",
                    "SharedRestore",
                    2,
                    new BepInEx.Configuration.ConfigDescription(
                        "",
                        new BepInEx.Configuration.AcceptableValueRange<int>(0, 5)));
            config.Save();

            string olderBackup = configPath + ".pre-schema-1-20260101-000000.bak";
            string newestSupportedBackup =
                configPath + ".pre-schema-1-20260102-000000.bak";
            string newestUnsupportedBackup =
                configPath + ".pre-schema-0-20260103-000000.bak";

            string older =
                "[1. Core]" + System.Environment.NewLine
                + Entry("Int32", "1", "ConfigSchemaVersion", "1")
                + System.Environment.NewLine
                + "[2. Values]" + System.Environment.NewLine
                + Entry("Int32", "1", "Customized", "3");
            System.IO.File.WriteAllText(olderBackup, older);

            string supported =
                "[1. Core]" + System.Environment.NewLine
                + Entry("Int32", "1", "ConfigSchemaVersion", "1")
                + System.Environment.NewLine
                + "[2. Values]" + System.Environment.NewLine
                + Entry("Int32", "1", "Customized", "7")
                + Entry("Int32", "1", "UntouchedOldDefault", "1")
                + Entry("Single", "1", "NumericEquivalent", "1.0")
                + Entry("String", "old", "Text", "mine")
                + Entry("String", "4", "TypeChanged", "mine")
                + Entry("Int32", "1", "Invalid", "not-an-int")
                + "# Setting type: Int32" + System.Environment.NewLine
                + "MissingDefault = 8" + System.Environment.NewLine
                + Entry("Int32", "1", "Blocked", "8")
                + Entry("Int32", "1", "Permanent", "8")
                + Entry("Boolean", "false", "PermanentAction", "true")
                + Entry("Int32", "1", "Removed", "8")
                + System.Environment.NewLine
                + "[99. Import Previous Settings]" + System.Environment.NewLine
                + Entry("Boolean", "false", "ImportPreviousSettingsNow", "true");
            System.IO.File.WriteAllText(newestSupportedBackup, supported);

            string unsupported =
                "[1. Core]" + System.Environment.NewLine
                + Entry("Int32", "0", "ConfigSchemaVersion", "0")
                + System.Environment.NewLine
                + "[2. Values]" + System.Environment.NewLine
                + Entry("Int32", "1", "Customized", "4");
            System.IO.File.WriteAllText(newestUnsupportedBackup, unsupported);
            System.IO.File.SetLastWriteTimeUtc(
                olderBackup,
                new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
            System.IO.File.SetLastWriteTimeUtc(
                newestSupportedBackup,
                new System.DateTime(2026, 1, 2, 0, 0, 0, System.DateTimeKind.Utc));
            System.IO.File.SetLastWriteTimeUtc(
                newestUnsupportedBackup,
                new System.DateTime(2026, 1, 3, 0, 0, 0, System.DateTimeKind.Utc));

            ConfigRecoveryKeepCurrentDefaultRule[] rules =
                new ConfigRecoveryKeepCurrentDefaultRule[]
                {
                    new ConfigRecoveryKeepCurrentDefaultRule(
                        2,
                        "2. Values",
                        "Blocked",
                        "Contract fixture")
                };
            BepInEx.Configuration.ConfigDefinition[] exclusions =
                new BepInEx.Configuration.ConfigDefinition[]
                {
                    new BepInEx.Configuration.ConfigDefinition(
                        "2. Values",
                        "Permanent"),
                    new BepInEx.Configuration.ConfigDefinition(
                        "2. Values",
                        "PermanentAction")
                };
            ConfigRecoveryCustomizationProfile profile =
                ConfigPreviousSettingsRecovery.ReadCustomizationProfile(
                    newestSupportedBackup,
                    1,
                    2,
                    rules,
                    exclusions);
            Assert(
                profile.ShouldRecover<int>("2. Values", "Customized"),
                "The profile rejected a customized compatible value.");
            Assert(
                !profile.ShouldRecover<float>("2. Values", "NumericEquivalent"),
                "Typed-equivalent numeric defaults were treated as customized.");
            Assert(
                !profile.ShouldRecover<int>("2. Values", "TypeChanged"),
                "The profile accepted an incompatible type.");
            Assert(
                !profile.ShouldRecover<int>("2. Values", "Invalid"),
                "The profile accepted an invalid value.");
            Assert(
                !profile.ShouldRecover<int>("2. Values", "Blocked"),
                "The profile ignored a schema transition safety rule.");
            Assert(
                !profile.ShouldRecover<bool>("2. Values", "PermanentAction"),
                "The profile ignored a permanent exclusion.");
            int capturedCustomized;
            Assert(
                profile.TryGetCustomizedValue(
                    "2. Values",
                    "Customized",
                    out capturedCustomized)
                    && capturedCustomized == 7,
                "The shared preservation API did not return the typed customized value.");
            int rejectedType;
            Assert(
                !profile.TryGetCustomizedValue(
                    "2. Values",
                    "TypeChanged",
                    out rejectedType),
                "The shared preservation API returned an incompatible value.");
            bool sharedRestoreClamped;
            Assert(
                ConfigPreviousSettingsRecovery.TryRestore(
                    sharedRestore,
                    9,
                    out sharedRestoreClamped)
                    && sharedRestore.Value == 5
                    && sharedRestoreClamped,
                "The shared restore API did not apply the current acceptable range.");
            sharedRestore.Value = 2;
            bool bound = ConfigPreviousSettingsRecovery.Bind(
                config,
                new BepInEx.Logging.ManualLogSource("ConfigRecoveryContract"),
                "contract",
                2,
                1,
                rules,
                exclusions);
            Assert(bound, "A supported backup should expose the recovery action.");

            BepInEx.Configuration.ConfigDefinition actionDefinition =
                new BepInEx.Configuration.ConfigDefinition(
                    ConfigPreviousSettingsRecovery.RecoverySection,
                    ConfigPreviousSettingsRecovery.RecoveryKey);
            BepInEx.Configuration.ConfigDefinition currentSchemaDefinition =
                new BepInEx.Configuration.ConfigDefinition(
                    ConfigPreviousSettingsRecovery.RecoverySection,
                    ConfigPreviousSettingsRecovery.CurrentSchemaKey);
            BepInEx.Configuration.ConfigDefinition backupSchemaDefinition =
                new BepInEx.Configuration.ConfigDefinition(
                    ConfigPreviousSettingsRecovery.RecoverySection,
                    ConfigPreviousSettingsRecovery.AvailableBackupSchemaKey);
            Assert(config.ContainsKey(actionDefinition), "The recovery action was not bound.");
            Assert(
                config.ContainsKey(currentSchemaDefinition)
                    && (string)config[currentSchemaDefinition].BoxedValue == "2",
                "The current recovery schema was not exposed.");
            Assert(
                config.ContainsKey(backupSchemaDefinition)
                    && (string)config[backupSchemaDefinition].BoxedValue == "1",
                "The newest compatible backup schema was not exposed.");
            BepInEx.Configuration.ConfigEntryBase action = config[actionDefinition];
            Assert(
                action.Description.Tags.Length == 1
                    && ((ConfigRecoveryUiMetadata)action.Description.Tags[0]).DisplaySection
                        == "Import Previous Settings",
                "The recovery action is not assigned to the explicit final tab.");

            action.BoxedValue = true;
            Assert((bool)action.BoxedValue == false, "The one-shot action did not reset.");
            Assert(customized.Value == 5, "Customized numeric value was not clamped and imported.");
            Assert(untouchedOldDefault.Value == 9, "An untouched old default replaced the new default.");
            Assert(numericEquivalent.Value == 9.0f, "A typed-equivalent old default replaced the new default.");
            Assert(text.Value == "mine", "A customized compatible string was not imported.");
            Assert(typeChanged.Value == 4, "A type-incompatible value was imported.");
            Assert(invalid.Value == 4, "An invalid value was imported.");
            Assert(missingDefault.Value == 4, "A value with no previous default was imported.");
            Assert(blocked.Value == 11, "A schema transition safety rule was ignored.");
            Assert(permanent.Value == 12, "A permanent exclusion was ignored.");
            Assert(!permanentAction.Value, "A permanent action value was imported.");
            Assert(permanentActionTriggers == 0, "A permanent action was triggered during import.");
            Assert(newSetting.Value == 13, "A new setting was changed.");
            Assert(
                System.IO.Directory.GetFiles(
                    scratchRoot,
                    "recovery.cfg.pre-import-*.bak").Length == 1,
                "The transactional pre-import backup was not created.");
            action.BoxedValue = true;
            Assert(
                System.IO.Directory.GetFiles(
                    scratchRoot,
                    "recovery.cfg.pre-import-*.bak").Length == 1,
                "A repeated no-op import created another transactional backup.");

            config.Reload();
            Assert(customized.Value == 5 && text.Value == "mine", "Imported values were not saved.");

            string noBackupPath =
                System.IO.Path.Combine(scratchRoot, "no-backup.cfg");
            BepInEx.Configuration.ConfigFile noBackup =
                new BepInEx.Configuration.ConfigFile(noBackupPath, true);
            noBackup.Bind("1. Core", "ConfigSchemaVersion", 2, "");
            noBackup.Save();
            Assert(
                ConfigPreviousSettingsRecovery.Bind(
                    noBackup,
                    new BepInEx.Logging.ManualLogSource("NoBackupContract"),
                    "contract",
                    2,
                    1),
                "The recovery tab was not bound without a supported backup.");
            Assert(
                noBackup.ContainsKey(actionDefinition)
                    && noBackup.ContainsKey(currentSchemaDefinition)
                    && noBackup.ContainsKey(backupSchemaDefinition),
                "The final recovery tab was incomplete without a supported backup.");
            Assert(
                (string)noBackup[currentSchemaDefinition].BoxedValue == "2"
                    && (string)noBackup[backupSchemaDefinition].BoxedValue == "None",
                "The no-backup recovery schema status was incorrect.");
            noBackup[actionDefinition].BoxedValue = true;
            Assert(
                (bool)noBackup[actionDefinition].BoxedValue == false,
                "The unavailable one-shot action did not reset.");
        }
    }
}
'@

try {
    Assert-Contract (Test-Path -LiteralPath $helperPath -PathType Leaf) 'The shared recovery helper is missing.'
    Assert-Contract (Test-Path -LiteralPath $bepInExPath -PathType Leaf) "BepInEx.dll was not found under '$GameRoot'."

    $helperSource = Get-Content -LiteralPath $helperPath -Raw
    foreach ($token in @(
        'Import Previous Settings',
        '.pre-schema-',
        '.pre-import-',
        'KeepCurrentDefaultRule',
        'AcceptableValues.Clamp',
        'UntouchedPreviousDefault',
        'TryRefreshFoAModManager'
    )) {
        Assert-Contract ($helperSource.Contains($token)) "The shared helper is missing '$token'."
    }

    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $discoveryFixtureRoot = Join-Path $scratchRoot 'discovery-mods'
    $bindOnlyModRoot = Join-Path $discoveryFixtureRoot 'BindOnly'
    New-Item -ItemType Directory -Path (
        Join-Path $bindOnlyModRoot 'src'
    ) -Force | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $bindOnlyModRoot 'mod.json'),
        '{"sourceFiles":["src/Plugin.cs"]}'
    )
    [IO.File]::WriteAllText(
        (Join-Path $bindOnlyModRoot 'src\Plugin.cs'),
        'class Plugin { void Bind() { Config.Bind("Core", "Enabled", true); } }'
    )
    $bindOnlyOwners = @(Get-ConfigOwningModPaths -ModsRoot $discoveryFixtureRoot)
    Assert-Contract (
        $bindOnlyOwners.Count -eq 1 -and
        $bindOnlyOwners[0] -eq 'BindOnly'
    ) 'Config-owner discovery missed a mod that binds config without declaring a schema.'

    $modsRoot = Join-Path $RepositoryRoot 'mods'
    $discoveredConfigOwners = @(
        Get-ConfigOwningModPaths -ModsRoot $modsRoot
    )
    $contractMods = @($modContracts.Mod | Sort-Object -Unique)
    $contractDifference = @(
        Compare-Object -ReferenceObject $discoveredConfigOwners -DifferenceObject $contractMods
    )
    Assert-Contract (
        $contractDifference.Count -eq 0
    ) ("Config-owner discovery and the contract table differ: " +
        (($contractDifference | ForEach-Object {
            "$($_.SideIndicator) $($_.InputObject)"
        }) -join ', '))

    foreach ($contract in $modContracts) {
        $modRoot = Join-Path (Join-Path $RepositoryRoot 'mods') $contract.Mod
        $manifestPath = Join-Path $modRoot 'mod.json'
        $sourcePath = Join-Path $modRoot $contract.Source
        Assert-Contract (Test-Path -LiteralPath $manifestPath -PathType Leaf) "$($contract.Mod) mod.json is missing."
        Assert-Contract (Test-Path -LiteralPath $sourcePath -PathType Leaf) "$($contract.Mod) source is missing."

        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $recoverySources = @($manifest.sourceFiles | Where-Object {
            $_ -like '*tools/shared/ConfigPreviousSettingsRecovery.cs'
        })
        Assert-Contract ($recoverySources.Count -eq 1) "$($contract.Mod) must compile exactly one shared recovery helper."
        $resolvedHelper = [IO.Path]::GetFullPath((Join-Path $modRoot $recoverySources[0]))
        Assert-Contract ($resolvedHelper -eq [IO.Path]::GetFullPath($helperPath)) "$($contract.Mod) points at the wrong recovery helper."

        $source = Get-Content -LiteralPath $sourcePath -Raw
        Assert-Contract (
            $source -match '\b(?:Current)?ConfigSchemaVersion\b'
        ) "$($contract.Mod) binds config without declaring a schema version."
        Assert-Contract ($source.Contains("ConfigRecoveryBaselineSchema = $($contract.Schema);")) "$($contract.Mod) recovery baseline changed unexpectedly."
        Assert-Contract ($source.Contains('ConfigRecoveryKeepCurrentDefaultRules')) "$($contract.Mod) has no per-schema safety-rule scaffold."
        Assert-Contract ($source.Contains('ConfigRecoveryPermanentExclusions')) "$($contract.Mod) has no permanent-exclusion scaffold."

        $expectedExclusions = @()
        if ($expectedPermanentExclusions.ContainsKey($contract.Mod)) {
            $expectedExclusions = @($expectedPermanentExclusions[$contract.Mod])
        }
        $exclusionInitializer = Get-ArrayInitializer `
            -Source $source `
            -Name 'ConfigRecoveryPermanentExclusions'
        Assert-Contract ($null -ne $exclusionInitializer) "$($contract.Mod) permanent-exclusion initializer is missing."
        $declaredExclusionCount = @(
            [regex]::Matches(
                $exclusionInitializer,
                'new\s+ConfigDefinition\s*\(')
        ).Count
        Assert-Contract (
            $declaredExclusionCount -eq $expectedExclusions.Count
        ) "$($contract.Mod) permanent exclusions differ from the contract."
        foreach ($expectedExclusion in $expectedExclusions) {
            $expectedPattern = 'new\s+ConfigDefinition\s*\(\s*"' +
                [regex]::Escape($expectedExclusion.Section) +
                '"\s*,\s*"' +
                [regex]::Escape($expectedExclusion.Key) +
                '"\s*\)'
            Assert-Contract (
                $exclusionInitializer -match $expectedPattern
            ) "$($contract.Mod) is missing permanent exclusion '$($expectedExclusion.Section) / $($expectedExclusion.Key)'."
        }

        $recoveryBindCall = Get-SourceCall `
            -Source $source `
            -Token 'ConfigPreviousSettingsRecovery.Bind('
        Assert-Contract ($null -ne $recoveryBindCall) "$($contract.Mod) does not bind recovery."
        Assert-Contract (
            $recoveryBindCall.Contains('ConfigRecoveryPermanentExclusions')
        ) "$($contract.Mod) does not pass permanent exclusions to manual recovery."

        $recoveryBindIndex = $source.IndexOf(
            'ConfigPreviousSettingsRecovery.Bind(',
            [StringComparison]::Ordinal
        )
        $recoveryBindEnd = $recoveryBindIndex + $recoveryBindCall.Length
        $afterRecovery = $source.Substring($recoveryBindEnd)
        $saveMatch = [regex]::Match(
            $afterRecovery,
            '\b[A-Za-z_][A-Za-z0-9_]*\.Save\s*\('
        )
        Assert-Contract ($saveMatch.Success) "$($contract.Mod) does not save after binding recovery."
        $beforeSave = $afterRecovery.Substring(0, $saveMatch.Index)
        Assert-Contract (
            $beforeSave -notmatch '\b[A-Za-z_][A-Za-z0-9_]*\.Bind\s*\(' -and
            $beforeSave -notmatch '\bBind[A-Za-z0-9_]*Config\s*\('
        ) "$($contract.Mod) binds normal settings after recovery."

        Assert-Contract (
            ([regex]::Matches($source, 'ConfigRecoveryPermanentExclusions')).Count -ge 2
        ) "$($contract.Mod) does not pass its permanent exclusions to recovery."
        if ($contract.AutoPreserves) {
            Assert-Contract ($source.Contains('ReadCustomizationProfile(')) "$($contract.Mod) automatic preservation does not use the shared customization profile."
            $profileCall = Get-SourceCall `
                -Source $source `
                -Token '.ReadCustomizationProfile('
            Assert-Contract (
                $null -ne $profileCall -and
                $profileCall.Contains('ConfigRecoveryPermanentExclusions')
            ) "$($contract.Mod) does not pass permanent exclusions to automatic preservation."
            Assert-Contract ($source.Contains('profile.TryGetCustomizedValue(')) "$($contract.Mod) automatic preservation bypasses shared typed-value recovery."
            Assert-Contract ($source.Contains('ConfigPreviousSettingsRecovery.TryRestore(')) "$($contract.Mod) automatic preservation bypasses shared current-range clamping."
        }
    }

    $noPlayerLightManifest = Get-Content -LiteralPath (
        Join-Path $modsRoot 'NoPlayerLight\mod.json'
    ) -Raw | ConvertFrom-Json
    Assert-Contract (
        @($noPlayerLightManifest.sourceFiles | Where-Object {
            $_ -like '*ConfigPreviousSettingsRecovery.cs'
        }).Count -eq 0
    ) 'No Player Light must remain excluded because it owns no config.'

    [void][Reflection.Assembly]::LoadFrom($bepInExPath)
    Add-Type `
        -TypeDefinition ($helperSource + [Environment]::NewLine + $harnessSource) `
        -ReferencedAssemblies $bepInExPath
    [Grailwright.Shared.ConfigRecoveryContractHarness]::Run($scratchRoot)

    Write-Host "Config recovery contracts passed: $($modContracts.Count) config-owning mods and runtime import fixture."
}
finally {
    if (Test-Path -LiteralPath $scratchRoot) {
        Remove-Item -LiteralPath $scratchRoot -Recurse -Force
    }
}
