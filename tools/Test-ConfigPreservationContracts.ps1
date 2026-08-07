[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Config preservation contract failed: $Message"
    }
}

$sourceContracts = @(
    [pscustomobject]@{
        Name = 'Glorious UI'
        Source = 'mods\GloriousUI\src\GloriousUI.cs'
        Capture = 'CapturePreservedUserSettings('
        Restore = 'RestorePreservedUserSettings();'
        Save = 'Config.Save();'
        Tokens = @('QuickSlotHudOffsetX', 'WyrdSkillIndicatorScale', 'HealthPotionHotkey', 'ManaPotionHotkey')
    },
    [pscustomobject]@{
        Name = 'Grail Floating Text'
        Source = 'mods\GrailFloatingText\src\GrailFloatingText.cs'
        Capture = 'CapturePreservedPresentation('
        Restore = 'RestorePreservedPresentation();'
        Save = 'Config.Save();'
        Tokens = @('Source ', 'Scale', 'CenterX', 'BaseCenterY')
    },
    [pscustomobject]@{
        Name = 'Dishonored Dynamic Crosshair'
        Source = 'mods\DishonoredDynamicCrosshair\src\Plugin.cs'
        Capture = 'CapturePreservedVisualProfile('
        Restore = 'RestorePreservedVisualProfile();'
        Save = 'Config.Save();'
        Tokens = @('GeneralSprite', 'BloodMagicScale', 'IdleOpacity', 'CrouchIndicatorVerticalOffset')
    },
    [pscustomobject]@{
        Name = 'Ultrawide Fixes'
        Source = 'mods\UltrawideFixes\src\UltrawideFixes.cs'
        Capture = 'CapturePreservedDisplayCalibration('
        Restore = 'RestorePreservedDisplayCalibration();'
        Save = 'Config.Save();'
        Tokens = @('TargetAspect', 'CropVideoUv', 'VerticalCropFocus')
    },
    [pscustomobject]@{
        Name = 'Battlecry Voice Tuner'
        Source = 'mods\BattlecryVoiceTuner\src\BattlecryVoiceTuner.cs'
        Capture = 'CapturePreservedVoiceTuning('
        Restore = 'RestorePreservedVoiceTuning();'
        Save = 'Config.Save();'
        Tokens = @('PitchSemitones', 'RandomPitchSemitones', 'VolumeMultiplier')
    },
    [pscustomobject]@{
        Name = 'First Person Arms Adjuster'
        Source = 'mods\FirstPersonArmsAdjuster\src\FirstPersonArmsAdjuster.cs'
        Capture = 'CapturePreservedSettings('
        Restore = 'RestorePreservedSettings();'
        Save = 'Config.Save();'
        Tokens = @('ForwardOffset', 'HorizontalOffset', 'VerticalOffset')
    },
    [pscustomobject]@{
        Name = 'Blood Magic Expansion'
        Source = 'mods\BloodMagicExpansion\src\BloodMagicExpansion.cs'
        Capture = 'CapturePreservedConfigValues('
        Restore = 'RestorePreservedConfigValues();'
        Save = 'config.Save();'
        Tokens = @('BloodWhitelistTerms', 'BloodSpellTemplateGuid', 'CorpseLeechSoundVolume', 'BloodTransfusionIntensityMultiplier', 'LifeTransfusionIntensityMultiplier', 'AbhartachCallingIntensityMultiplier')
    },
    [pscustomobject]@{
        Name = 'Killing Blow Mastery'
        Source = 'mods\KillingBlowMastery\src\KillingBlowMastery.cs'
        Capture = 'CapturePreservedConfigValues('
        Restore = 'RestorePreservedConfigValues();'
        Save = 'Config.Save();'
        Tokens = @('FinisherSoundRangeVolume', 'NotificationTextFormat', 'StatisticsCharacterKeyOverride')
    },
    [pscustomobject]@{
        Name = 'Enemy Respawn Control'
        Source = 'mods\EnemyRespawnControl\src\EnemyRespawnControl.cs'
        Capture = 'CapturePreservedSpawnerOverrides('
        Restore = 'RestorePreservedSpawnerOverrides();'
        Save = 'Config.Save();'
        Tokens = @('AdditionalControlledSpawnerTerms', 'IgnoredSpawnerTerms')
    },
    [pscustomobject]@{
        Name = 'Eyes in the Dark'
        Source = 'mods\EyesInTheDark\src\EyesInTheDark.cs'
        Capture = 'CapturePreservedConfigValues('
        Restore = 'RestorePreservedConfigValues();'
        Save = 'Config.Save();'
        Tokens = @('PassiveThreatPerNight', 'CombatResponseSeconds', 'ProtectedDecayPerMinute', 'PurpleThreatMeterColor', 'OrangeThreatMeterColor', 'PurpleThreatMeterRedColor', 'OrangeThreatMeterRedColor', 'PurpleThreatMeterBrightness', 'OrangeThreatMeterBrightness', 'ShowExactThreatValue', 'MeterOffsetX', 'BoundaryRenderMode', 'BoundaryBrightness', 'NearRingRadius', 'OuterRingIntensityMultiplier', 'BoundaryPulseAmount', 'WyrdnightBrightness')
    },
    [pscustomobject]@{
        Name = 'KS Better Movement Addon'
        Source = 'mods\KSAddons\KSBetterMovementAddon\src\BetterMovementAddon.cs'
        Capture = 'CapturePreservedSettings('
        Restore = 'RestorePreservedSettings();'
        Save = 'Config.Save();'
        Tokens = @('Volume', 'MinimumSpeedVolumeScale', 'SurfaceCheckIntervalSeconds')
    },
    [pscustomobject]@{
        Name = 'TG All Lights Cast Shadows Addon'
        Source = 'mods\KSAddons\KSTGAllLightsCastShadowsAddon\src\TGAllLightsCastShadowsAddon.cs'
        Capture = 'CapturePreservedAdditionalExcludedLightPathFragments('
        Restore = 'RestorePreservedAdditionalExcludedLightPathFragments();'
        Save = 'Config.Save();'
        Tokens = @('BuiltInExcludedLightPathFragments', 'AdditionalExcludedLightPathFragments')
    },
    [pscustomobject]@{
        Name = 'Wyrdsoul Reserve'
        Source = 'mods\WyrdsoulReserve\src\WyrdsoulReserve.cs'
        Capture = 'CapturePreservedConfigValues('
        Restore = 'RestorePreservedConfigValues();'
        Save = 'Config.Save();'
        Tokens = @('ActivationCostPercent', 'PassiveFullRechargeMinutes', 'ReserveGainEfficiencyPercent', 'ReserveOffsetX')
    }
)

foreach ($contract in $sourceContracts) {
    $sourcePath = Join-Path $RepositoryRoot $contract.Source
    Assert-Contract (Test-Path -LiteralPath $sourcePath -PathType Leaf) "$($contract.Name) source is missing."
    $content = Get-Content -LiteralPath $sourcePath -Raw
    $schemaMatchIndex = $content.IndexOf('storedSchemaVersion ==', [StringComparison]::Ordinal)
    $captureIndex = $content.IndexOf($contract.Capture, [StringComparison]::Ordinal)
    $copyIndex = $content.IndexOf('File.Copy(configPath, backupPath', [StringComparison]::Ordinal)
    $restoreIndex = $content.IndexOf($contract.Restore, [StringComparison]::Ordinal)
    $saveIndex = $content.IndexOf($contract.Save, [StringComparison]::Ordinal)

    Assert-Contract ($captureIndex -ge 0) "$($contract.Name) does not call its preservation capture."
    Assert-Contract ($schemaMatchIndex -ge 0 -and $captureIndex -gt $schemaMatchIndex) "$($contract.Name) must capture only after confirming a schema mismatch."
    Assert-Contract ($copyIndex -gt $captureIndex) "$($contract.Name) must capture before backing up and clearing the stale config."
    Assert-Contract ($restoreIndex -ge 0) "$($contract.Name) does not call its preservation restore."
    Assert-Contract ($saveIndex -ge 0) "$($contract.Name) does not save restored values."
    Assert-Contract ($content.Contains('ReadCustomizationProfile(')) "$($contract.Name) does not use the shared customization profile."
    Assert-Contract ($content.Contains('profile.TryGetCustomizedValue(')) "$($contract.Name) does not use shared typed-value recovery."
    Assert-Contract ($content.Contains('ConfigPreviousSettingsRecovery.TryRestore(')) "$($contract.Name) does not use shared current-range clamping."
    foreach ($token in $contract.Tokens) {
        Assert-Contract ($content.Contains($token)) "$($contract.Name) is missing contract token '$token'."
    }
}

$allLightsSource = Get-Content -LiteralPath (
    Join-Path $RepositoryRoot 'mods\KSAddons\KSTGAllLightsCastShadowsAddon\src\TGAllLightsCastShadowsAddon.cs'
) -Raw
Assert-Contract (
    -not $allLightsSource.Contains('"ExcludedLightPathFragments"')
) 'All Lights must not migrate or bind the removed combined ExcludedLightPathFragments key.'

Write-Host "Config preservation contracts passed: $($sourceContracts.Count) source lifecycles using shared typed recovery and clamping."
