$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "src\BloodMagicExpansion.cs")
$changelog = Get-Content -Raw -LiteralPath (
    Join-Path $modRoot "CHANGELOG.txt")

foreach ($required in @(
    'using Awaken.TG.Main.Heroes.Combat;',
    'private VCHeroRaycaster _heroRaycaster;',
    'typeof(VCHeroRaycaster)',
    '"OnAttach"',
    '"OnDiscard"',
    'private bool TryGetCorpseLookRay(out Vector3 position, out Vector3 forward)',
    '_heroRaycaster.GetViewRay(out position, out forward);',
    'Camera camera = Camera.main;',
    'Physics.Raycast(rayPosition, rayForward',
    'Physics.RaycastAll(rayPosition, rayForward')) {
    if (!$source.Contains($required)) {
        throw "Missing third-person corpse-targeting contract: $required"
    }
}

$lookRayBlock = [regex]::Match(
    $source,
    '(?s)private bool TryGetCorpseLookRay\(.+?(?=\r?\n\s*private )')
if (!$lookRayBlock.Success -or
    $lookRayBlock.Value.IndexOf('_heroRaycaster.GetViewRay(', [StringComparison]::Ordinal) -gt
        $lookRayBlock.Value.IndexOf('Camera camera = Camera.main;', [StringComparison]::Ordinal)) {
    throw 'The native hero view ray must be preferred before the Camera.main fallback.'
}

if ($source.Contains('Physics.Raycast(camera.transform.position, camera.transform.forward') -or
    $source.Contains('Physics.RaycastAll(camera.transform.position, camera.transform.forward')) {
    throw 'Corpse targeting still casts directly from the displaced third-person camera.'
}

if ($changelog.IndexOf('True Third Person', [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
    $changelog.IndexOf('perspective-aware hero view ray', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw 'The changelog does not document the True Third Person corpse-targeting fix.'
}

Write-Host "Blood Magic Expansion third-person corpse-targeting contracts passed."
