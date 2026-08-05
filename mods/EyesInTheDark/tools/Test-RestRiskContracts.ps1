[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$riskSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\RestRisk.cs") -Raw

$tests = @'
namespace EyesInTheDark
{
    public static class RestRiskContractHarness
    {
        public static void Run()
        {
            RestRiskWindow fullNight;
            Ensure(RestRiskPolicy.TryCreateWindow(
                21f / 24f, 12f, out fullNight),
                "rest crossing nightfall has overlap");
            Near(fullNight.OverlapStartHours, 1.08f, 0.001f,
                "night begins at 22.08");
            Near(fullNight.OverlapHours, RestRiskPolicy.NightHours,
                0.001f, "complete night overlap");

            RestRiskWindow activeNight;
            Ensure(RestRiskPolicy.TryCreateWindow(
                0f, 1f, out activeNight),
                "midnight rest overlaps Wyrdnight");
            Near(activeNight.OverlapStartHours, 0f, 0.001f,
                "active-night overlap begins immediately");
            Near(activeNight.OverlapHours, 1f, 0.001f,
                "one-hour active-night overlap");

            RestRiskWindow daylight;
            Ensure(!RestRiskPolicy.TryCreateWindow(
                12f / 24f, 2f, out daylight),
                "daytime-only rest has no Wyrdnight risk");

            Near(RestRiskPolicy.Chance(0f, 45f, 75f),
                0.45f, 0.001f, "zero-threat chance");
            Near(RestRiskPolicy.Chance(50f, 45f, 75f),
                0.60f, 0.001f, "mid-threat chance");
            Near(RestRiskPolicy.Chance(100f, 45f, 75f),
                0.75f, 0.001f, "maximum-threat chance");

            RestRiskTracker guaranteed = new RestRiskTracker(10);
            RestRiskDecision eyes = guaranteed.Evaluate(
                fullNight, 100f, 100f, 100f, false, 0f);
            Ensure(eyes.InterruptedByEyes,
                "100 percent full-night risk interrupts");
            Ensure(eyes.HoursUntilInterrupt > fullNight.OverlapStartHours
                && eyes.HoursUntilInterrupt
                    < fullNight.OverlapStartHours + fullNight.OverlapHours,
                "Eyes interruption occurs within Wyrdnight");
            Ensure(guaranteed.Disturbed,
                "Eyes interruption locks further exposed rest");

            RestRiskTracker nativeFirst = new RestRiskTracker(10);
            RestRiskDecision native = nativeFirst.Evaluate(
                fullNight, 100f, 100f, 100f, true, 3f);
            Ensure(native.InterruptedByNative
                && !native.InterruptedByEyes,
                "native interruption remains authoritative");

            RestRiskTracker cumulative = new RestRiskTracker(20);
            RestRiskWindow quarter = new RestRiskWindow
            {
                RequestedHours = RestRiskPolicy.NightHours / 4f,
                OverlapStartHours = 0f,
                OverlapHours = RestRiskPolicy.NightHours / 4f
            };
            RestRiskDecision first = cumulative.Evaluate(
                quarter, 0f, 0f, 0f, false, 0f);
            RestRiskDecision second = cumulative.Evaluate(
                quarter, 0f, 0f, 0f, false, 0f);
            Ensure(!first.InterruptedByEyes && !second.InterruptedByEyes,
                "zero Eyes risk never interrupts");
            Near(cumulative.Exposure, 0.5f, 0.001f,
                "segmented rests accumulate one shared exposure value");
        }

        private static void Near(
            float actual,
            float expected,
            float tolerance,
            string message)
        {
            Ensure(System.Math.Abs(actual - expected) <= tolerance,
                message + ": expected=" + expected + ", actual=" + actual);
        }

        private static void Ensure(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.InvalidOperationException(message);
            }
        }
    }
}
'@

$source = "using System;" + [Environment]::NewLine `
    + $tests + [Environment]::NewLine `
    + ($riskSource -replace '(?m)^using .+;\r?\n', '')
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.RestRiskContractHarness]::Run()

$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
foreach ($required in @(
    'nameof(GameRealTime.WillSkipTimeBeInterrupted)',
    '"WillBeSurprisedByWyrdNight"',
    'ApplyRestInterruptionRisk(',
    'ShouldSuppressNativeWyrdnightSurprise(',
    'RestInterruptionChanceAtZeroThreat',
    'RestInterruptionChanceAtMaximumThreat',
    '_pendingRestHunt = true;',
    '_huntDirector.ResetNight(restHuntTuning);',
    'RequestOfficialHunterPlacement(')) {
    if (!$plugin.Contains($required)) {
        throw "Rest-risk integration is missing token: $required"
    }
}

Write-Host "Eyes in the Dark cumulative rest-risk contracts passed."
