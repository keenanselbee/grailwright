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
    '"ShoulderRetraction"',
    'DisplayName = "Shoulder Retraction (m)"',
    'DefaultShoulderSpineRetractionWeight = 0.35f',
    'DefaultShoulderSpine1RetractionWeight = 0.75f',
    'DefaultShoulderSpine2RetractionWeight = 1.0f',
    'DefaultShoulderJointRetractionWeight = 1.0f',
    'DefaultShoulderUpperArmRetractionWeight = 0.6f',
    'DefaultShoulderForearmRetractionWeight = 0.2f',
    'ShoulderRetraction * shoulderWeight',
    'GetShoulderBoneProfile(rig)',
    '"Spine"',
    '"Spine1"',
    '"Spine2"',
    '"LeftShoulder"',
    '"RightShoulder"',
    '"LeftArm"',
    '"RightArm"',
    '"LeftForeArm"',
    '"RightForeArm"',
    'ReportShoulderRetractionDiagnostics(',
    'configuredMeters=',
    'appliedMeters=',
    'affectedBones=',
    'effectiveRegionMeters=',
    'Shoulder retraction bone profiles:',
    'TestBoneIndex',
    'AllBones=[',
    'Renderers=[',
    '_shoulderBoneProfiles.Clear();'
)

foreach ($fragment in $requiredFragments) {
    if (-not $source.Contains($fragment)) {
        throw "Missing shoulder-retraction contract: $fragment"
    }
}

if ($source -notmatch '(?s)"TestRetractionBoneName",\s*string\.Empty,.+?DisplaySection = "Advanced - Retraction Profile".+?DisplayName = "Test Retraction Bone Name"' -or
    $source -notmatch '(?s)"TestBoneRetractionPercent",\s*0\.0f,.+?AcceptableValueRange<float>\(0\.0f, 400\.0f\).+?DisplayName = "Test Bone Retraction \(%\)"') {
    throw "The exact-name diagnostic target must remain off by default with a live 0-400 percent test range."
}

$profileSettings = @(
    @('SpineRetractionPercent', '0.0f', 'Spine Retraction (%)'),
    @('Spine1RetractionPercent', '0.0f', 'Spine1 Retraction (%)'),
    @('Spine2RetractionPercent', '100.0f', 'Spine2 Retraction (%)'),
    @('LeftShoulderRetractionPercent', '100.0f', 'Left Shoulder Retraction (%)'),
    @('RightShoulderRetractionPercent', '100.0f', 'Right Shoulder Retraction (%)'),
    @('UpperArmRetractionPercent', '30.0f', 'Upper-Arm Retraction (%)'),
    @('ForearmRetractionPercent', '20.0f', 'Forearm Retraction (%)')
)
foreach ($setting in $profileSettings) {
    $key = [regex]::Escape($setting[0])
    $defaultValue = [regex]::Escape($setting[1])
    $label = [regex]::Escape($setting[2])
    if ($source -notmatch ('(?s)"' + $key + '",\s*' + $defaultValue + '.+?AcceptableValueRange<float>\(0\.0f, 200\.0f\).+?DisplaySection = "Advanced - Retraction Profile".+?DisplayName = "' + $label + '"')) {
        throw "Missing live 0-200 percent retraction profile control: $($setting[0])"
    }
    if ($source -notmatch ('(?s)TryGetCustomizedValue\(.+?"Advanced - Retraction Profile",\s*"' + $key + '"') -or
        $source -notmatch ('(?s)RestorePreservedFloat\(.+?_hasPending' + $key)) {
        throw "Retraction profile setting must participate in typed config preservation: $($setting[0])"
    }
}

$groupSettings = @(
    @('LowerTorsoRetractionPercent', 'Lower Torso Retraction (%)'),
    @('ChestHelperRetractionPercent', 'Chest Helper Retraction (%)'),
    @('ShoulderFixRetractionPercent', 'Shoulder-Fix Retraction (%)'),
    @('NativeClothRetractionPercent', 'Native Cloth Retraction (%)')
)
foreach ($setting in $groupSettings) {
    $key = [regex]::Escape($setting[0])
    $label = [regex]::Escape($setting[1])
    if ($source -notmatch ('(?s)"' + $key + '",\s*0\.0f,.+?AcceptableValueRange<float>\(0\.0f, 400\.0f\).+?DisplaySection = "Advanced - Retraction Profile".+?DisplayName = "' + $label + '"')) {
        throw "Missing off-by-default grouped 0-400 percent retraction control: $($setting[0])"
    }
    if ($source -notmatch ('(?s)TryGetCustomizedValue\(.+?"Advanced - Retraction Profile",\s*"' + $key + '"') -or
        $source -notmatch ('(?s)RestorePreservedFloat\(.+?_hasPending' + $key)) {
        throw "Grouped retraction setting must participate in typed config preservation: $($setting[0])"
    }
}

if ($source -notmatch '(?s)"ShoulderRetraction",\s*0\.05f,.+?AcceptableValueRange<float>\(0\.0f, 0\.25f\).+?DisplaySection = "Position"') {
    throw "Shoulder retraction must default to 0.05 metres with a 0.25 metre supported maximum."
}

$refreshMethod = [regex]::Match(
    $source,
    '(?s)private void RefreshCurrentVisualWorldOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($refreshMethod -notmatch '(?s)configuredShoulderRetraction = Mathf\.Clamp\(.+?_shoulderRetraction\.Value.+?0\.0f,.+?0\.25f' -or
    $refreshMethod -notmatch '(?s)_currentShoulderRetractionMeters = Mathf\.Lerp\(.+?configuredShoulderRetraction.+?DodgeRetractionMaximumMeters.+?dodgeRetractionProgress' -or
    $refreshMethod -notmatch '(?s)_currentShoulderRetractionWorldOffset =\s*visualBasis\.TransformVector\(.+?-_currentShoulderRetractionMeters') {
    throw "Shoulder retraction must interpolate independently toward the dodge maximum and transform through the current arms basis."
}
if ($refreshMethod -match 'GetEffectiveForwardOffset\(hero\)') {
    throw "Shoulder retraction must be able to pass behind the vanilla position independently of equipment depth."
}
if ($refreshMethod -match '(?s)_currentShoulderRetractionMeters =.+?configuredLocalOffset\.z') {
    throw "Shoulder retraction must not fade with the guarded effective presentation offset."
}

$boneJob = [regex]::Match(
    $source,
    '(?s)private struct OffsetKandraBonesJob.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($boneJob -notmatch '(?s)RendererRetraction.+?ApplyShoulderRetraction.+?SpineRetractionWeight.+?Spine1RetractionWeight.+?Spine2RetractionWeight.+?LeftShoulderRetractionWeight.+?RightShoulderRetractionWeight.+?UpperArmRetractionWeight.+?ForearmRetractionWeight.+?LowerTorsoRetractionWeight.+?ChestHelperRetractionWeight.+?ShoulderFixRetractionWeight.+?NativeClothRetractionWeight.+?TestBoneRetractionWeight.+?TestBoneIndex.+?HipsIndex.+?LeftBreastBaseIndex.+?RightBreastBaseIndex.+?LeftBreastIndex.+?RightBreastIndex.+?LeftShoulderFixIndex.+?RightShoulderFixIndex.+?NativeClothStartIndex.+?NativeClothEndIndex.+?NativeClothIndicesContiguous.+?SpineIndex.+?Spine1Index.+?Spine2Index.+?LeftShoulderIndex.+?RightShoulderIndex.+?LeftUpperArmIndex.+?RightUpperArmIndex.+?LeftForearmIndex.+?RightForearmIndex' -or
    $boneJob -notmatch '(?s)bone\.boneTransform\.c3.+?Translation.+?RendererRetraction.+?ShoulderRetraction \* shoulderWeight') {
    throw "The existing Kandra bone job must apply one tapered post-animation shoulder correction alongside the shared translation."
}

if ($source -notmatch '(?s)_hasPendingTestRetractionBoneName =.+?TryGetCustomizedValue\(.+?"TestRetractionBoneName".+?_pendingTestRetractionBoneName' -or
    $source -notmatch '(?s)TryRestore\(\s*_testRetractionBoneName,\s*_pendingTestRetractionBoneName' -or
    $source -notmatch '(?s)_hasPendingTestBoneRetractionPercent =.+?"TestBoneRetractionPercent".+?_pendingTestBoneRetractionPercent' -or
    $source -notmatch '(?s)RestorePreservedFloat\(\s*_hasPendingTestBoneRetractionPercent,\s*_testBoneRetractionPercent') {
    throw "Diagnostic bone target settings must participate in typed config preservation."
}
if ($boneJob -match '(?:Left|Right)HandIndex' -or
    $boneJob -match 'localScale') {
    throw "Shoulder retraction must stop before the hands and must not scale the live skeleton."
}

$lateOffset = [regex]::Match(
    $source,
    '(?s)private void ApplyLateKandraOffset\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ([regex]::Matches($lateOffset, 'new OffsetKandraBonesJob').Count -ne 1 -or
    $lateOffset -notmatch '(?s)RendererRetraction = rendererRetraction.+?ShoulderRetraction = shoulderRetraction.+?ApplyShoulderRetraction =.+?Schedule\(length, 32, dependency\)') {
    throw "Shoulder retraction must reuse the single existing per-rig Kandra job without adding another bone pass."
}

if ($source -notmatch '(?s)_hasPendingShoulderRetraction =.+?TryGetCustomizedValue\(.+?"Position",\s*"ShoulderRetraction".+?_pendingShoulderRetraction' -or
    $source -notmatch '(?s)RestorePreservedFloat\(\s*_hasPendingShoulderRetraction,\s*_shoulderRetraction,\s*_pendingShoulderRetraction' -or
    $source -notmatch '_hasPendingShoulderRetraction = false;') {
    throw "Shoulder retraction must participate in typed config preservation and cleanup."
}

if ($source -notmatch '(?s)ConfigRecoveryKeepCurrentDefaultRule\(\s*16,\s*"Position",\s*"ShoulderRetraction"') {
    throw "Schema 16 must not automatically restore a legacy custom ShoulderRetraction value under the stronger torso taper."
}

Write-Host "First Person Arms Adjuster shoulder-retraction contracts passed."
