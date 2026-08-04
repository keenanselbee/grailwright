$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\NightState.cs") -Raw
$tests = @'
namespace EyesInTheDark
{
    public static class NightStateContractHarness
    {
        public static void Run()
        {
            NightObservation valid = ValidObservation();
            Expect(valid, DirectorState.Roaming, InactiveReason.None, "valid outdoor Wyrd Night");
            Ensure(
                NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "valid outdoor Wyrd Night should show the threat meter, including while protected");

            valid.GameSaysNight = false;
            valid.HeroSaysNight = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.Daylight, "daylight");
            Ensure(
                !NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "daylight should hide the threat meter");

            valid = ValidObservation();
            valid.IsOutdoor = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.NotOutdoor, "interior");
            Ensure(
                !NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "interiors should hide the threat meter");

            valid = ValidObservation();
            valid.IsLoading = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.Loading, "loading");
            Ensure(
                !NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "loading should hide the threat meter");

            valid = ValidObservation();
            valid.AtTitleScreen = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.TitleScreen, "title screen");

            valid = ValidObservation();
            valid.IsTransitioning = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.Transition, "transition");

            valid = ValidObservation();
            valid.IsTraveling = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.Travel, "travel");

            valid = ValidObservation();
            valid.IsTraveling = true;
            valid.IsTransitioning = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.Travel, "travel overlapping a transition");

            valid = ValidObservation();
            valid.IsResting = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.Resting, "rest");

            valid = ValidObservation();
            valid.HeroAlive = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.HeroDead, "death");

            valid = ValidObservation();
            valid.HasPlayableHero = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.NoPlayableHero, "missing Hero");
            Ensure(
                !NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "missing Hero should hide the threat meter");

            valid = ValidObservation();
            valid.GameSaysNight = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.NightStateMismatch, "night disagreement");

            valid = ValidObservation();
            valid.SceneInitialized = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.SceneNotReady, "scene initialization");

            valid = ValidObservation();
            valid.SceneKnown = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.SceneUnknown, "unknown scene");

            valid = ValidObservation();
            valid.HasWorldTime = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.WorldTimeUnknown, "unknown world time");

            valid = ValidObservation();
            valid.HasHeroNightState = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.WyrdNightUnknown, "unknown hero night state");

            valid = ValidObservation();
            valid.AllowsWyrdNight = false;
            Expect(valid, DirectorState.Inactive, InactiveReason.WyrdNightNotAllowed, "scene disallows Wyrd Night");

            valid = ValidObservation();
            valid.IsPrologue = true;
            Expect(valid, DirectorState.Inactive, InactiveReason.WyrdNightNotAllowed, "prologue");

            valid = ValidObservation();
            Ensure(NightStateEvaluator.CanAdvanceActiveClock(valid, false), "active clock should advance in ready gameplay");
            Ensure(!NightStateEvaluator.CanAdvanceActiveClock(valid, true), "active clock must stop while paused");
            valid.IsOutdoor = false;
            Ensure(NightStateEvaluator.CanAdvanceActiveClock(valid, false), "active clock should remain available indoors");

            float start = NightStateEvaluator.NormalizeNightProgress(0.9201f, true);
            float midnight = NightStateEvaluator.NormalizeNightProgress(0f, true);
            float nearDawn = NightStateEvaluator.NormalizeNightProgress(0.2299f, true);
            Ensure(start >= 0f && start < 0.01f, "night progress should begin near zero");
            Ensure(midnight > 0.25f && midnight < 0.27f, "midnight progress should cross the wrapped day boundary");
            Ensure(nearDawn > 0.99f && nearDawn <= 1f, "night progress should end near one");
            Ensure(NightStateEvaluator.NormalizeNightProgress(0.5f, false) == 0f, "daylight has no night progress");

            ActiveRealTimeClock clock = new ActiveRealTimeClock(0.5f);
            Ensure(clock.Advance(0.25f, true), "valid active frame should advance clock");
            Ensure(!clock.Advance(0.25f, false), "suppressed frame should not advance clock");
            Ensure(!clock.Advance(1.5f, true), "long catch-up frame should be rejected");
            Ensure(System.Math.Abs(clock.Seconds - 0.25d) < 0.0001d, "clock total should contain only valid active time");
        }

        private static NightObservation ValidObservation()
        {
            return new NightObservation
            {
                HasPlayableHero = true,
                HeroAlive = true,
                AtTitleScreen = false,
                IsLoading = false,
                IsTransitioning = false,
                IsTraveling = false,
                IsResting = false,
                SceneKnown = true,
                SceneInitialized = true,
                HasWorldTime = true,
                HasHeroNightState = true,
                IsOutdoor = true,
                AllowsWyrdNight = true,
                IsPrologue = false,
                GameSaysNight = true,
                HeroSaysNight = true
            };
        }

        private static void Expect(
            NightObservation observation,
            DirectorState expectedState,
            InactiveReason expectedReason,
            string scenario)
        {
            NightStateDecision decision = NightStateEvaluator.Evaluate(observation);
            Ensure(decision.State == expectedState, scenario + " returned the wrong state");
            Ensure(decision.Reason == expectedReason, scenario + " returned the wrong reason");
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

Add-Type -TypeDefinition ($source + [Environment]::NewLine + $tests) -Language CSharp
[EyesInTheDark.NightStateContractHarness]::Run()

$pluginSource = Get-Content -LiteralPath (Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$serviceGuardIndex = $pluginSource.IndexOf("TryGet<WyrdnessService>()", [StringComparison]::Ordinal)
$sceneLifetimeIndex = $pluginSource.IndexOf("SceneLifetimeEvents.Get", [StringComparison]::Ordinal)
if ($serviceGuardIndex -lt 0 -or $sceneLifetimeIndex -lt 0 -or $serviceGuardIndex -ge $sceneLifetimeIndex) {
    throw "SceneLifetimeEvents must not be touched before the game-owned WyrdnessService readiness guard."
}

if (!$pluginSource.Contains("if (wyrdnessService == null")) {
    throw "The startup readiness guard must fail closed while WyrdnessService is unavailable."
}

$travelDecisionIndex = $source.IndexOf("if (observation.IsTraveling)", [StringComparison]::Ordinal)
$transitionDecisionIndex = $source.IndexOf("if (observation.IsTransitioning)", [StringComparison]::Ordinal)
if ($travelDecisionIndex -lt 0 -or $transitionDecisionIndex -lt 0 -or $travelDecisionIndex -ge $transitionDecisionIndex) {
    throw "Travel must take diagnostic priority over the generic transition state."
}

if ($pluginSource.Contains("Transition trace") -or $pluginSource.Contains("TraceCheckpoint")) {
    throw "Temporary high-volume transition checkpoint tracing must not remain in the normal build."
}

Write-Host "Eyes in the Dark night-state contracts passed."
