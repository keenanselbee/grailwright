[CmdletBinding()]
param(
    [string]$ModRoot = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ModRoot)) {
    $ModRoot = Split-Path -Parent $PSScriptRoot
}
$sourcePath = Join-Path $ModRoot 'src\TGContactShadowsAddon.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw

function Assert-Contract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Stable Contact Shadows contract failed: $Message"
    }
}

Assert-Contract ($source.Contains('InteriorsOnly')) 'Interior-only configuration is missing.'
Assert-Contract ($source.Contains('!sceneService.IsOpenWorld || lifetime.InInterior')) 'Playable interior detection is missing.'
Assert-Contract ($source.Contains('LightType.Point') -and $source.Contains('LightType.Spot')) 'Point/spot filtering is missing.'
Assert-Contract (-not $source.Contains('LightType.Directional')) 'Directional lights must not enter stable selection.'
Assert-Contract ($source.Contains('MinimumLightHoldSeconds')) 'Minimum light hold time is missing.'
Assert-Contract ($source.Contains('SwitchAdvantagePercent')) 'Challenger hysteresis is missing.'
Assert-Contract ($source.Contains('MaximumContactShadowLights')) 'Bounded multi-light configuration is missing.'
Assert-Contract ($source.Contains('Dictionary<int, SelectedLight>')) 'Independent selected-light state is missing.'
Assert-Contract ($source.Contains('CandidateRefreshSeconds')) 'Candidate cache refresh configuration is missing.'
Assert-Contract ($source.Contains('RestoreSelectedLights')) 'Exact multi-light restoration is missing.'
Assert-Contract ($source.Contains('RestoreCameraStates')) 'Camera frame-setting restoration is missing.'
Assert-Contract ($source.Contains('DestroyVolume')) 'Parent volume cleanup is missing.'
Assert-Contract ($source.Contains('ParentVisualSnapshot')) 'Transactional parent visual override is missing.'
Assert-Contract ($source.Contains('snapshot.Restore();')) 'Parent JSON-backed values are not restored after runtime application.'
Assert-Contract ($source.Contains('return true;') -and $source.Contains('addon disabled')) 'Disabling the addon must return control to the parent.'
Assert-Contract ($source.Contains('TryShowSystemNotification')) 'Parent toggle notification integration is missing.'
$beforeParentApplyIndex = $source.IndexOf('internal bool BeforeParentApply()', [System.StringComparison]::Ordinal)
$enableNotificationIndex = $source.IndexOf('ObserveParentRuntimeState(true);', $beforeParentApplyIndex, [System.StringComparison]::Ordinal)
$controllerIndex = $source.IndexOf('RunController(true);', $beforeParentApplyIndex, [System.StringComparison]::Ordinal)
Assert-Contract (
    $beforeParentApplyIndex -ge 0 -and
    $enableNotificationIndex -gt $beforeParentApplyIndex -and
    $controllerIndex -gt $enableNotificationIndex
) 'The intercepted parent enable path must notify before the controller consumes the state change.'

Write-Host 'Stable Contact Shadows contracts passed.'
