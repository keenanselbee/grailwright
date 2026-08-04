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
    public static class WorldTimescaleContractHarness
    {
        public static void Run()
        {
            const float vanillaCycleMinutes = 20f;
            Near(WorldTimescalePolicy.PhaseMinutes(
                vanillaCycleMinutes, 0.23f, false), 60f, 0.01f,
                "0.23 day duration");
            Near(WorldTimescalePolicy.PhaseMinutes(
                vanillaCycleMinutes, 0.413f, true), 15f, 0.02f,
                "0.413 night duration");
            float cycle = WorldTimescalePolicy.PhaseMinutes(
                    vanillaCycleMinutes, 0.23f, false)
                + WorldTimescalePolicy.PhaseMinutes(
                    vanillaCycleMinutes, 0.413f, true);
            Near(cycle, 75f, 0.03f, "default complete cycle");

            var log = new BepInEx.Logging.ManualLogSource();
            var controller = new WorldTimescaleController(log);
            var first = Clock(false, 72f);
            controller.Update(first, vanillaCycleMinutes,
                true, 0.23f, 0.413f);
            Ensure(first.SetterCalls == 1, "initial day apply");
            Near(first.WeatherSecondsPerRealSecond, 16.56f, 0.001f,
                "day weather rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 0.23f, 0.413f);
            Ensure(first.SetterCalls == 1,
                "unchanged state never repeats the setter");

            first.WeatherTime = new Awaken.TG.Main.Timing.TestWeatherTime
            {
                IsNight = true
            };
            controller.Update(first, vanillaCycleMinutes,
                true, 0.23f, 0.413f);
            Ensure(first.SetterCalls == 2, "nightfall apply");
            Near(first.WeatherSecondsPerRealSecond, 29.736f, 0.001f,
                "night weather rate");

            controller.Update(first, vanillaCycleMinutes,
                true, 0.23f, 0.5f);
            Ensure(first.SetterCalls == 3, "live night config apply");
            Near(first.WeatherSecondsPerRealSecond, 36f, 0.001f,
                "live night config rate");

            var loaded = Clock(true, 72f);
            controller.Update(loaded, vanillaCycleMinutes,
                true, 0.23f, 0.5f);
            Ensure(loaded.SetterCalls == 1, "new clock/load reapply");

            controller.Update(loaded, vanillaCycleMinutes,
                false, 0.23f, 0.5f);
            Ensure(loaded.SetterCalls == 2, "disable restores vanilla");
            Near(loaded.WeatherSecondsPerRealSecond, 72f, 0.001f,
                "vanilla restoration rate");
            controller.Update(loaded, vanillaCycleMinutes,
                false, 0.23f, 0.5f);
            Ensure(loaded.SetterCalls == 2,
                "disabled state does not repeat restoration");

            controller.Update(loaded, vanillaCycleMinutes,
                true, 0.23f, 0.413f);
            Ensure(loaded.SetterCalls == 3, "re-enable applies once");
            loaded.WeatherSecondsPerRealSecond = 99f;
            controller.Update(loaded, vanillaCycleMinutes,
                true, 0.23f, 0.413f);
            Ensure(loaded.SetterCalls == 3,
                "external override is not overwritten every poll");
            controller.Update(loaded, vanillaCycleMinutes,
                false, 0.23f, 0.413f);
            Ensure(loaded.SetterCalls == 3,
                "external override blocks vanilla restoration");
            Near(loaded.WeatherSecondsPerRealSecond, 99f, 0.001f,
                "external rate remains untouched");

            Near(WorldTimescalePolicy.ClampMultiplier(0f), 0.01f,
                0.0001f, "minimum clamp");
            Near(WorldTimescalePolicy.ClampMultiplier(9f), 5f,
                0.0001f, "maximum clamp");
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
    '"DayTimescale"',
    '"NightTimescale"',
    'DefaultDayTimescale = 0.23f',
    'DefaultNightTimescale = 0.413f',
    'UpdateWorldTimescale();')) {
    if (!$pluginSource.Contains($required)) {
        throw "Dynamic world-timescale integration is missing token: $required"
    }
}
if ($pluginSource.Contains('Time.timeScale =')) {
    throw "Eyes must not write Unity gameplay Time.timeScale."
}

Write-Host "Eyes in the Dark dynamic world-timescale contracts passed."
