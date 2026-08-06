$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$worldTimescaleSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\WorldTimescale.cs") -Raw

$stubsAndTests = @'
namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        public void LogInfo(object value) { }
        public void LogWarning(object value) { }
    }
}

namespace Awaken.TG.Main.Timing
{
    internal struct TestWeatherTime
    {
        public bool IsNight;
    }

    internal sealed class GameRealTime
    {
        public bool HasBeenDiscarded { get; set; }
        public TestWeatherTime WeatherTime;
        public float WeatherSecondsPerRealSecond;
        public int SetterCalls;

        public void SetWeatherDayDuration(float minutes)
        {
            SetterCalls++;
            WeatherSecondsPerRealSecond = 1440f / minutes;
        }
    }
}

namespace EyesInTheDark
{
    internal static class NightStateEvaluator
    {
        public const float NightStartFraction = 0.92f;
        public const float NightEndFraction = 0.23f;
    }

    public static class WorldTimescaleContractHarness
    {
        public static void Run()
        {
            const float vanillaCycleMinutes = 20f;
            Near(WorldTimescalePolicy.CycleMinutesForPhase(60f, false),
                86.95652f, 0.001f, "60-minute day cycle rate");
            Near(WorldTimescalePolicy.DynamicNightMinutes(6f, 12f, 0f),
                6f, 0.001f, "zero-threat night duration");
            Near(WorldTimescalePolicy.DynamicNightMinutes(6f, 12f, 50f),
                9f, 0.001f, "mid-threat night duration");
            Near(WorldTimescalePolicy.DynamicNightMinutes(6f, 12f, 100f),
                12f, 0.001f, "maximum-threat night duration");
            Near(WorldTimescalePolicy.PhaseDurationMultiplier(
                vanillaCycleMinutes, 12f, true), 1.935484f, 0.001f,
                "maximum night pacing multiplier");
            float twelveMinuteNightRate = 1440f
                / WorldTimescalePolicy.CycleMinutesForPhase(12f, true);
            Near(WorldTimescalePolicy.RemainingNightRealSeconds(
                11f / 12f, twelveMinuteNightRate), 60f, 0.01f,
                "one real minute remains before dawn");
            Near(WorldTimescalePolicy.RemainingNightRealSeconds(
                23f / 24f, twelveMinuteNightRate), 30f, 0.01f,
                "thirty real seconds remain before dawn");
            Near(WorldTimescalePolicy.RemainingNightRealSeconds(
                1f, twelveMinuteNightRate), 0f, 0.001f,
                "dawn has no remaining night time");
            Near(WorldTimescalePolicy.ElapsedNightRealSeconds(
                1f / 24f, twelveMinuteNightRate), 30f, 0.01f,
                "thirty real seconds elapsed after nightfall");
            float sixtyMinuteDayRate = 1440f
                / WorldTimescalePolicy.CycleMinutesForPhase(60f, false);
            float thirtySecondsBeforeNightfall =
                NightStateEvaluator.NightStartFraction
                - 30f * sixtyMinuteDayRate / 86400f;
            Near(WorldTimescalePolicy.RemainingDaylightRealSeconds(
                thirtySecondsBeforeNightfall,
                sixtyMinuteDayRate), 30f, 0.01f,
                "thirty real seconds remain before nightfall");

            var log = new BepInEx.Logging.ManualLogSource();
            var controller = new WorldTimescaleController(log);
            var first = Clock(false, 72f);
            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 0f);
            Ensure(first.SetterCalls == 1, "initial day apply");
            Near(first.WeatherSecondsPerRealSecond, 16.56f, 0.001f,
                "day weather rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 0f);
            Ensure(first.SetterCalls == 1,
                "unchanged state never repeats the setter");

            first.WeatherTime = new Awaken.TG.Main.Timing.TestWeatherTime
            {
                IsNight = true
            };
            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 0f);
            Ensure(first.SetterCalls == 2, "nightfall apply");
            Near(first.WeatherSecondsPerRealSecond, 74.4f, 0.001f,
                "zero-threat night weather rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 0.25f);
            Ensure(first.SetterCalls == 2,
                "tiny threat changes do not rewrite the clock");

            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 50f);
            Ensure(first.SetterCalls == 3, "threat-stretched night apply");
            Near(first.WeatherSecondsPerRealSecond, 49.6f, 0.001f,
                "mid-threat night weather rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 50f);
            Ensure(first.SetterCalls == 4, "live duration config apply");
            Near(first.WeatherSecondsPerRealSecond, 34.33846f, 0.001f,
                "live duration config rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 50f, true, 2f);
            Ensure(first.SetterCalls == 5,
                "diagnostic multiplier override apply");
            Near(first.WeatherSecondsPerRealSecond, 144f, 0.001f,
                "twice-vanilla diagnostic rate");
            first.WeatherTime = new Awaken.TG.Main.Timing.TestWeatherTime
            {
                IsNight = false
            };
            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 80f, true, 2f);
            Ensure(first.SetterCalls == 5,
                "override ignores phase and threat changes");
            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 80f, true, 0.5f);
            Ensure(first.SetterCalls == 6,
                "live diagnostic multiplier change apply");
            Near(first.WeatherSecondsPerRealSecond, 36f, 0.001f,
                "half-vanilla diagnostic rate");
            controller.Update(first, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 80f, false, 1f);
            Ensure(first.SetterCalls == 7,
                "disabling override resumes dynamic phase timing");
            Near(first.WeatherSecondsPerRealSecond, 16.56f, 0.001f,
                "dynamic day rate resumes after override");

            var loaded = Clock(true, 72f);
            controller.Update(loaded, vanillaCycleMinutes,
                true, 60f, 6f, 20f, 50f);
            Ensure(loaded.SetterCalls == 1, "new clock/load reapply");

            controller.Update(loaded, vanillaCycleMinutes,
                false, 60f, 6f, 20f, 50f);
            Ensure(loaded.SetterCalls == 2, "disable restores vanilla");
            Near(loaded.WeatherSecondsPerRealSecond, 72f, 0.001f,
                "vanilla restoration rate");
            controller.Update(loaded, vanillaCycleMinutes,
                false, 60f, 6f, 20f, 50f);
            Ensure(loaded.SetterCalls == 2,
                "disabled state does not repeat restoration");

            controller.Update(loaded, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 100f);
            Ensure(loaded.SetterCalls == 3, "re-enable applies once");
            loaded.WeatherSecondsPerRealSecond = 99f;
            controller.Update(loaded, vanillaCycleMinutes,
                true, 60f, 6f, 12f, 100f);
            Ensure(loaded.SetterCalls == 3,
                "external override is not overwritten every poll");
            controller.Update(loaded, vanillaCycleMinutes,
                false, 60f, 6f, 12f, 100f);
            Ensure(loaded.SetterCalls == 3,
                "external override blocks vanilla restoration");
            Near(loaded.WeatherSecondsPerRealSecond, 99f, 0.001f,
                "external rate remains untouched");

            Near(WorldTimescalePolicy.ClampPhaseMinutes(0f), 1f,
                0.0001f, "minimum clamp");
            Near(WorldTimescalePolicy.ClampPhaseMinutes(900f), 600f,
                0.0001f, "maximum clamp");
            Near(WorldTimescalePolicy.ClampOverrideMultiplier(0f), 0.01f,
                0.0001f, "minimum diagnostic multiplier clamp");
            Near(WorldTimescalePolicy.ClampOverrideMultiplier(10f), 5f,
                0.0001f, "maximum diagnostic multiplier clamp");
        }

        private static Awaken.TG.Main.Timing.GameRealTime Clock(
            bool night,
            float rate)
        {
            return new Awaken.TG.Main.Timing.GameRealTime
            {
                WeatherTime = new Awaken.TG.Main.Timing.TestWeatherTime
                {
                    IsNight = night
                },
                WeatherSecondsPerRealSecond = rate
            };
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
    + "using System.Globalization;" + [Environment]::NewLine `
    + "using Awaken.TG.Main.Timing;" + [Environment]::NewLine `
    + "using BepInEx.Logging;" + [Environment]::NewLine `
    + $stubsAndTests + [Environment]::NewLine `
    + ($worldTimescaleSource -replace '(?m)^using .+;\r?\n', '')
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.WorldTimescaleContractHarness]::Run()

$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
foreach ($required in @(
    '"2. World Timescale"',
    '"EnableDynamicTimescale"',
    '"DayMinutes"',
    '"BaseNightMinutes"',
    '"MaximumThreatNightMinutes"',
    'DefaultDayMinutes = 60f',
    'DefaultBaseNightMinutes = 6f',
    'DefaultMaximumThreatNightMinutes = 12f',
    '"EnableTimescaleOverride"',
    '"TimescaleOverrideMultiplier"',
    'WorldTimescalePolicy.MinimumOverrideMultiplier',
    'WorldTimescalePolicy.MaximumOverrideMultiplier',
    'UpdateWorldTimescale(nextContext);',
    'private void UpdateWorldTimescale(RuntimeContext context)')) {
    if (!$pluginSource.Contains($required)) {
        throw "Dynamic world-timescale integration is missing token: $required"
    }
}
if ($pluginSource.Contains('Time.timeScale =')) {
    throw "Eyes must not write Unity gameplay Time.timeScale."
}

Write-Host "Eyes in the Dark dynamic world-timescale contracts passed."
