using System;
using System.Collections.Generic;
using System.Globalization;

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

    internal enum GameplayTuningPreset
    {
        Custom,
        UneasyNight,
        WatchfulNight,
        CursedNight
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

    [Flags]
    internal enum HunterSafetyFlags
    {
        None = 0,
        ReviewedUniversal = 1,
        CanBeSidecar = 2,
        WyrdspiritCluster = 4,
        SoloOnly = 8,
        Elite = 16
    }

    internal sealed class HunterProfile
    {
        public readonly string Id;
        public readonly string TemplateGuid;
        public readonly string TemplateName;
        public readonly string DisplayName;
        public readonly HunterFamily Family;
        public readonly HuntRegion Region;
        public readonly int Tier;
        public readonly int MinimumPlayerLevel;
        public readonly float DangerCost;
        public readonly float PrimaryWeight;
        public readonly float SidecarWeight;
        public readonly int MaximumCopies;
        public readonly HunterSafetyFlags SafetyFlags;

        public bool IsUniversal
        {
            get
            {
                return (SafetyFlags
                    & HunterSafetyFlags.ReviewedUniversal) != 0;
            }
        }

        public bool CanBeSidecar
        {
            get
            {
                return (SafetyFlags
                    & HunterSafetyFlags.CanBeSidecar) != 0;
            }
        }

        public bool IsElite
        {
            get
            {
                return (SafetyFlags
                    & HunterSafetyFlags.Elite) != 0;
            }
        }

        public HunterProfile(
            string id,
            string templateGuid,
            string templateName,
            string displayName,
            HunterFamily family,
            HuntRegion region,
            int tier,
            int minimumPlayerLevel,
            float dangerCost,
            float primaryWeight,
            float sidecarWeight,
            int maximumCopies,
            HunterSafetyFlags safetyFlags)
        {
            Id = id;
            TemplateGuid = templateGuid;
            TemplateName = templateName;
            DisplayName = displayName;
            Family = family;
            Region = region;
            Tier = tier;
            MinimumPlayerLevel = minimumPlayerLevel;
            DangerCost = dangerCost;
            PrimaryWeight = primaryWeight;
            SidecarWeight = sidecarWeight;
            MaximumCopies = maximumCopies;
            SafetyFlags = safetyFlags;
        }
    }

    internal sealed class HuntEncounterPlan
    {
        private readonly List<HunterProfile> _members;

        public HunterProfile Primary
        {
            get { return _members[0]; }
        }

        public IList<HunterProfile> Members
        {
            get { return _members.AsReadOnly(); }
        }

        public float DangerCost { get; private set; }

        public int Count
        {
            get { return _members.Count; }
        }

        public HuntEncounterPlan(HunterProfile primary)
        {
            _members = new List<HunterProfile> { primary };
            DangerCost = primary.DangerCost;
        }

        public void AddSidecar(HunterProfile profile)
        {
            _members.Add(profile);
            DangerCost += profile.DangerCost;
        }

        public void ApplyCostMultiplier(float multiplier)
        {
            DangerCost *= multiplier;
        }

        public string DescribeComposition()
        {
            string[] names = new string[_members.Count];
            for (int index = 0; index < _members.Count; index++)
            {
                names[index] = _members[index].DisplayName;
            }
            return string.Join(" + ", names);
        }

        public bool ContainsProfile(string profileId)
        {
            for (int index = 0; index < _members.Count; index++)
            {
                if (string.Equals(
                    _members[index].Id,
                    profileId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal struct HunterSelectionContext
    {
        public HuntRegion Region;
        public int PlayerLevel;
        public float Threat;
        public float RemainingBudget;
        public float DangerCostMultiplier;
        public float SidecarChance;
        public int MaximumPackSize;
        public bool AllowEliteEnemies;
    }

    internal sealed class HunterSelectionResult
    {
        public readonly HuntEncounterPlan Plan;
        public readonly string FilterSummary;
        public readonly string WeightSummary;
        public readonly string Reason;

        public bool Success
        {
            get { return Plan != null; }
        }

        public HunterSelectionResult(
            HuntEncounterPlan plan,
            string filterSummary,
            string weightSummary,
            string reason)
        {
            Plan = plan;
            FilterSummary = filterSummary ?? string.Empty;
            WeightSummary = weightSummary ?? string.Empty;
            Reason = reason ?? string.Empty;
        }
    }

    internal static class HuntRegionResolver
    {
        public static HuntRegion Resolve(string sceneName)
        {
            if (string.Equals(
                sceneName,
                "CampaignMap_HOS",
                StringComparison.Ordinal))
            {
                return HuntRegion.HornsOfTheSouth;
            }
            if (string.Equals(
                sceneName,
                "CampaignMap_Cuanacht",
                StringComparison.Ordinal))
            {
                return HuntRegion.Cuanacht;
            }
            if (string.Equals(
                sceneName,
                "CampaignMap_Forlorn",
                StringComparison.Ordinal))
            {
                return HuntRegion.Forlorn;
            }
            if (string.Equals(
                sceneName,
                "CampaignMap_Sarras",
                StringComparison.Ordinal))
            {
                return HuntRegion.Sarras;
            }
            return HuntRegion.Unknown;
        }

        public static string ShortName(HuntRegion region)
        {
            switch (region)
            {
                case HuntRegion.HornsOfTheSouth:
                    return "HoS";
                case HuntRegion.Cuanacht:
                    return "Cuanacht";
                case HuntRegion.Forlorn:
                    return "Forlorn";
                case HuntRegion.Sarras:
                    return "Sarras";
                default:
                    return "Unknown";
            }
        }
    }

    internal sealed class HunterCatalogDirector
    {
        public const float MinimumBaseDangerCost = 8f;
        public const float EliteThreatThreshold = 75f;
        private const int FailureRejectionCount = 3;
        private const int HistoryLimit = 4;

        private static readonly HunterProfile[] Profiles =
        {
            new HunterProfile(
                "wyrdspirit-contact",
                "843643575fa01ba4292e60afb9291fea",
                "Spec_EnemyMonster_T1_Wyrdspirit",
                "Wyrdspirit",
                HunterFamily.Wyrdspirit,
                HuntRegion.Unknown,
                1,
                1,
                8f,
                1.15f,
                1.25f,
                3,
                HunterSafetyFlags.ReviewedUniversal
                    | HunterSafetyFlags.CanBeSidecar
                    | HunterSafetyFlags.WyrdspiritCluster),
            new HunterProfile(
                "flamegobbler-hos",
                "b6bf58d3c36663048bc83341ff0111d2",
                "Spec_EnemyMonster_T1_Flamegobbler",
                "Flamegobbler",
                HunterFamily.Flamegobbler,
                HuntRegion.HornsOfTheSouth,
                1,
                4,
                9f,
                1.05f,
                0.8f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "grindylow-hos",
                "fa79aaa0bff59484dab2cf35c5ea805c",
                "Spec_EnemyMonster_T1_Grindylow",
                "Grindylow",
                HunterFamily.Grindylow,
                HuntRegion.HornsOfTheSouth,
                1,
                5,
                10f,
                1f,
                0.75f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "redcap-hos",
                "a32e5074492cce34f89ff0667fdb41b7",
                "Spec_EnemyMonster_T1_Redcap",
                "Redcap",
                HunterFamily.Redcap,
                HuntRegion.HornsOfTheSouth,
                1,
                4,
                10f,
                1.0f,
                0.75f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "corpse-eater-hos",
                "1a41678c288c2264c8bcfad7a6eb3ba3",
                "Spec_EnemyMonster_T1_CorpseEater",
                "Corpse Eater",
                HunterFamily.CorpseEater,
                HuntRegion.HornsOfTheSouth,
                1,
                7,
                12f,
                0.9f,
                0.6f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "zombie-hos",
                "1d110a8ec95ab1745a364562ec311e50",
                "Spec_EnemyZombie_T1_Classic",
                "Wandering Dead",
                HunterFamily.Zombie,
                HuntRegion.HornsOfTheSouth,
                1,
                6,
                10f,
                0.95f,
                0.7f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "drowner-hos",
                "bb613531c5d3bf5499ea3b8103a4024e",
                "Spec_EnemyZombie_T1_Drowner",
                "Drowner",
                HunterFamily.Zombie,
                HuntRegion.HornsOfTheSouth,
                1,
                7,
                11f,
                0.9f,
                0.65f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "skeleton-hos",
                "386190b9f098e414c8d88472306aaad8",
                "Spec_EnemySkeleton_Melee1H",
                "Restless Skeleton",
                HunterFamily.Skeleton,
                HuntRegion.HornsOfTheSouth,
                2,
                8,
                13f,
                0.85f,
                0.55f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "mistling-hos",
                "db4a2490470378f49be51ff8848541e9",
                "Spec_EnemyMonster_T2_Mistling_Hos",
                "Mistling",
                HunterFamily.Mistling,
                HuntRegion.HornsOfTheSouth,
                2,
                10,
                14f,
                0.85f,
                0.45f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "bee-swarm-hos",
                "82af26ad1bbfd5f42a4c3c26c70978f2",
                "Spec_EnemyMonster_T2_Swarm_Bees",
                "Wyrd Bee Swarm",
                HunterFamily.Swarm,
                HuntRegion.HornsOfTheSouth,
                2,
                10,
                12f,
                0.7f,
                0.45f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "sharg-hos",
                "324e9b5ed131ce34eb12a520cdb2b52a",
                "Spec_EnemyMonster_T2_ShargHoS",
                "Sharg",
                HunterFamily.Sharg,
                HuntRegion.HornsOfTheSouth,
                2,
                15,
                18f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.Elite),
            new HunterProfile(
                "ogre-hos",
                "3f7d4ccf62c440b40b1fca822ef6ac1b",
                "Spec_EnemyMonster_T3_Ogre",
                "Ogre",
                HunterFamily.Ogre,
                HuntRegion.HornsOfTheSouth,
                3,
                20,
                24f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.SoloOnly),
            new HunterProfile(
                "corpse-eater-cuanacht",
                "ec6ee283175f87240b6f292697bc9d9c",
                "Spec_EnemyMonster_T3_CorpseEater_Cuanacht",
                "Cuanacht Corpse Eater",
                HunterFamily.CorpseEater,
                HuntRegion.Cuanacht,
                3,
                15,
                16f,
                0.95f,
                0.45f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "flamegobbler-cuanacht",
                "3ee77eec66700c04bbd68559c54ab196",
                "Spec_EnemyMonster_T3_FlamegobblerCuanacht",
                "Cuanacht Flamegobbler",
                HunterFamily.Flamegobbler,
                HuntRegion.Cuanacht,
                3,
                15,
                16f,
                0.9f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "grindylow-cuanacht",
                "1b0f005502932a54cbc99e4376837125",
                "Spec_EnemyMonster_T3_Grindylow_Cuanacht",
                "Cuanacht Grindylow",
                HunterFamily.Grindylow,
                HuntRegion.Cuanacht,
                3,
                15,
                18f,
                0.9f,
                0.35f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "redcap-cuanacht",
                "2f0d374f6ac405648adc3b610d305a61",
                "Spec_EnemyMonster_T3_Redcap_Cuanacht",
                "Cuanacht Redcap",
                HunterFamily.Redcap,
                HuntRegion.Cuanacht,
                3,
                15,
                17f,
                0.9f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "zombie-cuanacht",
                "cb4b4e390f0134049ade6d9aa0680787",
                "Spec_EnemyZombie_T3_ZombieCuanacht",
                "Cuanacht Dead",
                HunterFamily.Zombie,
                HuntRegion.Cuanacht,
                3,
                16,
                16f,
                0.85f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "mistling-cuanacht",
                "f673e122b6f7e984fab5758d91f84031",
                "Spec_EnemyMonster_T3_Mistling_Cuanacht",
                "Cuanacht Mistling",
                HunterFamily.Mistling,
                HuntRegion.Cuanacht,
                3,
                20,
                18f,
                0.9f,
                0.35f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "skeleton-cuanacht",
                "bc13b5942a2d42746b7d5d08a5932146",
                "Spec_EnemyMonster_T3_Skeleton2H_Cuanacht",
                "Cuanacht Greatsword Skeleton",
                HunterFamily.Skeleton,
                HuntRegion.Cuanacht,
                3,
                20,
                20f,
                0.8f,
                0.3f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "drowner-cuanacht",
                "66f8b8c379a0b64449781232dcbebf70",
                "Spec_EnemyZombie_T3_DrownerCuanacht",
                "Cuanacht Drowner",
                HunterFamily.Zombie,
                HuntRegion.Cuanacht,
                3,
                20,
                20f,
                0.8f,
                0.3f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "lost-knight-cuanacht",
                "4b7066c81a33ff94fb304721a5bc306d",
                "Spec_EnemyMonster_T3_LostKnight",
                "Cuanacht Lost Knight",
                HunterFamily.LostKnight,
                HuntRegion.Cuanacht,
                3,
                20,
                22f,
                0.7f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "slugholder-mage-cuanacht",
                "169a81f342550d245abea12ab926bb49",
                "Spec_EnemyMonster_T3_SlugholderMage",
                "Slugholder Mage",
                HunterFamily.Slugholder,
                HuntRegion.Cuanacht,
                3,
                20,
                22f,
                0.7f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "sharg-cuanacht",
                "a6893b6bbb474aa4aa359fc1cfab3aa8",
                "Spec_EnemyMonster_T4_ShargCuanacht",
                "Cuanacht Sharg",
                HunterFamily.Sharg,
                HuntRegion.Cuanacht,
                4,
                30,
                30f,
                0.75f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "barnaclator-cuanacht",
                "2f4941a56985c754489a90fe424a1429",
                "Spec_EnemyMonster_T4_Barnaclator",
                "Barnaclator",
                HunterFamily.Barnaclator,
                HuntRegion.Cuanacht,
                4,
                30,
                28f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "nuckelavee-cuanacht",
                "4c9fcf93446d40c4daa0d41c6ec43759",
                "Spec_EnemyMonster_T4_Nuckelavee",
                "Nuckelavee",
                HunterFamily.Nuckelavee,
                HuntRegion.Cuanacht,
                4,
                30,
                30f,
                0.6f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "ogre-cuanacht",
                "4203075aa3e840144a18c7c6dee46ed6",
                "Spec_EnemyMonster_T4_Ogre_Cuanacht",
                "Cuanacht Ogre",
                HunterFamily.Ogre,
                HuntRegion.Cuanacht,
                4,
                26,
                28f,
                0.6f,
                0f,
                1,
                HunterSafetyFlags.SoloOnly),
            new HunterProfile(
                "redcap-forlorn",
                "47a85f2bc9a369a488a454d70435caac",
                "Spec_EnemyMonster_T4_Redcap_Forlorn",
                "Forlorn Redcap",
                HunterFamily.Redcap,
                HuntRegion.Forlorn,
                4,
                25,
                20f,
                0.95f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "mistling-forlorn",
                "6f7fcf075b9e8f64495fc893f853bceb",
                "Spec_EnemyMonster_T4_Mistling_Forlorn",
                "Forlorn Mistling",
                HunterFamily.Mistling,
                HuntRegion.Forlorn,
                4,
                30,
                24f,
                0.75f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "bonemask-mage-forlorn",
                "32ca9a1e4c52a2644bd1cfb2bfdeaba1",
                "Spec_EnemyMonster_T4_Bonemask_Mage",
                "Bonemask Mage",
                HunterFamily.Bonemask,
                HuntRegion.Forlorn,
                4,
                30,
                24f,
                0.8f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "bonemask-melee-forlorn",
                "15fa95ee39d224a47be5c17d489ecbb2",
                "Spec_EnemyMonster_T4_Bonemask_Melee",
                "Bonemask Warrior",
                HunterFamily.Bonemask,
                HuntRegion.Forlorn,
                4,
                30,
                25f,
                0.8f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "zombie-forlorn",
                "05f867f360d54da4bbd50560c73abdf9",
                "Spec_EnemyZombie_T5_ZombieForlorn",
                "Forlorn Dead",
                HunterFamily.Zombie,
                HuntRegion.Forlorn,
                5,
                30,
                28f,
                0.75f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "corpse-eater-forlorn",
                "dfd5303226380e34f8ed8a59db6da5fa",
                "Spec_EnemyMonster_T5_CorpseEater_Forlorn",
                "Forlorn Corpse Eater",
                HunterFamily.CorpseEater,
                HuntRegion.Forlorn,
                5,
                40,
                28f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "frostbitten-forlorn",
                "925680acc37514c4086622e71fa3c13a",
                "Spec_EnemyMonster_T5_FrostbittenWarrior_Male",
                "Frostbitten Warrior",
                HunterFamily.Frostbitten,
                HuntRegion.Forlorn,
                5,
                40,
                32f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "smaller-sharg-forlorn",
                "4186b8fdcf380fe42981626cb6676927",
                "Spec_EnemyMonster_T5_ShargSmallerForlorn",
                "Forlorn Sharg",
                HunterFamily.Sharg,
                HuntRegion.Forlorn,
                5,
                40,
                32f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "skeleton-archer-forlorn",
                "808209eaa7587794aa089b2ed84ab6e3",
                "Spec_EnemyMonster_T5_SkeletonArcher",
                "Forlorn Skeleton Archer",
                HunterFamily.Skeleton,
                HuntRegion.Forlorn,
                5,
                40,
                30f,
                0.65f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "swarm-forlorn",
                "63c3b5802b687a0429ac03f9e7a1b133",
                "Spec_EnemyMonster_T5_Swarm",
                "Forlorn Swarm",
                HunterFamily.Swarm,
                HuntRegion.Forlorn,
                5,
                40,
                28f,
                0.6f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "elite-skeleton-forlorn",
                "6c39d9e11b351764f86d6ddf3ff64bf3",
                "Spec_EnemyMonster_T6_SkeletonElite",
                "Forlorn Elite Skeleton",
                HunterFamily.Skeleton,
                HuntRegion.Forlorn,
                6,
                50,
                38f,
                0.45f,
                0f,
                1,
                HunterSafetyFlags.Elite),
            new HunterProfile(
                "elite-sharg-forlorn",
                "db756acb12fd9824b8f76cfd04d00cf5",
                "Spec_EnemyMonster_T5_ShargForlorn",
                "Forlorn Alpha Sharg",
                HunterFamily.Sharg,
                HuntRegion.Forlorn,
                6,
                60,
                44f,
                0.35f,
                0f,
                1,
                HunterSafetyFlags.Elite
                    | HunterSafetyFlags.SoloOnly),
            new HunterProfile(
                "drowner-sarras",
                "bf9643d310c0076468095f825960adc1",
                "Spec_SoS_EnemyZombie_T3_Drowner",
                "Sarras Drowner",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                3,
                25,
                18f,
                0.95f,
                0.5f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "drowner-two-hand-sarras",
                "a5d157a124dbe114a9deb6a37da3358c",
                "Spec_SoS_EnemyZombie_T4_Drowner_2H",
                "Sarras Drowner Brute",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                4,
                27,
                20f,
                0.85f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "deckhand-sarras",
                "c446b238e2f87d34ebb56bd87ce6a8b2",
                "Spec_SoS_EnemyMonster_T4_DrownedDeckhand",
                "Drowned Deckhand",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                4,
                28,
                20f,
                0.9f,
                0.4f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "mariner-sarras",
                "629e018fb6cd5c04d880ae6cb4b8bc12",
                "Spec_SoS_EnemyMonster_T4_DrownedMariner",
                "Drowned Mariner",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                4,
                28,
                22f,
                0.85f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "finbled-light-sarras",
                "9257f74ad720b4d4cab2fad445d6eabb",
                "Spec_SoS_EnemyMonster_T4_Finbled_Light",
                "Finbled Stalker",
                HunterFamily.Finbled,
                HuntRegion.Sarras,
                5,
                30,
                24f,
                0.85f,
                0.3f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "finbled-javelin-sarras",
                "2ae4e298453780f488898d1c8efa40ae",
                "Spec_SoS_EnemyMonster_T4_Finbled_JavelinThrower",
                "Finbled Javelin Hunter",
                HunterFamily.Finbled,
                HuntRegion.Sarras,
                5,
                30,
                26f,
                0.75f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "finbled-heavy-sarras",
                "1c3007f0399936747a3a46080220678c",
                "Spec_SoS_EnemyMonster_T4_Finbled_Heavy",
                "Finbled Heavy",
                HunterFamily.Finbled,
                HuntRegion.Sarras,
                5,
                30,
                28f,
                0.7f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "tadpole-sarras",
                "bceaf319958bbf54e8ddb1a4ebda2010",
                "Spec_SoS_EnemyMonster_T4_Tadpole",
                "Tadpole",
                HunterFamily.Tadpole,
                HuntRegion.Sarras,
                5,
                30,
                24f,
                0.8f,
                0.3f,
                1,
                HunterSafetyFlags.CanBeSidecar),
            new HunterProfile(
                "wailcap-sarras",
                "9140bd847e233604288a5a66d9d12c3b",
                "Spec_SoS_EnemyMonster_T4_Wailcap",
                "Wailcap",
                HunterFamily.Wailcap,
                HuntRegion.Sarras,
                5,
                30,
                26f,
                0.75f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "tidewraith-sarras",
                "f7aca4c4aa9722844977c33da5ad55f1",
                "Spec_SoS_EnemyMonster_T5_Tidewraith",
                "Tidewraith",
                HunterFamily.Tidewraith,
                HuntRegion.Sarras,
                5,
                30,
                28f,
                0.7f,
                0f,
                1,
                HunterSafetyFlags.None),
            new HunterProfile(
                "drowned-knight-sarras",
                "1aa9c02f06e33f140ba8dcdfd8969f65",
                "Spec_SoS_EnemyMonster_T6_DrownedKnight",
                "Drowned Knight",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                6,
                35,
                36f,
                0.45f,
                0f,
                1,
                HunterSafetyFlags.Elite),
            new HunterProfile(
                "drowned-knight-female-sarras",
                "0babf25cb7633f848b7a3d926ca5b988",
                "Spec_SoS_EnemyMonster_T6_DrownedKnight_Female",
                "Drowned Knight Huntress",
                HunterFamily.Drowned,
                HuntRegion.Sarras,
                6,
                35,
                36f,
                0.45f,
                0f,
                1,
                HunterSafetyFlags.Elite)
        };

        private readonly Random _random;
        private readonly List<string> _recentProfiles =
            new List<string>();
        private readonly List<HunterFamily> _recentFamilies =
            new List<HunterFamily>();
        private readonly Dictionary<string, int> _failureCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public HunterCatalogDirector(int seed)
        {
            _random = new Random(seed);
        }

        public HunterSelectionResult Select(
            HunterSelectionContext context)
        {
            List<WeightedProfile> primaryPool =
                new List<WeightedProfile>();
            List<string> filters = new List<string>();
            float costMultiplier = Clamp(
                context.DangerCostMultiplier,
                0.5f,
                2f);

            for (int index = 0; index < Profiles.Length; index++)
            {
                HunterProfile profile = Profiles[index];
                string reason = HardFilterReason(
                    profile,
                    context,
                    costMultiplier,
                    false);
                if (!string.IsNullOrEmpty(reason))
                {
                    filters.Add(profile.Id + "(" + reason + ")");
                    continue;
                }

                primaryPool.Add(new WeightedProfile(
                    profile,
                    PrimaryWeight(profile, context)));
            }

            string filterSummary = JoinLimited(filters, 120);
            string weightSummary = DescribeWeights(primaryPool);
            HunterProfile primary = Choose(primaryPool);
            if (primary == null)
            {
                return new HunterSelectionResult(
                    null,
                    filterSummary,
                    weightSummary,
                    "eligible pool is empty");
            }

            HuntEncounterPlan plan = new HuntEncounterPlan(primary);
            int packCap = SafetyPackCap(context.PlayerLevel);
            packCap = Math.Min(
                packCap,
                Clamp(context.MaximumPackSize, 1, 3));
            if ((primary.SafetyFlags
                    & HunterSafetyFlags.SoloOnly) != 0)
            {
                packCap = 1;
            }

            while (plan.Count < packCap
                && ShouldAddSidecar(
                    context.SidecarChance,
                    context.Threat,
                    plan.Count))
            {
                List<WeightedProfile> sidecarPool =
                    BuildSidecarPool(
                        plan,
                        context,
                        costMultiplier);
                HunterProfile sidecar = Choose(sidecarPool);
                if (sidecar == null)
                {
                    break;
                }
                plan.AddSidecar(sidecar);
            }

            ScalePlanCost(plan, costMultiplier);
            return new HunterSelectionResult(
                plan,
                filterSummary,
                weightSummary,
                "selected");
        }

        public void RecordConfirmed(HuntEncounterPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            for (int index = 0; index < plan.Members.Count; index++)
            {
                HunterProfile profile = plan.Members[index];
                _failureCounts.Remove(profile.Id);
            }
            PushHistory(plan.Primary);
        }

        public void RecordFailure(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            int count;
            _failureCounts.TryGetValue(profileId, out count);
            _failureCounts[profileId] = count + 1;
        }

        public bool IsSessionRejected(string profileId)
        {
            int count;
            return !string.IsNullOrWhiteSpace(profileId)
                && _failureCounts.TryGetValue(profileId, out count)
                && count >= FailureRejectionCount;
        }

        private List<WeightedProfile> BuildSidecarPool(
            HuntEncounterPlan plan,
            HunterSelectionContext context,
            float costMultiplier)
        {
            List<WeightedProfile> pool =
                new List<WeightedProfile>();
            bool wyrdspiritCluster = plan.Primary.Family
                == HunterFamily.Wyrdspirit;

            for (int index = 0; index < Profiles.Length; index++)
            {
                HunterProfile profile = Profiles[index];
                if (!profile.CanBeSidecar
                    || profile.SidecarWeight <= 0f
                    || profile.Tier > plan.Primary.Tier
                    || (wyrdspiritCluster
                        && profile.Family
                            != HunterFamily.Wyrdspirit)
                    || (!wyrdspiritCluster
                        && plan.ContainsProfile(profile.Id))
                    || CountProfile(plan, profile.Id)
                        >= profile.MaximumCopies)
                {
                    continue;
                }

                HunterSelectionContext remainingContext = context;
                remainingContext.RemainingBudget =
                    context.RemainingBudget - plan.DangerCost
                        * costMultiplier;
                if (!string.IsNullOrEmpty(HardFilterReason(
                    profile,
                    remainingContext,
                    costMultiplier,
                    true)))
                {
                    continue;
                }

                float weight = profile.SidecarWeight;
                if (profile.Tier == plan.Primary.Tier)
                {
                    weight *= 0.55f;
                }
                if (profile.Family == plan.Primary.Family)
                {
                    weight *= wyrdspiritCluster ? 0.85f : 0.35f;
                }
                pool.Add(new WeightedProfile(profile, weight));
            }
            return pool;
        }

        private string HardFilterReason(
            HunterProfile profile,
            HunterSelectionContext context,
            float costMultiplier,
            bool sidecar)
        {
            if (context.Region == HuntRegion.Unknown)
            {
                return "unknown-region";
            }
            if (!profile.IsUniversal
                && profile.Region != context.Region)
            {
                return "region";
            }
            if (context.PlayerLevel < profile.MinimumPlayerLevel)
            {
                return "level<"
                    + profile.MinimumPlayerLevel.ToString(
                        CultureInfo.InvariantCulture);
            }
            if (profile.IsElite && !context.AllowEliteEnemies)
            {
                return "elite-disabled";
            }
            if (profile.IsElite
                && context.Threat <= EliteThreatThreshold)
            {
                return "threat<=75";
            }
            if (IsSessionRejected(profile.Id))
            {
                return "session-rejected";
            }
            if (profile.DangerCost * costMultiplier
                > context.RemainingBudget + 0.001f)
            {
                return "budget";
            }
            if (sidecar
                && (profile.SafetyFlags
                    & HunterSafetyFlags.SoloOnly) != 0)
            {
                return "solo-only";
            }
            return string.Empty;
        }

        private float PrimaryWeight(
            HunterProfile profile,
            HunterSelectionContext context)
        {
            float threat = Clamp(context.Threat / 100f, 0f, 1f);
            float strength = Clamp((profile.Tier - 1f) / 2f, 0f, 1f);
            float threatFit = 0.6f
                + (1f - strength) * (1f - threat) * 0.8f
                + strength * threat * 1.4f;
            float weight = profile.PrimaryWeight * threatFit;

            if (_recentProfiles.Count > 0
                && string.Equals(
                    _recentProfiles[0],
                    profile.Id,
                    StringComparison.Ordinal))
            {
                weight *= 0.2f;
            }
            else if (_recentProfiles.Contains(profile.Id))
            {
                weight *= 0.55f;
            }

            if (_recentFamilies.Count > 0
                && _recentFamilies[0] == profile.Family)
            {
                weight *= 0.45f;
            }
            else if (_recentFamilies.Contains(profile.Family))
            {
                weight *= 0.72f;
            }
            return Math.Max(0.001f, weight);
        }

        private bool ShouldAddSidecar(
            float configuredChance,
            float threat,
            int currentCount)
        {
            float chance = Clamp(configuredChance, 0f, 1f)
                * (0.15f + 0.85f
                    * Clamp(threat / 100f, 0f, 1f));
            if (currentCount >= 2)
            {
                chance *= 0.55f;
            }
            return _random.NextDouble() < chance;
        }

        private HunterProfile Choose(List<WeightedProfile> pool)
        {
            float total = 0f;
            for (int index = 0; index < pool.Count; index++)
            {
                total += pool[index].Weight;
            }
            if (total <= 0f)
            {
                return null;
            }

            double roll = _random.NextDouble() * total;
            for (int index = 0; index < pool.Count; index++)
            {
                roll -= pool[index].Weight;
                if (roll <= 0d)
                {
                    return pool[index].Profile;
                }
            }
            return pool[pool.Count - 1].Profile;
        }

        private void PushHistory(HunterProfile profile)
        {
            _recentProfiles.Insert(0, profile.Id);
            _recentFamilies.Insert(0, profile.Family);
            if (_recentProfiles.Count > HistoryLimit)
            {
                _recentProfiles.RemoveAt(HistoryLimit);
                _recentFamilies.RemoveAt(HistoryLimit);
            }
        }

        private static int SafetyPackCap(int playerLevel)
        {
            if (playerLevel < 8)
            {
                return 1;
            }
            if (playerLevel < 15)
            {
                return 2;
            }
            return 3;
        }

        private static int CountProfile(
            HuntEncounterPlan plan,
            string profileId)
        {
            int count = 0;
            for (int index = 0; index < plan.Members.Count; index++)
            {
                if (string.Equals(
                    plan.Members[index].Id,
                    profileId,
                    StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static void ScalePlanCost(
            HuntEncounterPlan plan,
            float multiplier)
        {
            if (Math.Abs(multiplier - 1f) < 0.001f)
            {
                return;
            }

            plan.ApplyCostMultiplier(multiplier);
        }

        private static string DescribeWeights(
            List<WeightedProfile> pool)
        {
            List<string> weights = new List<string>();
            for (int index = 0; index < pool.Count; index++)
            {
                weights.Add(
                    pool[index].Profile.Id
                    + "="
                    + pool[index].Weight.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture));
            }
            return JoinLimited(weights, 120);
        }

        private static string JoinLimited(
            List<string> values,
            int maximumCharacters)
        {
            string result = string.Empty;
            for (int index = 0; index < values.Count; index++)
            {
                string candidate = string.IsNullOrEmpty(result)
                    ? values[index]
                    : result + ", " + values[index];
                if (candidate.Length > maximumCharacters)
                {
                    return string.IsNullOrEmpty(result)
                        ? candidate.Substring(0, maximumCharacters)
                        : result + ", ...";
                }
                result = candidate;
            }
            return result;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minimum;
            }
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private struct WeightedProfile
        {
            public readonly HunterProfile Profile;
            public readonly float Weight;

            public WeightedProfile(
                HunterProfile profile,
                float weight)
            {
                Profile = profile;
                Weight = weight;
            }
        }
    }
}
