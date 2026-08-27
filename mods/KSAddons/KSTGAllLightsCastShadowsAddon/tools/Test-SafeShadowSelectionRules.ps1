param(
    [string]$RepositoryRoot = (
        Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
    ).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Rule {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "Safe shadow selection rule failed: $Message"
    }
}

$rulesPath = Join-Path $RepositoryRoot `
    'mods\KSAddons\KSTGAllLightsCastShadowsAddon\src\SafeShadowSelectionRules.cs'
$rules = Get-Content -LiteralPath $rulesPath -Raw
$harness = @'
namespace TGAllLightsCastShadowsAddon
{
    public static class SafeShadowSelectionRuleHarness
    {
        public static bool WithinDistance(float distance, bool active, float maximum, float hysteresis)
        {
            return SafeShadowSelectionRules.IsWithinSelectionDistance(distance, active, maximum, hysteresis);
        }

        public static bool ViewRelevant(bool intersects, bool active, float elapsed, float delay)
        {
            return SafeShadowSelectionRules.IsEffectivelyViewRelevant(intersects, active, elapsed, delay);
        }

        public static float Score(float distance, float intensity, float range, bool active)
        {
            return SafeShadowSelectionRules.CalculateCandidateScore(distance, intensity, range, active);
        }

        public static int FaceCost(bool point)
        {
            return SafeShadowSelectionRules.ShadowMapFaceCost(point);
        }

        public static int AvailableFaces(int maximumFaces, int externalFaces)
        {
            return SafeShadowSelectionRules.AvailableShadowMapFaces(maximumFaces, externalFaces);
        }

        public static int DawnDuskMinutes(int configured, bool normalize, float seconds, float rate)
        {
            return SafeShadowSelectionRules.ResolveDawnDuskBlendMinutes(configured, normalize, seconds, rate);
        }

        public static bool Fits(int lights, int faces, int candidateFaces, int maxLights, int maxFaces)
        {
            return SafeShadowSelectionRules.FitsSelectionBudget(lights, faces, candidateFaces, maxLights, maxFaces);
        }
    }
}
'@

Add-Type -TypeDefinition ($rules + [Environment]::NewLine + $harness) `
    -Language CSharp `
    -ErrorAction Stop

$type = [TGAllLightsCastShadowsAddon.SafeShadowSelectionRuleHarness]

Assert-Rule ($type::WithinDistance(25, $false, 25, 8)) `
    'a new light at the acquisition boundary must remain eligible.'
Assert-Rule (-not $type::WithinDistance(25.01, $false, 25, 8)) `
    'a new light outside the acquisition boundary must be rejected.'
Assert-Rule ($type::WithinDistance(33, $true, 25, 8)) `
    'an active light must receive the configured retention hysteresis.'
Assert-Rule (-not $type::WithinDistance(33.01, $true, 25, 8)) `
    'hysteresis must remain bounded.'
Assert-Rule ($type::ViewRelevant($false, $true, 0.75, 0.75)) `
    'a selected light must retain view priority through the exit-delay boundary.'
Assert-Rule (-not $type::ViewRelevant($false, $true, 0.751, 0.75)) `
    'view priority must expire after the exit delay.'
Assert-Rule (-not $type::ViewRelevant($false, $false, 0.1, 0.75)) `
    'an unseen unselected light must not inherit view priority.'
Assert-Rule ($type::Score(10, 2, 5, $true) -gt $type::Score(10, 2, 5, $false)) `
    'active lights must receive a retention bonus.'
Assert-Rule ($type::FaceCost($true) -eq 6 -and $type::FaceCost($false) -eq 1) `
    'point lights must cost six faces and spot lights one.'
Assert-Rule ($type::AvailableFaces(48, 6) -eq 42) `
    'an active external point-light shadow must reserve six faces.'
Assert-Rule ($type::AvailableFaces(4, 6) -eq 0) `
    'external reservations must never produce a negative face budget.'
Assert-Rule ($type::AvailableFaces(48, -6) -eq 48) `
    'invalid negative external costs must not expand the face budget.'
Assert-Rule ($type::DawnDuskMinutes(10, $false, 30, 74.4) -eq 10) `
    'non-normalized dawn/dusk blending must retain configured in-game minutes.'
Assert-Rule ($type::DawnDuskMinutes(10, $true, 30, 16.56) -eq 8) `
    'Eyes daylight must convert a 30-second target to approximately eight game minutes.'
Assert-Rule ($type::DawnDuskMinutes(10, $true, 30, 74.4) -eq 37) `
    'Eyes quiet night must convert a 30-second target to approximately 37 game minutes.'
Assert-Rule ($type::DawnDuskMinutes(10, $true, 30, 37.2) -eq 19) `
    'Eyes maximum-threat night must convert a 30-second target to approximately 19 game minutes.'
Assert-Rule ($type::DawnDuskMinutes(10, $true, 30, 0) -eq 10) `
    'an unavailable world-clock rate must safely fall back to configured minutes.'
Assert-Rule ($type::Fits(7, 42, 6, 16, 48)) `
    'a point light must fit exactly at the face boundary.'
Assert-Rule (-not $type::Fits(8, 48, 1, 16, 48)) `
    'the face limit must reject additional work.'
Assert-Rule (-not $type::Fits(16, 10, 1, 16, 48)) `
    'the light-count limit must remain independent of the face limit.'

Write-Host 'Safe shadow selection rule tests passed.'
