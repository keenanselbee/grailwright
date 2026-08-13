[CmdletBinding()]
param(
    [string]$ModRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $ModRoot 'src\TGGlobalIlluminationAddon.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $ModRoot '..\..\..'))
$notifierPath = Join-Path $repositoryRoot 'tools\shared\GrailFloatingTextLoadErrorNotifier.cs'
$notifier = Get-Content -LiteralPath $notifierPath -Raw

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Adaptive GI contract failed: $Message"
    }
}

Assert-Contract ($source.Contains('ContextMaximumTier')) 'Context-specific maximum tiers are missing.'
Assert-Contract ($source.Contains('EnvironmentKind.Interior')) 'Interior detection is missing.'
Assert-Contract ($source.Contains('EnvironmentKind.Exterior')) 'Exterior detection is missing.'
Assert-Contract ($source.Contains('SampleWindowSeconds')) 'Smoothed FPS configuration is missing.'
Assert-Contract ($source.Contains('DowngradeHoldSeconds')) 'Sustained downgrade timing is missing.'
Assert-Contract ($source.Contains('UpgradeHoldSeconds')) 'Sustained upgrade timing is missing.'
Assert-Contract ($source.Contains('ChangeCooldownSeconds')) 'Quality-change cooldown is missing.'
Assert-Contract (
    $source -match '(?s)_startAtPerformance = Config\.Bind\(\s*"2\. Adaptive Presets",\s*"StartAtPerformance",\s*true,'
) 'Default-on Performance-first configuration is missing.'
Assert-Contract (
    $source -match '(?s)QualityTier initial = _startAtPerformance\.Value\s*\?\s*QualityTier\.Performance\s*:\s*maximum;'
) 'Performance-first startup selection is missing.'
Assert-Contract ($source.Contains('TryEnableSsgi = false')) 'Performance SSGI disable is missing.'
Assert-Contract ($source.Contains('Math.Min(4, SampleCount)')) 'Balanced sample cap is missing.'
Assert-Contract ($source.Contains('Math.Min(1, BounceCount)')) 'Balanced bounce cap is missing.'
Assert-Contract (-not $source.Contains('TGAllLightsCastShadows')) 'Shadow-mod behavior must remain out of this addon.'
Assert-Contract ($source.Contains('AfterGlobalIlluminationUpdate')) 'Parent GI toggle observation is missing.'
Assert-Contract ($source.Contains('"gi-toggle"')) 'GI toggle notification collapse key is missing.'
Assert-Contract ($source.Contains('ShowAdaptiveTierNotification')) 'Adaptive tier notification handling is missing.'
Assert-Contract ($source.Contains('!_diagnostics.Value')) 'Adaptive notifications must be gated by Diagnostics.'
Assert-Contract ($source.Contains('"ShowGrailFloatingTextDiagnostics"')) 'The standardized GFT diagnostic setting is missing.'
Assert-Contract ($source.Contains('!_showGrailFloatingTextDiagnostics.Value')) 'Adaptive notifications must be gated by ShowGrailFloatingTextDiagnostics.'
Assert-Contract ($source.Contains('TryShowSystemNotification')) 'Grail Floating Text System notification integration is missing.'
Assert-Contract ($notifier.Contains('internal static bool TryShowSystemNotification')) 'The shared System notification bridge is missing.'
Assert-Contract ($notifier.Contains('"system"')) 'System notifications must request the system icon.'
Assert-Contract ($notifier.Contains('"System"')) 'System notifications must use the System duration bucket.'

Write-Host 'Adaptive GI contracts passed.'
