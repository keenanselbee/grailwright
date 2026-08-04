$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$catalogSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\HunterCatalog.cs") -Raw

$tests = @'
namespace EyesInTheDark
{
    public static class CuratedDirectorContractHarness
    {
        public static void Run()
        {
            Ensure(HuntRegionResolver.Resolve("CampaignMap_HOS")
                == HuntRegion.HornsOfTheSouth, "HoS scene mapping");
            Ensure(HuntRegionResolver.Resolve("CampaignMap_Cuanacht")
                == HuntRegion.Cuanacht, "Cuanacht scene mapping");
            Ensure(HuntRegionResolver.Resolve("CampaignMap_Forlorn")
                == HuntRegion.Forlorn, "Forlorn scene mapping");
            Ensure(HuntRegionResolver.Resolve("CampaignMap_Sarras")
                == HuntRegion.Sarras, "Sarras scene mapping");
            Ensure(HuntRegionResolver.Resolve("Dungeon_Unknown")
                == HuntRegion.Unknown, "Unknown regions fail closed");

            HunterCatalogDirector earlyDirector =
                new HunterCatalogDirector(11);
            HunterSelectionResult early = earlyDirector.Select(
                Context(HuntRegion.HornsOfTheSouth, 1, 100f, 100f, 3, 1f));
            Ensure(early.Success, "Early HoS pool");
            Ensure(early.Plan.Count == 1,
                "Early characters are capped at solo encounters");
            Ensure(early.Plan.Primary.Family == HunterFamily.Wyrdspirit,
                "Early characters receive only the level-safe Wyrdspirit");

            HunterSelectionResult unknown = earlyDirector.Select(
                Context(HuntRegion.Unknown, 30, 100f, 100f, 3, 1f));
            Ensure(!unknown.Success,
                "Unknown region produces an empty pool");

            AssertProfile("wyrdspirit-contact", HuntRegion.Unknown, 1, 8f,
                HunterSafetyFlags.ReviewedUniversal
                    | HunterSafetyFlags.CanBeSidecar
                    | HunterSafetyFlags.WyrdspiritCluster, 3);
            AssertProfile("corpse-eater-cuanacht", HuntRegion.Cuanacht, 15, 16f,
                HunterSafetyFlags.CanBeSidecar, 1);
            AssertProfile("mistling-cuanacht", HuntRegion.Cuanacht, 18, 18f,
                HunterSafetyFlags.CanBeSidecar, 1);
            AssertProfile("sharg-cuanacht", HuntRegion.Cuanacht, 22, 22f,
                HunterSafetyFlags.None, 1);
            AssertProfile("ogre-cuanacht", HuntRegion.Cuanacht, 26, 28f,
                HunterSafetyFlags.SoloOnly, 1);
            AssertProfile("redcap-forlorn", HuntRegion.Forlorn, 22, 18f,
                HunterSafetyFlags.CanBeSidecar, 1);
            AssertProfile("mistling-forlorn", HuntRegion.Forlorn, 26, 24f,
                HunterSafetyFlags.None, 1);
            AssertProfile("corpse-eater-forlorn", HuntRegion.Forlorn, 30, 28f,
                HunterSafetyFlags.None, 1);
            AssertProfile("wyrdspawn-sarras-t5", HuntRegion.Sarras, 28, 26f,
                HunterSafetyFlags.None, 1);
            AssertProfile("wyrdspawn-sarras-t6", HuntRegion.Sarras, 34, 32f,
                HunterSafetyFlags.None, 1);
            AssertProfile("wyrdheir-sarras", HuntRegion.Sarras, 36, 34f,
                HunterSafetyFlags.SoloOnly, 1);

            foreach (HunterProfile profile in Profiles())
            {
                if (profile.MinimumPlayerLevel <= 1)
                {
                    continue;
                }
                HunterCatalogDirector gateDirector =
                    new HunterCatalogDirector(13);
                HunterSelectionContext below = Context(
                    profile.Region,
                    profile.MinimumPlayerLevel - 1,
                    100f,
                    200f,
                    3,
                    1f);
                HunterSelectionContext exact = below;
                exact.PlayerLevel = profile.MinimumPlayerLevel;
                Ensure(HardFilter(gateDirector, profile, below)
                        == "level<" + profile.MinimumPlayerLevel,
                    profile.Id + " exact lower level gate");
                Ensure(HardFilter(gateDirector, profile, exact) == string.Empty,
                    profile.Id + " becomes eligible at its exact level");
            }

            HunterCatalogDirector strict =
                new HunterCatalogDirector(19);
            for (int index = 0; index < 200; index++)
            {
                HunterSelectionResult selected = strict.Select(
                    Context(HuntRegion.Cuanacht, 30, 70f, 100f, 1, 0f));
                Ensure(selected.Success, "Cuanacht selection");
                Ensure(selected.Plan.Primary.IsUniversal
                        || selected.Plan.Primary.Region
                            == HuntRegion.Cuanacht,
                    "Cuanacht cannot import another regional profile");
            }

            foreach (HuntRegion region in new[]
            {
                HuntRegion.Forlorn,
                HuntRegion.Sarras
            })
            {
                HunterSelectionResult regional = strict.Select(
                    Context(region, 40, 100f, 200f, 3, 1f));
                Ensure(regional.Success,
                    region + " regional pool");
                for (int member = 0;
                    member < regional.Plan.Members.Count;
                    member++)
                {
                    HunterProfile profile = regional.Plan.Members[member];
                    Ensure(profile.IsUniversal || profile.Region == region,
                        region + " uses only universal or native profiles");
                }
            }

            int lowStrong = 0;
            int highStrong = 0;
            HunterCatalogDirector lowDirector =
                new HunterCatalogDirector(29);
            HunterCatalogDirector highDirector =
                new HunterCatalogDirector(29);
            for (int index = 0; index < 3000; index++)
            {
                if (lowDirector.Select(Context(
                    HuntRegion.HornsOfTheSouth,
                    30,
                    0f,
                    100f,
                    1,
                    0f)).Plan.Primary.Tier > 1)
                {
                    lowStrong++;
                }
                if (highDirector.Select(Context(
                    HuntRegion.HornsOfTheSouth,
                    30,
                    100f,
                    100f,
                    1,
                    0f)).Plan.Primary.Tier > 1)
                {
                    highStrong++;
                }
            }
            Ensure(highStrong > lowStrong,
                "High threat raises stronger-candidate probability");
            Ensure(lowStrong > 0,
                "Strong eligible candidates retain nonzero weight at low threat");
            Ensure(highStrong < 3000,
                "High threat does not force the strongest candidate");

            HunterCatalogDirector history =
                new HunterCatalogDirector(41);
            string previous = string.Empty;
            int immediateRepeats = 0;
            for (int index = 0; index < 300; index++)
            {
                HuntEncounterPlan plan = history.Select(Context(
                    HuntRegion.HornsOfTheSouth,
                    30,
                    55f,
                    100f,
                    1,
                    0f)).Plan;
                if (plan.Primary.Id == previous)
                {
                    immediateRepeats++;
                }
                previous = plan.Primary.Id;
                history.RecordConfirmed(plan);
            }
            Ensure(immediateRepeats < 75,
                "Immediate profile and family repeats are strongly reduced");

            HunterCatalogDirector rejection =
                new HunterCatalogDirector(47);
            rejection.RecordFailure("wyrdspirit-contact");
            rejection.RecordFailure("wyrdspirit-contact");
            rejection.RecordFailure("wyrdspirit-contact");
            Ensure(rejection.IsSessionRejected("wyrdspirit-contact"),
                "Three failures reject a template for the session");
            Ensure(!rejection.Select(Context(
                HuntRegion.HornsOfTheSouth,
                1,
                100f,
                100f,
                3,
                1f)).Success,
                "Repeated failure can safely empty a regional pool");

            HunterCatalogDirector packs =
                new HunterCatalogDirector(53);
            HunterSelectionResult mid = packs.Select(Context(
                HuntRegion.Forlorn,
                10,
                100f,
                100f,
                3,
                1f));
            Ensure(mid.Plan.Count <= 2,
                "Mid-level pack safety cap");
            bool foundCluster = false;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                HuntEncounterPlan plan = packs.Select(Context(
                    HuntRegion.Forlorn,
                    30,
                    100f,
                    100f,
                    3,
                    1f)).Plan;
                if (plan.Count == 3)
                {
                    foundCluster = true;
                    for (int member = 0;
                        member < plan.Members.Count;
                        member++)
                    {
                        Ensure(plan.Members[member].Family
                                == HunterFamily.Wyrdspirit,
                            "Wyrdspirit cluster contains only Wyrdspirits");
                    }
                    break;
                }
            }
            Ensure(foundCluster,
                "Late-level Wyrdspirit cluster can reach three members");

            HunterSelectionResult exactBudget = packs.Select(Context(
                HuntRegion.Forlorn,
                30,
                100f,
                8f,
                3,
                1f));
            Ensure(exactBudget.Success && exactBudget.Plan.Count == 1,
                "Budget caps a plan before sidecars");
            Ensure(!packs.Select(Context(
                HuntRegion.Forlorn,
                30,
                100f,
                7.9f,
                3,
                1f)).Success,
                "Insufficient budget safely empties the pool");

            HunterSelectionContext multipliedContext = Context(
                HuntRegion.Forlorn,
                30,
                50f,
                100f,
                1,
                0f);
            multipliedContext.DangerCostMultiplier = 1.5f;
            HuntEncounterPlan multiplied = packs.Select(
                multipliedContext).Plan;
            Near(multiplied.DangerCost, 12f,
                "Danger cost multiplier");

            HunterCatalogDirector invariants =
                new HunterCatalogDirector(61);
            HuntRegion[] regions =
            {
                HuntRegion.HornsOfTheSouth,
                HuntRegion.Cuanacht,
                HuntRegion.Forlorn,
                HuntRegion.Sarras
            };
            for (int sample = 0; sample < 4000; sample++)
            {
                HuntRegion region = regions[sample % regions.Length];
                int level = 1 + sample % 35;
                float budget = sample % 61;
                int requestedCap = 1 + sample % 3;
                HunterSelectionContext context = Context(
                    region,
                    level,
                    sample % 101,
                    budget,
                    requestedCap,
                    (sample % 11) / 10f);
                context.DangerCostMultiplier = 0.5f
                    + (sample % 16) / 10f;
                HunterSelectionResult result = invariants.Select(context);
                if (!result.Success)
                {
                    continue;
                }

                HuntEncounterPlan plan = result.Plan;
                Ensure(plan.DangerCost <= budget + 0.001f,
                    "Composition never exceeds remaining budget");
                int levelCap = level < 8 ? 1 : level < 15 ? 2 : 3;
                Ensure(plan.Count <= requestedCap
                        && plan.Count <= levelCap,
                    "Composition obeys configured and level caps");

                var copies = new System.Collections.Generic.Dictionary<string, int>();
                for (int member = 0;
                    member < plan.Members.Count;
                    member++)
                {
                    HunterProfile profile = plan.Members[member];
                    Ensure(profile.IsUniversal || profile.Region == region,
                        "Every member obeys strict region eligibility");
                    Ensure(level >= profile.MinimumPlayerLevel,
                        "Every member obeys its player-level gate");
                    if (member > 0)
                    {
                        Ensure(profile.Tier <= plan.Primary.Tier,
                            "Sidecars are not stronger than the primary");
                    }

                    int count;
                    copies.TryGetValue(profile.Id, out count);
                    count++;
                    copies[profile.Id] = count;
                    Ensure(count <= profile.MaximumCopies,
                        "Composition obeys per-profile copy caps");
                }
            }
        }

        private static HunterSelectionContext Context(
            HuntRegion region,
            int level,
            float threat,
            float budget,
            int maximumPackSize,
            float sidecarChance)
        {
            return new HunterSelectionContext
            {
                Region = region,
                PlayerLevel = level,
                Threat = threat,
                RemainingBudget = budget,
                DangerCostMultiplier = 1f,
                SidecarChance = sidecarChance,
                MaximumPackSize = maximumPackSize
            };
        }

        private static HunterProfile[] Profiles()
        {
            return (HunterProfile[])typeof(HunterCatalogDirector)
                .GetField("Profiles",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static)
                .GetValue(null);
        }

        private static HunterProfile Profile(string id)
        {
            foreach (HunterProfile profile in Profiles())
            {
                if (profile.Id == id)
                {
                    return profile;
                }
            }
            throw new System.InvalidOperationException(
                "Missing profile " + id);
        }

        private static void AssertProfile(
            string id,
            HuntRegion region,
            int minimumLevel,
            float dangerCost,
            HunterSafetyFlags safetyFlags,
            int maximumCopies)
        {
            HunterProfile profile = Profile(id);
            Ensure(profile.Region == region, id + " region");
            Ensure(profile.MinimumPlayerLevel == minimumLevel,
                id + " minimum player level");
            Near(profile.DangerCost, dangerCost, id + " danger cost");
            Ensure(profile.SafetyFlags == safetyFlags,
                id + " role/safety flags");
            Ensure(profile.MaximumCopies == maximumCopies,
                id + " one-copy/cluster limit");
        }

        private static string HardFilter(
            HunterCatalogDirector director,
            HunterProfile profile,
            HunterSelectionContext context)
        {
            return (string)typeof(HunterCatalogDirector)
                .GetMethod("HardFilterReason",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance)
                .Invoke(director, new object[]
                {
                    profile,
                    context,
                    1f,
                    false
                });
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
    + "using System.Collections.Generic;" + [Environment]::NewLine `
    + "using System.Globalization;" + [Environment]::NewLine `
    + ($catalogSource -replace '(?m)^using .+;\r?\n', '') `
    + [Environment]::NewLine `
    + $tests
Add-Type -TypeDefinition $source -Language CSharp
[EyesInTheDark.CuratedDirectorContractHarness]::Run()

$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$runtimeSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\FirstHunterRuntime.cs") -Raw

foreach ($required in @(
    'GameplayTuningPreset.UneasyNight',
    'GameplayTuningPreset.WatchfulNight',
    'GameplayTuningPreset.CursedNight',
    '_gameplayPreset.Value = GameplayTuningPreset.Custom;',
    'new ConfigDefinition(',
    '"2. Gameplay Preset"',
    '"ApplyPreset"',
    'selection.FilterSummary',
    'selection.WeightSummary',
    'plan.DescribeComposition()',
    'ShowDiagnosticSystem(')) {
    if (!$pluginSource.Contains($required)) {
        throw "Curated director integration is missing token: $required"
    }
}

if ($catalogSource.Contains('ProgressionTier') -or
    $catalogSource.Contains('MinimumProgressionTier') -or
    $pluginSource.Contains('ProgressionTier')) {
    throw "Dormant progression-tier plumbing remains in the curated director."
}

foreach ($required in @(
    'private readonly List<SpawnedMember> _members',
    'MinimumMemberSeparationMeters',
    'CleanupLiveLocations();',
    'QueueEvent(',
    'HunterRuntimeEventKind.PlacementConfirmed',
    'ReleaseReferences(false);')) {
    if (!$runtimeSource.Contains($required)) {
        throw "Atomic encounter runtime is missing token: $required"
    }
}

if (!$pluginSource.Contains('_pacing.TrySpend(') -or
    !$pluginSource.Contains('confirmedPlan.DangerCost') -or
    !$pluginSource.Contains('_pacing.Refund(_activeHuntDangerCost)')) {
    throw "Exact composition-cost spending and refund behavior is incomplete."
}

Write-Host "Eyes in the Dark curated director contracts passed."
