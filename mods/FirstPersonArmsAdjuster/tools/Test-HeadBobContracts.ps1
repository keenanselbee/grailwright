[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\FirstPersonArmsAdjuster.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Missing source file: $sourcePath"
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$requiredFragments = @(
    "UpdateHeadBob();",
    "&& IsHeadBobAccessibilityEnabled()",
    "private bool CanApplyHeadBob(Camera camera)",
    "if (!CanApplyHeadBob(camera))",
    "Awaken.TG.MVC.World.Any<HeadBobbingSetting>()",
    "nativeIntensity > 0.0f",
    '"EnableHeadBob"',
    '"HeadBobPreset"',
    '"HeadBobSmoothness"',
    '"SprintEmphasis"',
    '"HeadBobSpeedPercent"',
    '"StabilizeViewmodelDuringHeadBob"',
    '"ViewmodelHeadBobFollowPercent"',
    '"SuppressMotionBlurDuringHeadBob"',
    '"TemporalSafeHeadBobTiming"',
    "HeadBobPreset.Subtle",
    "HeadBobPreset.Balanced",
    "HeadBobPreset.Strong",
    "hero.IsSprinting",
    "HeadBobMaximumWalkVerticalCadenceHz = 3.2f",
    "HeadBobMaximumSprintVerticalCadenceHz = 4.2f",
    "HeadBobCadenceSoftKneeRatio = 0.8f",
    "SoftLimitHeadBobCadence(",
    "rawVerticalCadence,",
    "maximumVerticalCadence);",
    "effectiveVerticalCadence",
    "sprintImpact * _headBobSprintWeight",
    "_headBobSmoothedVerticalCadence",
    "_headBobSmoothedVerticalAmplitude",
    "_headBobSmoothedLateralAmplitude",
    "* Mathf.PI",
    "rawVerticalCadenceHz=",
    "effectiveVerticalCadenceHz=",
    "targetVerticalCadenceHz=",
    "smoothedVerticalCadenceHz=",
    "verticalAmplitude=",
    "lateralAmplitude=",
    "headBobSpeedPercent=",
    "stabilizeViewmodel=",
    "viewmodelFollowPercent=",
    "cameraBobWorldOffset=",
    "appliedViewmodelStabilization=",
    "remainingViewmodelBob=",
    "GetViewmodelHeadBobFollowWorldOffset(",
    '"camera-pre-cull"',
    '"begin-camera-rendering"',
    '"before-hdrp-camera-update"',
    "RestoreHeadBob(camera);",
    "TrySuppressHeadBobCameraMotionBlur(camera);",
    "HDCamera.GetOrCreate(camera)",
    "volumeStack.GetComponent<MotionBlur>()",
    "motionBlur.cameraMotionBlur.value = false;",
    "RestoreHeadBobMotionBlurSuppression();",
    "HDCameraUpdateHeadBobPatch",
    'typeof(HDCamera)',
    '"Update"',
    "TryApplyHeadBobBeforeHdrpCameraUpdate(",
    "ReportHeadBobAfterHdrpCameraUpdate(",
    "hdCamera.mainViewConstants.worldSpaceCameraPos",
    "camera.transform.TransformVector(",
    "_headBobLocalOffset);",
    "NativeHeadBobIntensityField.GetValue(setting)",
    "NativeHeadBobEnabledProperty.GetValue(setting, null)",
    "Hero.TppActive",
    "instance.SuppressNativeFirstPersonHeadBob("
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing locomotion or first-person camera contract: $fragment"
    }
}

if (([regex]::Matches(
        $source,
        "IsHeadBobAccessibilityEnabled\(\)")).Count -lt 2) {
    throw "Head bob must check the vanilla accessibility setting while updating and through the shared render gate."
}

$headBobPatch = [regex]::Match(
    $source,
    '(?s)internal static class HeadBobbingIntensityPatch.+?^    }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if (-not $headBobPatch.Contains("TrueThirdPersonPluginGuid") -or
    -not $headBobPatch.Contains("Priority.Last")) {
    throw "The first-person head-bob patch must run after True Third Person."
}

$removedFragments = @(
    "UpdateLocomotionDepthGuardBlend",
    "LocomotionDepthOffsetScale",
    "MitigateLocomotionBodyIntrusion",
    "HeadBobStrengthMultiplier",
    "EnableVanillaHeadBob",
    "EnableAlternateCameraMotion",
    "AlternateCameraMotionPreset",
    "AlternateCameraMotionSmoothness",
    "AlternateCameraSprintImpact",
    "Camera Motion - Vanilla Head Bob",
    "Camera Motion - Alternate",
    "ImmersiveCameraMotionPluginGuid"
)
foreach ($fragment in $removedFragments) {
    if ($source.Contains($fragment)) {
        throw "Removed vanilla or alternate camera-motion contract remains: $fragment"
    }
}

if ($source -notmatch '(?s)"EnableHeadBob",\s*true,.+?Accessibility / Head Bob setting remains the global master switch.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"HeadBobPreset",\s*HeadBobPreset\.Balanced,.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"HeadBobSmoothness",\s*0\.7f,.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"SprintEmphasis",\s*0\.75f,.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"HeadBobSpeedPercent",\s*75\.0f,.+?AcceptableValueRange<float>\(50\.0f, 150\.0f\).+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"StabilizeViewmodelDuringHeadBob",\s*true,.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"ViewmodelHeadBobFollowPercent",\s*100\.0f,.+?AcceptableValueRange<float>\(0\.0f, 100\.0f\).+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"SuppressMotionBlurDuringHeadBob",\s*false,.+?DisplaySection = "Head Bob"' -or
    $source -notmatch '(?s)"TemporalSafeHeadBobTiming",\s*true,.+?DisplaySection = "Head Bob"') {
    throw "The consolidated head-bob defaults or UI contract changed."
}

if ($source -notmatch '(?s)GetViewmodelHeadBobFollowWorldOffset\(.+?_viewmodelHeadBobFollowPercent == null.+?_stabilizeViewmodelDuringHeadBob == null.+?!CanApplyHeadBob\(camera\).+?_stabilizeViewmodelDuringHeadBob\.Value\s*\? 1\.0f.+?_viewmodelHeadBobFollowPercent\.Value / 100\.0f.+?camera\.transform\.TransformVector\(_headBobLocalOffset\).+?_headBobCameraWorldOffset \* followWeight' -or
    $source -notmatch '(?s)configuredLocalOffset =\s*GetEffectiveLocalOffset\(hero\).+?configuredWorldOffset = visualBasis\.TransformVector\(\s*configuredLocalOffset\).+?viewmodelHeadBobWorldOffset =\s*GetViewmodelHeadBobFollowWorldOffset\(camera\).+?_currentVisualWorldOffset = configuredWorldOffset\s*\+ viewmodelHeadBobWorldOffset') {
    throw "Viewmodel head-bob follow must keep configured positioning in arms-pivot space while applying optional exact camera-space bob stabilization through the shared world offset."
}

if ($source -notmatch '(?s)TrySuppressHeadBobCameraMotionBlur\(Camera camera\).+?HDCamera\.GetOrCreate\(camera\).+?volumeStack\.GetComponent<MotionBlur>\(\).+?_headBobMotionBlurOriginalCameraValue.+?cameraMotionBlur\.value = false' -or
    $source -notmatch '(?s)RestoreHeadBobMotionBlurSuppression\(\).+?cameraMotionBlur\.value =\s*_headBobMotionBlurOriginalCameraValue') {
    throw "Head-bob suppression must disable only HDRP camera motion blur and restore its exact prior value."
}

if ($source -notmatch '(?s)rawVerticalCadence = speed.+?\* 2\.0f.+?/ strideLength' -or
    $source -notmatch '(?s)maximumVerticalCadence = Mathf\.Lerp\(.+?HeadBobMaximumWalkVerticalCadenceHz.+?HeadBobMaximumSprintVerticalCadenceHz.+?sprintImpact \* _headBobSprintWeight' -or
    $source -notmatch '(?s)effectiveVerticalCadence =\s*SoftLimitHeadBobCadence\(.+?rawVerticalCadence.+?maximumVerticalCadence' -or
    $source -notmatch '(?s)cadenceScale = _headBobSpeedPercent == null.+?_headBobSpeedPercent\.Value.+?50\.0f.+?150\.0f.+?targetVerticalCadence =\s*effectiveVerticalCadence \* cadenceScale' -or
    $source -notmatch '(?s)SoftLimitHeadBobCadence\(.+?knee = maximumCadence\s*\* HeadBobCadenceSoftKneeRatio.+?rawCadence <= knee.+?maximumCadence\s*- \(range \* Mathf\.Exp\(' -or
    $source -notmatch '(?s)_headBobStridePhase = Mathf\.Repeat\(.+?_headBobSmoothedVerticalCadence.+?deltaTime.+?Mathf\.PI' -or
    $source -notmatch '(?s)ReportHeadBobDiagnostics.+?rawVerticalCadenceHz=.+?effectiveVerticalCadenceHz=.+?targetVerticalCadenceHz=.+?smoothedVerticalCadenceHz=.+?verticalAmplitude=.+?lateralAmplitude=') {
    throw "Head bob must apply the speed scale after the soft-knee limiter, preserve the two-peak gait, and expose raw, effective, target, smoothed, and amplitude diagnostics."
}

if ($source.Contains('Vector3.SmoothDamp(') -or
    $source.Contains('_headBobLocalOffsetVelocity') -or
    $source -notmatch '(?s)_headBobSmoothedVerticalCadence =\s*Mathf\.SmoothDamp\(.+?_headBobSmoothedVerticalAmplitude =\s*Mathf\.SmoothDamp\(.+?_headBobSmoothedLateralAmplitude =\s*Mathf\.SmoothDamp\(' -or
    $source -notmatch '(?s)_headBobLocalOffset = new Vector3\(.+?_headBobSmoothedLateralAmplitude.+?_headBobSmoothedVerticalAmplitude') {
    throw "Smoothness must ease cadence and amplitude envelopes without filtering or distorting the completed gait waveform."
}

function Get-ExpectedSoftLimitedCadence {
    param(
        [double] $RawCadence,
        [double] $MaximumCadence
    )

    $knee = $MaximumCadence * 0.8
    if ($MaximumCadence -le 0.0) {
        return 0.0
    }
    if ($RawCadence -le $knee) {
        return [Math]::Max(0.0, $RawCadence)
    }

    $range = $MaximumCadence - $knee
    return $MaximumCadence - (
        $range * [Math]::Exp(-($RawCadence - $knee) / $range))
}

$softLimitSamples = @(0.0, 1.5, 2.56, 3.2, 5.0, 10.0) |
    ForEach-Object { Get-ExpectedSoftLimitedCadence $_ 3.2 }
for ($index = 1; $index -lt $softLimitSamples.Count; $index++) {
    if ($softLimitSamples[$index] -lt $softLimitSamples[$index - 1] -or
        $softLimitSamples[$index] -gt 3.2) {
        throw "The representative walk/jog cadence curve is not monotonic and bounded."
    }
}
if ([Math]::Abs($softLimitSamples[2] - 2.56) -gt 0.000001 -or
    $softLimitSamples[3] -le 2.9 -or
    $softLimitSamples[3] -ge 3.2 -or
    $softLimitSamples[-1] -le 3.19 -or
    $softLimitSamples[-1] -ge 3.2) {
    throw "The representative cadence curve does not preserve its knee and approach the cap smoothly."
}

$beginCameraRendering = [regex]::Match(
    $source,
    '(?s)private void OnBeginCameraRendering\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if (-not $beginCameraRendering.Contains(
        'TrySuppressHeadBobCameraMotionBlur(camera);')) {
    throw "Camera motion blur must be suppressed after HDRP resolves the main camera's volume stack."
}

$hdCameraTimingPatch = [regex]::Match(
    $source,
    '(?s)internal static class HDCameraUpdateHeadBobPatch.+?^    }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
$temporalApply = [regex]::Match(
    $source,
    '(?s)internal void TryApplyHeadBobBeforeHdrpCameraUpdate\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
$preCull = [regex]::Match(
    $source,
    '(?s)private void OnCameraPreCull\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($source -notmatch '(?s)hdCameraUpdate.+AccessTools\.Method\(\s*typeof\(HDCamera\),\s*"Update"\)' -or
    $source -notmatch '_temporalSafeHeadBobPatchInstalled = true' -or
    $temporalApply -notmatch 'UsesTemporalSafeHeadBobTiming\(\)' -or
    $temporalApply -notmatch 'hdCamera\.camera' -or
    $temporalApply -notmatch 'before-hdrp-camera-update' -or
    $hdCameraTimingPatch -notmatch 'TryApplyHeadBobBeforeHdrpCameraUpdate' -or
    $hdCameraTimingPatch -notmatch 'ReportHeadBobAfterHdrpCameraUpdate' -or
    $preCull -notmatch '!UsesTemporalSafeHeadBobTiming\(\)' -or
    $beginCameraRendering -notmatch '!UsesTemporalSafeHeadBobTiming\(\)' -or
    $source -notmatch 'hdCamera\.mainViewConstants\.worldSpaceCameraPos' -or
    $source -notmatch 'RestoreHeadBob\(camera\);') {
    throw "Temporal-safe head bob must A/B the existing render timing against a main-camera HDRP-update prefix while retaining shared restoration and capture diagnostics."
}

Write-Host "First Person Arms Adjuster head-bob contracts passed."
