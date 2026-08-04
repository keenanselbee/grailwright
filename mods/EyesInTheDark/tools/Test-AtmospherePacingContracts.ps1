$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pacingSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\PacingState.cs") -Raw
$atmosphereSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\Atmosphere.cs") -Raw

$tests = @'
namespace EyesInTheDark
{
    internal enum ThreatStage
    {
        Unnoticed,
        Watched,
        Hunted,
        Marked
    }

    public static class AtmospherePacingContractHarness
    {
        public static void Run()
        {
            NightPacingState pacing = new NightPacingState();
            PacingTuning tuning = new PacingTuning
            {
                BaseDangerBudget = 30f,
                LongNightBonusScale = 0.35f,
                MaximumLongNightBonus = 0.75f
            };

            NightBudgetSnapshot vanilla = pacing.BeginNight(1f, tuning);
            Near(vanilla.BonusFraction, 0f, "Vanilla night bonus");
            Near(vanilla.InitialBudget, 30f, "Vanilla night budget");

            NightBudgetSnapshot fourTimes = pacing.BeginNight(4f, tuning);
            Near(fourTimes.BonusFraction, 0.35f, "Four-times night bonus");
            Near(fourTimes.InitialBudget, 40.5f, "Four-times night budget");

            NightBudgetSnapshot tenTimes = pacing.BeginNight(10f, tuning);
            Near(tenTimes.BonusFraction, 0.75f, "Ten-times night capped bonus");
            Near(tenTimes.InitialBudget, 52.5f, "Ten-times night capped budget");

            NightBudgetSnapshot extreme = pacing.BeginNight(100f, tuning);
            Near(extreme.BonusFraction, 0.75f, "Extreme night bonus cap");
            Near(extreme.InitialBudget, 52.5f, "Extreme night budget cap");
            float beforeSpend;
            float afterSpend;
            Ensure(pacing.TrySpend(10f, out beforeSpend, out afterSpend),
                "Confirmed placement budget spend");
            Near(beforeSpend, 52.5f, "Budget before spend");
            Near(afterSpend, 42.5f, "Budget after spend");
            Ensure(!pacing.TrySpend(50f, out beforeSpend, out afterSpend),
                "Overspend rejected");
            pacing.Refund(10f);
            Near(pacing.RemainingBudget, 52.5f, "Invalid-target refund");
            pacing.Reset();
            Ensure(!pacing.IsInitialized, "Pacing reset");

            Ensure(!AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Minimal,
                AtmosphereEventKind.NightBegin),
                "Minimal excludes night atmosphere");
            Ensure(AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Minimal,
                AtmosphereEventKind.HuntCommitted),
                "Minimal includes committed hunts");
            Ensure(AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Minimal,
                AtmosphereEventKind.HunterKilled),
                "Minimal includes hunt outcomes");
            Ensure(AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Atmospheric,
                AtmosphereEventKind.NightBegin),
                "Atmospheric includes night begin");
            Ensure(AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Atmospheric,
                AtmosphereEventKind.UpwardStage),
                "Atmospheric includes upward stages");
            Ensure(!AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Atmospheric,
                AtmosphereEventKind.ProtectionEntered),
                "Atmospheric excludes protection changes");
            Ensure(AtmospherePolicy.ShouldNotify(
                GftNotificationPreset.Detailed,
                AtmosphereEventKind.ProtectionEntered),
                "Detailed includes protection changes");

            AtmosphereTextPools pools = new AtmosphereTextPools(7);
            string previous = pools.Select(
                AtmosphereEventKind.NightBegin,
                ThreatStage.Unnoticed);
            for (int index = 0; index < 50; index++)
            {
                string current = pools.Select(
                    AtmosphereEventKind.NightBegin,
                    ThreatStage.Unnoticed);
                Ensure(current != previous, "Immediate pool repeat");
                previous = current;
            }

            NotificationCooldowns cooldowns =
                new NotificationCooldowns();
            Ensure(cooldowns.CanEmit("diagnostics", "same", 0d, 3f),
                "First diagnostic emission");
            Ensure(!cooldowns.CanEmit("diagnostics", "same", 4d, 3f),
                "Identical diagnostic extended suppression");
            Ensure(cooldowns.CanEmit("diagnostics", "same", 10d, 3f),
                "Identical diagnostic eventually allowed");
            Ensure(!cooldowns.CanEmit("diagnostics", "different", 10d, 3f),
                "Same active time remains on cooldown while paused");
        }

        private static void Near(float actual, float expected, string message)
        {
            Ensure(System.Math.Abs(actual - expected) < 0.001f,
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

$usings = "using System;" + [Environment]::NewLine `
    + "using System.Collections.Generic;" + [Environment]::NewLine
$source = $usings `
    + ($pacingSource -replace '(?m)^using .+;\r?\n', '') `
    + [Environment]::NewLine `
    + ($atmosphereSource -replace '(?m)^using .+;\r?\n', '') `
    + [Environment]::NewLine `
    + $tests
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.AtmospherePacingContractHarness]::Run()

$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$boundarySource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\BoundaryController.cs") -Raw
$gftSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingTextBridge.cs") -Raw

foreach ($required in @(
    "WeatherSecondsPerRealSecond",
    "dayDurationInMinutes",
    "BeginNightPacing(",
    "BaseNightlyDangerBudget",
    "LongNightBonusScale",
    "MaximumLongNightBonus")) {
    if (!$pluginSource.Contains($required)) {
        throw "Adaptive pacing is missing source token: $required"
    }
}

foreach ($required in @(
    "HeroWyrdNightEdge",
    'AccessTools.Field(_edgeType, "color")',
    'AccessTools.Field(_edgeType, "radius")',
    'AccessTools.Field(_edgeType, "thickness")',
    "_originalColor",
    "Restore()")) {
    if (!$boundarySource.Contains($required)) {
        throw "Boundary ownership is missing source token: $required"
    }
}
if ($boundarySource.Contains("maskIntensity") -or
    $boundarySource.Contains("MaskIntensity")) {
    throw "Boundary customization must not modify native mask intensity."
}

foreach ($required in @(
    '"System"',
    '"Low"',
    '"Immediate"',
    '"eyes-in-the-dark-diagnostics"',
    '"wyrd"')) {
    if (!$gftSource.Contains($required)) {
        throw "GFT integration is missing source token: $required"
    }
}
if (!$pluginSource.Contains("_diagnostics.Value") -or
    !$pluginSource.Contains("ShowDiagnosticSystem(")) {
    throw "Diagnostics must gate concise GFT System summaries."
}

Write-Host "Eyes in the Dark atmosphere, pacing, boundary, and GFT contracts passed."
