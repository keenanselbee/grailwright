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

        public static float Score(
            float distance,
            float intensity,
            float range,
            bool active,
            float retention,
            float centerWeight,
            float centerPriority)
        {
            return SafeShadowSelectionRules.CalculateCandidateScore(
                distance,
                intensity,
                range,
                active,
                retention,
                centerWeight,
                centerPriority);
        }

        public static bool SphereOutside(float distance, float radius)
        {
            return SafeShadowSelectionRules.IsSphereOutsideFrustumPlane(distance, radius);
        }

        public static float CenterWeight(float x, float y, float depth)
        {
            return SafeShadowSelectionRules.CalculateScreenCenterWeight(x, y, depth);
        }

        public static int HandoffPhase(float elapsed, float duration)
        {
            return (int)SafeShadowSelectionRules.ResolveShadowHandoffProgress(elapsed, duration).Phase;
        }

        public static float HandoffStrength(float elapsed, float duration)
        {
            return SafeShadowSelectionRules.ResolveShadowHandoffProgress(elapsed, duration).StrengthMultiplier;
        }

        public static int InitialFillLimit(bool pending, int batchSize, int missing)
        {
            return SafeShadowSelectionRules.ResolveInitialFillActivationLimit(pending, batchSize, missing);
        }

        public static int ResolutionCap(
            bool generalActive,
            int generalCap,
            bool interiorActive,
            int interiorCap,
            bool combatActive,
            int combatCap)
        {
            return SafeShadowSelectionRules.ResolveShadowResolutionCap(
                generalActive,
                generalCap,
                interiorActive,
                interiorCap,
                combatActive,
                combatCap);
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
Assert-Rule ($type::Score(10, 2, 5, $true, 2, 0, 4) -gt
    $type::Score(10, 2, 5, $false, 2, 0, 4)) `
    'active lights must receive the configured retention advantage.'
Assert-Rule ($type::Score(10, 2, 5, $true, 0, 0, 4) -eq
    $type::Score(10, 2, 5, $false, 0, 0, 4)) `
    'zero retention must remove the active-light ranking advantage.'
Assert-Rule ($type::Score(10, 2, 5, $false, 2, 1, 4) -gt
    $type::Score(10, 2, 5, $false, 2, 0, 4)) `
    'screen-centre priority must improve ranking without changing eligibility.'
Assert-Rule (-not $type::SphereOutside(-5, 5)) `
    'a light influence sphere touching a frustum plane must remain relevant.'
Assert-Rule ($type::SphereOutside(-5.01, 5)) `
    'a light influence sphere fully beyond a frustum plane must be rejected.'
Assert-Rule ($type::CenterWeight(0.5, 0.5, 1) -eq 1) `
    'a visible source at screen centre must receive full centre priority.'
Assert-Rule ($type::CenterWeight(0, 0.5, 1) -eq 0) `
    'a source at the screen edge must receive no centre priority.'
Assert-Rule ($type::CenterWeight(0.5, 0.5, -1) -eq 0) `
    'a source behind the camera must receive no centre priority.'
Assert-Rule ($type::HandoffPhase(0, 0.6) -eq 0 -and
    $type::HandoffStrength(0, 0.6) -eq 1) `
    'a handoff must begin with the outgoing shadow at full strength.'
Assert-Rule ($type::HandoffPhase(0.3, 0.6) -eq 1 -and
    $type::HandoffStrength(0.3, 0.6) -eq 0) `
    'the budget slot must transfer at zero shadow strength.'
Assert-Rule ([Math]::Abs($type::HandoffStrength(0.45, 0.6) - 0.5) -lt 0.001) `
    'the incoming shadow must fade in through the second half.'
Assert-Rule ($type::HandoffPhase(0.6, 0.6) -eq 2 -and
    $type::HandoffStrength(0.6, 0.6) -eq 1) `
    'a completed handoff must restore full configured strength.'
Assert-Rule ($type::InitialFillLimit($true, 4, 10) -eq 4) `
    'initial filling must respect the configured batch size.'
Assert-Rule ($type::InitialFillLimit($true, 4, 2) -eq 2) `
    'initial filling must not exceed the remaining desired lights.'
Assert-Rule ($type::InitialFillLimit($false, 4, 10) -eq 10) `
    'steady-state filling must not retain the startup batch limit.'
Assert-Rule ($type::ResolutionCap($true, 256, $false, 128, $false, 512) -eq 256) `
    'the general resolution cap must apply by itself.'
Assert-Rule ($type::ResolutionCap($false, 256, $true, 128, $false, 512) -eq 128) `
    'the interior resolution cap must apply without the general atlas guard.'
Assert-Rule ($type::ResolutionCap($false, 256, $false, 128, $true, 512) -eq 512) `
    'combat resolution must remain independently active.'
Assert-Rule ($type::ResolutionCap($false, 256, $true, 256, $true, 512) -eq 256) `
    'combat must not raise an active interior resolution cap.'
Assert-Rule ($type::ResolutionCap($true, 128, $true, 256, $true, 512) -eq 128) `
    'combined resolution caps must retain the strictest active value.'
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
