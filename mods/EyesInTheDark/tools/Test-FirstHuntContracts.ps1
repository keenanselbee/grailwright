$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$directorSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\HuntDirector.cs") -Raw

$tests = @'
namespace EyesInTheDark
{
    internal enum DirectorState
    {
        Inactive,
        Roaming,
        Warning,
        ActiveHunt,
        Recovery
    }

    public static class FirstHuntContractHarness
    {
        public static void Run()
        {
            HuntTuning tuning = Tuning();
            HuntFrame eligible = Frame(75f, 0.5f, 30f);

            HuntDirector protectedDirector = new HuntDirector(11);
            HuntFrame protectedFrame = eligible;
            protectedFrame.IsProtected = true;
            protectedFrame.IsExposed = false;
            protectedDirector.Tick(protectedFrame, tuning);
            Near(protectedDirector.HazardPressure, 0f,
                "Protected hazard suspension");

            HuntDirector noBudgetDirector = new HuntDirector(11);
            HuntFrame noBudget = eligible;
            noBudget.RemainingDangerBudget = 9f;
            noBudgetDirector.Tick(noBudget, tuning);
            Near(noBudgetDirector.HazardPressure, 0f,
                "Insufficient-budget hazard suspension");

            HuntDirector low = new HuntDirector(17);
            HuntDirector high = new HuntDirector(17);
            low.Tick(Frame(10f, 0.5f, 30f), tuning);
            high.Tick(Frame(90f, 0.5f, 30f), tuning);
            Ensure(high.HazardPressure > low.HazardPressure,
                "Higher threat must accumulate hazard faster");

            HuntDirector director = new HuntDirector(23);
            HuntDirective warning = default(HuntDirective);
            for (int index = 0; index < 20; index++)
            {
                warning = director.Tick(eligible, tuning);
                if (warning.Kind == HuntDirectiveKind.WarningCommitted)
                {
                    break;
                }
            }
            Ensure(warning.Kind == HuntDirectiveKind.WarningCommitted,
                "Accumulated hazard commits one warning");
            Ensure(director.State == DirectorState.Warning,
                "Warning state");

            HuntFrame combat = eligible;
            combat.HeroInUnrelatedCombat = true;
            HuntDirective cancelled = director.Tick(combat, tuning);
            Ensure(cancelled.Kind == HuntDirectiveKind.WarningCancelled,
                "Unrelated combat cancels pre-placement warning");
            Ensure(director.State == DirectorState.Roaming,
                "Cancelled warning returns to roaming");

            director = new HuntDirector(29);
            while (director.Tick(eligible, tuning).Kind
                != HuntDirectiveKind.WarningCommitted)
            {
            }
            HuntDirective placement = director.Tick(eligible, tuning);
            while (placement.Kind != HuntDirectiveKind.RequestPlacement)
            {
                placement = director.Tick(eligible, tuning);
            }
            director.ConfirmPlacement();
            Ensure(director.State == DirectorState.ActiveHunt,
                "Confirmed placement creates one active hunt");
            Ensure(director.Tick(eligible, tuning).Kind
                == HuntDirectiveKind.None,
                "Active hunt cannot commit another hunt");

            director.Resolve(HuntResolution.HunterKilled, tuning);
            Ensure(director.State == DirectorState.Recovery,
                "Kill enters recovery");
            Near(director.RecoveryRemainingSeconds, 90f,
                "Kill recovery");

            director.ConfirmPlacement();
            director.Resolve(HuntResolution.Escaped, tuning);
            Near(director.RecoveryRemainingSeconds, 180f,
                "Escape recovery is longer");
            Ensure(director.RecoveryRemainingSeconds > 90f,
                "Escape residual pursuit exceeds kill recovery");

            director.FailPlacement(tuning);
            Ensure(director.LastResolution
                == HuntResolution.PlacementFailed,
                "Failed placement outcome");
            Near(director.RecoveryRemainingSeconds, 30f,
                "Failed placement retry protection");
        }

        private static HuntTuning Tuning()
        {
            return new HuntTuning
            {
                BaseHazardPerMinute = 0.01f,
                ThreatHazardPerMinute = 0.42f,
                NightProgressHazardPerMinute = 0.08f,
                MinimumHazardTarget = 0.85f,
                MaximumHazardTarget = 1.15f,
                WarningSeconds = 6f,
                KillRecoverySeconds = 90f,
                EscapeRecoverySeconds = 180f,
                FailedPlacementRecoverySeconds = 30f,
                HunterDangerCost = 10f
            };
        }

        private static HuntFrame Frame(
            float threat,
            float progress,
            float budget)
        {
            return new HuntFrame
            {
                IsValidWyrdNight = true,
                IsExposed = true,
                IsProtected = false,
                HeroInUnrelatedCombat = false,
                CanAdvance = true,
                ActiveSeconds = 60f,
                Threat = threat,
                NightProgress = progress,
                RemainingDangerBudget = budget
            };
        }

        private static void Near(
            float actual,
            float expected,
            string message)
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

$source = "using System;" + [Environment]::NewLine `
    + ($directorSource -replace '(?m)^using .+;\r?\n', '') `
    + [Environment]::NewLine `
    + $tests
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.FirstHuntContractHarness]::Run()

$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\FirstHunterRuntime.cs") -Raw
$catalogSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\HunterCatalog.cs") -Raw
$pacingSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\PacingState.cs") -Raw
$threatSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatState.cs") -Raw
$gftSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GrailFloatingTextBridge.cs") -Raw
$providerSource = Get-Content -LiteralPath (
    Join-Path $modRoot "..\GrailFloatingText\src\GrailFloatingText.cs") -Raw

foreach ($required in @(
    '843643575fa01ba4292e60afb9291fea',
    'Spec_EnemyMonster_T1_Wyrdspirit',
    'a32e5074492cce34f89ff0667fdb41b7',
    '1a41678c288c2264c8bcfad7a6eb3ba3',
    '324e9b5ed131ce34eb12a520cdb2b52a',
    '3f7d4ccf62c440b40b1fca822ef6ac1b')) {
    if (!$catalogSource.Contains($required)) {
        throw "Curated hunter catalog is missing reviewed identity: $required"
    }
}

foreach ($required in @(
    'BaseLocationSpawner.VerifyPosition(',
    'template.SpawnLocation(',
    'location.MarkedNotSaved = true',
    'wyrdness.IsInRepeller(verified)',
    'hero.HasElement<PacifistMarker>()',
    '.Npc.NpcAI.EnterCombatWith(hero)',
    '.Npc.NpcAI.EnterCombatWith(hero, true)',
    'member.Npc.NpcAI.InCombat',
    'ReferenceEquals(member.Npc.GetCurrentTarget(), hero)',
    'native combat entry did not acquire the exact Hero target',
    'ReacquisitionIntervalSeconds = 2f',
    'ReacquisitionDistanceMeters = 60f',
    'MaximumReacquisitionAttemptsPerMember = 3',
    'member.ReacquisitionAttempts++',
    'ReferenceEquals(npc, _members[0].Npc)')) {
    if (!$runtimeSource.Contains($required)) {
        throw "First hunter runtime is missing safety token: $required"
    }
}

if (!$pluginSource.Contains('case HunterRuntimeEventKind.PlacementConfirmed:') -or
    !$pluginSource.Contains('_pacing.TrySpend(')) {
    throw "Danger budget spending is not gated by placement confirmation."
}
if (!$pluginSource.Contains('HuntResolution.LostTarget') -or
    !$pluginSource.Contains('_pacing.Refund(')) {
    throw "Invalid active-target handling does not restore its danger cost."
}
if (!$pacingSource.Contains('public bool TrySpend(') -or
    !$threatSource.Contains('OfficialHunterKilled') -or
    !$threatSource.Contains('HunterEscaped')) {
    throw "Pacing spend/refund or differentiated threat relief is incomplete."
}
if (!$gftSource.Contains('warning ? "Warning" : "Wyrd"') -or
    !$pluginSource.Contains('"eyes-in-the-dark-hunt"')) {
    throw "Committed-hunt GFT Warning presentation is incomplete."
}
foreach ($required in @(
    'ScanEyesInTheDarkCompatibility();',
    '"kane.tgfoa.wyrd-hunt"',
    '"ks.tgfoa.eyes-in-the-dark"',
    '"Wyrd Hunt is flagged as incompatible with Eyes in the Dark."',
    '"DeathWrench.TimeMod"',
    '"TimeMod"',
    '"Custom Timescale is flagged as incompatible with Eyes in the Dark."',
    '"OnMainMenu"')) {
    if (!$providerSource.Contains($required)) {
        throw "GFT soft incompatibility convention is missing token: $required"
    }
}
if ($pluginSource.Contains('kane.tgfoa.wyrd-hunt') -or
    $runtimeSource.Contains('kane.tgfoa.wyrd-hunt')) {
    throw "Eyes in the Dark must not implement its own Wyrd Hunt scanner."
}

Write-Host "Eyes in the Dark first official hunt contracts passed."
