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
            Expect(valid, DirectorState.Roaming, InactiveReason.None, "valid outdoor Wyrdnight");
            Ensure(
                NightStateEvaluator.ShouldShowThreatMeter(
                    NightStateEvaluator.Evaluate(valid)),
                "valid outdoor Wyrdnight should show the threat meter, including while protected");

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
            Ensure(NightStateEvaluator.IsActiveWyrdnightPhaseForRest(valid),
                "Open rest UI must not hide the current Wyrdnight phase from rest safety");
            Ensure(!NightStateEvaluator.CanBeginRest(true, false, valid, false),
                "Active Eyes Wyrdnight blocks unprotected rest by default");
            Ensure(NightStateEvaluator.CanBeginRest(true, false, valid, true),
                "Native safe-rest protection allows Wyrdnight rest");
            Ensure(NightStateEvaluator.CanBeginRest(true, true, valid, false),
                "The explicit option allows unprotected Wyrdnight rest");
            Ensure(NightStateEvaluator.CanBeginRest(false, false, valid, false),
                "Disabled Eyes leaves native rest behavior unchanged");
            Ensure(!NightStateEvaluator.IsStableAfterRest(valid),
                "Rest UI is not a stable post-rest context");
            valid.IsResting = false;
            Ensure(NightStateEvaluator.IsStableAfterRest(valid),
                "Ready gameplay is a stable post-rest context");

            valid.GameSaysNight = false;
            valid.HeroSaysNight = false;
            Ensure(NightStateEvaluator.CanBeginRest(true, false, valid, false),
                "Daylight rest remains available outside protection");

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
            Expect(valid, DirectorState.Inactive, InactiveReason.WyrdNightNotAllowed, "scene disallows Wyrdnight");

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

if ($pluginSource -notmatch '(?s)"AllowUnprotectedWyrdnightRest",\s*true,\s*UiDescription') {
    throw "Unprotected Wyrdnight rest must default to enabled for Watchful tuning."
}

$travelDecisionIndex = $source.IndexOf("if (observation.IsTraveling)", [StringComparison]::Ordinal)
$transitionDecisionIndex = $source.IndexOf("if (observation.IsTransitioning)", [StringComparison]::Ordinal)
if ($travelDecisionIndex -lt 0 -or $transitionDecisionIndex -lt 0 -or $travelDecisionIndex -ge $transitionDecisionIndex) {
    throw "Travel must take diagnostic priority over the generic transition state."
}

if ($pluginSource.Contains("Transition trace") -or $pluginSource.Contains("TraceCheckpoint")) {
    throw "Temporary high-volume transition checkpoint tracing must not remain in the normal build."
}

foreach ($required in @(
    "PatchRest()",
    "AccessTools.PropertyGetter(",
    "typeof(HeroDevelopment)",
    "nameof(HeroDevelopment.CanRest)",
    "AfterCanRest",
    "CanUseNativeRest",
    "ApplyRestInterruptionRisk",
    "ShouldSuppressNativeWyrdnightSurprise",
    '"OwnRestMenu"',
    '"RestInterruptionChanceAtZeroThreat"',
    '"RestInterruptionChanceAtMaximumThreat"',
    "typeof(VFireplaceUI)",
    "typeof(VWyrdRepellingFireplaceUI)",
    "nameof(VWyrdRepellingFireplaceUI.RefreshActions)",
    "AfterFireplaceInitialize",
    "AfterFireplaceRefresh",
    "AfterFireplaceDiscard",
    "RefreshActiveRestAvailability",
    "restButton.Interactable = interactable",
    '"AllowUnprotectedWyrdnightRest"',
    "wyrdnessService.IsInRepeller(hero.Coords)",
    "NightStateEvaluator.CanBeginRest(",
    "restPopup.IsSafelyResting",
    "restPopup.Close();",
    "_restAtmosphereReconciliationPending",
    "TryCompleteRestAtmosphereReconciliation(",
    "slept-through transitions suppressed")) {
    if (!$pluginSource.Contains($required)) {
        throw "Wyrdnight rest integration is missing contract token: $required"
    }
}

foreach ($retired in @(
    "NotifyRestBlockedOnce",
    "_restBlockNoticeShown",
    "FancyPanelType.Custom.Spawn",
    "You can rest during a Wyrdnight only within a protective boundary.")) {
    if ($pluginSource.Contains($retired)) {
        throw "Retired blocked-rest warning behavior remains: $retired"
    }
}

Write-Host "Eyes in the Dark night-state contracts passed."
