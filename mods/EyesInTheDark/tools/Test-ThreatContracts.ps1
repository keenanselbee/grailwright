$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\ThreatState.cs") -Raw
$tests = @'
namespace EyesInTheDark
{
    public static class ThreatContractHarness
    {
        public static void Run()
        {
            TestTimescaleInvariantPassiveThreat();
            TestLoadReconstructionAndGrace();
            TestProtectedAndInteriorDecay();
            TestInteriorExitGraceAndDawnReset();
            TestSuppressedProgressDoesNotCatchUp();
            TestStages();
            TestDiagnosticThreatOverride();
            TestSourceThrottlesAndRepeatProtection();
        }

        private static void TestTimescaleInvariantPassiveThreat()
        {
            float atOne = SimulateFullNight(1.0f);
            float atHalf = SimulateFullNight(0.5f);
            float atQuarter = SimulateFullNight(0.25f);
            float atTenth = SimulateFullNight(0.1f);
            EnsureNear(atOne, 20f, 0.01f, "vanilla-timescale passive baseline");
            EnsureNear(atHalf, atOne, 0.01f, "0.5 timescale passive baseline");
            EnsureNear(atQuarter, atOne, 0.01f, "0.25 timescale passive baseline");
            EnsureNear(atTenth, atOne, 0.01f, "0.1 timescale passive baseline");
        }

        private static float SimulateFullNight(float worldTimescale)
        {
            ThreatState state = new ThreatState();
            ThreatTuning tuning = Tuning();
            tuning.LoadReconstructionAtDawn = 0f;
            tuning.GraceSeconds = 0f;
            state.Advance(NightFrame(0f, true, false, 0f), tuning);

            int steps = (int)System.Math.Round(100f / worldTimescale);
            for (int index = 1; index <= steps; index++)
            {
                float progress = (float)index / steps;
                state.Advance(
                    NightFrame(progress, true, false, 0.2f),
                    tuning);
            }
            return state.Value;
        }

        private static void TestLoadReconstructionAndGrace()
        {
            ThreatState state = new ThreatState();
            ThreatUpdateResult result = state.Advance(
                NightFrame(0.5f, true, false, 0.2f),
                Tuning());
            Ensure(result.Cause == ThreatChangeCause.LoadReconstruction, "load should reconstruct threat");
            EnsureNear(state.Value, 4f, 0.001f, "midnight reconstruction");
            EnsureNear(state.GraceRemainingSeconds, 15f, 0.001f, "load grace");
            Ensure(!state.CanAcceptActivity, "activity must be blocked during load grace");

            state.Advance(NightFrame(0.5f, true, false, 15f), Tuning());
            Ensure(state.CanAcceptActivity, "activity should resume after active-time grace");
        }

        private static void TestProtectedAndInteriorDecay()
        {
            ThreatState protectedState = SeedTenThreat();
            protectedState.Advance(NightFrame(0.5f, true, true, 30f), Tuning());
            EnsureNear(protectedState.Value, 8f, 0.001f, "protected outdoor decay");
            Ensure(!protectedState.CanAcceptActivity, "protected activity must be rejected");

            ThreatState interiorState = SeedTenThreat();
            interiorState.Advance(NightFrame(0.5f, false, false, 60f), Tuning());
            EnsureNear(interiorState.Value, 9f, 0.001f, "interior active-real-time decay");
            Ensure(!interiorState.CanAcceptActivity, "interior activity must be rejected");
        }

        private static ThreatState SeedTenThreat()
        {
            ThreatState state = new ThreatState();
            ThreatTuning tuning = Tuning();
            tuning.LoadReconstructionAtDawn = 0f;
            tuning.GraceSeconds = 0f;
            state.Advance(NightFrame(0f, true, false, 0f), tuning);
            state.Advance(NightFrame(0.5f, true, false, 1f), tuning);
            EnsureNear(state.Value, 10f, 0.001f, "seed threat");
            return state;
        }

        private static void TestInteriorExitGraceAndDawnReset()
        {
            ThreatState state = SeedTenThreat();
            state.Advance(NightFrame(0.5f, false, false, 1f), Tuning());
            state.Advance(NightFrame(0.5f, true, false, 1f), Tuning());
            Ensure(state.GraceRemainingSeconds > 13.9f, "leaving an interior should start grace");
            Ensure(!state.CanAcceptActivity, "interior-exit grace should suppress activity");

            ThreatFrame dawn = new ThreatFrame
            {
                IsKnownDaylight = true,
                CanAdvanceActiveTime = true,
                ActiveSeconds = 1f
            };
            ThreatUpdateResult result = state.Advance(dawn, Tuning());
            Ensure(result.Cause == ThreatChangeCause.DawnReset, "dawn reset cause");
            Ensure(state.Value == 0f, "dawn should reset threat");
            Ensure(state.Stage == ThreatStage.Unnoticed, "dawn should reset stage");
            Ensure(state.GraceRemainingSeconds == 0f, "dawn should reset grace");
        }

        private static void TestStages()
        {
            Ensure(ThreatState.GetStage(0f) == ThreatStage.Unnoticed, "zero stage");
            Ensure(ThreatState.GetStage(24.9f) == ThreatStage.Unnoticed, "unnoticed upper bound");
            Ensure(ThreatState.GetStage(25f) == ThreatStage.Watched, "watched lower bound");
            Ensure(ThreatState.GetStage(50f) == ThreatStage.Hunted, "hunted lower bound");
            Ensure(ThreatState.GetStage(75f) == ThreatStage.Marked, "marked lower bound");
            Ensure(ThreatState.GetStage(100f) == ThreatStage.Marked, "marked upper bound");
        }

        private static void TestSuppressedProgressDoesNotCatchUp()
        {
            ThreatState state = SeedTenThreat();
            ThreatFrame suppressed = NightFrame(0.5f, true, false, 0f);
            suppressed.IsValidWyrdNight = false;
            suppressed.CanAdvanceActiveTime = false;
            state.Advance(suppressed, Tuning());
            state.Advance(NightFrame(0.8f, true, false, 0.2f), Tuning());
            EnsureNear(state.Value, 10f, 0.001f, "transition progress must not add catch-up threat");
        }

        private static void TestDiagnosticThreatOverride()
        {
            ThreatState state = SeedTenThreat();
            ThreatUpdateResult forced = state.SetDiagnosticOverride(true, 72f);
            Ensure(forced.Cause == ThreatChangeCause.DiagnosticOverride,
                "override cause");
            EnsureNear(state.Value, 72f, 0.001f, "forced threat");
            Ensure(state.DiagnosticOverrideActive, "override active state");

            state.Advance(NightFrame(0.8f, true, true, 60f), Tuning());
            EnsureNear(state.Value, 72f, 0.001f,
                "natural gain and decay are suppressed by override");
            state.AddActivity(20f, ThreatChangeCause.Combat);
            state.Reduce(20f, ThreatChangeCause.OfficialHunterKilled);
            EnsureNear(state.Value, 72f, 0.001f,
                "activity and relief are suppressed by override");

            state.SetDiagnosticOverride(true, 1000f);
            EnsureNear(state.Value, 100f, 0.001f, "override upper clamp");
            state.SetDiagnosticOverride(false, 0f);
            Ensure(!state.DiagnosticOverrideActive, "override disabled state");

            ThreatFrame dawn = new ThreatFrame
            {
                IsKnownDaylight = true,
                CanAdvanceActiveTime = true,
                ActiveSeconds = 1f
            };
            state.Advance(dawn, Tuning());
            EnsureNear(state.Value, 0f, 0.001f, "dawn still resets override threat");
        }

        private static void TestSourceThrottlesAndRepeatProtection()
        {
            ThreatActivityLimiter limiter = new ThreatActivityLimiter();
            Ensure(limiter.AdvanceMovement(true, 14.9f, 4f) == 0f, "movement must be sustained");
            EnsureNear(limiter.AdvanceMovement(true, 0.1f, 4f), 1f, 0.001f, "movement interval gain");
            limiter.AdvanceMovement(true, 5f, 4f);
            limiter.AdvanceMovement(false, 0.1f, 4f);
            Ensure(limiter.AdvanceMovement(true, 10f, 4f) == 0f, "movement interruption should reset accumulation");

            Ensure(limiter.RecordCombat(1f, "dealt:npc", 10d), "first combat event");
            Ensure(!limiter.RecordCombat(1f, "dealt:npc", 10.05d), "immediate duplicate combat event");
            Ensure(limiter.RecordCombat(1f, "dealt:other", 10.1d), "different combat event");
            Ensure(limiter.FlushCombat(11.49d, 2f, 1.5f) == 0f, "combat should wait for its configured response window");
            EnsureNear(limiter.FlushCombat(11.5d, 2f, 1.5f), 2f, 0.001f, "combat window cap");

            Ensure(limiter.RecordAcquisition(0.75f, "item-a", 20d), "first acquisition");
            Ensure(!limiter.RecordAcquisition(0.75f, "item-a", 21d), "same item cannot farm acquisition threat");
            Ensure(limiter.RecordAcquisition(0.75f, "item-b", 21d), "second unique acquisition");
            Ensure(limiter.RecordAcquisition(0.75f, "item-c", 22d), "third unique acquisition");
            Ensure(limiter.RecordAcquisition(0.75f, "item-d", 23d), "fourth unique acquisition");
            Ensure(limiter.RecordAcquisition(0.75f, "item-e", 24d), "fifth unique acquisition");
            EnsureNear(limiter.FlushAcquisition(25d, 3f), 3f, 0.001f, "bulk acquisition cap");

            Ensure(limiter.RecordWyrdKill("wyrd-a"), "first Wyrd kill");
            Ensure(!limiter.RecordWyrdKill("wyrd-a"), "same Wyrd kill cannot repeat");
            Ensure(limiter.RecordWyrdKill("wyrd-b"), "different Wyrd kill");
        }

        private static ThreatFrame NightFrame(
            float progress,
            bool outdoor,
            bool protectedArea,
            float activeSeconds)
        {
            return new ThreatFrame
            {
                IsKnownDaylight = false,
                IsValidWyrdNight = true,
                IsOutdoor = outdoor,
                IsProtected = protectedArea,
                CanAdvanceActiveTime = true,
                NightProgress = progress,
                ActiveSeconds = activeSeconds
            };
        }

        private static ThreatTuning Tuning()
        {
            return new ThreatTuning
            {
                PassiveThreatPerNight = 20f,
                ProtectedDecayPerMinute = 4f,
                InteriorDecayPerMinute = 1f,
                LoadReconstructionAtDawn = 8f,
                GraceSeconds = 15f
            };
        }

        private static void EnsureNear(float actual, float expected, float tolerance, string message)
        {
            Ensure(System.Math.Abs(actual - expected) <= tolerance, message + ": expected=" + expected + ", actual=" + actual);
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
[EyesInTheDark.ThreatContractHarness]::Run()

$meterSource = Get-Content -LiteralPath (Join-Path $modRoot "src\ThreatMeter.cs") -Raw
if (!$meterSource.Contains("VCHeroHealthBar")) {
    throw "The standalone Wyrd Threat meter must anchor from the vanilla health bar."
}
if (!$meterSource.Contains("topRect.anchoredPosition - spacing")) {
    throw "The standalone Wyrd Threat meter must occupy a row above the top vanilla resource bar."
}
if (!$meterSource.Contains("TryMirrorVisuals(") -or
    !$meterSource.Contains("mirrorHorizontally ? -1f : 1f") -or
    !$meterSource.Contains("mirrorVertically ? -1f : 1f")) {
    throw "The Wyrd Threat meter must use horizontally and vertically mirrored artwork."
}

$pluginSource = Get-Content -LiteralPath (Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
if (!$pluginSource.Contains("public static class EyesInTheDarkHudApi") -or
    !$pluginSource.Contains("RequestBelowVanillaBars(") -or
    !$pluginSource.Contains("_placeMeterBelowResourceBars")) {
    throw "The versioned Eyes-owned meter placement contract is incomplete."
}
if (!$pluginSource.Contains('PluginName = "Eyes in the Dark";')) {
    throw "The BepInEx and config title must be Eyes in the Dark."
}
foreach ($required in @(
    'ICharacter.Events.OnAttackStart',
    'ICharacter.Events.HitEnvironment',
    '_environmentImpactSeenThisAttack',
    'maximum * 0.5f',
    'CombatResponseSeconds',
    '"environment:" + ModelId(data.Item)')) {
    if (!$pluginSource.Contains($required)) {
        throw "Confirmed environment-impact threat is missing: $required"
    }
}
foreach ($required in @(
    'ICharacter.Events.OnFiredProjectile',
    'ICharacter.Events.CastingEnded',
    'QueueRangedActionThreat(',
    'sourceWeapon.IsMagic',
    '"ranged-action:" + ModelId(item)',
    'maximum * 0.25f')) {
    if (!$pluginSource.Contains($required)) {
        throw "Confirmed ranged or spell-use threat is missing: $required"
    }
}
$attackStart = $pluginSource.IndexOf(
    'private void OnAttackStarted(',
    [StringComparison]::Ordinal)
$environmentHit = $pluginSource.IndexOf(
    'private void OnEnvironmentHit(',
    $attackStart,
    [StringComparison]::Ordinal)
if ($attackStart -lt 0 -or $environmentHit -le $attackStart -or
    $pluginSource.Substring($attackStart, $environmentHit - $attackStart).Contains('RecordCombat(')) {
    throw "Melee attack start must not add threat before a confirmed hit."
}
foreach ($required in @(
    'ThreatMeterColor',
    'ThreatMeterController.DefaultColorText',
    '"EnableThreatOverride"',
    '"ThreatOverrideValue"',
    'SetDiagnosticOverride(')) {
    if (!$pluginSource.Contains($required)) {
        throw "Configurable threat-meter color is missing: $required"
    }
}
foreach ($required in @(
    'public const string DefaultColorText = "#8032FF";',
    'ColorUtility.TryParseHtmlString(',
    'BrightnessMultiplier = 1.5f;',
    'WyrdVisualMath.ShiftTowardRed(')) {
    if (!$meterSource.Contains($required)) {
        throw "Threat-meter color application is missing: $required"
    }
}

Write-Host "Eyes in the Dark threat, source, timescale, and meter-placement contracts passed."
