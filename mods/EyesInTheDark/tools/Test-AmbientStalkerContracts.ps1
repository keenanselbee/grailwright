$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$directorSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\AmbientStalkerDirector.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\AmbientStalkerRuntime.cs") -Raw
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$huntSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\HuntDirector.cs") -Raw
$atmosphereSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\Atmosphere.cs") -Raw
$threatSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\ThreatState.cs") -Raw
$manifestSource = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw

$stubs = @'
namespace EyesInTheDark
{
    internal enum HuntRegion
    {
        Unknown,
        HornsOfTheSouth,
        Cuanacht,
        Forlorn,
        Sarras
    }

    internal enum HunterFamily
    {
        Wyrdspirit,
        Redcap,
        CorpseEater,
        Mistling,
        Flamegobbler,
        Grindylow,
        Skeleton,
        Zombie,
        Swarm,
        Sharg,
        Ogre,
        LostKnight,
        Slugholder,
        Barnaclator,
        Nuckelavee,
        Bonemask,
        Frostbitten,
        Finbled,
        Drowned,
        Tadpole,
        Wailcap,
        Tidewraith
    }

}
'@

$tests = @'
namespace EyesInTheDark
{
    public static class AmbientStalkerContractHarness
    {
        public static void Run()
        {
            AssertBandsAndThresholds();
            AssertCooldowns();
            AssertMovementAndPursuit();
            AssertCameraAndCleanup();
            AssertDirectorExclusivity();
            AssertRosterAndFailureGates();
        }

        private static void AssertBandsAndThresholds()
        {
            AmbientStalkerBand band;
            Ensure(AmbientStalkerPolicy.TryResolveBand(0f, false, out band)
                && band == AmbientStalkerBand.Ordinary,
                "Zero threat ordinary band");
            Ensure(AmbientStalkerPolicy.TryResolveBand(49.999f, false, out band)
                && band == AmbientStalkerBand.Ordinary,
                "Below 50 ordinary band");
            Ensure(!AmbientStalkerPolicy.TryResolveBand(50f, false, out band),
                "High-pressure band requires elite permission");
            Ensure(AmbientStalkerPolicy.TryResolveBand(50f, true, out band)
                && band == AmbientStalkerBand.HighPressure,
                "Threat 50 high-pressure band");
            Ensure(AmbientStalkerPolicy.TryResolveBand(74.999f, true, out band)
                && band == AmbientStalkerBand.HighPressure,
                "Below 75 high-pressure band");
            Ensure(!AmbientStalkerPolicy.TryResolveBand(75f, true, out band),
                "Threat 75 ends new ambient selection");

            Near(AmbientStalkerPolicy.AggressionThreshold(
                    AmbientStalkerBand.Ordinary, 0d),
                45f, "Ordinary aggression minimum");
            Near(AmbientStalkerPolicy.AggressionThreshold(
                    AmbientStalkerBand.Ordinary, 1d),
                55f, "Ordinary aggression maximum");
            Near(AmbientStalkerPolicy.AggressionThreshold(
                    AmbientStalkerBand.HighPressure, 0d),
                70f, "High-pressure aggression minimum");
            Near(AmbientStalkerPolicy.AggressionThreshold(
                    AmbientStalkerBand.HighPressure, 1d),
                80f, "High-pressure aggression maximum");
            Ensure(AmbientStalkerPolicy.ShouldEscalate(
                    48f, 48f, false, false),
                "Threat threshold escalates");
            Ensure(!AmbientStalkerPolicy.ShouldEscalate(
                    47.9f, 48f, false, false),
                "Threat below threshold remains passive");
            Ensure(AmbientStalkerPolicy.ShouldEscalate(
                    0f, 55f, true, false),
                "Exact Hero damage escalates immediately");
            Ensure(!AmbientStalkerPolicy.ShouldEscalate(
                    100f, 45f, true, true),
                "Already-hostile escalation is one-shot");
        }

        private static void AssertCooldowns()
        {
            AmbientStalkerTuning tuning = new AmbientStalkerTuning
            {
                Enabled = true,
                MinimumCooldownSeconds = 55f,
                MaximumCooldownSeconds = 165f,
                MaximumCooldownAtFiftyThreatSeconds = 70f
            };
            Near(AmbientStalkerPolicy.CooldownUpperBound(0f, tuning),
                165f, "Cooldown upper bound at zero threat");
            Near(AmbientStalkerPolicy.CooldownUpperBound(25f, tuning),
                117.5f, "Cooldown interpolation at 25 threat");
            Near(AmbientStalkerPolicy.CooldownUpperBound(50f, tuning),
                70f, "Cooldown upper bound at 50 threat");
            Near(AmbientStalkerPolicy.CooldownUpperBound(75f, tuning),
                70f, "Cooldown floor beyond 50 threat");

            tuning.MaximumCooldownAtFiftyThreatSeconds = 10f;
            Near(AmbientStalkerPolicy.CooldownUpperBound(50f, tuning),
                55f, "Cooldown upper bound clamps to minimum");
        }

        private static void AssertMovementAndPursuit()
        {
            Ensure(AmbientStalkerPolicy.ShouldFleeFromApproach(
                    25f, 0.9f, 3f, 2f, 0.8f),
                "Deliberate closing pursuit triggers flee");
            Ensure(!AmbientStalkerPolicy.ShouldFleeFromApproach(
                    25f, -0.2f, 3f, 2f, 0.8f),
                "Running away does not trigger flee");
            Ensure(!AmbientStalkerPolicy.ShouldFleeFromApproach(
                    25f, 0.9f, 1f, 2f, 0.8f),
                "Slow approach does not trigger flee");
            Ensure(AmbientStalkerPolicy.NextPassiveMovementMode(
                    AmbientMovementMode.Observe, false, false, true)
                    == AmbientMovementMode.Follow,
                "Observe transitions to Follow");
            Ensure(AmbientStalkerPolicy.NextPassiveMovementMode(
                    AmbientMovementMode.Follow, true, false, false)
                    == AmbientMovementMode.Flee,
                "Pursuit transitions to Flee");
            Ensure(AmbientStalkerPolicy.NextPassiveMovementMode(
                    AmbientMovementMode.Flee, false, true, false)
                    == AmbientMovementMode.Observe,
                "Completed flee returns to Observe");
            Ensure(AmbientStalkerPolicy.ShouldEscalateFromClosePursuit(
                    AmbientMovementMode.Flee, 8f),
                "Closing to the defensive boundary while it flees escalates");
            Ensure(!AmbientStalkerPolicy.ShouldEscalateFromClosePursuit(
                    AmbientMovementMode.Flee, 8.01f),
                "A fleeing stalker remains passive outside its defensive boundary");
            Ensure(!AmbientStalkerPolicy.ShouldEscalateFromClosePursuit(
                    AmbientMovementMode.Follow, 4f),
                "Ordinary safe-distance following does not self-escalate");
        }

        private static void AssertCameraAndCleanup()
        {
            Ensure(AmbientStalkerPolicy.IsViewportPointVisible(
                    0.5f, 0.5f, 10f, 0f),
                "Positive-depth center is visible");
            Ensure(!AmbientStalkerPolicy.IsViewportPointVisible(
                    0.5f, 0.5f, -1f, 0f),
                "Behind-camera point is hidden");
            Ensure(!AmbientStalkerPolicy.IsViewportPointVisible(
                    1.1f, 0.5f, 10f, 0.04f),
                "Point beyond camera margin is hidden");
            Ensure(AmbientStalkerPolicy.IsViewportPointVisible(
                    1.03f, 0.5f, 10f, 0.04f),
                "Point inside camera margin is visible");

            Ensure(AmbientStalkerPolicy.CanPassivelyDespawn(
                    false, false, 2.5f, 2.5f, 70f, 65f, true, false),
                "Seen passive stalker can clean up off-camera at distance");
            Ensure(!AmbientStalkerPolicy.CanPassivelyDespawn(
                    true, false, 99f, 2.5f, 200f, 65f, true, true),
                "Hostile stalker never uses atmospheric cleanup");
            Ensure(!AmbientStalkerPolicy.CanPassivelyDespawn(
                    false, true, 99f, 2.5f, 200f, 65f, true, true),
                "Visible stalker cannot clean up");
            Ensure(!AmbientStalkerPolicy.CanPassivelyDespawn(
                    false, false, 2.5f, 2.5f, 60f, 65f, true, false),
                "Nearby stalker cannot clean up");
            Ensure(!AmbientStalkerPolicy.CanPassivelyDespawn(
                    false, false, 2.5f, 2.5f, 70f, 65f, false, false),
                "Unseen unexpired stalker cannot clean up");
        }

        private static void AssertDirectorExclusivity()
        {
            AmbientStalkerTuning tuning = new AmbientStalkerTuning
            {
                Enabled = true,
                MinimumCooldownSeconds = 1f,
                MaximumCooldownSeconds = 1f,
                MaximumCooldownAtFiftyThreatSeconds = 1f
            };
            AmbientStalkerFrame frame = EligibleFrame();
            AmbientStalkerDirector director =
                new AmbientStalkerDirector(7);
            AmbientStalkerDirective request = director.Tick(frame, tuning);
            Ensure(request.Kind
                    == AmbientStalkerDirectiveKind.RequestPlacement,
                "Eligible elapsed cooldown requests placement");

            frame = EligibleFrame();
            frame.OfficialEncounterLaneBusy = true;
            Ensure(new AmbientStalkerDirector(7).Tick(frame, tuning).Kind
                    == AmbientStalkerDirectiveKind.None,
                "Official lane excludes ambient request");
            frame = EligibleFrame();
            frame.RuntimeBusy = true;
            Ensure(new AmbientStalkerDirector(7).Tick(frame, tuning).Kind
                    == AmbientStalkerDirectiveKind.None,
                "Existing ambient runtime excludes another request");
            frame = EligibleFrame();
            frame.IsProtected = true;
            Ensure(new AmbientStalkerDirector(7).Tick(frame, tuning).Kind
                    == AmbientStalkerDirectiveKind.None,
                "Protected area excludes ambient request");
        }

        private static void AssertRosterAndFailureGates()
        {
            var field = typeof(AmbientStalkerCatalogDirector).GetField(
                "Profiles",
                System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);
            AmbientStalkerProfile[] profiles =
                (AmbientStalkerProfile[])field.GetValue(null);
            Ensure(profiles.Length == 33,
                "Broad roster contains 26 ordinary and 7 high-pressure profiles");
            var ids = new System.Collections.Generic.HashSet<string>();
            var guids = new System.Collections.Generic.HashSet<string>();
            int ordinary = 0;
            int highPressure = 0;
            foreach (AmbientStalkerProfile profile in profiles)
            {
                Ensure(ids.Add(profile.Id), profile.Id + " unique id");
                Ensure(guids.Add(profile.TemplateGuid),
                    profile.Id + " unique reviewed template");
                Ensure(profile.TemplateGuid.Length == 32,
                    profile.Id + " GUID length");
                Ensure(profile.MinimumPlayerLevel >= 1,
                    profile.Id + " level gate");
                foreach (string blocked in new[]
                {
                    "Boss", "Friendly", "Summon", "Challenge", "Trial",
                    "Story", "Custom", "Arena", "Flamegobbler", "Swarm",
                    "Skeleton", "Ogre", "Barnaclator", "Nuckelavee"
                })
                {
                    Ensure(profile.TemplateName.IndexOf(
                            blocked,
                            System.StringComparison.OrdinalIgnoreCase) < 0,
                        profile.Id + " excludes unsafe " + blocked);
                }
                if (profile.Band == AmbientStalkerBand.Ordinary)
                {
                    ordinary++;
                }
                else
                {
                    highPressure++;
                }
            }
            Ensure(ordinary == 26, "Ordinary roster count");
            Ensure(highPressure == 7, "High-pressure roster count");

            AmbientStalkerCatalogDirector early =
                new AmbientStalkerCatalogDirector(11);
            AmbientStalkerSelection first = early.Select(
                Context(HuntRegion.HornsOfTheSouth, 1, 20f, false));
            Ensure(first.Success
                    && first.Profile.Id == "wyrdspirit-stalker",
                "Level-one universal fallback");
            AmbientStalkerSelection unknown = early.Select(
                Context(HuntRegion.Unknown, 99, 20f, false));
            Ensure(!unknown.Success, "Unknown region fails closed");
            AmbientStalkerSelection lockedHigh = early.Select(
                Context(HuntRegion.HornsOfTheSouth, 99, 60f, false));
            Ensure(!lockedHigh.Success,
                "High-pressure roster requires elite toggle");
            AmbientStalkerSelection sharg = early.Select(
                Context(HuntRegion.HornsOfTheSouth, 15, 60f, true));
            Ensure(sharg.Success
                    && sharg.Profile.Id == "sharg-stalker-hos",
                "HoS high-pressure Sharg level gate");
            AmbientStalkerSelection lostKnight = early.Select(
                Context(HuntRegion.Cuanacht, 20, 60f, true));
            Ensure(lostKnight.Success
                    && lostKnight.Profile.Id
                        == "lost-knight-stalker-cuanacht",
                "Cuanacht Lost Knight level gate");
            AmbientStalkerSelection finbled = early.Select(
                Context(HuntRegion.Sarras, 30, 60f, true));
            Ensure(finbled.Success
                    && finbled.Profile.Id
                        == "finbled-heavy-stalker-sarras",
                "Sarras high-pressure Finbled level gate");

            AmbientStalkerCatalogDirector failures =
                new AmbientStalkerCatalogDirector(3);
            failures.RecordFailure("wyrdspirit-stalker");
            failures.RecordFailure("wyrdspirit-stalker");
            failures.RecordFailure("wyrdspirit-stalker");
            Ensure(failures.IsSessionRejected("wyrdspirit-stalker"),
                "Three placement failures reject a profile for the session");
            Ensure(!failures.Select(Context(
                    HuntRegion.HornsOfTheSouth, 1, 20f, false)).Success,
                "Rejected level-one pool fails closed");

            AmbientStalkerCatalogDirector recovery =
                new AmbientStalkerCatalogDirector(5);
            recovery.RecordFailure("wyrdspirit-stalker");
            recovery.RecordFailure("wyrdspirit-stalker");
            recovery.RecordConfirmed(first.Profile);
            recovery.RecordFailure("wyrdspirit-stalker");
            Ensure(!recovery.IsSessionRejected("wyrdspirit-stalker"),
                "Confirmed placement clears prior failure count");
        }

        private static AmbientStalkerFrame EligibleFrame()
        {
            return new AmbientStalkerFrame
            {
                IsValidWyrdNight = true,
                IsExposed = true,
                IsProtected = false,
                HeroInCombat = false,
                OfficialEncounterLaneBusy = false,
                RuntimeBusy = false,
                AllowHighPressure = false,
                CanAdvance = true,
                ActiveSeconds = 2f,
                Threat = 20f
            };
        }

        private static AmbientStalkerSelectionContext Context(
            HuntRegion region,
            int level,
            float threat,
            bool allowHighPressure)
        {
            return new AmbientStalkerSelectionContext
            {
                Region = region,
                PlayerLevel = level,
                Threat = threat,
                AllowHighPressure = allowHighPressure
            };
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
    + "using System.Collections.Generic;" + [Environment]::NewLine `
    + "using System.Globalization;" + [Environment]::NewLine
$source = $usings `
    + $stubs `
    + [Environment]::NewLine `
    + ($directorSource -replace '(?m)^using .+;\r?\n', '') `
    + [Environment]::NewLine `
    + $tests
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.AmbientStalkerContractHarness]::Run()

foreach ($required in @(
    "BaseLocationSpawner.VerifyPosition",
    "HasConnectedPath(",
    "MarkedNotSaved = true",
    "IsLocationVisible(",
    "GetComponentsInChildren<Renderer>",
    "private Renderer[] _renderers",
    ".GetComponentsInChildren<Renderer>();",
    "Bounds bounds = renderer.bounds;",
    "new Vector3(min.x, min.y, min.z)",
    "camera.WorldToViewportPoint(bounds.center)",
    "new Observe()",
    "new FollowMovement(",
    "new Flee(hero)",
    "FleeRearmSeconds",
    "PassiveObserveDistanceMeters",
    "AmbientStalkerEscalationCause.ClosePursuit",
    "BlockEnterCombatMarker",
    "HideEnemyFromPlayer",
    "HealthElement.Events.BeforeDamageTaken",
    "ReferenceEquals(target, _npc)",
    "the exact ambient stalker was already hostile",
    "verifiedDistance < minimumDistance",
    "ReleasePassiveGuards()",
    "native combat AI",
    "never make a hostile actor disappear")) {
    if (!$runtimeSource.Contains($required)) {
        throw "Ambient runtime is missing contract token: $required"
    }
}

if ($runtimeSource.Contains("new Vector3[]")) {
    throw "Ambient visibility recreates a corner array in the steady-state camera check"
}

foreach ($forbidden in @(
    "SetFaction",
    "OverrideFaction",
    "Time.timeScale =",
    "TrySpend(",
    "OfficialHunterKilled")) {
    if ($runtimeSource.Contains($forbidden)) {
        throw "Ambient runtime contains forbidden ownership/budget token: $forbidden"
    }
}

foreach ($required in @(
    "EncounterLaneBusy = _stalkerRuntime.IsBusy",
    "OfficialEncounterLaneBusy =",
    "TryProvoke(",
    "ThreatChangeCause.StalkerProvoked",
    "huntBudgetSpent=0",
    "officialRelief=0",
    '"eyes-in-the-dark-stalker"',
    "stalkerEvent",
    "!stalkerEvent")) {
    if (!$pluginSource.Contains($required)) {
        throw "Eyes integration is missing contract token: $required"
    }
}

foreach ($required in @(
    "EncounterLaneBusy",
    "ambient stalker lane busy")) {
    if (!$huntSource.Contains($required)) {
        throw "Official director exclusivity is missing token: $required"
    }
}

foreach ($required in @(
    "StalkerSighted",
    "StalkerRetreated",
    "StalkerVanished",
    "StalkerProvoked",
    "StalkerAwakened")) {
    if (!$atmosphereSource.Contains($required)) {
        throw "Ambient atmosphere is missing token: $required"
    }
}

if (!$threatSource.Contains("StalkerProvoked")) {
    throw "Threat causes do not include StalkerProvoked"
}
if (!$pluginSource.Contains("private const int ConfigSchemaVersion = 22;")) {
    throw "Eyes config schema is not at the clock-and-config UX reset boundary"
}
foreach ($required in @(
    '"5. Ambient Stalkers"',
    '"EnableAmbientStalkers"',
    '"ProvocationThreat"',
    '"PassiveDespawnDistanceMeters"',
    '"OffCameraDespawnSeconds"')) {
    if (!$pluginSource.Contains($required)) {
        throw "Ambient config/preservation is missing token: $required"
    }
}
foreach ($required in @(
    '"src/AmbientStalkerDirector.cs"',
    '"src/AmbientStalkerRuntime.cs"',
    "AstarPathfindingProject.dll",
    "PackageTools.dll",
    "Drawing.dll")) {
    if (!$manifestSource.Contains($required)) {
        throw "Ambient manifest is missing token: $required"
    }
}

Write-Host "Eyes in the Dark ambient stalker contracts passed."
