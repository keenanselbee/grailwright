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

if ($source.Contains('RendererIsolationTarget') -or
    $source.Contains('Renderer Isolation Target')) {
    throw "The completed renderer-isolation selector must not remain in runtime source."
}

if ($source -notmatch '(?s)"TorsoRendererRetractionPercent",\s*50\.0f,.+?AcceptableValueRange<float>\(0\.0f, 400\.0f\).+?DisplaySection = "Advanced - Retraction Profile".+?DisplayName = "Torso Renderer Retraction \(%\)"') {
    throw "Torso renderer retraction must expose a live 0-400 percent control defaulting to the validated 50 percent."
}

if ($source -notmatch '(?s)TryGetCustomizedValue\(.+?"Advanced - Retraction Profile",\s*"TorsoRendererRetractionPercent"' -or
    $source -notmatch '(?s)RestorePreservedFloat\(\s*_hasPendingTorsoRendererRetractionPercent,\s*_torsoRendererRetractionPercent' -or
    $source -notmatch '_hasPendingTorsoRendererRetractionPercent = false;') {
    throw "Torso renderer retraction must participate in typed config preservation and cleanup."
}

$findMethod = [regex]::Match(
    $source,
    '(?s)private static KandraRenderer FindTorsoRenderer\(.+?^        }',
    [System.Text.RegularExpressions.RegexOptions]::Multiline
).Value
if ($findMethod -notmatch 'mesh\.name\.IndexOf' -or
    $findMethod -notmatch 'material\.name\.IndexOf' -or
    $findMethod -notmatch '"Torso"' -or
    $findMethod -notmatch 'clothOrdinal == 2' -or
    $findMethod -notmatch 'return cloth2Fallback;') {
    throw "Torso ownership must prefer semantic mesh/material matching and retain Cloth2 as the compatibility fallback."
}

foreach ($fragment in @(
    'UpdateTorsoRendererRetractionRig();',
    'target.enabled = false;',
    'KandraRendererManager.IsInvalidId(',
    'rigObject.SetActive(false);',
    'rigObject.AddComponent<KandraRig>()',
    '(Transform[])originalRig.bones.Clone()',
    '(ushort[])originalRig.boneParents.Clone()',
    '(FixedString64Bytes[])originalRig.boneNames.Clone()',
    'rigObject.SetActive(true);',
    'renderer.rendererData.rig = dedicatedRig;',
    'RendererRetraction = rendererRetraction',
    'Translations = translations',
    'renderer == _torsoRetractionRenderer',
    'torsoRendererBones=',
    'torsoRendererMatched='
)) {
    if (-not $source.Contains($fragment)) {
        throw "Missing torso-renderer retraction contract: $fragment"
    }
}

if ([regex]::Matches($source, 'new OffsetKandraBonesJob').Count -ne 1) {
    throw "Torso renderer retraction must reuse the single existing Kandra bone job."
}
if ($source -match 'AddComponent<KandraRenderer>' -or
    $source -match 'new Camera\(') {
    throw "Torso renderer retraction must not add another renderer or camera."
}

Write-Host "First Person Arms Adjuster torso-renderer retraction contracts passed."
