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
    '"SuppressMotionBlurDuringHeadBob"',
    "HeadBobPreset.Subtle",
    "HeadBobPreset.Balanced",
    "HeadBobPreset.Strong",
    "hero.IsSprinting",
    "TryApplyHeadBob(camera);",
    "RestoreHeadBob(camera);",
    "TrySuppressHeadBobCameraMotionBlur(camera);",
    "HDCamera.GetOrCreate(camera)",
    "volumeStack.GetComponent<MotionBlur>()",
    "motionBlur.cameraMotionBlur.value = false;",
    "RestoreHeadBobMotionBlurSuppression();",
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
    $source -notmatch '(?s)"SuppressMotionBlurDuringHeadBob",\s*false,.+?DisplaySection = "Head Bob"') {
    throw "The consolidated head-bob defaults or UI contract changed."
}

if ($source -notmatch '(?s)TrySuppressHeadBobCameraMotionBlur\(Camera camera\).+?HDCamera\.GetOrCreate\(camera\).+?volumeStack\.GetComponent<MotionBlur>\(\).+?_headBobMotionBlurOriginalCameraValue.+?cameraMotionBlur\.value = false' -or
    $source -notmatch '(?s)RestoreHeadBobMotionBlurSuppression\(\).+?cameraMotionBlur\.value =\s*_headBobMotionBlurOriginalCameraValue') {
    throw "Head-bob suppression must disable only HDRP camera motion blur and restore its exact prior value."
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

Write-Host "First Person Arms Adjuster head-bob contracts passed."
