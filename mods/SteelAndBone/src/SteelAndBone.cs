using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.Attachments;
using Awaken.TG.Main.Heroes.Items.Attachments.Audio;
using Awaken.TG.Main.Settings.Accessibility;
using Awaken.TG.Main.Utility.Animations;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

[assembly: AssemblyTitle("Steel and Bone")]
[assembly: AssemblyDescription("Lightweight but impactful difficulty mod for Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("Steel and Bone")]
[assembly: AssemblyVersion("3.9.0.0")]
[assembly: AssemblyFileVersion("3.9.0.0")]
[assembly: AssemblyInformationalVersion("3.9.0")]

namespace SteelAndBone
{
    public static class SteelAndBoneHitFeedbackApi
    {
        public const int ApiVersion = 5;

        public static event Action<float, float, bool, bool, bool, bool, string, float>
            HitResolved;

        public static event Action<int, float, float, bool, bool, bool, bool, string, float>
            KillingBlowResolved;

        internal static void Publish(
            float effectivenessMultiplier,
            float visualEffectivenessMultiplier,
            bool immune,
            bool critical,
            bool weakSpot,
            bool damageOverTime,
            string color,
            float durationSeconds)
        {
            Action<float, float, bool, bool, bool, bool, string, float> handlers =
                HitResolved;
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<float, float, bool, bool, bool, bool, string, float>)subscribers[i])(
                        effectivenessMultiplier,
                        visualEffectivenessMultiplier,
                        immune,
                        critical,
                        weakSpot,
                        damageOverTime,
                        color,
                        durationSeconds);
                }
                catch
                {
                    // Optional presentation integrations must not affect combat.
                }
            }
        }

        internal static void PublishKillingBlow(
            int qualityTier,
            float quality01,
            float visualEffectivenessMultiplier,
            bool immune,
            bool critical,
            bool weakSpot,
            bool damageOverTime,
            string color,
            float durationSeconds)
        {
            Action<int, float, float, bool, bool, bool, bool, string, float> handlers =
                KillingBlowResolved;
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<int, float, float, bool, bool, bool, bool, string, float>)subscribers[i])(
                        qualityTier,
                        quality01,
                        visualEffectivenessMultiplier,
                        immune,
                        critical,
                        weakSpot,
                        damageOverTime,
                        color,
                        durationSeconds);
                }
                catch
                {
                    // Optional presentation integrations must not affect combat.
                }
            }
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(VersatileWeaponsPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(BetterUiPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class SteelAndBonePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.steel-and-bone";
        public const string PluginName = "Steel and Bone";
        public const string PluginVersion = "3.9.0";

        private const string VersatileWeaponsPluginGuid =
            "ks.tgfoa.versatile-weapons";
        private const int ConfigSchemaVersion = 26;
        private const int ConfigRecoveryBaselineSchema = 14;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        15,
                        "1. Core",
                        "Preset",
                        "Preset now controls global difficulty systems in addition to material-rule intensity."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        16,
                        "1. Core",
                        "Preset",
                        "Preset now applies stronger incoming, outgoing, and experience pressure, including on Tempered."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        23,
                        "4. Target Families",
                        "ConstructTerms",
                        "The broad Crystal term was replaced with exact crystal-bodied enemy terms so Crystal Kyrus is not classified as a construct."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        24,
                        "4. Target Families",
                        "FloraTerms",
                        "Wailcaps are now corrected to their Sea Creature identity instead of being treated as broad flora."),
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        24,
                        "4. Target Families",
                        "FleshUndeadTerms",
                        "Wights are now corrected to their Wyrd-flora identity instead of being treated as broad flesh undead.")
                };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private const string DefaultDamageNumberBaseColor = "#E3BD02";
        private const int MaxPendingDamageFeedback = 128;
        private const float PendingDamageFeedbackLifetimeSeconds = 4.0f;
        private const string HealthElementTypeName = "Awaken.TG.Main.Character.HealthElement";
        private const string HeroTypeName = "Awaken.TG.Main.Heroes.Hero";
        private const string DamageSubTypeTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageSubType";
        private const string DamageReceivedMultiplierDataBaseTypeName = "Awaken.TG.Main.Fights.DamageInfo.DamageReceivedMultiplierDataBase";

        private static readonly Color DefaultDamageNumberColor = new Color32(0xE3, 0xBD, 0x02, 0xFF);
        private static readonly Color ResistedDamageNumberColor = new Color32(0x68, 0x68, 0x66, 0xFF);
        private static readonly Color WeaknessDamageNumberColor = new Color32(0xFF, 0x2F, 0x18, 0xFF);
        private static readonly Color ImmuneDamageNumberColor = new Color32(0xB8, 0xB8, 0xB4, 0xFF);
        private static readonly Color DamageNumberOutlineColor = new Color32(0x16, 0x10, 0x08, 0xFF);

        private static readonly DamageRule[] DamageRules =
        {
            new DamageRule(TargetFamily.BoneUndead, DamageTag.Cold, "Bone", "Cold", 0.66f, 60),
            new DamageRule(TargetFamily.BoneUndead, DamageTag.BloodMagic | DamageTag.Bleed, "Bone", "Blood/Bleed", 0.25f, 100),
            new DamageRule(TargetFamily.BoneBody, DamageTag.Slashing | DamageTag.Piercing, "Bone", "Slash/Pierce", 0.55f, 80),
            new DamageRule(TargetFamily.BoneBody, DamageTag.Bludgeoning, "Bone", "Blunt", 1.08f, 70),
            new DamageRule(TargetFamily.BoneBody, DamageTag.GenericPhysical, "Bone", "Physical", 0.85f, 40),
            new DamageRule(TargetFamily.Construct, DamageTag.Cold, "Construct", "Cold", 0.66f, 60),
            new DamageRule(TargetFamily.Construct, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Construct", "Blood/Bleed/Poison", 0.25f, 100),
            new DamageRule(TargetFamily.StoneBody, DamageTag.Slashing | DamageTag.Piercing, "Stone", "Slash/Pierce", 0.75f, 70),
            new DamageRule(TargetFamily.StoneBody, DamageTag.Bludgeoning, "Stone", "Blunt", 1.15f, 80),
            new DamageRule(TargetFamily.StoneBody, DamageTag.GenericPhysical, "Stone", "Physical", 0.85f, 40),
            new DamageRule(TargetFamily.Flesh, DamageTag.BloodMagic, "Flesh", "Blood", 1.10f, 25),
            new DamageRule(TargetFamily.Flesh, DamageTag.Bleed | DamageTag.Poison, "Flesh", "Bleed/Poison", 1.06f, 20),
            new DamageRule(TargetFamily.Flesh, DamageTag.Piercing, "Flesh", "Pierce", 1.06f, 16),
            new DamageRule(TargetFamily.Flesh, DamageTag.Slashing, "Flesh", "Slash", 1.04f, 15),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Undead", "Blood/Bleed/Poison", 0.78f, 55),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Piercing, "Undead", "Pierce", 0.90f, 56),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Fire, "Undead", "Fire", 1.08f, 50),
            new DamageRule(TargetFamily.FleshUndead, DamageTag.Bludgeoning, "Undead", "Blunt", 1.05f, 45),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.BloodMagic | DamageTag.Bleed, "Drowned", "Blood/Bleed", 0.65f, 80),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Electric, "Drowned", "Electric", 1.15f, 70),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Piercing, "Drowned", "Pierce", 0.90f, 65),
            new DamageRule(TargetFamily.DrownedZombie, DamageTag.Bludgeoning, "Drowned", "Blunt", 1.10f, 60),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Poison, "Infected", "Poison", 0.66f, 80),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Fire, "Infected", "Fire", 1.15f, 70),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Piercing, "Infected", "Pierce", 1.06f, 62),
            new DamageRule(TargetFamily.InfectedFlesh, DamageTag.Slashing, "Infected", "Slash", 1.04f, 61),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Cold, "Sea", "Cold", 0.70f, 70),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Electric, "Sea", "Electric", 1.12f, 60),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Piercing, "Sea", "Pierce", 1.06f, 56),
            new DamageRule(TargetFamily.SeaFlesh, DamageTag.Slashing, "Sea", "Slash", 1.04f, 55),
            new DamageRule(TargetFamily.Spirit, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Spirit", "Blood/Bleed/Poison", 0.35f, 90),
            new DamageRule(TargetFamily.Spirit, DamageTag.Wyrdness, "Spirit", "Wyrdness", 1.15f, 60),
            new DamageRule(TargetFamily.Spirit, DamageTag.GenericPhysical | DamageTag.Slashing | DamageTag.Piercing | DamageTag.Bludgeoning, "Spirit", "Physical", 0.85f, 50),
            new DamageRule(TargetFamily.Flora, DamageTag.Poison | DamageTag.Bleed | DamageTag.Piercing, "Flora", "Poison/Bleed/Pierce", 0.70f, 70),
            new DamageRule(TargetFamily.Flora, DamageTag.Fire | DamageTag.Slashing, "Flora", "Fire/Slash", 1.15f, 70)
        };

        private static readonly ExactDamageRule[] ExactDamageRules =
        {
            new ExactDamageRule(ExactTarget.FrostbittenWarrior, DamageTag.Fire, "Frozen Undead", "Fire", 1.15f),
            new ExactDamageRule(ExactTarget.FrostbittenWarrior, DamageTag.Cold, "Frozen Undead", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.Frostgrot, DamageTag.Fire, "Frostgrot", "Fire", 1.15f),
            new ExactDamageRule(ExactTarget.Frostgrot, DamageTag.Cold, "Frostgrot", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.MissingCorpseEaterReaction, DamageTag.Fire, "Corpse Eater", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.MissingCorpseEaterReaction, DamageTag.Wyrdness, "Corpse Eater", "Wyrdness", 0.80f),
            new ExactDamageRule(ExactTarget.ElectricStagfatherGolem, DamageTag.Poison, "Electric Stagfather Golem", "Poison", 1.33f),
            new ExactDamageRule(ExactTarget.Mistbearer, DamageTag.Fire, "Mistbearer", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.Mistbearer, DamageTag.Wyrdness, "Mistbearer", "Wyrdness", 0.80f),
            new ExactDamageRule(ExactTarget.WyrdheirChallenge, DamageTag.Cold, "Wyrdheir", "Cold", 0.60f),
            new ExactDamageRule(ExactTarget.Nivera, DamageTag.Fire, "Nivera", "Fire", 1.33f),
            new ExactDamageRule(ExactTarget.Rimefiend, DamageTag.Fire, "Rimefiend", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.FrostWolf, DamageTag.Fire, "Frost Wolf", "Fire", 1.15f),
            new ExactDamageRule(ExactTarget.FrostWolf, DamageTag.Cold, "Frost Wolf", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.StrawParent, DamageTag.Fire, "Straw Construct", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.StrawParent, DamageTag.Slashing, "Straw Construct", "Slash", 1.15f),
            new ExactDamageRule(ExactTarget.Wyrdspawn, DamageTag.Slashing, "Wyrdspawn", "Slash", 1.10f),
            new ExactDamageRule(ExactTarget.Ogre, DamageTag.Piercing, "Ogre", "Pierce", 1.15f),
            new ExactDamageRule(ExactTarget.Ogre, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Ogre", "Biological", 1.10f),
            new ExactDamageRule(ExactTarget.Ogre, DamageTag.Bludgeoning, "Ogre", "Blunt", 0.90f),
            new ExactDamageRule(ExactTarget.FireAligned, DamageTag.Wet, "Fire-Aligned", "Wet", 1.20f),
            new ExactDamageRule(ExactTarget.DrownedSkeletonSailor, DamageTag.Electric, "Drowned Skeleton", "Electric", 1.12f),
            new ExactDamageRule(ExactTarget.FrostAngel, DamageTag.Fire, "Frost Angel", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.FrostAngel, DamageTag.Cold, "Frost Angel", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.IceWeaverChampion, DamageTag.Fire, "Ice Weaver Champion", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.IceWeaverChampion, DamageTag.Cold, "Ice Weaver Champion", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.IceWeaverWolf, DamageTag.Fire, "Ice Weaver Wolf", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.IceWeaverWolf, DamageTag.Cold, "Ice Weaver Wolf", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.IceTrialWyrd, DamageTag.Fire, "Ice Trial Wyrd", "Fire", 1.15f),
            new ExactDamageRule(ExactTarget.IceTrialWyrd, DamageTag.Cold, "Ice Trial Wyrd", "Cold", 0.75f),
            new ExactDamageRule(ExactTarget.CharredConclaveWyrdspawn, DamageTag.Cold, "Charred Wyrdspawn", "Cold", 1.15f),
            new ExactDamageRule(ExactTarget.CharredConclaveWyrdspawn, DamageTag.Fire, "Charred Wyrdspawn", "Fire", 0.75f),
            new ExactDamageRule(ExactTarget.IceStatue, DamageTag.Fire, "Ice Statue", "Fire", 1.20f),
            new ExactDamageRule(ExactTarget.IceStatue, DamageTag.Cold, "Ice Statue", "Cold", 0.60f),
            new ExactDamageRule(ExactTarget.AncientBeholder, DamageTag.Piercing, "Ancient Beholder", "Pierce", 1.12f),
            new ExactDamageRule(ExactTarget.AncientBeholder, DamageTag.BloodMagic | DamageTag.Bleed | DamageTag.Poison, "Ancient Beholder", "Biological", 1.08f),
            new ExactDamageRule(ExactTarget.AncientBeholder, DamageTag.Bludgeoning, "Ancient Beholder", "Blunt", 0.90f),
            new ExactDamageRule(ExactTarget.Singworm, DamageTag.Slashing, "Singworm", "Slash", 1.15f),
            new ExactDamageRule(ExactTarget.Singworm, DamageTag.Bludgeoning, "Singworm", "Blunt", 0.85f),
            new ExactDamageRule(ExactTarget.LirTentacle, DamageTag.Slashing, "Lir Tentacle", "Slash", 1.15f),
            new ExactDamageRule(ExactTarget.LirTentacle, DamageTag.Bludgeoning, "Lir Tentacle", "Blunt", 0.85f),
            new ExactDamageRule(ExactTarget.BloodAbomination, DamageTag.Slashing, "Blood Abomination", "Slash", 1.20f),
            new ExactDamageRule(ExactTarget.BloodAbomination, DamageTag.Bludgeoning, "Blood Abomination", "Blunt", 0.80f),
            new ExactDamageRule(ExactTarget.WyrdSlime, DamageTag.Bludgeoning, "Wyrd Slime", "Blunt", 0.80f),
            new ExactDamageRule(ExactTarget.Tidewraith, DamageTag.Bludgeoning, "Tidewraith", "Blunt", 0.90f)
        };

        private static readonly NativeSubtypeCheck[] NativeSubtypeChecks =
        {
            new NativeSubtypeCheck(DamageTag.Wyrdness, "Wyrdness"),
            new NativeSubtypeCheck(DamageTag.GenericPhysical, "GenericPhysical"),
            new NativeSubtypeCheck(DamageTag.Slashing, "Slashing"),
            new NativeSubtypeCheck(DamageTag.Piercing, "Piercing"),
            new NativeSubtypeCheck(DamageTag.Bludgeoning, "Bludgeoning"),
            new NativeSubtypeCheck(DamageTag.GenericMagical, "GenericMagical"),
            new NativeSubtypeCheck(DamageTag.Fire, "Fire"),
            new NativeSubtypeCheck(DamageTag.Cold, "Cold"),
            new NativeSubtypeCheck(DamageTag.Poison, "Poison"),
            new NativeSubtypeCheck(DamageTag.Electric, "Electric"),
            new NativeSubtypeCheck(DamageTag.Wet, "Wet")
        };

        private static readonly string[] BleedTerms = { "bleed" };
        private static readonly string[] PoisonTerms = { "poison", "toxic", "venom" };
        private static readonly string[] WyrdTerms = { "wyrd" };
        private static readonly string[] BloodMagicTerms = { "blood", "transfusion", "abhartach", "sanguine", "sanguis", "hematic" };
        private static readonly string[] MetadataBoneUndeadTerms = { "Skeleton", "BoneMask" };
        private static readonly string[] MetadataConstructTerms = { "Construct", "Automaton", "Golem" };
        private static readonly string[] MetadataWyrdTerms = { "WyrdnessBound" };
        private static readonly string[] MetadataDrownedZombieTerms = { "Scourge" };
        private static readonly string[] MetadataSeaFleshTerms = { "SarrasCreature", "ReefboundBody" };
        private static readonly string[] MetadataSpiritTerms = { "Ghost" };
        private static readonly string[] MetadataFloraTerms = { "Flora" };
        private static readonly string[] MetadataFleshUndeadTerms = { "Zombie", "Bloody" };
        private static readonly string[] MetadataFleshTerms = { "Animal", "Animal_Prey", "Bandit", "Cultist", "Human", "Humanoid" };
        private static readonly string[] MetadataEliteTerms = { "Elite", "MiniBoss", "Boss", "Type:Elite" };
        private static readonly string[] MetadataBossTerms = { "MiniBoss", "Boss" };
        private static readonly string[] MetadataConfirmedSkeletonTerms = { "Skeleton" };
        private static readonly string[] MetadataBoneBodyTerms = { "HitBones" };
        private static readonly string[] MetadataStoneBodyTerms = { "HitStone" };
        private static readonly string[] MetadataWoodBodyTerms = { "HitWood" };
        private static readonly string[] MetadataHumanoidTerms = { "Human", "Humanoid" };
        private static readonly string[] ConfirmedSkeletonTerms = { "Skeleton", "JollySkeleton", "Keeper Of The Barrow", "KeeperOfTheBarrow" };
        private static readonly string[] HumanoidFleshTerms = { "Human", "Humanoid", "Bandit", "Outlaw", "Cultist" };
        private static readonly string[] SwarmTerms = { "Swarm", "Bee Swarm", "BeeSwarm" };
        private static readonly string[] EnemyMovementBearTerms = { "AnimalBear", "Forlorn Bear" };
        private static readonly string[] EnemyMovementBulkyMonsterTerms = { "Beholder", "Slugholder" };
        private static readonly string[] InheritedColdWeaknessTerms =
        {
            "Grindylow_Summon",
            "Grindylow Summon",
            "BloodAbominationsSummon",
            "Blood Abominations Summon",
            "BonemaskWarrior_Summon",
            "Bonemask Warrior Summon"
        };
        private static readonly string[] FlamegobblerTerms = { "Flamegobbler" };
        private static readonly string[] CrystalBodyTerms =
        {
            "CrystalCrawler",
            "Crystal Crawler",
            "CrystalWalker",
            "Crystal Walker"
        };
        private static readonly string[] WyrdSlimeColdWeaknessTerms = { "WyrdSlime", "Wyrd Slime" };
        private static readonly string[] RootambusherTerms = { "Rootambusher" };
        private static readonly string[] FrostbittenWarriorTerms = { "FrostbittenWarrior", "Frostbitten Warrior" };
        private static readonly string[] FrostgrotTerms = { "Frostgrot" };
        private static readonly string[] WightTerms =
        {
            "EnemyMonster_T4_Wight",
            "EnemyMonster_T2_WightHoS",
            "EnemyMonster_T4_Wight_LostInLuminal",
            "EnemyMonster_T6_Wight_Bodil"
        };
        private static readonly string[] GiantTerms = { "Abstract:Giant" };
        private static readonly string[] MissingCorpseEaterReactionTerms = { "CorpseEater_Summon", "Corpse Eater Summon", "EnemyMonster_T1_CorpseEaterBig" };
        private static readonly string[] ElectricStagfatherGolemTerms = { "StagFather_ElectricGolem", "StagFather Electric Golem" };
        private static readonly string[] MistbearerTerms = { "EnemyBoss_T3_MistBearer_Base", "EnemyBoss_T3_MistBearer_Mimic", "MistBearer" };
        private static readonly string[] WyrdheirChallengeTerms = { "EnemyBoss_T6_Wyrdheir_Challenge" };
        private static readonly string[] NiveraTerms = { "Nivera" };
        private static readonly string[] RimefiendTerms = { "EnemyMonster_T6_Rimefiend", "Rimefiend" };
        private static readonly string[] FrostWolfTerms = { "AnimalFrostWolf_Summon", "AnimalFrostWolf_SummonCuanacht" };
        private static readonly string[] StrawParentTerms = { "EnemyMonster_T4_StrawDad", "EnemyMonster_T4_StrawSon" };
        private static readonly string[] StagfatherTerms = { "StagFather", "Stagfather" };
        private static readonly string[] GhostOfBrocMealaTerms = { "GhostOfBrocMeala", "Ghost Of Broc Meala" };
        private static readonly string[] SleepwalkerTerms = { "EnemyMonster_T6_Sleepwalker", "Sleepwalker" };
        private static readonly string[] WailcapTerms = { "Wailcap" };
        private static readonly string[] WyrdspawnTerms = { "Wyrdspawn" };
        private static readonly string[] OgreTerms = { "Ogre" };
        private static readonly string[] FireAlignedTerms =
        {
            "Flamegobbler",
            "Cindermar",
            "Forgeborn",
            "StagFather_FireGolem",
            "StagFather Fire Golem",
            "ElementalGolemFire",
            "Elemental Golem Fire"
        };
        private static readonly string[] ElementalStagfatherGolemTerms =
        {
            "StagFather_ElectricGolem",
            "StagFather Electric Golem",
            "StagFather_FireGolem",
            "StagFather Fire Golem",
            "StagFather_IceGolem",
            "StagFather Ice Golem"
        };
        private static readonly string[] DrownedSkeletonSailorTerms = { "DrownedDeckhand", "DrownedMariner" };
        private static readonly string[] FrostAngelTerms = { "Enemy_Special_FrostAngel", "Frost Angel" };
        private static readonly string[] IceWeaverChampionTerms = { "IceWeaversChampion", "Ice Weaver's Champion", "Ice Weavers Champion" };
        private static readonly string[] IceWeaverWolfTerms = { "AnimalIceWeaverWolf_Summon", "Ice Weaver Wolf" };
        private static readonly string[] IceTrialWyrdTerms = { "Wyrdspawn_IceTrial", "Wyrdspirit_IceTrial" };
        private static readonly string[] CharredConclaveWyrdspawnTerms = { "WyrdspawnCharredConclave", "Charred Conclave Wyrdspawn" };
        private static readonly string[] IceStatueTerms = { "Special_Trial_IceStatue", "Trial Ice Statue" };
        private static readonly string[] AncientBeholderTerms = { "EnemyMonster_T6_AncientBeholder", "Ancient Beholder" };
        private static readonly string[] SingwormTerms = { "Singworm" };
        private static readonly string[] LirTentacleTerms = { "Lir_Tentacle_Summon", "Lir Tentacle" };
        private static readonly string[] BloodAbominationTerms = { "BloodAbomination", "Blood Abomination" };
        private static readonly string[] TidewraithTerms = { "Tidewraith", "Tide Wraith" };

        internal static SteelAndBonePlugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private MethodInfo _heroCurrentGetter;
        private Type _damageSubTypeType;
        private MethodInfo _getMultiplierForSubtypeMethod;
        private DamageNumberRenderer _damageNumberRenderer;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<Preset> _preset;
        private ConfigEntry<bool> _respectVanillaMultipliers;
        private ConfigEntry<bool> _arrowMaterialRulesEnabled;
        private ConfigEntry<bool> _materialImpactRulesEnabled;
        private ConfigEntry<bool> _armoredSpellWeaknessEnabled;
        private ConfigEntry<bool> _techniqueMatchupRulesEnabled;
        private ConfigEntry<bool> _amplifyVanillaMultipliers;
        private ConfigEntry<float> _temperedVanillaAmplification;
        private ConfigEntry<float> _hardenedVanillaAmplification;
        private ConfigEntry<float> _crucibleVanillaAmplification;
        private ConfigEntry<float> _minimumAmplifiedVanillaResistance;
        private ConfigEntry<float> _maximumAmplifiedVanillaWeakness;
        private ConfigEntry<bool> _eliteRuleClampsEnabled;
        private ConfigEntry<float> _eliteWeaknessBonusReduction;
        private ConfigEntry<float> _eliteMinimumResistanceMultiplier;
        private ConfigEntry<bool> _damageNumbersEnabled;
        private ConfigEntry<DamageNumberMode> _damageNumberMode;
        private ConfigEntry<string> _damageNumberBaseColor;
        private ConfigEntry<int> _damageNumberFontSize;
        private ConfigEntry<DamageNumberFontMode> _damageNumberFontMode;
        private ConfigEntry<float> _damageNumberDurationSeconds;
        private ConfigEntry<float> _damageNumberCriticalDurationSeconds;
        private ConfigEntry<float> _meleeDamageNumberDurationMultiplier;
        private ConfigEntry<float> _damageNumberHorizontalDrift;
        private ConfigEntry<float> _damageNumberVerticalDrift;
        private ConfigEntry<float> _damageOverTimeNumberHeightMultiplier;
        private ConfigEntry<float> _damageOverTimeNumberScale;
        private ConfigEntry<float> _damageNumberSizeContrast;
        private ConfigEntry<float> _damageNumberColorContrast;
        private ConfigEntry<float> _effectivenessFeedbackSensitivity;
        private ConfigEntry<float> _damageNumberMinimumAmount;
        private ConfigEntry<int> _damageNumberMaximumActive;
        private ConfigEntry<string> _boneUndeadTerms;
        private ConfigEntry<string> _constructTerms;
        private ConfigEntry<string> _wyrdTerms;
        private ConfigEntry<string> _drownedZombieTerms;
        private ConfigEntry<string> _infectedFleshTerms;
        private ConfigEntry<string> _seaFleshTerms;
        private ConfigEntry<string> _spiritTerms;
        private ConfigEntry<string> _floraTerms;
        private ConfigEntry<string> _fleshUndeadTerms;
        private ConfigEntry<string> _fleshTerms;
        private ConfigEntry<string> _armoredHumanoidTerms;
        private ConfigEntry<bool> _diagnostics;
        private ConfigEntry<bool> _showGrailFloatingTextDiagnostics;
        private ConfigEntry<bool> _logPatchWarnings;

        private readonly Dictionary<int, TargetClassification> _targetClassifications =
            new Dictionary<int, TargetClassification>();
        private readonly Dictionary<int, PendingDamageFeedback> _pendingDamageFeedback =
            new Dictionary<int, PendingDamageFeedback>();
        private readonly DamageClassification _partDamageClassification = new DamageClassification();
        private float[] _damagePartAdjustments = new float[8];

        private string _cachedBoneUndeadTermsRaw;
        private string[] _cachedBoneUndeadTerms = new string[0];
        private string _cachedConstructTermsRaw;
        private string[] _cachedConstructTerms = new string[0];
        private string _cachedWyrdTermsRaw;
        private string[] _cachedWyrdTerms = new string[0];
        private string _cachedDrownedZombieTermsRaw;
        private string[] _cachedDrownedZombieTerms = new string[0];
        private string _cachedInfectedFleshTermsRaw;
        private string[] _cachedInfectedFleshTerms = new string[0];
        private string _cachedSeaFleshTermsRaw;
        private string[] _cachedSeaFleshTerms = new string[0];
        private string _cachedSpiritTermsRaw;
        private string[] _cachedSpiritTerms = new string[0];
        private string _cachedFloraTermsRaw;
        private string[] _cachedFloraTerms = new string[0];
        private string _cachedFleshUndeadTermsRaw;
        private string[] _cachedFleshUndeadTerms = new string[0];
        private string _cachedFleshTermsRaw;
        private string[] _cachedFleshTerms = new string[0];
        private string _cachedArmoredHumanoidTermsRaw;
        private string[] _cachedArmoredHumanoidTerms = new string[0];
        private int _targetTermsRevision = 1;
        private string _lastDamageNumberFontDiagnosticKey;
        private string _lastGftDamageDiagnosticSignature;
        private float _nextGftDamageDiagnosticTime;
        private FontAsset _imguiDefaultFontAsset;
        private Grailwright.Shared.ConfigRecoveryCustomizationProfile _pendingConfigRecoveryProfile;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                BindConfig();
                _damageNumberRenderer = gameObject.AddComponent<DamageNumberRenderer>();
                _damageNumberRenderer.Initialize(this);
                CacheGameAccessors();
                if (!PatchGame())
                {
                    enabled = false;
                    return;
                }

                InitializeDifficultyOverhaul();
                Log.LogInfo(PluginName + " " + PluginVersion + " loaded. Preset=" + _preset.Value + ".");
            }
            catch (Exception ex)
            {
                Log.LogError(PluginName + " " + PluginVersion + " failed during startup: " + ex.GetBaseException().Message);
                Log.LogError(ex.ToString());
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, ex);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            ShutdownDifficultyOverhaul();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            _pendingDamageFeedback.Clear();
            if (_damageNumberRenderer != null)
            {
                Destroy(_damageNumberRenderer);
                _damageNumberRenderer = null;
            }

            if (_imguiDefaultFontAsset != null)
            {
                Destroy(_imguiDefaultFontAsset);
                _imguiDefaultFontAsset = null;
            }

            Instance = null;
        }

        private static ConfigDescription ConfigUi(
            string description,
            string displaySection,
            string displayName,
            int sectionOrder,
            int order,
            AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new Grailwright.Shared.ConfigRecoveryUiMetadata
                {
                    DisplaySection = displaySection,
                    DisplayName = displayName,
                    SectionOrder = sectionOrder,
                    Order = order
                });
        }

        private void BindConfig()
        {
            ResetConfigIfSchemaChanged();

            _enabled = Config.Bind("General", "Enabled", true, ConfigUi("Master switch.", "General", "Enabled", 0, 0));
            Config.Bind(
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. It changes only when an update requires fresh defaults.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _preset = Config.Bind("General", "Preset", Preset.Hardened, ConfigUi("Difficulty profile. Tempered applies 5% incoming, outgoing, and experience pressure while keeping resource, armor-weight, recovery, poise, and enemy movement modifiers neutral. Hardened applies 10% damage and experience pressure plus the default 5% supporting profile. Crucible applies 15% damage and experience pressure plus the 10% supporting profile and stronger material rules.", "General", "Preset", 0, 10));
            _respectVanillaMultipliers = Config.Bind("General", "RespectVanillaMultipliers", true, ConfigUi("Skip Steel and Bone subtype overlays when the target already has a non-neutral vanilla multiplier for the same damage subtype.", "Combat Rules", "Respect Native Multipliers", 10, 0));
            _arrowMaterialRulesEnabled = Config.Bind("General", "ArrowMaterialRulesEnabled", true, ConfigUi("Give direct arrow hits a distinct material identity. Physical arrow damage strongly rewards exposed flesh and is resisted by armor, bone, swarms, flora or wood, spirits, and constructs or stone. Existing numerical armor prevents duplicate resistance from becoming excessive.", "Combat Rules", "Arrow Material Rules", 10, 10));
            _materialImpactRulesEnabled = Config.Bind("General", "MaterialImpactRulesEnabled", true, ConfigUi("Let resistance shape secondary impact for direct player hits. Resistance partially reduces poise and force without amplifying weaknesses, while immunity or very strong resistance removes the routine small flinch. Genuine poise breaks, stumbles, and ragdolls remain possible.", "Combat Rules", "Material Impact Rules", 10, 15));
            _armoredSpellWeaknessEnabled = Config.Bind("General", "ArmoredSpellWeaknessEnabled", true, ConfigUi("Give direct player spells tiered advantages against armor, with Fire, Electric, and Cold also reacting to the armor's native Fabric, Leather, or Metal surface when vanilla does not already define the subtype reaction.", "Combat Rules", "Armored Spell Weaknesses", 10, 20));
            _techniqueMatchupRulesEnabled = Config.Bind("General", "TechniqueMatchupRulesEnabled", true, ConfigUi("Enable the compact technique layer: pommel strikes count as limited Blunt against rigid targets, heavy melee attacks partially breach custom rigid resistance, and direct area hits pressure swarms.", "Combat Rules", "Technique Matchup Rules", 10, 30));
            _eliteRuleClampsEnabled = Config.Bind("General", "EliteRuleClampsEnabled", true, ConfigUi("Reduce custom Steel and Bone weakness bonuses and floor custom resistances on elite-class targets.", "Combat Rules", "Elite Rule Limits", 10, 40));
            _eliteWeaknessBonusReduction = Config.Bind("General", "EliteWeaknessBonusReduction", 0.10f, ConfigUi("Flat reduction applied to custom Steel and Bone weakness bonuses on elite-class targets when Elite Rule Limits is enabled. 0.10 turns a 1.15 weakness into 1.05.", "Advanced - Elite Rules", "Weakness Bonus Reduction", 20, 0, new AcceptableValueRange<float>(0.0f, 0.50f)));
            _eliteMinimumResistanceMultiplier = Config.Bind("General", "EliteMinimumResistanceMultiplier", 0.20f, ConfigUi("Lowest custom Steel and Bone non-immunity resistance multiplier allowed on elite-class targets when Elite Rule Limits is enabled.", "Advanced - Elite Rules", "Minimum Resistance Multiplier", 20, 10, new AcceptableValueRange<float>(0.05f, 0.95f)));

            _amplifyVanillaMultipliers = Config.Bind("Vanilla Multipliers", "AmplifyVanillaMultipliers", true, ConfigUi("Amplify vanilla enemy weakness and resistance multipliers according to the Steel and Bone preset.", "Advanced - Vanilla Multipliers", "Amplify Native Multipliers", 30, 0));
            _temperedVanillaAmplification = Config.Bind("Vanilla Multipliers", "TemperedVanillaAmplification", 0.00f, ConfigUi("Extra distance from neutral applied to vanilla weakness and resistance multipliers on Tempered when Amplify Native Multipliers is enabled. 0 leaves vanilla unchanged.", "Advanced - Vanilla Multipliers", "Tempered Amplification", 30, 10, new AcceptableValueRange<float>(0.0f, 2.0f)));
            _hardenedVanillaAmplification = Config.Bind("Vanilla Multipliers", "HardenedVanillaAmplification", 0.35f, ConfigUi("Extra distance from neutral applied to vanilla weakness and resistance multipliers on Hardened when Amplify Native Multipliers is enabled.", "Advanced - Vanilla Multipliers", "Hardened Amplification", 30, 20, new AcceptableValueRange<float>(0.0f, 2.0f)));
            _crucibleVanillaAmplification = Config.Bind("Vanilla Multipliers", "CrucibleVanillaAmplification", 0.70f, ConfigUi("Extra distance from neutral applied to vanilla weakness and resistance multipliers on Crucible when Amplify Native Multipliers is enabled.", "Advanced - Vanilla Multipliers", "Crucible Amplification", 30, 30, new AcceptableValueRange<float>(0.0f, 2.0f)));
            _minimumAmplifiedVanillaResistance = Config.Bind("Vanilla Multipliers", "MinimumAmplifiedVanillaResistance", 0.20f, ConfigUi("Lowest non-immune vanilla resistance multiplier Steel and Bone amplification can produce when Amplify Native Multipliers is enabled.", "Advanced - Vanilla Multipliers", "Minimum Amplified Resistance", 30, 40, new AcceptableValueRange<float>(0.01f, 0.95f)));
            _maximumAmplifiedVanillaWeakness = Config.Bind("Vanilla Multipliers", "MaximumAmplifiedVanillaWeakness", 1.85f, ConfigUi("Highest vanilla weakness multiplier Steel and Bone amplification can produce when Amplify Native Multipliers is enabled.", "Advanced - Vanilla Multipliers", "Maximum Amplified Weakness", 30, 50, new AcceptableValueRange<float>(1.05f, 3.0f)));

            _damageNumbersEnabled = Config.Bind("Feedback", "DamageNumbersEnabled", true, ConfigUi("Master switch for Steel and Bone floating combat text. Damage Number Mode controls whether this shows numbers or only resistance and immunity notices.", "Damage Numbers", "Enabled", 40, 0));
            _damageNumberMode = Config.Bind("Feedback", "DamageNumberMode", DamageNumberMode.AllDamage, ConfigUi("AllDamage shows the current outgoing damage numbers. ResistAndImmuneOnly replaces numbers with RESISTED or IMMUNE on every qualifying direct hit. ResistAndImmuneOnlyOnce shows each notice only once per enemy. Damage-over-time ticks never produce notice-only text.", "Damage Numbers", "Mode", 40, 5));
            _damageNumberBaseColor = Config.Bind("Feedback", "DamageNumberBaseColor", DefaultDamageNumberBaseColor, ConfigUi("When Damage Numbers is enabled, sets the neutral outgoing color and baseline for resistance/weakness tinting. Use a hex color such as #E3BD02.", "Damage Numbers", "Base Color", 40, 10));
            _damageNumberFontSize = Config.Bind("Feedback", "DamageNumberFontSize", 34, ConfigUi("When Damage Numbers is enabled, sets the base floating damage-number font size.", "Damage Numbers", "Font Size", 40, 20, new AcceptableValueRange<int>(12, 80)));
            _damageNumberFontMode = Config.Bind("Feedback", "DamageNumberFontMode", DamageNumberFontMode.GameDefault, ConfigUi("Font used when Damage Numbers is enabled. GameDefault follows the game's Accessibility font choice, Sans forces the simple game font, Serif forces the stylized game font, and ImguiDefault keeps Unity's IMGUI fallback font.", "Damage Numbers", "Font", 40, 30));
            _damageNumberDurationSeconds = Config.Bind("Feedback", "DamageNumberDurationSeconds", 0.85f, ConfigUi("When Damage Numbers is enabled, sets how many seconds a normal number remains visible.", "Damage Numbers", "Normal Duration (Seconds)", 40, 40, new AcceptableValueRange<float>(0.35f, 2.50f)));
            _damageNumberCriticalDurationSeconds = Config.Bind("Feedback", "DamageNumberCriticalDurationSeconds", 1.10f, ConfigUi("When Damage Numbers is enabled, sets how many seconds a critical number remains visible.", "Damage Numbers", "Critical Duration (Seconds)", 40, 50, new AcceptableValueRange<float>(0.45f, 3.00f)));
            _meleeDamageNumberDurationMultiplier = Config.Bind("Feedback", "MeleeDamageNumberDurationMultiplier", 2.0f, ConfigUi("When Damage Numbers is enabled, multiplies the final duration of direct melee numbers so they remain readable while the camera follows a swing. 1 uses the same duration as other damage numbers.", "Damage Numbers", "Melee Duration Multiplier", 40, 60, new AcceptableValueRange<float>(1.0f, 3.0f)));
            _damageNumberHorizontalDrift = Config.Bind("Feedback", "DamageNumberHorizontalDrift", 1.0f, ConfigUi("When Damage Numbers is enabled, multiplies left/right travel. 0 disables horizontal travel, 1 keeps the default motion, and values above 1 exaggerate it.", "Damage Numbers", "Horizontal Drift", 40, 70, new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberVerticalDrift = Config.Bind("Feedback", "DamageNumberVerticalDrift", 1.0f, ConfigUi("When Damage Numbers is enabled, multiplies upward travel and curved settling. 0 disables vertical travel, 1 keeps the default motion, and values above 1 exaggerate it.", "Damage Numbers", "Vertical Drift", 40, 80, new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageOverTimeNumberHeightMultiplier = Config.Bind("Feedback", "DamageOverTimeNumberHeightMultiplier", 3.0f, ConfigUi("When Damage Numbers is enabled, multiplies the initial world-space height of Bleed, Poison, Burn, and Breath status-tick numbers. 1 uses the ordinary height, while 3 starts them three times higher.", "Damage Numbers", "Damage-Over-Time Height Multiplier", 40, 90, new AcceptableValueRange<float>(0.0f, 6.0f)));
            _damageOverTimeNumberScale = Config.Bind("Feedback", "DamageOverTimeNumberScale", 0.75f, ConfigUi("When Damage Numbers is enabled, scales the text size of Bleed, Poison, Burn, and Breath status-tick numbers after normal resistance, weakness, weak-spot, and critical sizing. 1 uses the ordinary size, while 0.75 makes status ticks 25% smaller.", "Damage Numbers", "Damage-Over-Time Text Scale", 40, 100, new AcceptableValueRange<float>(0.5f, 2.0f)));
            _damageNumberSizeContrast = Config.Bind("Feedback", "DamageNumberSizeContrast", 1.0f, ConfigUi("When Damage Numbers is enabled, controls the size difference between resisted, neutral, and weakness numbers. 0 uses neutral sizing, 1 keeps the default contrast, and values above 1 exaggerate it. Critical and weak-spot pop remain independent.", "Damage Numbers", "Size Contrast", 40, 110, new AcceptableValueRange<float>(0.0f, 3.0f)));
            _effectivenessFeedbackSensitivity = Config.Bind("Feedback", "EffectivenessFeedbackSensitivity", GetPresetEffectivenessFeedbackSensitivity(_preset.Value), ConfigUi("Scales resistance and weakness distance from neutral for hit-marker tier selection and damage-number color only. Changing Preset sets this to 1.20 for Tempered, 1.10 for Hardened, or 1.00 for Crucible; customize it afterward without changing combat damage, number size, or duration.", "Damage Numbers", "Effectiveness Feedback Sensitivity", 40, 120, new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberColorContrast = Config.Bind("Feedback", "DamageNumberColorContrast", 1.0f, ConfigUi("When Damage Numbers is enabled, controls resistance grey and weakness red-orange tinting after effectiveness sensitivity is applied. 0 keeps non-immune numbers neutral, 1 keeps the default contrast, and values above 1 reach the endpoint colors sooner.", "Damage Numbers", "Color Contrast", 40, 130, new AcceptableValueRange<float>(0.0f, 3.0f)));
            _damageNumberMinimumAmount = Config.Bind("Feedback", "DamageNumberMinimumAmount", 0.10f, ConfigUi("When Damage Numbers is enabled, suppresses non-immune numbers below this final damage amount.", "Damage Numbers", "Minimum Amount", 40, 140, new AcceptableValueRange<float>(0.0f, 1000.0f)));
            _damageNumberMaximumActive = Config.Bind("Feedback", "DamageNumberMaximumActive", 36, ConfigUi("When Damage Numbers is enabled, limits how many Steel and Bone floating numbers remain on screen at once.", "Damage Numbers", "Maximum Active", 40, 150, new AcceptableValueRange<int>(1, 128)));

            _boneUndeadTerms = Config.Bind(
                "Target Families",
                "BoneUndeadTerms",
                "Skeleton;Skull;Bone;Animated Armor;JollySkeleton;Keeper Of The Barrow;KeeperOfTheBarrow",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for skeleton, bone, or animated armor enemies.", "Advanced - Target Families", "Bone Undead Terms", 50, 0));
            _constructTerms = Config.Bind(
                "Target Families",
                "ConstructTerms",
                "Stone;Golem;Construct;Automaton;Statue;CrystalCrawler;Crystal Crawler;CrystalWalker;Crystal Walker;Lost Knight;LostKnight;Forgeborn;ForgeBorn;Cairnguard;Tibby;Sentinel;Barnaclator",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for stone, golem, or construct enemies.", "Advanced - Target Families", "Construct Terms", 50, 10));
            _wyrdTerms = Config.Bind(
                "Target Families",
                "WyrdTerms",
                "Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for Wyrd enemies.", "Advanced - Target Families", "Wyrd Terms", 50, 20));
            _drownedZombieTerms = Config.Bind(
                "Target Families",
                "DrownedZombieTerms",
                "Drowner;Drowned;Drowned Knight;Ghost Crew;Scourge",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for drowned undead and corpse-sea enemies.", "Advanced - Target Families", "Drowned Zombie Terms", 50, 30));
            _infectedFleshTerms = Config.Bind(
                "Target Families",
                "InfectedFleshTerms",
                "Red Death;RedDeath;Infected",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for Red Death and infected flesh enemies.", "Advanced - Target Families", "Infected Flesh Terms", 50, 40));
            _seaFleshTerms = Config.Bind(
                "Target Families",
                "SeaFleshTerms",
                "Sarras;Finbled;Tadpole;Tidewraith;Scion;Archivist;Floatling;Reefback;Wailcap;Grindylow;Croakmaw",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for sea creatures and Sarras aquatic enemies.", "Advanced - Target Families", "Sea Flesh Terms", 50, 50));
            _spiritTerms = Config.Bind(
                "Target Families",
                "SpiritTerms",
                "Ghost;Spirit;Wraith;Banshee;Melancholy;Mistling;Mistbearer;Strawchild;Strawfather",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for spirit, ghost, and mist enemies.", "Advanced - Target Families", "Spirit Terms", 50, 60));
            _floraTerms = Config.Bind(
                "Target Families",
                "FloraTerms",
                "Dryad;Gloomfrond;Fleshtree",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for plant and fungus enemies.", "Advanced - Target Families", "Flora Terms", 50, 70));
            _fleshUndeadTerms = Config.Bind(
                "Target Families",
                "FleshUndeadTerms",
                "Zombie;Undead;Bloody;Frostbitten Warrior;Plaguewraith",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for fleshy undead. Specific drowned and infected families win when also detected.", "Advanced - Target Families", "Flesh Undead Terms", 50, 80));
            _fleshTerms = Config.Bind(
                "Target Families",
                "FleshTerms",
                "Bandit;Outlaw;Human;Humanoid;Remor;Redcap;Corpse Eater;Wolf;Bear",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for ordinary flesh targets. Specific undead, sea, spirit, flora, construct, and armor families win when also detected.", "Advanced - Target Families", "Flesh Terms", 50, 90));
            _armoredHumanoidTerms = Config.Bind(
                "Target Families",
                "ArmoredHumanoidTerms",
                "Knight;Guard;Squire;Warrior;Deserter;Kamelot;Soldier;Armor;Armored",
                ConfigUi("Semicolon, comma, pipe, or newline separated target terms for armored humanoids. This high-specificity family can override broad flesh metadata.", "Advanced - Target Families", "Armored Humanoid Terms", 50, 100));

            _diagnostics = Config.Bind("Diagnostics", "Diagnostics", false, ConfigUi("Log damage-rule classification, global difficulty adjustments, compatibility overlaps, vanilla multiplier checks, and multiplier decisions.", "Diagnostics", "Diagnostics", 90, 0));
            _showGrailFloatingTextDiagnostics = Config.Bind("Diagnostics", "ShowGrailFloatingTextDiagnostics", true, ConfigUi("When Diagnostics is enabled and Grail Floating Text is installed, show concise damage-decision summaries. Detailed BepInEx logging remains active when this is disabled.", "Diagnostics", "Show Grail Floating Text Diagnostics", 90, 5));
            _logPatchWarnings = Config.Bind("Diagnostics", "LogPatchWarnings", true, ConfigUi("Log warnings when required game methods cannot be patched.", "Diagnostics", "Patch Failure Warnings", 90, 10));

            BindDifficultyConfig();
            RestorePreservedConfigSettings();

            Grailwright.Shared.ConfigPreviousSettingsRecovery.Bind(
                Config,
                Logger,
                PluginName,
                ConfigSchemaVersion,
                ConfigRecoveryBaselineSchema,
                ConfigRecoveryKeepCurrentDefaultRules,
                ConfigRecoveryPermanentExclusions);
            Config.Save();
        }

        private void ResetConfigIfSchemaChanged()
        {
            string configPath = Config.ConfigFilePath;
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if ((string.Equals(currentSection, "1. Core", StringComparison.Ordinal)
                    || string.Equals(currentSection, "General", StringComparison.Ordinal))
                    && line.StartsWith(schemaPrefix, StringComparison.Ordinal))
                {
                    int.TryParse(
                        line.Substring(schemaPrefix.Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out storedSchemaVersion);
                    break;
                }
            }

            if (storedSchemaVersion == ConfigSchemaVersion)
            {
                return;
            }

            _pendingConfigRecoveryProfile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, string.Empty);
                Config.Clear();
                Config.Reload();
                Log.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowConfigReset(
                    PluginGuid, PluginName, storedSchemaVersion, ConfigSchemaVersion);
            }
            catch (Exception ex)
            {
                _pendingConfigRecoveryProfile = null;

                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, configPath, true);
                        Config.Clear();
                        Config.Reload();
                    }
                }
                catch (Exception restoreEx)
                {
                    Log.LogError("Failed to restore Steel and Bone config backup after schema reset failure: " + restoreEx.GetBaseException().Message);
                }

                throw new InvalidOperationException("Failed to reset Steel and Bone config schema. Original config was left in place when possible.", ex);
            }
        }

        private void RestorePreservedConfigSettings()
        {
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                _pendingConfigRecoveryProfile;
            if (profile == null)
            {
                return;
            }

            int restoredCount = 0;
            int clampedCount = 0;
            RestorePreservedSetting(profile, _enabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _preset, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _respectVanillaMultipliers, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _arrowMaterialRulesEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _materialImpactRulesEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _armoredSpellWeaknessEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _techniqueMatchupRulesEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _passiveShieldProtectionEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _eliteRuleClampsEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _eliteWeaknessBonusReduction, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _eliteMinimumResistanceMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _amplifyVanillaMultipliers, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _temperedVanillaAmplification, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _hardenedVanillaAmplification, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _crucibleVanillaAmplification, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _minimumAmplifiedVanillaResistance, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _maximumAmplifiedVanillaWeakness, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumbersEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberMode, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberBaseColor, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberFontSize, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberFontMode, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberDurationSeconds, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberCriticalDurationSeconds, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _meleeDamageNumberDurationMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberHorizontalDrift, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberVerticalDrift, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageOverTimeNumberHeightMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageOverTimeNumberScale, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberSizeContrast, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _effectivenessFeedbackSensitivity, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberColorContrast, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberMinimumAmount, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _damageNumberMaximumActive, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _boneUndeadTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _constructTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _wyrdTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _drownedZombieTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _infectedFleshTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _seaFleshTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _spiritTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _floraTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _fleshUndeadTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _fleshTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _armoredHumanoidTerms, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _diagnostics, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _showGrailFloatingTextDiagnostics, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _logPatchWarnings, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _difficultyModifiersEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPlayerDamageDealt, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _weakSpotDamageBonus, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPlayerDamageTaken, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyStaminaUsage, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyManaUsage, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyCombatManaRegeneration, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _combatManaRegenerationMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyParryWindowBonus, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _positiveParryWindowBonusMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPlayerPoiseDamageDealt, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _progressiveTenacityEnabled, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPlayerArrowVelocity, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPlayerArrowDrop, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _playerArrowGravityMultiplier, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyArmorWeightPenalties, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyLightArmorMobility, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyArmorPhysicalProtection, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyPotionOverdrinking, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyFoodRecovery, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _preventFoodUseInCombat, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _staminaDepletedVignetteMode, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _staminaDepletedVignetteFadeSeconds, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemyAttackSlots, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _enemyAttackSlotCap, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemyAttackRecovery, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemyMovementSpeed, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyHostileArrowVelocity, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _hostileArcherAimScatter, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemySightRange, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemyHearingRange, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyEnemyAggroPersistence, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyKillExperience, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyQuestExperience, ref restoredCount, ref clampedCount);
            RestorePreservedSetting(profile, _modifyProficiencyExperience, ref restoredCount, ref clampedCount);

            Log.LogInfo(
                "Preserved "
                + restoredCount.ToString(CultureInfo.InvariantCulture)
                + " customized setting(s) across the config schema reset; clamped="
                + clampedCount.ToString(CultureInfo.InvariantCulture)
                + ".");
            _pendingConfigRecoveryProfile = null;
        }

        private static void RestorePreservedSetting<T>(
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile,
            ConfigEntry<T> entry,
            ref int restoredCount,
            ref int clampedCount)
        {
            if (profile == null || entry == null)
            {
                return;
            }

            T previousValue;
            if (!profile.TryGetCustomizedValue(
                entry.Definition.Section,
                entry.Definition.Key,
                out previousValue))
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                previousValue,
                out clamped))
            {
                return;
            }

            restoredCount++;
            if (clamped)
            {
                clampedCount++;
            }
        }

        private void CacheGameAccessors()
        {
            Type heroType = AccessTools.TypeByName(HeroTypeName);
            if (heroType != null)
            {
                _heroCurrentGetter = AccessTools.PropertyGetter(heroType, "Current");
            }

            _damageSubTypeType = AccessTools.TypeByName(DamageSubTypeTypeName);
            Type multiplierDataBaseType = AccessTools.TypeByName(DamageReceivedMultiplierDataBaseTypeName);
            if (multiplierDataBaseType != null)
            {
                _getMultiplierForSubtypeMethod = AccessTools.Method(multiplierDataBaseType, "GetMultiplierForSubtype");
            }
        }

        private bool PatchGame()
        {
            _harmony = new Harmony(PluginGuid);

            Type healthElementType = AccessTools.TypeByName(HealthElementTypeName);
            if (healthElementType == null)
            {
                Warn("Could not find " + HealthElementTypeName + ". " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            MethodInfo original = AccessTools.Method(healthElementType, "ApplyDamageModifiers");
            MethodInfo postfix = AccessTools.Method(
                typeof(ApplyDamageModifiersPatch),
                nameof(ApplyDamageModifiersPatch.Postfix));
            if (original == null || postfix == null)
            {
                Warn("Could not patch HealthElement.ApplyDamageModifiers. " + PluginName + " is inactive.");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, "load-time error. Mod inactive; check BepInEx log.");
                return false;
            }

            _harmony.Patch(original, null, new HarmonyMethod(postfix));
            LogDiagnostic("Patched " + healthElementType.FullName + ".ApplyDamageModifiers.");

            MethodInfo outcomeOriginal = AccessTools.Method(healthElementType, "AfterHealthDecreaseEvents");
            MethodInfo outcomePostfix = AccessTools.Method(
                typeof(AfterHealthDecreaseEventsPatch),
                nameof(AfterHealthDecreaseEventsPatch.Postfix));
            if (outcomeOriginal == null || outcomePostfix == null)
            {
                Warn("Could not patch HealthElement.AfterHealthDecreaseEvents. Steel and Bone damage numbers are unavailable, but damage rules remain active.");
                PatchDifficultyOverhaul();
                return true;
            }

            _harmony.Patch(outcomeOriginal, null, new HarmonyMethod(outcomePostfix));
            LogDiagnostic("Patched " + healthElementType.FullName + ".AfterHealthDecreaseEvents.");
            PatchDifficultyOverhaul();
            return true;
        }

        internal void ApplyDamageRuleModifier(
            object healthElement,
            object damage,
            DamageModifiersInfo modifiersInfo,
            ref float damageModifier)
        {
            if (_enabled == null || !_enabled.Value || healthElement == null || damage == null)
            {
                return;
            }

            Damage typedDamage = damage as Damage;
            if (typedDamage != null && typedDamage.Type == DamageType.Interact)
            {
                return;
            }

            object hero = GetCurrentHero();
            if (hero == null)
            {
                return;
            }

            object heroHealthElement = GetOptionalPropertyValue(hero, "HealthElement");
            object target = ResolveDamageTargetOwner(healthElement, damage);
            bool targetIsHero = ReferenceEquals(healthElement, heroHealthElement)
                || (target != null && IsSameModelOrOwner(target, hero));
            if (targetIsHero)
            {
                ApplyIncomingHealthDamageModifier(ref damageModifier);
                ApplyPassiveShieldProtection(hero as Hero, damage as Damage, ref damageModifier);
                return;
            }

            if (!IsHeroDamageSource(damage, hero))
            {
                return;
            }

            ApplyWeakSpotDamageBonus(modifiersInfo, ref damageModifier);
            ApplyOutgoingHealthDamageModifier(ref damageModifier);

            TargetClassification targetClass = GetTargetClassification(target, healthElement);
            DamageClassification damageClass = ClassifyDamage(damage);
            LogDamageCheckDiagnostic(target ?? healthElement, damage, targetClass, damageClass);

            if (TryApplyWeightedDamageComposition(targetClass, damageClass, damage, ref damageModifier))
            {
                return;
            }

            VanillaMultiplierAmplification vanillaAmplification;
            bool appliedVanillaAmplification =
                TryApplyVanillaMultiplierAmplification(damage, damageClass, ref damageModifier, out vanillaAmplification);

            DamageRuleMatch match;
            bool skippedForVanilla;
            bool skippedForEliteClamp;
            bool matchedRule;
            DamageRuleMatch arrowMatch;
            if (damageClass.IsArrow
                && (_arrowMaterialRulesEnabled == null || _arrowMaterialRulesEnabled.Value)
                && TryResolveArrowMaterialRule(targetClass, damage, out arrowMatch))
            {
                DamageRuleMatch payloadMatch;
                bool payloadSkippedForVanilla;
                bool payloadSkippedForEliteClamp;
                bool matchedPayloadRule = TryResolveDamageRule(
                    targetClass,
                    damageClass,
                    damage,
                    DamageTag.Slashing | DamageTag.Piercing | DamageTag.Bludgeoning | DamageTag.GenericPhysical | DamageTag.Arrow | DamageTag.DirectSpell,
                    out payloadMatch,
                    out payloadSkippedForVanilla,
                    out payloadSkippedForEliteClamp);

                float physicalShare = GetPhysicalDamageShare(damage, damageClass);
                float payloadMultiplier = matchedPayloadRule ? payloadMatch.Multiplier : 1.0f;
                float combinedMultiplier = (physicalShare * arrowMatch.Multiplier)
                    + ((1.0f - physicalShare) * payloadMultiplier);
                match = new DamageRuleMatch(
                    combinedMultiplier,
                    arrowMatch.TargetLabel,
                    matchedPayloadRule ? "Arrow + " + payloadMatch.DamageLabel : "Arrow",
                    arrowMatch.Priority,
                    GetRuleImpact(combinedMultiplier),
                    arrowMatch.PresetMultiplier,
                    arrowMatch.WasEliteClamped || (matchedPayloadRule && payloadMatch.WasEliteClamped));
                skippedForVanilla = payloadSkippedForVanilla;
                skippedForEliteClamp = payloadSkippedForEliteClamp;
                matchedRule = true;
            }
            else if (TryResolvePommelMaterialRule(
                targetClass,
                damageClass,
                damage,
                out match,
                out skippedForVanilla))
            {
                skippedForEliteClamp = false;
                matchedRule = true;
            }
            else if (TryResolveColdWeaknessRule(
                targetClass,
                damageClass,
                damage,
                DamageTag.None,
                out match,
                out skippedForVanilla,
                out skippedForEliteClamp))
            {
                matchedRule = true;
            }
            else if (TryResolveArmoredSpellRule(targetClass, damageClass, damage, out match, out skippedForVanilla))
            {
                skippedForEliteClamp = false;
                matchedRule = true;
            }
            else
            {
                matchedRule = TryResolveDamageRule(
                    targetClass,
                    damageClass,
                    damage,
                    out match,
                    out skippedForVanilla,
                    out skippedForEliteClamp);
            }
            if (matchedRule)
            {
                TryApplyHeavyMaterialBreach(targetClass, damageClass, damage, ref match);
            }
            else if (TryResolveAreaSwarmRule(
                targetClass,
                damageClass,
                damage,
                out match,
                out skippedForVanilla))
            {
                skippedForEliteClamp = false;
                matchedRule = true;
            }
            if (!matchedRule && !appliedVanillaAmplification)
            {
                LogNoRuleDiagnostic(target ?? healthElement, targetClass, damageClass, skippedForVanilla, skippedForEliteClamp);
                if (skippedForVanilla || skippedForEliteClamp)
                {
                    string outcome = skippedForVanilla
                        ? "vanilla response preserved"
                        : "elite clamp kept the result neutral";
                    ShowDamageDecisionDiagnostic(
                        "skip|" + DescribeTargetFamilies(targetClass) + "|" + DescribeDamageTags(damageClass) + "|" + outcome,
                        DescribeTargetFamilies(targetClass)
                            + " + "
                            + DescribeDamageTags(damageClass)
                            + " -> "
                            + outcome);
                }
                RememberDamageFeedback(damage, 1.0f, "Neutral", "Neutral");
                return;
            }

            if (!matchedRule)
            {
                LogNoRuleDiagnostic(target ?? healthElement, targetClass, damageClass, skippedForVanilla, skippedForEliteClamp);
                RememberDamageFeedback(
                    damage,
                    vanillaAmplification.AmplifiedMultiplier,
                    "Vanilla",
                    vanillaAmplification.SubtypeName);
                ShowDamageDecisionDiagnostic(
                    "vanilla|"
                        + vanillaAmplification.SubtypeName
                        + "|"
                        + vanillaAmplification.AmplifiedMultiplier.ToString("0.###", CultureInfo.InvariantCulture),
                    "Vanilla "
                        + vanillaAmplification.SubtypeName
                        + " -> x"
                        + vanillaAmplification.AmplifiedMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }

            float multiplier = Clamp(match.Multiplier, 0.05f, 2.0f);
            float before = damageModifier;
            damageModifier *= multiplier;
            if (multiplier <= 0.0001f)
            {
                damageModifier = 0.0f;
            }

            float feedbackMultiplier = multiplier;
            if (appliedVanillaAmplification)
            {
                feedbackMultiplier *= vanillaAmplification.AmplifiedMultiplier;
            }

            RememberDamageFeedback(damage, feedbackMultiplier, match.TargetLabel, match.DamageLabel);
            ShowDamageDecisionDiagnostic(
                "rule|"
                    + match.TargetLabel
                    + "|"
                    + match.DamageLabel
                    + "|"
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + "|"
                    + match.WasEliteClamped,
                match.TargetLabel
                    + " + "
                    + match.DamageLabel
                    + " -> x"
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + (match.WasEliteClamped ? " (elite-clamped)" : ""));
            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Applied damage rule: target="
                    + DescribeObject(target ?? healthElement)
                    + ", detectedFamilies="
                    + DescribeTargetFamilies(targetClass)
                    + ", targetFlags="
                    + DescribeTargetFlags(targetClass)
                    + ", detectedDamageTags="
                    + DescribeDamageTags(damageClass)
                    + ", family="
                    + match.TargetLabel
                    + ", damage="
                    + match.DamageLabel
                    + ", preset="
                    + _preset.Value
                    + ", multiplier="
                    + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + (match.WasEliteClamped
                        ? ", eliteClamp="
                            + match.PresetMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + " -> "
                            + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                        : "")
                    + ", damageModifier "
                    + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + " -> "
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private bool TryApplyVanillaMultiplierAmplification(
            object damage,
            DamageClassification damageClass,
            ref float damageModifier,
            out VanillaMultiplierAmplification amplification)
        {
            amplification = default(VanillaMultiplierAmplification);
            if (_amplifyVanillaMultipliers == null
                || !_amplifyVanillaMultipliers.Value
                || damage == null
                || damageClass == null)
            {
                return false;
            }

            float strength = GetVanillaAmplificationStrength();
            if (strength <= 0.0001f)
            {
                return false;
            }

            if (!TryBuildVanillaMultiplierAmplification(damage, damageClass, strength, out amplification))
            {
                return false;
            }

            if (!HasMeaningfulEffect(amplification.AdjustmentMultiplier))
            {
                amplification = default(VanillaMultiplierAmplification);
                return false;
            }

            float before = damageModifier;
            damageModifier *= amplification.AdjustmentMultiplier;

            if (DiagnosticsEnabled())
            {
                LogDiagnostic(
                    "Amplified vanilla multiplier: subtypes="
                    + amplification.SubtypeName
                    + ", preset="
                    + (_preset == null ? Preset.Hardened : _preset.Value).ToString()
                    + ", vanillaMultiplier="
                    + amplification.NativeMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", amplifiedMultiplier="
                    + amplification.AmplifiedMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", adjustment="
                    + amplification.AdjustmentMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", damageModifier "
                    + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + " -> "
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
            }

            return true;
        }

        private bool TryBuildVanillaMultiplierAmplification(
            object damage,
            DamageClassification damageClass,
            float strength,
            out VanillaMultiplierAmplification amplification)
        {
            amplification = default(VanillaMultiplierAmplification);
            if (damage == null || damageClass == null)
            {
                return false;
            }

            StringBuilder subtypeBuilder = new StringBuilder();
            float nativeProduct = 1.0f;
            float amplifiedProduct = 1.0f;
            float adjustmentProduct = 1.0f;
            bool found = false;

            for (int i = 0; i < NativeSubtypeChecks.Length; i++)
            {
                NativeSubtypeCheck check = NativeSubtypeChecks[i];
                if (!damageClass.HasAny(check.Tag) || !DamageHasSubtype(damage, check.SubtypeName))
                {
                    continue;
                }

                float multiplier;
                if (!TryGetNativeDamageMultiplier(damage, check.SubtypeName, out multiplier) || !HasMeaningfulEffect(multiplier))
                {
                    continue;
                }

                if (multiplier <= 0.0001f)
                {
                    continue;
                }

                float amplifiedMultiplier = AmplifyVanillaMultiplier(multiplier, strength);
                if (!HasMeaningfulEffect(amplifiedMultiplier)
                    || Math.Abs(amplifiedMultiplier - multiplier) <= 0.001f)
                {
                    continue;
                }

                if (subtypeBuilder.Length > 0)
                {
                    subtypeBuilder.Append("|");
                }

                subtypeBuilder.Append(check.SubtypeName);
                nativeProduct *= multiplier;
                amplifiedProduct *= amplifiedMultiplier;
                adjustmentProduct *= amplifiedMultiplier / multiplier;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            amplification = new VanillaMultiplierAmplification(
                subtypeBuilder.ToString(),
                nativeProduct,
                amplifiedProduct,
                adjustmentProduct);
            return true;
        }

        private float AmplifyVanillaMultiplier(float nativeMultiplier, float strength)
        {
            float amplified = 1.0f + ((nativeMultiplier - 1.0f) * (1.0f + strength));
            if (nativeMultiplier < 1.0f)
            {
                return Clamp(amplified, GetMinimumAmplifiedVanillaResistance(), 0.999f);
            }

            return Clamp(amplified, 1.001f, GetMaximumAmplifiedVanillaWeakness());
        }

        private float GetVanillaAmplificationStrength()
        {
            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float value;
            switch (preset)
            {
                case Preset.Tempered:
                    value = _temperedVanillaAmplification == null ? 0.0f : _temperedVanillaAmplification.Value;
                    break;
                case Preset.Crucible:
                    value = _crucibleVanillaAmplification == null ? 0.70f : _crucibleVanillaAmplification.Value;
                    break;
                case Preset.Hardened:
                default:
                    value = _hardenedVanillaAmplification == null ? 0.35f : _hardenedVanillaAmplification.Value;
                    break;
            }

            return Clamp(value, 0.0f, 2.0f);
        }

        private float GetMinimumAmplifiedVanillaResistance()
        {
            float value = _minimumAmplifiedVanillaResistance == null ? 0.20f : _minimumAmplifiedVanillaResistance.Value;
            return Clamp(value, 0.01f, 0.95f);
        }

        private float GetMaximumAmplifiedVanillaWeakness()
        {
            float value = _maximumAmplifiedVanillaWeakness == null ? 1.85f : _maximumAmplifiedVanillaWeakness.Value;
            return Clamp(value, 1.05f, 3.00f);
        }

        private bool EliteRuleClampsEnabled()
        {
            return _eliteRuleClampsEnabled == null || _eliteRuleClampsEnabled.Value;
        }

        private float GetEliteWeaknessBonusReduction()
        {
            float value = _eliteWeaknessBonusReduction == null ? 0.10f : _eliteWeaknessBonusReduction.Value;
            return Clamp(value, 0.0f, 0.50f);
        }

        private float GetEliteMinimumResistanceMultiplier()
        {
            float value = _eliteMinimumResistanceMultiplier == null ? 0.20f : _eliteMinimumResistanceMultiplier.Value;
            return Clamp(value, 0.05f, 0.95f);
        }

        private float ApplyEliteRuleClamp(float multiplier, TargetClassification targetClass)
        {
            if (!EliteRuleClampsEnabled()
                || targetClass == null
                || !targetClass.IsEliteClass
                || !HasMeaningfulEffect(multiplier))
            {
                return multiplier;
            }

            if (multiplier > 1.0f)
            {
                float bonus = multiplier - 1.0f;
                float reducedBonus = Math.Max(0.0f, bonus - GetEliteWeaknessBonusReduction());
                return Clamp(1.0f + reducedBonus, 1.0f, 2.0f);
            }

            return Clamp(Math.Max(multiplier, GetEliteMinimumResistanceMultiplier()), 0.05f, 1.0f);
        }

        private bool TryGetOrdinaryHumanoidArmorTier(
            TargetClassification targetClass,
            out EnemyArmorTier tier,
            out string evidence)
        {
            tier = EnemyArmorTier.Unknown;
            evidence = "";
            if (targetClass == null
                || (!targetClass.IsHumanoidFlesh && !targetClass.IsArmoredHumanoid)
                || targetClass.IsBoneUndead
                || targetClass.IsConstruct
                || targetClass.IsFleshUndead
                || targetClass.IsWyrd
                || targetClass.IsDrownedZombie
                || targetClass.IsInfectedFlesh
                || targetClass.IsSeaFlesh
                || targetClass.IsSpirit
                || targetClass.IsFlora
                || targetClass.IsConfirmedSkeleton
                || targetClass.HasStoneBody
                || targetClass.HasWoodBody
                || targetClass.IsSwarm)
            {
                return false;
            }

            if (targetClass.ArmorProfile != null)
            {
                tier = targetClass.ArmorProfile.Tier;
                evidence = targetClass.ArmorProfile.Evidence;
                if (tier != EnemyArmorTier.Unknown)
                {
                    return true;
                }
            }

            if (targetClass.IsArmoredHumanoid)
            {
                tier = EnemyArmorTier.Medium;
                evidence = "terms:ArmoredHumanoid:MediumFallback";
                return true;
            }

            if (targetClass.IsFlesh && targetClass.IsHumanoidFlesh)
            {
                tier = EnemyArmorTier.Exposed;
                evidence = "family:ExposedHumanoidFlesh";
                return true;
            }

            return false;
        }

        private bool IsEffectivelyArmoredHumanoid(TargetClassification targetClass)
        {
            EnemyArmorTier tier;
            string evidence;
            return TryGetOrdinaryHumanoidArmorTier(targetClass, out tier, out evidence)
                && tier != EnemyArmorTier.Exposed;
        }

        private EnemyArmorMaterial GetOrdinaryHumanoidArmorMaterial(TargetClassification targetClass)
        {
            return targetClass != null && targetClass.ArmorProfile != null
                ? targetClass.ArmorProfile.Material
                : EnemyArmorMaterial.Unknown;
        }

        private string GetArmorTargetLabel(EnemyArmorTier tier, EnemyArmorMaterial material)
        {
            if (tier == EnemyArmorTier.Exposed)
            {
                return "Exposed Flesh";
            }

            return material == EnemyArmorMaterial.Unknown || material == EnemyArmorMaterial.None
                ? tier + " Armor"
                : tier + " " + material + " Armor";
        }

        private float DampArmorTierResistanceAgainstNativeArmor(
            float multiplier,
            TargetClassification targetClass,
            object damage)
        {
            if (multiplier >= 1.0f
                || targetClass == null
                || damage == null
                || !GetOptionalBoolProperty(damage, "CanBeReducedByArmor")
                || GetOptionalBoolProperty(damage, "IgnoreArmor"))
            {
                return multiplier;
            }

            NpcElement npc = targetClass.Key as NpcElement;
            if (npc == null && targetClass.ArmorProfile != null)
            {
                npc = targetClass.ArmorProfile.ParentModel;
            }
            if (npc == null)
            {
                return multiplier;
            }

            float armorPenetration = 0.0f;
            object parameters = GetOptionalMemberValue(damage, "Parameters");
            TryGetFloatMemberValue(parameters, "ArmorPenetration", out armorPenetration);
            float effectiveArmor = npc.TotalArmor(DamageSubType.GenericPhysical) - armorPenetration;
            if (effectiveArmor <= 0.0f)
            {
                return multiplier;
            }

            float vanillaRemaining = Damage.GetArmorMitigatedMultiplier(effectiveArmor);
            return 1.0f + ((multiplier - 1.0f) * vanillaRemaining);
        }

        private bool TryResolveArrowMaterialRule(
            TargetClassification targetClass,
            object damage,
            out DamageRuleMatch match)
        {
            match = default(DamageRuleMatch);
            if (targetClass == null)
            {
                return false;
            }

            string targetLabel;
            float baseMultiplier;
            bool ordinaryArmorResistance = false;
            if (targetClass.IsConfirmedSkeleton)
            {
                targetLabel = "Skeleton";
                baseMultiplier = 0.20f;
            }
            else if (targetClass.IsSwarm)
            {
                targetLabel = "Swarm";
                baseMultiplier = 0.35f;
            }
            else if (targetClass.IsConstruct || targetClass.HasStoneBody)
            {
                targetLabel = "Construct/Stone";
                baseMultiplier = 0.50f;
            }
            else if (targetClass.IsSpirit)
            {
                targetLabel = "Spirit";
                baseMultiplier = 0.55f;
            }
            else if (targetClass.IsFlora || targetClass.HasWoodBody)
            {
                targetLabel = "Flora/Wood";
                baseMultiplier = 0.60f;
            }
            else if (targetClass.IsDrownedZombie)
            {
                targetLabel = "Drowned";
                baseMultiplier = 0.85f;
            }
            else if (targetClass.IsInfectedFlesh)
            {
                targetLabel = "Infected Flesh";
                baseMultiplier = 1.15f;
            }
            else if (targetClass.IsSeaFlesh)
            {
                targetLabel = "Sea Flesh";
                baseMultiplier = 1.10f;
            }
            else if (targetClass.IsFleshUndead)
            {
                targetLabel = "Flesh Undead";
                baseMultiplier = 0.85f;
            }
            else if (targetClass.IsWyrd)
            {
                return false;
            }
            else
            {
                EnemyArmorTier armorTier;
                string armorEvidence;
                if (TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence))
                {
                    EnemyArmorMaterial armorMaterial = GetOrdinaryHumanoidArmorMaterial(targetClass);
                    targetLabel = GetArmorTargetLabel(armorTier, armorMaterial);
                    switch (armorTier)
                    {
                        case EnemyArmorTier.Exposed:
                            baseMultiplier = 1.20f;
                            break;
                        case EnemyArmorTier.Light:
                            baseMultiplier = 1.08f;
                            break;
                        case EnemyArmorTier.Medium:
                            baseMultiplier = 1.00f;
                            break;
                        case EnemyArmorTier.Heavy:
                            baseMultiplier = 0.75f;
                            ordinaryArmorResistance = true;
                            break;
                        default:
                            return false;
                    }
                }
                else if (targetClass.IsFlesh)
                {
                    targetLabel = "Flesh";
                    baseMultiplier = 1.12f;
                }
                else if (targetClass.IsBoneUndead)
                {
                    targetLabel = "Bone Body";
                    baseMultiplier = 0.55f;
                }
                else
                {
                    return false;
                }
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(baseMultiplier, preset);
            if (ordinaryArmorResistance)
            {
                presetMultiplier = DampArmorTierResistanceAgainstNativeArmor(
                    presetMultiplier,
                    targetClass,
                    damage);
            }
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            match = new DamageRuleMatch(
                multiplier,
                targetLabel,
                "Arrow",
                120,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TryResolveArmoredSpellRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            out DamageRuleMatch match,
            out bool skippedForVanilla)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            if ((_armoredSpellWeaknessEnabled != null && !_armoredSpellWeaknessEnabled.Value)
                || targetClass == null
                || damageClass == null
                || !damageClass.IsDirectSpell
                || damageClass.IsBloodMagic
                || damageClass.IsWyrdness
                || damageClass.IsBleed
                || damageClass.IsPoison
                || damageClass.IsWet)
            {
                return false;
            }

            EnemyArmorTier armorTier;
            string armorEvidence;
            if (!TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence)
                || armorTier == EnemyArmorTier.Exposed)
            {
                return false;
            }

            DamageTag spellTags = damageClass.Tags & (
                DamageTag.GenericMagical
                | DamageTag.Fire
                | DamageTag.Cold
                | DamageTag.Electric);
            if (spellTags == DamageTag.None)
            {
                return false;
            }

            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                spellTags,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                return false;
            }

            float tierBonus = 0.0f;
            if (GetOptionalBoolProperty(damage, "CanBeReducedByArmor")
                && !GetOptionalBoolProperty(damage, "IgnoreArmor"))
            {
                switch (armorTier)
                {
                    case EnemyArmorTier.Light:
                        tierBonus = 0.02f;
                        break;
                    case EnemyArmorTier.Medium:
                        tierBonus = 0.07f;
                        break;
                    case EnemyArmorTier.Heavy:
                        tierBonus = 0.12f;
                        break;
                }
            }

            EnemyArmorMaterial armorMaterial = GetOrdinaryHumanoidArmorMaterial(targetClass);
            float materialBonus = GetSpellArmorMaterialBonus(damageClass, armorMaterial);
            float baseMultiplier = Clamp(1.0f + tierBonus + materialBonus, 1.0f, 1.25f);
            if (!HasMeaningfulEffect(baseMultiplier))
            {
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(baseMultiplier, preset);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            string damageLabel = damageClass.IsElectric
                ? "Electric Spell"
                : damageClass.IsFire
                    ? "Fire Spell"
                    : damageClass.IsCold
                        ? "Cold Spell"
                        : "Direct Spell";
            match = new DamageRuleMatch(
                multiplier,
                GetArmorTargetLabel(armorTier, armorMaterial),
                damageLabel,
                110,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private float GetSpellArmorMaterialBonus(
            DamageClassification damageClass,
            EnemyArmorMaterial armorMaterial)
        {
            if (damageClass == null)
            {
                return 0.0f;
            }

            if (damageClass.IsElectric)
            {
                if (armorMaterial == EnemyArmorMaterial.Metal)
                {
                    return 0.10f;
                }
                if (armorMaterial == EnemyArmorMaterial.Leather)
                {
                    return 0.02f;
                }
            }

            if (damageClass.IsFire)
            {
                if (armorMaterial == EnemyArmorMaterial.Fabric
                    || armorMaterial == EnemyArmorMaterial.Metal)
                {
                    return 0.08f;
                }
                if (armorMaterial == EnemyArmorMaterial.Leather)
                {
                    return 0.05f;
                }
            }

            return damageClass.IsCold && armorMaterial == EnemyArmorMaterial.Metal
                ? 0.03f
                : 0.0f;
        }

        private float GetPhysicalDamageShare(object damage, DamageClassification damageClass)
        {
            object damageTypeData = GetOptionalMemberValue(damage, "DamageTypeData");
            object parts = GetOptionalMemberValue(damageTypeData, "Parts");
            float totalWeight = 0.0f;
            float physicalWeight = 0.0f;
            AccumulateDamagePartWeights(parts, ref totalWeight, ref physicalWeight);
            if (totalWeight > 0.0001f)
            {
                return Clamp(physicalWeight / totalWeight, 0.0f, 1.0f);
            }

            return damageClass != null && damageClass.HasAny(
                DamageTag.Slashing | DamageTag.Piercing | DamageTag.Bludgeoning | DamageTag.GenericPhysical)
                ? 1.0f
                : 0.0f;
        }

        private void AccumulateDamagePartWeights(object parts, ref float totalWeight, ref float physicalWeight)
        {
            IEnumerable enumerable = parts as IEnumerable;
            if (enumerable != null)
            {
                foreach (object part in enumerable)
                {
                    AccumulateDamagePartWeight(part, ref totalWeight, ref physicalWeight);
                }
                return;
            }

            int count = GetOptionalIntProperty(parts, "Count", -1);
            PropertyInfo indexer = count > 0 && parts != null ? GetIndexerProperty(parts.GetType()) : null;
            for (int i = 0; i < count && indexer != null; i++)
            {
                AccumulateDamagePartWeight(GetIndexedValue(indexer, parts, i), ref totalWeight, ref physicalWeight);
            }
        }

        private void AccumulateDamagePartWeight(object part, ref float totalWeight, ref float physicalWeight)
        {
            float weight;
            if (part == null
                || !TryGetFloatMemberValue(part, "TotalDamageMultiplier", out weight)
                || weight <= 0.0f
                || float.IsNaN(weight)
                || float.IsInfinity(weight))
            {
                return;
            }

            totalWeight += weight;
            object subtype = GetOptionalMemberValue(part, "SubType");
            if (ValueNameContains(subtype, "GenericPhysical")
                || ValueNameContains(subtype, "Slashing")
                || ValueNameContains(subtype, "Piercing")
                || ValueNameContains(subtype, "Bludgeoning"))
            {
                physicalWeight += weight;
            }
        }

        private bool TryResolveArmorTierPhysicalRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            DamageTag excludedTags,
            out DamageRuleMatch match,
            out bool skippedForVanilla)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            if (targetClass == null
                || damageClass == null
                || damageClass.IsArrow
                || damageClass.IsDirectSpell)
            {
                return false;
            }

            DamageTag damageTag;
            string damageLabel;
            if (damageClass.IsBludgeoning && (excludedTags & DamageTag.Bludgeoning) == DamageTag.None)
            {
                damageTag = DamageTag.Bludgeoning;
                damageLabel = "Blunt";
            }
            else if (damageClass.IsPiercing && (excludedTags & DamageTag.Piercing) == DamageTag.None)
            {
                damageTag = DamageTag.Piercing;
                damageLabel = "Pierce";
            }
            else if (damageClass.IsSlashing && (excludedTags & DamageTag.Slashing) == DamageTag.None)
            {
                damageTag = DamageTag.Slashing;
                damageLabel = "Slash";
            }
            else if (damageClass.IsGenericPhysical && (excludedTags & DamageTag.GenericPhysical) == DamageTag.None)
            {
                damageTag = DamageTag.GenericPhysical;
                damageLabel = "Physical";
            }
            else
            {
                return false;
            }

            EnemyArmorTier armorTier;
            string armorEvidence;
            if (!TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence))
            {
                return false;
            }

            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                damageTag,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                return false;
            }

            float baseMultiplier;
            switch (damageTag)
            {
                case DamageTag.Bludgeoning:
                    baseMultiplier = armorTier == EnemyArmorTier.Exposed
                        ? 1.00f
                        : armorTier == EnemyArmorTier.Light
                            ? 1.00f
                            : armorTier == EnemyArmorTier.Medium ? 1.08f : 1.15f;
                    break;
                case DamageTag.Piercing:
                    baseMultiplier = armorTier == EnemyArmorTier.Exposed
                        ? 1.06f
                        : armorTier == EnemyArmorTier.Light
                            ? 1.03f
                            : armorTier == EnemyArmorTier.Medium ? 1.00f : 0.90f;
                    break;
                case DamageTag.Slashing:
                    baseMultiplier = armorTier == EnemyArmorTier.Exposed
                        ? 1.04f
                        : armorTier == EnemyArmorTier.Light
                            ? 0.98f
                            : armorTier == EnemyArmorTier.Medium ? 0.92f : 0.82f;
                    break;
                default:
                    baseMultiplier = armorTier == EnemyArmorTier.Exposed
                        ? 1.00f
                        : armorTier == EnemyArmorTier.Light
                            ? 0.98f
                            : armorTier == EnemyArmorTier.Medium ? 0.94f : 0.88f;
                    break;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(baseMultiplier, preset);
            presetMultiplier = DampArmorTierResistanceAgainstNativeArmor(
                presetMultiplier,
                targetClass,
                damage);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            EnemyArmorMaterial armorMaterial = GetOrdinaryHumanoidArmorMaterial(targetClass);
            match = new DamageRuleMatch(
                multiplier,
                GetArmorTargetLabel(armorTier, armorMaterial),
                damageLabel,
                90,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TechniqueMatchupRulesEnabled()
        {
            return _techniqueMatchupRulesEnabled == null || _techniqueMatchupRulesEnabled.Value;
        }

        private bool MaterialImpactRulesEnabled()
        {
            return _enabled != null
                && _enabled.Value
                && (_materialImpactRulesEnabled == null || _materialImpactRulesEnabled.Value);
        }

        private bool TryResolvePommelMaterialRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            out DamageRuleMatch match,
            out bool skippedForVanilla)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            if (!TechniqueMatchupRulesEnabled()
                || targetClass == null
                || damageClass == null
                || !damageClass.IsPommel
                || damageClass.IsArrow
                || !IsMeleeDamage(damage))
            {
                return false;
            }

            DamageTag physicalTag;
            if (!TryGetPhysicalDamageTag(damageClass, out physicalTag))
            {
                return false;
            }

            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                physicalTag,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                return false;
            }

            string targetLabel;
            float baseMultiplier;
            if (TargetMatchesRule(targetClass, TargetFamily.StoneBody))
            {
                targetLabel = "Stone";
                baseMultiplier = 1.15f;
            }
            else if (TargetMatchesRule(targetClass, TargetFamily.BoneBody))
            {
                targetLabel = "Bone";
                baseMultiplier = 1.08f;
            }
            else
            {
                EnemyArmorTier armorTier;
                string armorEvidence;
                if (!TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence)
                    || armorTier == EnemyArmorTier.Exposed)
                {
                    return false;
                }

                targetLabel = GetArmorTargetLabel(
                    armorTier,
                    GetOrdinaryHumanoidArmorMaterial(targetClass));
                baseMultiplier = armorTier == EnemyArmorTier.Light
                    ? 1.00f
                    : armorTier == EnemyArmorTier.Medium ? 1.08f : 1.15f;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(baseMultiplier, preset);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);

            match = new DamageRuleMatch(
                multiplier,
                targetLabel,
                "Pommel (Blunt)",
                140,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TryApplyHeavyMaterialBreach(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            ref DamageRuleMatch match)
        {
            if (!TechniqueMatchupRulesEnabled()
                || targetClass == null
                || damageClass == null
                || !damageClass.IsHeavyAttack
                || damageClass.IsPommel
                || damageClass.IsArrow
                || !IsMeleeDamage(damage)
                || match.Multiplier >= 0.9999f
                || !IsRigidTechniqueTarget(targetClass))
            {
                return false;
            }

            DamageTag physicalTag;
            if (!TryGetPhysicalDamageTag(damageClass, out physicalTag))
            {
                return false;
            }

            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                physicalTag,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                return false;
            }

            float presetMultiplier = 1.0f + ((match.PresetMultiplier - 1.0f) * 0.60f);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            match = new DamageRuleMatch(
                multiplier,
                match.TargetLabel,
                match.DamageLabel + " (Heavy)",
                match.Priority,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TryResolveAreaSwarmRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            out DamageRuleMatch match,
            out bool skippedForVanilla)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            if (!TechniqueMatchupRulesEnabled()
                || targetClass == null
                || !targetClass.IsSwarm
                || damageClass == null
                || !damageClass.IsAreaAttack
                || damageClass.IsArrow
                || IsDamageOverTime(damage))
            {
                return false;
            }

            DamageTag nativeTags = damageClass.Tags & (
                DamageTag.Wyrdness
                | DamageTag.GenericPhysical
                | DamageTag.Slashing
                | DamageTag.Piercing
                | DamageTag.Bludgeoning
                | DamageTag.GenericMagical
                | DamageTag.Fire
                | DamageTag.Cold
                | DamageTag.Poison
                | DamageTag.Electric
                | DamageTag.Wet);
            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                nativeTags,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(1.15f, preset);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            if (!HasMeaningfulEffect(multiplier))
            {
                return false;
            }

            match = new DamageRuleMatch(
                multiplier,
                "Swarm",
                "Direct Area",
                35,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TryGetPhysicalDamageTag(
            DamageClassification damageClass,
            out DamageTag damageTag)
        {
            damageTag = DamageTag.None;
            if (damageClass == null)
            {
                return false;
            }

            if (damageClass.IsGenericPhysical)
            {
                damageTag = DamageTag.GenericPhysical;
            }
            else if (damageClass.IsBludgeoning)
            {
                damageTag = DamageTag.Bludgeoning;
            }
            else if (damageClass.IsPiercing)
            {
                damageTag = DamageTag.Piercing;
            }
            else if (damageClass.IsSlashing)
            {
                damageTag = DamageTag.Slashing;
            }

            return damageTag != DamageTag.None;
        }

        private bool IsRigidTechniqueTarget(TargetClassification targetClass)
        {
            if (targetClass == null)
            {
                return false;
            }
            if (TargetMatchesRule(targetClass, TargetFamily.BoneBody)
                || TargetMatchesRule(targetClass, TargetFamily.StoneBody))
            {
                return true;
            }

            EnemyArmorTier armorTier;
            string armorEvidence;
            return TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence)
                && armorTier != EnemyArmorTier.Exposed;
        }

        private bool TryResolveDamageRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            return TryResolveDamageRule(
                targetClass,
                damageClass,
                damage,
                DamageTag.None,
                out match,
                out skippedForVanilla,
                out skippedForEliteClamp);
        }

        private bool TryResolveDamageRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            DamageTag excludedTags,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            match = default(DamageRuleMatch);
            bool hasMatch = false;
            skippedForVanilla = false;
            skippedForEliteClamp = false;

            if (targetClass == null || damageClass == null)
            {
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            bool exactRuleSkippedForVanilla;
            bool exactRuleSkippedForEliteClamp;
            if (TryResolveExactDamageRule(
                targetClass,
                damageClass,
                damage,
                excludedTags,
                out match,
                out exactRuleSkippedForVanilla,
                out exactRuleSkippedForEliteClamp))
            {
                return true;
            }
            skippedForVanilla = exactRuleSkippedForVanilla;
            skippedForEliteClamp = exactRuleSkippedForEliteClamp;
            if (skippedForVanilla || skippedForEliteClamp)
            {
                return false;
            }

            bool axeRuleSkippedForVanilla;
            bool axeRuleSkippedForEliteClamp;
            if (TryResolveAxeMaterialRule(
                targetClass,
                damageClass,
                damage,
                excludedTags,
                out match,
                out axeRuleSkippedForVanilla,
                out axeRuleSkippedForEliteClamp))
            {
                return true;
            }
            skippedForVanilla = axeRuleSkippedForVanilla;
            skippedForEliteClamp = axeRuleSkippedForEliteClamp;
            if (skippedForVanilla || skippedForEliteClamp)
            {
                return false;
            }

            bool coldRuleSkippedForVanilla;
            bool coldRuleSkippedForEliteClamp;
            if (TryResolveColdWeaknessRule(
                targetClass,
                damageClass,
                damage,
                excludedTags,
                out match,
                out coldRuleSkippedForVanilla,
                out coldRuleSkippedForEliteClamp))
            {
                return true;
            }
            skippedForVanilla = coldRuleSkippedForVanilla;
            skippedForEliteClamp = coldRuleSkippedForEliteClamp;
            if (skippedForVanilla || skippedForEliteClamp)
            {
                return false;
            }

            bool armorTierSkippedForVanilla;
            if (TryResolveArmorTierPhysicalRule(
                targetClass,
                damageClass,
                damage,
                excludedTags,
                out match,
                out armorTierSkippedForVanilla))
            {
                return true;
            }
            skippedForVanilla = armorTierSkippedForVanilla;
            if (skippedForVanilla)
            {
                return false;
            }

            for (int i = 0; i < DamageRules.Length; i++)
            {
                DamageRule rule = DamageRules[i];
                DamageTag eligibleRuleTags = rule.DamageTags & ~excludedTags;
                if (eligibleRuleTags == DamageTag.None)
                {
                    continue;
                }
                if (rule.TargetFamily == TargetFamily.FleshUndead
                    && targetClass.IsInfectedFlesh
                    && (eligibleRuleTags & (
                        DamageTag.GenericPhysical
                        | DamageTag.Slashing
                        | DamageTag.Piercing
                        | DamageTag.Bludgeoning)) != DamageTag.None)
                {
                    continue;
                }
                if (!TargetMatchesRule(targetClass, rule.TargetFamily) || !damageClass.HasAny(eligibleRuleTags))
                {
                    continue;
                }

                string vanillaSubtype;
                float vanillaMultiplier;
                if (ShouldSkipForVanillaMultiplier(eligibleRuleTags, damageClass, damage, out vanillaSubtype, out vanillaMultiplier))
                {
                    skippedForVanilla = true;
                    if (DiagnosticsEnabled())
                    {
                        LogDiagnostic(
                            "Skipped Steel and Bone rule because vanilla already modifies "
                            + vanillaSubtype
                            + ": detectedFamilies="
                            + DescribeTargetFamilies(targetClass)
                            + ", targetFlags="
                            + DescribeTargetFlags(targetClass)
                            + ", detectedDamageTags="
                            + DescribeDamageTags(damageClass)
                            + ", targetFamily="
                            + rule.TargetLabel
                            + ", damage="
                            + rule.DamageLabel
                            + ", vanillaMultiplier="
                            + vanillaMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + ".");
                    }
                    continue;
                }

                float ruleMultiplier = ApplyPresetIntensity(rule.BaseMultiplier, preset);
                float presetMultiplier = ruleMultiplier;
                ruleMultiplier = ApplyEliteRuleClamp(ruleMultiplier, targetClass);
                if (!HasMeaningfulEffect(ruleMultiplier))
                {
                    if (Math.Abs(presetMultiplier - ruleMultiplier) > 0.001f)
                    {
                        skippedForEliteClamp = true;
                    }
                    continue;
                }

                DamageRuleMatch candidate = new DamageRuleMatch(
                    ruleMultiplier,
                    rule.TargetLabel,
                    rule.DamageLabel,
                    rule.Priority,
                    GetRuleImpact(ruleMultiplier),
                    presetMultiplier,
                    Math.Abs(presetMultiplier - ruleMultiplier) > 0.001f);

                if (!hasMatch
                    || candidate.Priority > match.Priority
                    || (candidate.Priority == match.Priority && candidate.Impact > match.Impact))
                {
                    match = candidate;
                    hasMatch = true;
                }
            }

            return hasMatch;
        }

        private bool TryResolveExactDamageRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            DamageTag excludedTags,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            match = default(DamageRuleMatch);
            bool hasMatch = false;
            skippedForVanilla = false;
            skippedForEliteClamp = false;
            if (targetClass == null || damageClass == null || targetClass.ExactTargets == ExactTarget.None)
            {
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            for (int i = 0; i < ExactDamageRules.Length; i++)
            {
                ExactDamageRule rule = ExactDamageRules[i];
                DamageTag eligibleRuleTags = rule.DamageTags & ~excludedTags;
                if ((targetClass.ExactTargets & rule.Target) == ExactTarget.None
                    || eligibleRuleTags == DamageTag.None
                    || !damageClass.HasAny(eligibleRuleTags))
                {
                    continue;
                }

                string vanillaSubtype;
                float vanillaMultiplier;
                if (ShouldSkipForVanillaMultiplier(
                    eligibleRuleTags,
                    damageClass,
                    damage,
                    out vanillaSubtype,
                    out vanillaMultiplier))
                {
                    skippedForVanilla = true;
                    continue;
                }

                float presetMultiplier = ApplyPresetIntensity(rule.BaseMultiplier, preset);
                float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
                if (!HasMeaningfulEffect(multiplier))
                {
                    skippedForEliteClamp |= Math.Abs(presetMultiplier - multiplier) > 0.001f;
                    continue;
                }

                DamageRuleMatch candidate = new DamageRuleMatch(
                    multiplier,
                    rule.TargetLabel,
                    rule.DamageLabel,
                    130,
                    GetRuleImpact(multiplier),
                    presetMultiplier,
                    Math.Abs(presetMultiplier - multiplier) > 0.001f);
                if (!hasMatch || candidate.Impact > match.Impact)
                {
                    match = candidate;
                    hasMatch = true;
                }
            }

            if (hasMatch)
            {
                skippedForVanilla = false;
                skippedForEliteClamp = false;
            }
            return hasMatch;
        }

        private bool TryResolveAxeMaterialRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            DamageTag excludedTags,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            skippedForEliteClamp = false;
            if (targetClass == null
                || damageClass == null
                || !damageClass.IsAxe
                || (!targetClass.IsFlora && !targetClass.HasWoodBody)
                || (!damageClass.IsSlashing && !damageClass.IsGenericPhysical)
                || (damageClass.IsSlashing && (excludedTags & DamageTag.Slashing) != DamageTag.None)
                || (!damageClass.IsSlashing && (excludedTags & DamageTag.GenericPhysical) != DamageTag.None))
            {
                return false;
            }

            DamageTag physicalTag = damageClass.IsSlashing ? DamageTag.Slashing : DamageTag.GenericPhysical;
            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                physicalTag,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(1.20f, preset);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            if (!HasMeaningfulEffect(multiplier))
            {
                skippedForEliteClamp = Math.Abs(presetMultiplier - multiplier) > 0.001f;
                return false;
            }

            match = new DamageRuleMatch(
                multiplier,
                "Flora/Wood",
                "Axe",
                125,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool TryResolveColdWeaknessRule(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damage,
            DamageTag excludedTags,
            out DamageRuleMatch match,
            out bool skippedForVanilla,
            out bool skippedForEliteClamp)
        {
            match = default(DamageRuleMatch);
            skippedForVanilla = false;
            skippedForEliteClamp = false;
            if (targetClass == null
                || damageClass == null
                || !damageClass.IsCold
                || (excludedTags & DamageTag.Cold) != DamageTag.None)
            {
                return false;
            }

            float baseMultiplier;
            string targetLabel;
            if (targetClass.HasInheritedColdWeakness)
            {
                baseMultiplier = 1.20f;
                targetLabel = "Inherited Cold Weakness";
            }
            else if (targetClass.IsFlamegobbler)
            {
                baseMultiplier = 1.15f;
                targetLabel = "Flamegobbler";
            }
            else if (targetClass.IsCrystalBody)
            {
                baseMultiplier = 1.20f;
                targetLabel = "Crystal Body";
            }
            else if (targetClass.IsWyrdSlime)
            {
                baseMultiplier = 1.10f;
                targetLabel = "Wyrd Slime";
            }
            else
            {
                return false;
            }

            string vanillaSubtype;
            float vanillaMultiplier;
            if (ShouldSkipForVanillaMultiplier(
                DamageTag.Cold,
                damageClass,
                damage,
                out vanillaSubtype,
                out vanillaMultiplier))
            {
                skippedForVanilla = true;
                if (DiagnosticsEnabled())
                {
                    LogDiagnostic(
                        "Skipped Steel and Bone Cold weakness because vanilla already modifies "
                        + vanillaSubtype
                        + ": targetFlags="
                        + DescribeTargetFlags(targetClass)
                        + ", targetFamily="
                        + targetLabel
                        + ", vanillaMultiplier="
                        + vanillaMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                        + ".");
                }
                return false;
            }

            Preset preset = _preset == null ? Preset.Hardened : _preset.Value;
            float presetMultiplier = ApplyPresetIntensity(baseMultiplier, preset);
            float multiplier = ApplyEliteRuleClamp(presetMultiplier, targetClass);
            if (!HasMeaningfulEffect(multiplier))
            {
                skippedForEliteClamp = Math.Abs(presetMultiplier - multiplier) > 0.001f;
                return false;
            }

            match = new DamageRuleMatch(
                multiplier,
                targetLabel,
                "Cold",
                130,
                GetRuleImpact(multiplier),
                presetMultiplier,
                Math.Abs(presetMultiplier - multiplier) > 0.001f);
            return true;
        }

        private bool ShouldSkipForVanillaMultiplier(
            DamageTag ruleTags,
            DamageClassification damageClass,
            object damage,
            out string subtypeName,
            out float nativeMultiplier)
        {
            subtypeName = "";
            nativeMultiplier = 1.0f;

            if (_respectVanillaMultipliers == null
                || !_respectVanillaMultipliers.Value
                || damageClass == null
                || damage == null)
            {
                return false;
            }

            for (int i = 0; i < NativeSubtypeChecks.Length; i++)
            {
                NativeSubtypeCheck check = NativeSubtypeChecks[i];
                if ((ruleTags & check.Tag) == DamageTag.None
                    || !damageClass.HasAny(check.Tag)
                    || !DamageHasSubtype(damage, check.SubtypeName))
                {
                    continue;
                }

                float multiplier;
                if (TryGetNativeDamageMultiplier(damage, check.SubtypeName, out multiplier) && HasMeaningfulEffect(multiplier))
                {
                    subtypeName = check.SubtypeName;
                    nativeMultiplier = multiplier;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetNativeDamageMultiplier(object damage, string subtypeName, out float multiplier)
        {
            multiplier = 1.0f;

            if (_damageSubTypeType == null || _getMultiplierForSubtypeMethod == null || string.IsNullOrEmpty(subtypeName))
            {
                return false;
            }

            object multiplierData = GetOptionalPropertyValue(damage, "DamageReceivedMultiplierData");
            if (multiplierData == null)
            {
                return false;
            }

            try
            {
                object subtype = Enum.Parse(_damageSubTypeType, subtypeName);
                object raw = _getMultiplierForSubtypeMethod.Invoke(multiplierData, new[] { subtype });
                if (raw is float)
                {
                    multiplier = (float)raw;
                    return true;
                }
                if (raw is double)
                {
                    multiplier = (float)(double)raw;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private float ApplyPresetIntensity(float baseMultiplier, Preset preset)
        {
            float strength = GetPresetIntensity(preset);
            float scaled = 1.0f + ((baseMultiplier - 1.0f) * strength);
            return Clamp(scaled, 0.05f, 2.0f);
        }

        private float GetPresetIntensity(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return 0.55f;
                case Preset.Crucible:
                    return 1.35f;
                case Preset.Hardened:
                default:
                    return 1.0f;
            }
        }

        private static float GetPresetEffectivenessFeedbackSensitivity(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tempered:
                    return 1.20f;
                case Preset.Crucible:
                    return 1.00f;
                case Preset.Hardened:
                default:
                    return 1.10f;
            }
        }

        private void ApplyPresetEffectivenessFeedbackSensitivity()
        {
            if (_effectivenessFeedbackSensitivity == null || _preset == null)
            {
                return;
            }

            float presetValue = GetPresetEffectivenessFeedbackSensitivity(_preset.Value);
            if (Math.Abs(_effectivenessFeedbackSensitivity.Value - presetValue) > 0.0001f)
            {
                _effectivenessFeedbackSensitivity.Value = presetValue;
            }
        }

        private bool HasMeaningfulEffect(float multiplier)
        {
            return GetRuleImpact(multiplier) > 0.001f;
        }

        private float GetRuleImpact(float multiplier)
        {
            return Math.Abs(multiplier - 1.0f);
        }

        private bool TargetMatchesRule(TargetClassification targetClass, TargetFamily family)
        {
            switch (family)
            {
                case TargetFamily.BoneUndead:
                    return targetClass.IsBoneUndead;
                case TargetFamily.BoneBody:
                    return targetClass.IsBoneUndead || targetClass.HasBoneBody;
                case TargetFamily.Construct:
                    return targetClass.IsConstruct;
                case TargetFamily.StoneBody:
                    return targetClass.IsConstruct || targetClass.HasStoneBody;
                case TargetFamily.ArmoredHumanoid:
                    return IsEffectivelyArmoredHumanoid(targetClass);
                case TargetFamily.Flesh:
                    return targetClass.IsFlesh;
                case TargetFamily.FleshUndead:
                    return targetClass.IsFleshUndead;
                case TargetFamily.Wyrd:
                    return targetClass.IsWyrd;
                case TargetFamily.DrownedZombie:
                    return targetClass.IsDrownedZombie;
                case TargetFamily.InfectedFlesh:
                    return targetClass.IsInfectedFlesh;
                case TargetFamily.SeaFlesh:
                    return targetClass.IsSeaFlesh;
                case TargetFamily.Spirit:
                    return targetClass.IsSpirit;
                case TargetFamily.Flora:
                    return targetClass.IsFlora;
                default:
                    return false;
            }
        }

        private TargetClassification GetTargetClassification(object target, object healthElement)
        {
            object key = target ?? healthElement;
            if (key == null)
            {
                return TargetClassification.Empty;
            }

            int cacheKey = RuntimeHelpers.GetHashCode(key);
            TargetClassification cached;
            if (_targetClassifications.TryGetValue(cacheKey, out cached)
                && cached.Revision == _targetTermsRevision
                && ReferenceEquals(cached.Key, key))
            {
                return cached;
            }

            string text = BuildObjectSearchText(target);
            if (healthElement != null && !ReferenceEquals(healthElement, target))
            {
                text = text + " " + BuildObjectSearchText(healthElement);
            }

            TargetClassification classification = new TargetClassification
            {
                Key = key,
                Revision = _targetTermsRevision
            };

            classification.HasInheritedColdWeakness = ContainsAnyTerm(text, InheritedColdWeaknessTerms);
            classification.IsFlamegobbler = ContainsAnyTerm(text, FlamegobblerTerms);
            classification.IsCrystalBody = ContainsAnyTerm(text, CrystalBodyTerms);
            classification.IsWyrdSlime = ContainsAnyTerm(text, WyrdSlimeColdWeaknessTerms);
            if (classification.IsCrystalBody)
            {
                classification.IsConstruct = true;
                classification.HasStoneBody = true;
                AppendClassificationEvidence(classification, "terms:CrystalBody");
            }

            string metadataText = BuildTargetMetadataSearchText(target, healthElement);
            classification.IsConfirmedSkeleton = ContainsAnyTerm(metadataText, MetadataConfirmedSkeletonTerms)
                || ContainsAnyTerm(text, ConfirmedSkeletonTerms);
            classification.HasBoneBody = ContainsAnyTerm(metadataText, MetadataBoneBodyTerms);
            classification.HasStoneBody = classification.IsCrystalBody
                || ContainsAnyTerm(metadataText, MetadataStoneBodyTerms);
            classification.HasWoodBody = ContainsAnyTerm(metadataText, MetadataWoodBodyTerms);
            classification.IsHumanoidFlesh = ContainsAnyTerm(metadataText, MetadataHumanoidTerms)
                || ContainsAnyTerm(text, HumanoidFleshTerms);
            classification.IsSwarm = ContainsAnyTerm(text, SwarmTerms);
            classification.IsBossClass = ContainsAnyTerm(metadataText, MetadataBossTerms)
                || ContainsAnyTerm(text, MetadataBossTerms);
            classification.IsBear = ContainsAnyTerm(text, EnemyMovementBearTerms);
            classification.IsBulkyMonster = ContainsAnyTerm(text, EnemyMovementBulkyMonsterTerms);
            ApplyMetadataTargetClassification(classification, metadataText);
            if (!classification.HasMetadataFamily())
            {
                ApplyTermTargetClassification(classification, text);
            }
            else
            {
                ApplyBroadFamilyTermOverrides(classification, text);
                ApplySpecificTermTargetClassification(classification, text);
            }
            ApplyExactTargetClassification(classification, text + " " + metadataText);

            NpcElement npc = target as NpcElement;
            if (npc != null && (classification.IsHumanoidFlesh || classification.IsArmoredHumanoid))
            {
                EnemyArmorProfile armorProfile = npc.TryGetElement<EnemyArmorProfile>();
                if (armorProfile == null)
                {
                    armorProfile = npc.AddElement(new EnemyArmorProfile());
                }

                classification.ArmorProfile = armorProfile;
            }

            _targetClassifications[cacheKey] = classification;
            return classification;
        }

        private void ApplyExactTargetClassification(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (ContainsAnyTerm(text, RootambusherTerms))
            {
                classification.ExactTargets |= ExactTarget.Rootambusher;
                SetExclusiveTargetFamily(classification, TargetFamily.Flora);
                AppendClassificationEvidence(classification, "exact:RootambusherFlora");
            }
            if (ContainsAnyTerm(text, FrostbittenWarriorTerms))
            {
                classification.ExactTargets |= ExactTarget.FrostbittenWarrior;
                SetExclusiveTargetFamily(classification, TargetFamily.FleshUndead);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:FrostbittenWarriorUndead");
            }
            if (ContainsAnyTerm(text, FrostgrotTerms))
            {
                classification.ExactTargets |= ExactTarget.Frostgrot;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:FrostgrotFlesh");
            }
            if (ContainsAnyTerm(text, WightTerms))
            {
                classification.ExactTargets |= ExactTarget.Wight;
                SetExclusiveTargetFamily(classification, TargetFamily.Flora);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:WightFlora");
            }
            if (ContainsAnyTerm(text, GiantTerms))
            {
                classification.ExactTargets |= ExactTarget.Giant;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:GiantFlesh");
            }
            if (ContainsAnyTerm(text, MissingCorpseEaterReactionTerms))
            {
                classification.ExactTargets |= ExactTarget.MissingCorpseEaterReaction;
            }
            if (ContainsAnyTerm(text, ElectricStagfatherGolemTerms))
            {
                classification.ExactTargets |= ExactTarget.ElectricStagfatherGolem;
            }
            if (ContainsAnyTerm(text, MistbearerTerms))
            {
                classification.ExactTargets |= ExactTarget.Mistbearer;
            }
            if (ContainsAnyTerm(text, WyrdheirChallengeTerms))
            {
                classification.ExactTargets |= ExactTarget.WyrdheirChallenge;
            }
            if (ContainsAnyTerm(text, NiveraTerms))
            {
                classification.ExactTargets |= ExactTarget.Nivera;
            }
            if (ContainsAnyTerm(text, RimefiendTerms))
            {
                classification.ExactTargets |= ExactTarget.Rimefiend;
            }
            if (ContainsAnyTerm(text, FrostWolfTerms))
            {
                classification.ExactTargets |= ExactTarget.FrostWolf;
            }
            if (ContainsAnyTerm(text, StrawParentTerms))
            {
                classification.ExactTargets |= ExactTarget.StrawParent;
                SetExclusiveTargetFamily(classification, TargetFamily.Spirit);
                classification.HasBoneBody = true;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:StrawParentSpiritBoneBody");
            }
            if (ContainsAnyTerm(text, StagfatherTerms))
            {
                SetExclusiveTargetFamily(classification, TargetFamily.Spirit);
                classification.HasBoneBody = true;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:StagfatherSpiritBoneBody");
            }
            if (ContainsAnyTerm(text, GhostOfBrocMealaTerms))
            {
                SetExclusiveTargetFamily(classification, TargetFamily.Spirit);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:GhostOfBrocMealaSpirit");
            }
            if (ContainsAnyTerm(text, SleepwalkerTerms))
            {
                SetExclusiveTargetFamily(classification, TargetFamily.Wyrd);
                classification.HasBoneBody = false;
                classification.HasStoneBody = true;
                AppendClassificationEvidence(classification, "exact:SleepwalkerWyrdStoneBody");
            }
            if (ContainsAnyTerm(text, WailcapTerms))
            {
                SetExclusiveTargetFamily(classification, TargetFamily.SeaFlesh);
                AppendClassificationEvidence(classification, "exact:WailcapSeaCreature");
            }
            if (ContainsAnyTerm(text, WyrdspawnTerms))
            {
                classification.ExactTargets |= ExactTarget.Wyrdspawn;
            }
            if (ContainsAnyTerm(text, OgreTerms))
            {
                classification.ExactTargets |= ExactTarget.Ogre;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:OgreFlesh");
            }
            if (ContainsAnyTerm(text, FireAlignedTerms))
            {
                classification.ExactTargets |= ExactTarget.FireAligned;
            }
            if (ContainsAnyTerm(text, ElementalStagfatherGolemTerms))
            {
                classification.ExactTargets |= ExactTarget.ElementalStagfatherGolem;
                SetExclusiveTargetFamily(classification, TargetFamily.Construct);
                classification.HasBoneBody = false;
                classification.HasStoneBody = true;
                AppendClassificationEvidence(classification, "exact:ElementalStagfatherGolemConstruct");
            }
            if (ContainsAnyTerm(text, DrownedSkeletonSailorTerms))
            {
                classification.ExactTargets |= ExactTarget.DrownedSkeletonSailor;
                SetExclusiveTargetFamily(classification, TargetFamily.BoneUndead);
                classification.HasBoneBody = true;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:DrownedSkeletonSailorBone");
            }
            if (ContainsAnyTerm(text, FrostAngelTerms))
            {
                classification.ExactTargets |= ExactTarget.FrostAngel;
            }
            if (ContainsAnyTerm(text, IceWeaverChampionTerms))
            {
                classification.ExactTargets |= ExactTarget.IceWeaverChampion;
            }
            if (ContainsAnyTerm(text, IceWeaverWolfTerms))
            {
                classification.ExactTargets |= ExactTarget.IceWeaverWolf;
            }
            if (ContainsAnyTerm(text, IceTrialWyrdTerms))
            {
                classification.ExactTargets |= ExactTarget.IceTrialWyrd;
                SetExclusiveTargetFamily(classification, TargetFamily.Wyrd);
                AppendClassificationEvidence(classification, "exact:IceTrialWyrd");
            }
            if (ContainsAnyTerm(text, CharredConclaveWyrdspawnTerms))
            {
                classification.ExactTargets |= ExactTarget.CharredConclaveWyrdspawn;
                SetExclusiveTargetFamily(classification, TargetFamily.Wyrd);
                AppendClassificationEvidence(classification, "exact:CharredConclaveWyrd");
            }
            if (ContainsAnyTerm(text, IceStatueTerms))
            {
                classification.ExactTargets |= ExactTarget.IceStatue;
                SetExclusiveTargetFamily(classification, TargetFamily.Construct);
                classification.HasBoneBody = false;
                classification.HasStoneBody = true;
                AppendClassificationEvidence(classification, "exact:IceStatueConstruct");
            }
            if (ContainsAnyTerm(text, AncientBeholderTerms))
            {
                classification.ExactTargets |= ExactTarget.AncientBeholder;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:AncientBeholderFlesh");
            }
            if (ContainsAnyTerm(text, SingwormTerms))
            {
                classification.ExactTargets |= ExactTarget.Singworm;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:SingwormFlesh");
            }
            if (ContainsAnyTerm(text, LirTentacleTerms))
            {
                classification.ExactTargets |= ExactTarget.LirTentacle;
                SetExclusiveTargetFamily(classification, TargetFamily.Flesh);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:LirTentacleFlesh");
            }
            if (ContainsAnyTerm(text, BloodAbominationTerms))
            {
                classification.ExactTargets |= ExactTarget.BloodAbomination;
                ClearTargetFamilies(classification);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:BloodAbominationSoftBody");
            }
            if (ContainsAnyTerm(text, WyrdSlimeColdWeaknessTerms))
            {
                classification.ExactTargets |= ExactTarget.WyrdSlime;
                SetExclusiveTargetFamily(classification, TargetFamily.Wyrd);
                classification.HasBoneBody = false;
                classification.HasStoneBody = false;
                AppendClassificationEvidence(classification, "exact:WyrdSlimeSoftBody");
            }
            if (ContainsAnyTerm(text, TidewraithTerms))
            {
                classification.ExactTargets |= ExactTarget.Tidewraith;
            }
        }

        private void SetExclusiveTargetFamily(TargetClassification classification, TargetFamily family)
        {
            classification.IsBoneUndead = family == TargetFamily.BoneUndead;
            classification.IsConstruct = family == TargetFamily.Construct;
            classification.IsArmoredHumanoid = family == TargetFamily.ArmoredHumanoid;
            classification.IsFlesh = family == TargetFamily.Flesh;
            classification.IsFleshUndead = family == TargetFamily.FleshUndead;
            classification.IsWyrd = family == TargetFamily.Wyrd;
            classification.IsDrownedZombie = family == TargetFamily.DrownedZombie;
            classification.IsInfectedFlesh = family == TargetFamily.InfectedFlesh;
            classification.IsSeaFlesh = family == TargetFamily.SeaFlesh;
            classification.IsSpirit = family == TargetFamily.Spirit;
            classification.IsFlora = family == TargetFamily.Flora;
        }

        private void ClearTargetFamilies(TargetClassification classification)
        {
            classification.IsBoneUndead = false;
            classification.IsConstruct = false;
            classification.IsArmoredHumanoid = false;
            classification.IsFlesh = false;
            classification.IsFleshUndead = false;
            classification.IsWyrd = false;
            classification.IsDrownedZombie = false;
            classification.IsInfectedFlesh = false;
            classification.IsSeaFlesh = false;
            classification.IsSpirit = false;
            classification.IsFlora = false;
        }

        private void ApplyMetadataTargetClassification(TargetClassification classification, string metadataText)
        {
            if (classification == null || string.IsNullOrEmpty(metadataText))
            {
                return;
            }

            if (ContainsAnyTerm(metadataText, MetadataBoneUndeadTerms))
            {
                classification.IsBoneUndead = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:BoneUndead");
            }
            if (ContainsAnyTerm(metadataText, MetadataConstructTerms))
            {
                classification.IsConstruct = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Construct");
            }
            if (ContainsAnyTerm(metadataText, MetadataWyrdTerms))
            {
                classification.IsWyrd = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Wyrd");
            }
            if (ContainsAnyTerm(metadataText, MetadataDrownedZombieTerms))
            {
                classification.IsDrownedZombie = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:DrownedZombie");
            }
            if (ContainsAnyTerm(metadataText, MetadataSeaFleshTerms))
            {
                classification.IsSeaFlesh = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:SeaFlesh");
            }
            if (ContainsAnyTerm(metadataText, MetadataSpiritTerms))
            {
                classification.IsSpirit = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Spirit");
            }
            if (ContainsAnyTerm(metadataText, MetadataFloraTerms))
            {
                classification.IsFlora = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Flora");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(metadataText, MetadataFleshUndeadTerms))
            {
                classification.IsFleshUndead = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:FleshUndead");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(metadataText, MetadataFleshTerms))
            {
                classification.IsFlesh = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:Flesh");
            }
            if (ContainsAnyTerm(metadataText, MetadataEliteTerms))
            {
                classification.IsEliteClass = true;
                classification.HasMetadataEvidence = true;
                AppendClassificationEvidence(classification, "metadata:EliteClass");
            }
        }

        private void ApplyTermTargetClassification(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (ContainsAnyTerm(text, GetBoneUndeadTerms()))
            {
                classification.IsBoneUndead = true;
                AppendClassificationEvidence(classification, "terms:BoneUndead");
            }
            if (ContainsAnyTerm(text, GetConstructTerms()))
            {
                classification.IsConstruct = true;
                AppendClassificationEvidence(classification, "terms:Construct");
            }
            if (ContainsAnyTerm(text, GetWyrdTerms()))
            {
                classification.IsWyrd = true;
                AppendClassificationEvidence(classification, "terms:Wyrd");
            }
            if (ContainsAnyTerm(text, GetDrownedZombieTerms()))
            {
                classification.IsDrownedZombie = true;
                AppendClassificationEvidence(classification, "terms:DrownedZombie");
            }
            if (ContainsAnyTerm(text, GetInfectedFleshTerms()))
            {
                classification.IsInfectedFlesh = true;
                AppendClassificationEvidence(classification, "terms:InfectedFlesh");
            }
            if (ContainsAnyTerm(text, GetSeaFleshTerms()))
            {
                classification.IsSeaFlesh = true;
                AppendClassificationEvidence(classification, "terms:SeaFlesh");
            }
            if (ContainsAnyTerm(text, GetSpiritTerms()))
            {
                classification.IsSpirit = true;
                AppendClassificationEvidence(classification, "terms:Spirit");
            }
            if (ContainsAnyTerm(text, GetFloraTerms()))
            {
                classification.IsFlora = true;
                AppendClassificationEvidence(classification, "terms:Flora");
            }
            if (!classification.HasAnyFamily() && ContainsAnyTerm(text, GetFleshUndeadTerms()))
            {
                classification.IsFleshUndead = true;
                AppendClassificationEvidence(classification, "terms:FleshUndead");
            }
            ApplySpecificTermTargetClassification(classification, text);
            if (!classification.HasAnyFamily() && ContainsAnyTerm(text, GetFleshTerms()))
            {
                classification.IsFlesh = true;
                AppendClassificationEvidence(classification, "terms:Flesh");
            }
        }

        private void ApplyBroadFamilyTermOverrides(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!classification.IsFlesh && !classification.IsFleshUndead)
            {
                return;
            }

            if (ContainsAnyTerm(text, GetBoneUndeadTerms()))
            {
                classification.IsBoneUndead = true;
                AppendClassificationEvidence(classification, "terms:BoneUndead");
            }
            if (ContainsAnyTerm(text, GetConstructTerms()))
            {
                classification.IsConstruct = true;
                AppendClassificationEvidence(classification, "terms:Construct");
            }
            if (ContainsAnyTerm(text, GetWyrdTerms()))
            {
                classification.IsWyrd = true;
                AppendClassificationEvidence(classification, "terms:Wyrd");
            }
            if (ContainsAnyTerm(text, GetDrownedZombieTerms()))
            {
                classification.IsDrownedZombie = true;
                AppendClassificationEvidence(classification, "terms:DrownedZombie");
            }
            if (ContainsAnyTerm(text, GetInfectedFleshTerms()))
            {
                classification.IsInfectedFlesh = true;
                AppendClassificationEvidence(classification, "terms:InfectedFlesh");
            }
            if (ContainsAnyTerm(text, GetSeaFleshTerms()))
            {
                classification.IsSeaFlesh = true;
                AppendClassificationEvidence(classification, "terms:SeaFlesh");
            }
            if (ContainsAnyTerm(text, GetSpiritTerms()))
            {
                classification.IsSpirit = true;
                AppendClassificationEvidence(classification, "terms:Spirit");
            }
            if (ContainsAnyTerm(text, GetFloraTerms()))
            {
                classification.IsFlora = true;
                AppendClassificationEvidence(classification, "terms:Flora");
            }
        }

        private void ApplySpecificTermTargetClassification(TargetClassification classification, string text)
        {
            if (classification == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (classification.HasAnyFamily() && !classification.IsFlesh && !classification.IsFleshUndead)
            {
                return;
            }

            if (ContainsAnyTerm(text, GetArmoredHumanoidTerms()))
            {
                classification.IsArmoredHumanoid = true;
                AppendClassificationEvidence(classification, "terms:ArmoredHumanoid");
            }
        }

        private void AppendClassificationEvidence(TargetClassification classification, string evidence)
        {
            if (classification == null || string.IsNullOrEmpty(evidence))
            {
                return;
            }

            if (classification.Evidence == null)
            {
                classification.Evidence = evidence;
                return;
            }

            if (classification.Evidence.IndexOf(evidence, StringComparison.OrdinalIgnoreCase) < 0)
            {
                classification.Evidence = classification.Evidence + "|" + evidence;
            }
        }

        private DamageClassification ClassifyDamage(object damage)
        {
            if (damage == null)
            {
                return DamageClassification.Empty;
            }

            DamageClassification classification = new DamageClassification();
            string damageSearchText = BuildDamageSearchText(damage).ToLowerInvariant();
            classification.IsBleed = ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Bleed")
                || TextContainsAny(damageSearchText, BleedTerms);
            classification.IsPoison = DamageHasSubtype(damage, "Poison")
                || ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Poison")
                || TextContainsAny(damageSearchText, PoisonTerms);
            classification.IsWyrdness = DamageHasSubtype(damage, "Wyrdness")
                || TextContainsAny(damageSearchText, WyrdTerms);
            classification.IsBloodMagic = TextContainsAny(damageSearchText, BloodMagicTerms);
            classification.IsSlashing = DamageHasSubtype(damage, "Slashing");
            classification.IsPiercing = DamageHasSubtype(damage, "Piercing");
            classification.IsBludgeoning = DamageHasSubtype(damage, "Bludgeoning");
            classification.IsGenericPhysical = DamageHasSubtype(damage, "GenericPhysical");
            classification.IsMiningToolCombatHit = IsMiningToolCombatHit(damage);
            if (classification.IsMiningToolCombatHit)
            {
                classification.IsSlashing = false;
                classification.IsPiercing = true;
                classification.IsBludgeoning = false;
                classification.IsGenericPhysical = false;
                classification.PhysicalTypeHint = "Piercing from Mining tool combat hit";
            }
            else if (!classification.HasSpecificPhysicalType()
                && (classification.IsGenericPhysical || ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "PhysicalHitSource")))
            {
                AddPhysicalWeaponTypeHints(damage, classification);
            }
            classification.IsAxe = !classification.IsMiningToolCombatHit && IsAxeDamage(damage);
            classification.IsGenericMagical = DamageHasSubtype(damage, "GenericMagical");
            classification.IsBurn = ValueNameContains(GetOptionalPropertyValue(damage, "StatusDamageType"), "Burn");
            classification.IsFire = DamageHasSubtype(damage, "Fire") || classification.IsBurn;
            classification.IsCold = DamageHasSubtype(damage, "Cold");
            classification.IsElectric = DamageHasSubtype(damage, "Electric");
            classification.IsWet = DamageHasSubtype(damage, "Wet");
            classification.IsArrow = IsArrowDamage(damage);
            classification.IsDirectSpell = ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "MagicalHitSource")
                && !IsDamageOverTime(damage);
            Damage typedDamage = damage as Damage;
            classification.IsPommel = typedDamage != null && typedDamage.IsPush;
            classification.IsHeavyAttack = typedDamage != null && typedDamage.IsHeavyAttack;
            classification.IsAreaAttack = typedDamage != null && typedDamage.Radius > 0.0001f;

            if (classification.IsBloodMagic)
            {
                classification.Tags |= DamageTag.BloodMagic;
            }
            if (classification.IsBleed)
            {
                classification.Tags |= DamageTag.Bleed;
            }
            if (classification.IsPoison)
            {
                classification.Tags |= DamageTag.Poison;
            }
            if (classification.IsWyrdness)
            {
                classification.Tags |= DamageTag.Wyrdness;
            }
            if (classification.IsSlashing)
            {
                classification.Tags |= DamageTag.Slashing;
            }
            if (classification.IsPiercing)
            {
                classification.Tags |= DamageTag.Piercing;
            }
            if (classification.IsBludgeoning)
            {
                classification.Tags |= DamageTag.Bludgeoning;
            }
            if (classification.IsGenericPhysical)
            {
                classification.Tags |= DamageTag.GenericPhysical;
            }
            if (classification.IsGenericMagical)
            {
                classification.Tags |= DamageTag.GenericMagical;
            }
            if (classification.IsFire)
            {
                classification.Tags |= DamageTag.Fire;
            }
            if (classification.IsCold)
            {
                classification.Tags |= DamageTag.Cold;
            }
            if (classification.IsElectric)
            {
                classification.Tags |= DamageTag.Electric;
            }
            if (classification.IsWet)
            {
                classification.Tags |= DamageTag.Wet;
            }
            if (classification.IsBurn)
            {
                classification.Tags |= DamageTag.Burn;
            }
            if (classification.IsArrow)
            {
                classification.Tags |= DamageTag.Arrow;
            }
            if (classification.IsDirectSpell)
            {
                classification.Tags |= DamageTag.DirectSpell;
            }

            return classification;
        }

        private bool IsArrowDamage(object damage)
        {
            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            if (projectile == null || IsDestroyedUnityObject(projectile))
            {
                return false;
            }

            Type projectileType = projectile.GetType();
            while (projectileType != null)
            {
                if (projectileType.Name.IndexOf("ThrowingKnife", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
                if (string.Equals(projectileType.Name, "Arrow", StringComparison.OrdinalIgnoreCase)
                    || projectileType.Name.EndsWith("Arrow", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                projectileType = projectileType.BaseType;
            }

            return false;
        }

        private bool IsDamageOverTime(object damage)
        {
            Damage typedDamage = damage as Damage;
            if (typedDamage != null)
            {
                return typedDamage.IsDamageOverTime;
            }

            object statusDamageType = GetOptionalPropertyValue(damage, "StatusDamageType");
            return ValueNameContains(statusDamageType, "Bleed")
                || ValueNameContains(statusDamageType, "Poison")
                || ValueNameContains(statusDamageType, "Burn")
                || ValueNameContains(statusDamageType, "Breath");
        }

        private bool IsOneDamageDirectAttack(object damage)
        {
            Damage typedDamage = damage as Damage;
            if (typedDamage == null
                || typedDamage.IsDamageOverTime
                || typedDamage.Amount != 1.0f)
            {
                return false;
            }

            return typedDamage.Type == DamageType.PhysicalHitSource
                || typedDamage.Type == DamageType.MagicalHitSource;
        }

        private bool IsMeleeDamage(object damage)
        {
            if (damage == null
                || IsDamageOverTime(damage)
                || !ValueNameContains(GetOptionalPropertyValue(damage, "Type"), "PhysicalHitSource"))
            {
                return false;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            return projectile == null;
        }

        private bool DamageHasSubtype(object damage, string subtypeName)
        {
            return EnumerablePartsContainName(GetOptionalPropertyValue(damage, "SubTypes"), "SubType", subtypeName)
                || EnumerablePartsContainName(GetOptionalPropertyValue(GetOptionalPropertyValue(damage, "DamageTypeData"), "OriginalParts"), "SubType", subtypeName);
        }

        private void AddPhysicalWeaponTypeHints(object damage, DamageClassification classification)
        {
            if (damage == null || classification == null)
            {
                return;
            }

            if (TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(damage, "Item"), classification))
            {
                return;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            if (TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(projectile, "SourceWeapon"), classification))
            {
                return;
            }

            TryApplyPhysicalWeaponTypeHint(GetOptionalPropertyValue(projectile, "SourceProjectile"), classification);
        }

        private bool IsAxeDamage(object damage)
        {
            if (damage == null)
            {
                return false;
            }

            if (IsAxeCandidate(GetOptionalPropertyValue(damage, "Item")))
            {
                return true;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            return IsAxeCandidate(GetOptionalPropertyValue(projectile, "SourceWeapon"))
                || IsAxeCandidate(GetOptionalPropertyValue(projectile, "SourceProjectile"));
        }

        private bool IsMiningToolCombatHit(object damage)
        {
            Damage typedDamage = damage as Damage;
            if (typedDamage == null
                || typedDamage.Type != DamageType.PhysicalHitSource
                || typedDamage.Item == null)
            {
                return false;
            }

            Tool tool;
            return typedDamage.Item.TryGetElement(out tool)
                && tool != null
                && tool.Type == ToolType.Mining;
        }

        private bool IsAxeCandidate(object candidate)
        {
            if (candidate == null || IsDestroyedUnityObject(candidate))
            {
                return false;
            }

            if (GetOptionalBoolProperty(candidate, "IsAxe"))
            {
                return true;
            }

            object item = GetOptionalPropertyValue(candidate, "Item");
            object parent = GetOptionalPropertyValue(candidate, "ParentModel");
            object template = GetOptionalPropertyValue(candidate, "Template");
            return (item != null && !ReferenceEquals(item, candidate) && GetOptionalBoolProperty(item, "IsAxe"))
                || (parent != null && !ReferenceEquals(parent, candidate) && GetOptionalBoolProperty(parent, "IsAxe"))
                || (template != null && !ReferenceEquals(template, candidate) && GetOptionalBoolProperty(template, "IsAxe"));
        }

        private bool TryApplyPhysicalWeaponTypeHint(object candidate, DamageClassification classification)
        {
            if (candidate == null || classification == null || IsDestroyedUnityObject(candidate))
            {
                return false;
            }

            if (TryApplyPhysicalWeaponTypeHintFromSingleObject(candidate, classification))
            {
                return true;
            }

            object item = GetOptionalPropertyValue(candidate, "Item");
            if (item != null && !ReferenceEquals(item, candidate) && TryApplyPhysicalWeaponTypeHintFromSingleObject(item, classification))
            {
                return true;
            }

            object parent = GetOptionalPropertyValue(candidate, "ParentModel");
            if (parent != null && !ReferenceEquals(parent, candidate) && TryApplyPhysicalWeaponTypeHintFromSingleObject(parent, classification))
            {
                return true;
            }

            object template = GetOptionalPropertyValue(candidate, "Template");
            return template != null
                && !ReferenceEquals(template, candidate)
                && TryApplyPhysicalWeaponTypeHintFromSingleObject(template, classification);
        }

        private bool TryApplyPhysicalWeaponTypeHintFromSingleObject(object candidate, DamageClassification classification)
        {
            if (candidate == null || classification == null || IsDestroyedUnityObject(candidate))
            {
                return false;
            }

            if (GetOptionalBoolProperty(candidate, "IsBlunt"))
            {
                classification.IsBludgeoning = true;
                classification.PhysicalTypeHint = "Bludgeoning from " + DescribeObject(candidate);
                return true;
            }

            if (GetOptionalBoolProperty(candidate, "IsPolearm")
                || GetOptionalBoolProperty(candidate, "IsDagger")
                || GetOptionalBoolProperty(candidate, "IsRanged")
                || GetOptionalBoolProperty(candidate, "IsArrow"))
            {
                classification.IsPiercing = true;
                classification.PhysicalTypeHint = "Piercing from " + DescribeObject(candidate);
                return true;
            }

            if (GetOptionalBoolProperty(candidate, "IsSword") || GetOptionalBoolProperty(candidate, "IsAxe"))
            {
                classification.IsSlashing = true;
                classification.PhysicalTypeHint = "Slashing from " + DescribeObject(candidate);
                return true;
            }

            return false;
        }

        private void LogDamageCheckDiagnostic(
            object target,
            object damage,
            TargetClassification targetClass,
            DamageClassification damageClass)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            object item = GetOptionalPropertyValue(damage, "Item");
            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            string physicalHint = string.IsNullOrEmpty(damageClass.PhysicalTypeHint)
                ? ""
                : ", physicalHint=" + damageClass.PhysicalTypeHint;
            string targetEvidence = targetClass == null || string.IsNullOrEmpty(targetClass.Evidence)
                ? ""
                : ", familyEvidence=" + targetClass.Evidence;
            string targetFlags = DescribeTargetFlags(targetClass);
            EnemyArmorTier armorTier;
            string armorEvidence;
            string armorProfile = TryGetOrdinaryHumanoidArmorTier(targetClass, out armorTier, out armorEvidence)
                ? ", armorTier=" + armorTier + ", armorEvidence=" + armorEvidence
                : "";

            LogDiagnostic(
                "Damage check: target="
                + DescribeObject(target)
                + ", families="
                + DescribeTargetFamilies(targetClass)
                + targetEvidence
                + ", targetFlags="
                + targetFlags
                + armorProfile
                + ", damageType="
                + DescribeValue(GetOptionalPropertyValue(damage, "Type"))
                + ", statusDamageType="
                + DescribeValue(GetOptionalPropertyValue(damage, "StatusDamageType"))
                + ", item="
                + DescribeObject(item)
                + ", projectile="
                + DescribeObject(projectile)
                + ", damageTags="
                + DescribeDamageTags(damageClass)
                + physicalHint
                + ".");
        }

        private void LogNoRuleDiagnostic(
            object target,
            TargetClassification targetClass,
            DamageClassification damageClass,
            bool skippedForVanilla,
            bool skippedForEliteClamp)
        {
            if (!DiagnosticsEnabled())
            {
                return;
            }

            LogDiagnostic(
                "No Steel and Bone rule matched: target="
                + DescribeObject(target)
                + ", families="
                + DescribeTargetFamilies(targetClass)
                + ", targetFlags="
                + DescribeTargetFlags(targetClass)
                + ", damageTags="
                + DescribeDamageTags(damageClass)
                + ", reason="
                + GetNoRuleReason(targetClass, damageClass, skippedForVanilla, skippedForEliteClamp)
                + ".");
        }

        private string GetNoRuleReason(
            TargetClassification targetClass,
            DamageClassification damageClass,
            bool skippedForVanilla,
            bool skippedForEliteClamp)
        {
            if (skippedForVanilla)
            {
                return "vanilla already handled matching subtype";
            }

            if (skippedForEliteClamp)
            {
                return "elite clamp neutralized custom rule";
            }

            if (targetClass == null || !targetClass.HasAnyFamily())
            {
                return "no target family";
            }

            if (damageClass == null || damageClass.Tags == DamageTag.None)
            {
                return "no damage tags";
            }

            return "no family/tag rule";
        }

        private bool EnumerablePartsContainName(object parts, string propertyName, string expected)
        {
            IEnumerable enumerable = parts as IEnumerable;
            if (enumerable != null)
            {
                foreach (object part in enumerable)
                {
                    if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                    {
                        return true;
                    }
                }
            }

            int count = GetOptionalIntProperty(parts, "Count", -1);
            if (count > 0)
            {
                PropertyInfo itemProperty = GetIndexerProperty(parts.GetType());
                if (itemProperty != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        object part = GetIndexedValue(itemProperty, parts, i);
                        if (ValueNameContains(GetOptionalPropertyValue(part, propertyName), expected) || ValueNameContains(part, expected))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TextContainsAny(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                string term = terms[i];
                if (!string.IsNullOrEmpty(term) && text.Contains(term.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildDamageSearchText(object damage)
        {
            if (damage == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(DescribeObject(damage)).Append(' ');
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Item"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "BlockingItem"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Skill"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Type"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "StatusDamageType"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "DamageTypeData"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(damage, "Parameters"));

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            AppendObjectSearchText(builder, projectile);
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceWeapon"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "SourceProjectile"));
            AppendObjectSearchText(builder, GetOptionalPropertyValue(projectile, "Skill"));

            return builder.ToString();
        }

        private void AppendObjectSearchText(StringBuilder builder, object obj)
        {
            if (builder == null || obj == null || IsDestroyedUnityObject(obj))
            {
                return;
            }

            builder.Append(BuildObjectSearchText(obj)).Append(' ');
            builder.Append(DescribeObject(obj)).Append(' ');
        }

        private string BuildObjectSearchText(object obj)
        {
            if (obj == null || IsDestroyedUnityObject(obj))
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            Type type = obj.GetType();
            builder.Append(type.FullName).Append(' ');
            builder.Append(type.Name).Append(' ');
            AppendStringProperty(builder, obj, "Name");
            AppendStringProperty(builder, obj, "DisplayName");
            AppendStringProperty(builder, obj, "DebugName");
            AppendStringProperty(builder, obj, "TechnicalName");
            AppendStringProperty(builder, obj, "Id");
            AppendStringProperty(builder, obj, "ID");
            AppendMemberSearchText(builder, obj, "SurfaceType");
            AppendMemberSearchText(builder, obj, "surfaceType");
            AppendMemberSearchText(builder, obj, "NpcType");
            AppendMemberSearchText(builder, obj, "npcType");
            AppendMemberSearchText(builder, obj, "Tags");
            AppendMemberSearchText(builder, obj, "tags");
            AppendMemberSearchText(builder, obj, "AbstractTypes");
            AppendMemberSearchText(builder, obj, "_abstractTypes");

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj) && !IsDestroyedUnityObject(template))
            {
                Type templateType = template.GetType();
                builder.Append(templateType.FullName).Append(' ');
                builder.Append(templateType.Name).Append(' ');
                AppendStringProperty(builder, template, "Name");
                AppendStringProperty(builder, template, "DisplayName");
                AppendStringProperty(builder, template, "DebugName");
                AppendStringProperty(builder, template, "TechnicalName");
                AppendStringProperty(builder, template, "GUID");
                AppendStringProperty(builder, template, "Guid");
                AppendMemberSearchText(builder, template, "SurfaceType");
                AppendMemberSearchText(builder, template, "surfaceType");
                AppendMemberSearchText(builder, template, "NpcType");
                AppendMemberSearchText(builder, template, "npcType");
                AppendMemberSearchText(builder, template, "Tags");
                AppendMemberSearchText(builder, template, "tags");
                AppendMemberSearchText(builder, template, "AbstractTypes");
                AppendMemberSearchText(builder, template, "_abstractTypes");
            }

            return builder.ToString();
        }

        private string BuildTargetMetadataSearchText(object target, object healthElement)
        {
            StringBuilder builder = new StringBuilder();
            AppendTargetMetadataSearchText(builder, target);
            if (healthElement != null && !ReferenceEquals(healthElement, target))
            {
                AppendTargetMetadataSearchText(builder, healthElement);
            }

            return builder.ToString();
        }

        private void AppendTargetMetadataSearchText(StringBuilder builder, object obj)
        {
            if (builder == null || obj == null || IsDestroyedUnityObject(obj))
            {
                return;
            }

            AppendSingleTargetMetadataSearchText(builder, obj);

            object template = GetOptionalPropertyValue(obj, "Template");
            if (template != null && !ReferenceEquals(template, obj) && !IsDestroyedUnityObject(template))
            {
                AppendSingleTargetMetadataSearchText(builder, template);
            }
        }

        private void AppendSingleTargetMetadataSearchText(StringBuilder builder, object obj)
        {
            AppendMemberSearchText(builder, obj, "SurfaceType");
            AppendMemberSearchText(builder, obj, "surfaceType");
            AppendMemberSearchText(builder, obj, "NpcType");
            AppendMemberSearchText(builder, obj, "npcType");
            AppendMemberSearchText(builder, obj, "Tags");
            AppendMemberSearchText(builder, obj, "tags");
            AppendMemberSearchText(builder, obj, "AbstractTypes");
            AppendMemberSearchText(builder, obj, "_abstractTypes");
        }

        private void AppendStringProperty(StringBuilder builder, object obj, string propertyName)
        {
            object raw = GetOptionalPropertyValue(obj, propertyName);
            if (raw == null)
            {
                return;
            }

            string value = raw as string;
            if (value == null)
            {
                value = raw.ToString();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append(value).Append(' ');
            }
        }

        private void AppendMemberSearchText(StringBuilder builder, object obj, string memberName)
        {
            object value = GetOptionalMemberValue(obj, memberName);
            AppendSearchValue(builder, value, 0);
        }

        private void AppendSearchValue(StringBuilder builder, object value, int depth)
        {
            if (builder == null || value == null || IsDestroyedUnityObject(value) || depth > 2)
            {
                return;
            }

            string text = value as string;
            if (text != null)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.Append(text).Append(' ');
                }
                return;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                builder.Append(value).Append(' ');
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count >= 32)
                    {
                        break;
                    }

                    AppendSearchValue(builder, item, depth + 1);
                    count++;
                }
                return;
            }

            if (depth < 2)
            {
                builder.Append(type.Name).Append(' ');
                AppendStringProperty(builder, value, "Name");
                AppendStringProperty(builder, value, "DisplayName");
                AppendStringProperty(builder, value, "DebugName");
                AppendStringProperty(builder, value, "TechnicalName");
            }
            else
            {
                builder.Append(value).Append(' ');
            }
        }

        private object ResolveDamageTargetOwner(object healthElement, object damage)
        {
            object target = GetOptionalPropertyValue(damage, "Target");
            if (target == null)
            {
                target = GetOptionalPropertyValue(damage, "TargetPure");
            }
            if (target == null)
            {
                target = ResolveHealthElementOwner(healthElement);
            }

            return target;
        }

        private object ResolveHealthElementOwner(object healthElement)
        {
            if (healthElement == null)
            {
                return null;
            }

            string[] ownerProperties = { "ParentModel", "GenericParentModel", "NpcElement", "Character", "CharacterView", "Owner", "Parent" };
            for (int i = 0; i < ownerProperties.Length; i++)
            {
                object value = GetOptionalPropertyValue(healthElement, ownerProperties[i]);
                if (value != null && !ReferenceEquals(value, healthElement))
                {
                    return value;
                }
            }

            return null;
        }

        private object GetCurrentHero()
        {
            if (_heroCurrentGetter == null)
            {
                return null;
            }

            try
            {
                return _heroCurrentGetter.Invoke(null, null);
            }
            catch
            {
                return null;
            }
        }

        private bool IsHeroDamageSource(object damage, object hero)
        {
            if (damage == null || hero == null)
            {
                return false;
            }

            object damageDealer = GetOptionalPropertyValue(damage, "DamageDealerPure");
            if (damageDealer == null)
            {
                damageDealer = GetOptionalPropertyValue(damage, "DamageDealer");
            }
            if (IsSameModelOrOwner(damageDealer, hero)
                || IsHeroSummonSource(damageDealer))
            {
                return true;
            }

            object projectile = GetOptionalPropertyValue(damage, "Projectile");
            object projectileOwner = GetOptionalPropertyValue(projectile, "Owner");
            return IsSameModelOrOwner(projectileOwner, hero)
                || IsHeroSummonSource(projectileOwner);
        }

        private static bool IsHeroSummonSource(object candidate)
        {
            NpcElement npc = candidate as NpcElement;
            return npc != null && npc.IsHeroSummon;
        }

        private bool IsSameModelOrOwner(object candidate, object expected)
        {
            if (candidate == null || expected == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, expected))
            {
                return true;
            }

            string[] properties = { "ParentModel", "GenericParentModel", "Owner", "Character", "Hero" };
            for (int i = 0; i < properties.Length; i++)
            {
                object value = GetOptionalPropertyValue(candidate, properties[i]);
                if (ReferenceEquals(value, expected))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] GetBoneUndeadTerms()
        {
            return GetTerms(_boneUndeadTerms, ref _cachedBoneUndeadTermsRaw, ref _cachedBoneUndeadTerms);
        }

        private string[] GetConstructTerms()
        {
            return GetTerms(_constructTerms, ref _cachedConstructTermsRaw, ref _cachedConstructTerms);
        }

        private string[] GetWyrdTerms()
        {
            return GetTerms(_wyrdTerms, ref _cachedWyrdTermsRaw, ref _cachedWyrdTerms);
        }

        private string[] GetDrownedZombieTerms()
        {
            return GetTerms(_drownedZombieTerms, ref _cachedDrownedZombieTermsRaw, ref _cachedDrownedZombieTerms);
        }

        private string[] GetInfectedFleshTerms()
        {
            return GetTerms(_infectedFleshTerms, ref _cachedInfectedFleshTermsRaw, ref _cachedInfectedFleshTerms);
        }

        private string[] GetSeaFleshTerms()
        {
            return GetTerms(_seaFleshTerms, ref _cachedSeaFleshTermsRaw, ref _cachedSeaFleshTerms);
        }

        private string[] GetSpiritTerms()
        {
            return GetTerms(_spiritTerms, ref _cachedSpiritTermsRaw, ref _cachedSpiritTerms);
        }

        private string[] GetFloraTerms()
        {
            return GetTerms(_floraTerms, ref _cachedFloraTermsRaw, ref _cachedFloraTerms);
        }

        private string[] GetFleshUndeadTerms()
        {
            return GetTerms(_fleshUndeadTerms, ref _cachedFleshUndeadTermsRaw, ref _cachedFleshUndeadTerms);
        }

        private string[] GetFleshTerms()
        {
            return GetTerms(_fleshTerms, ref _cachedFleshTermsRaw, ref _cachedFleshTerms);
        }

        private string[] GetArmoredHumanoidTerms()
        {
            return GetTerms(_armoredHumanoidTerms, ref _cachedArmoredHumanoidTermsRaw, ref _cachedArmoredHumanoidTerms);
        }

        private string[] GetTerms(ConfigEntry<string> entry, ref string cachedRaw, ref string[] cachedTerms)
        {
            string raw = entry == null ? "" : (entry.Value ?? "");
            if (raw != cachedRaw)
            {
                cachedRaw = raw;
                cachedTerms = SplitTerms(raw);
                _targetTermsRevision++;
                _targetClassifications.Clear();
            }

            return cachedTerms;
        }

        private string[] SplitTerms(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }

            string[] pieces = raw.Split(new[] { ';', ',', '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> terms = new List<string>();
            for (int i = 0; i < pieces.Length; i++)
            {
                string term = pieces[i].Trim();
                if (term.Length > 0)
                {
                    terms.Add(term);
                }
            }

            return terms.ToArray();
        }

        private bool ContainsAnyTerm(string text, string[] terms)
        {
            if (string.IsNullOrEmpty(text) || terms == null || terms.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!string.IsNullOrEmpty(terms[i]) && text.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RememberDamageFeedback(object damage, float multiplier, string targetLabel, string damageLabel)
        {
            if (_enabled == null || !_enabled.Value || damage == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            PrunePendingDamageFeedback(now);

            int key = RuntimeHelpers.GetHashCode(damage);
            _pendingDamageFeedback[key] = new PendingDamageFeedback
            {
                Damage = damage,
                Multiplier = multiplier,
                TargetLabel = targetLabel,
                DamageLabel = damageLabel,
                IsMelee = IsMeleeDamage(damage),
                CreatedAt = now
            };

            TrimPendingDamageFeedback();
        }

        internal void HandleDamageOutcome(object healthElement, object damageOutcome)
        {
            if (damageOutcome == null || _enabled == null || !_enabled.Value)
            {
                return;
            }

            object damage = GetOptionalMemberValue(damageOutcome, "Damage");
            PendingDamageFeedback feedback;
            if (!TryConsumeDamageFeedback(damage, out feedback) && !IsOutgoingHeroDamageOutcome(healthElement, damage))
            {
                return;
            }

            float finalAmount;
            if (!TryGetFloatMemberValue(damageOutcome, "FinalAmount", out finalAmount)
                && !TryGetFloatMemberValue(damage, "Amount", out finalAmount))
            {
                return;
            }

            if (float.IsNaN(finalAmount) || float.IsInfinity(finalAmount) || finalAmount < 0.0f)
            {
                return;
            }

            bool immune = finalAmount <= 0.0001f;
            Vector3 position;
            if (!TryGetVector3MemberValue(damageOutcome, "Position", out position)
                && !TryGetVector3MemberValue(damage, "Position", out position))
            {
                position = Vector3.zero;
            }

            object modifiersInfo = GetOptionalMemberValue(damageOutcome, "DamageModifiersInfo");
            bool critical = IsTrueMember(damage, "Critical")
                || IsTrueMember(damage, "IsCritical")
                || IsTrueMember(modifiersInfo, "IsCritical");
            bool weakSpot = IsTrueMember(damage, "WeakSpotHit")
                || IsTrueMember(damage, "IsWeakSpot")
                || IsTrueMember(modifiersInfo, "IsWeakSpot");

            float criticalBonus = critical ? 0.50f : 0.0f;
            float resolvedCriticalBonus;
            if (critical
                && TryGetFloatMemberValue(modifiersInfo, "CriticalMultiplier", out resolvedCriticalBonus))
            {
                criticalBonus = Mathf.Max(0.0f, resolvedCriticalBonus);
            }

            float nativeWeakSpotBonus = 0.0f;
            float resolvedWeakSpotBonus;
            if (weakSpot
                && TryGetFloatMemberValue(modifiersInfo, "WeakSpotMultiplier", out resolvedWeakSpotBonus))
            {
                nativeWeakSpotBonus = Mathf.Max(0.0f, resolvedWeakSpotBonus);
            }

            float steelAndBoneWeakSpotBonus = weakSpot
                ? GetActiveWeakSpotDamageBonus()
                : 0.0f;
            float precisionBonus = criticalBonus
                + nativeWeakSpotBonus
                + steelAndBoneWeakSpotBonus;

            bool damageOverTime = IsDamageOverTime(damage);
            bool oneDamageDirectAttack = finalAmount == 1.0f
                && IsOneDamageDirectAttack(damage);
            bool hitMarkerImmune = immune || oneDamageDirectAttack;
            float effectivenessMultiplier = feedback == null ? 1.0f : feedback.Multiplier;
            float visualEffectivenessMultiplier = ApplyEffectivenessFeedbackSensitivity(effectivenessMultiplier);
            DamageNumberVisual visual = BuildDamageNumberVisual(
                finalAmount,
                feedback,
                visualEffectivenessMultiplier,
                critical,
                weakSpot,
                precisionBonus,
                immune,
                damageOverTime,
                feedback == null ? IsMeleeDamage(damage) : feedback.IsMelee);
            if (oneDamageDirectAttack)
            {
                visual.Text = "RESISTED";
            }
            SteelAndBoneHitFeedbackApi.Publish(
                effectivenessMultiplier,
                visualEffectivenessMultiplier,
                hitMarkerImmune,
                critical,
                weakSpot,
                damageOverTime,
                "#" + ColorUtility.ToHtmlStringRGBA(visual.Color),
                visual.DurationSeconds);

            object resolvedTarget = ResolveDamageTargetOwner(healthElement, damage);
            object remainingHealth = GetOptionalPropertyValue(healthElement, "Health");
            if (finalAmount > 0.0001f
                && remainingHealth != null
                && ReadStatValue(remainingHealth) <= 0.0001f
                && resolvedTarget is NpcElement)
            {
                NpcElement defeatedNpc = (NpcElement)resolvedTarget;
                float killXp = TryReadKillXp(defeatedNpc);
                float maxHealth = TryReadMaxHealth(defeatedNpc);
                int nativeTier;
                bool hasNativeTier = TryReadNativeTier(defeatedNpc, out nativeTier);
                bool hasQualityEvidence;
                float quality01 = Grailwright.Shared.CorpseQualityBuckets.CalculateIntrinsicQuality01(
                    hasNativeTier ? nativeTier : -1,
                    killXp,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceKillXp,
                    maxHealth,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceMaxHealth,
                    out hasQualityEvidence,
                    out _);
                quality01 = Grailwright.Shared.CorpseQualityBuckets.ApplyThreatClassAdjustment(
                    quality01,
                    ResolveCorpseQualityThreatClass(defeatedNpc));
                quality01 = ApplyCorpseQualityLevelAdjustment(
                    quality01,
                    defeatedNpc,
                    out _);
                Grailwright.Shared.CorpseQualityTier qualityTier =
                    Grailwright.Shared.CorpseQualityBuckets.GetTier(
                        quality01,
                        hasQualityEvidence);
                if (qualityTier != Grailwright.Shared.CorpseQualityTier.None)
                {
                    SteelAndBoneHitFeedbackApi.PublishKillingBlow(
                        (int)qualityTier,
                        quality01,
                        visualEffectivenessMultiplier,
                        hitMarkerImmune,
                        critical,
                        weakSpot,
                        damageOverTime,
                        "#" + ColorUtility.ToHtmlStringRGBA(visual.Color),
                        visual.DurationSeconds);
                }
            }

            if (TryPrepareDamageNumberForDisplay(
                resolvedTarget,
                healthElement,
                visual,
                finalAmount,
                visualEffectivenessMultiplier,
                immune,
                oneDamageDirectAttack,
                damageOverTime))
            {
                _damageNumberRenderer.ShowDamageNumber(position, visual);
            }
        }

        private bool TryPrepareDamageNumberForDisplay(
            object target,
            object healthElement,
            DamageNumberVisual visual,
            float finalAmount,
            float visualEffectivenessMultiplier,
            bool immune,
            bool oneDamageDirectAttack,
            bool damageOverTime)
        {
            if (!DamageNumbersActive())
            {
                return false;
            }

            DamageNumberMode mode = GetDamageNumberMode();
            if (mode == DamageNumberMode.AllDamage)
            {
                return immune || finalAmount > GetDamageNumberMinimumAmount();
            }

            if (damageOverTime)
            {
                return false;
            }

            bool resisted = oneDamageDirectAttack || visualEffectivenessMultiplier < 0.95f;
            if (!immune && !resisted)
            {
                return false;
            }

            visual.Text = immune ? "IMMUNE" : "RESISTED";
            if (mode != DamageNumberMode.ResistAndImmuneOnlyOnce)
            {
                return true;
            }

            TargetClassification classification = GetTargetClassification(target, healthElement);
            if (ReferenceEquals(classification, TargetClassification.Empty))
            {
                return true;
            }

            if (immune)
            {
                if (classification.ImmunityNoticeShown)
                {
                    return false;
                }

                classification.ImmunityNoticeShown = true;
                return true;
            }

            if (classification.ResistanceNoticeShown)
            {
                return false;
            }

            classification.ResistanceNoticeShown = true;
            return true;
        }

        private bool DamageNumbersActive()
        {
            return _enabled != null
                && _enabled.Value
                && _damageNumbersEnabled != null
                && _damageNumbersEnabled.Value
                && _damageNumberRenderer != null;
        }

        private DamageNumberMode GetDamageNumberMode()
        {
            return _damageNumberMode == null
                ? DamageNumberMode.AllDamage
                : _damageNumberMode.Value;
        }

        private bool IsOutgoingHeroDamageOutcome(object healthElement, object damage)
        {
            object hero = GetCurrentHero();
            if (hero == null || !IsHeroDamageSource(damage, hero))
            {
                return false;
            }

            object heroHealthElement = GetOptionalPropertyValue(hero, "HealthElement");
            if (ReferenceEquals(healthElement, heroHealthElement))
            {
                return false;
            }

            object target = ResolveDamageTargetOwner(healthElement, damage);
            return target == null || !IsSameModelOrOwner(target, hero);
        }

        private float TryReadKillXp(object target)
        {
            if (target == null)
            {
                return 0.0f;
            }

            object template = GetOptionalPropertyValue(target, "Template");
            float value = TryReadKillXpDirect(template);
            return value > 0.0f ? value : TryReadKillXpDirect(target);
        }

        private bool TryReadNativeTier(NpcElement npc, out int nativeTier)
        {
            nativeTier = -1;
            if (npc == null || npc.Template == null || npc.Template.Tags == null)
            {
                return false;
            }

            foreach (string tag in npc.Template.Tags)
            {
                for (int tier = 0; tier <= 7; tier++)
                {
                    if (string.Equals(tag, "Tier:" + tier, StringComparison.Ordinal))
                    {
                        nativeTier = tier;
                        return true;
                    }
                }
            }

            nativeTier = -1;
            return false;
        }

        private float ApplyCorpseQualityLevelAdjustment(
            float intrinsicQuality01,
            NpcElement npc,
            out bool adjusted)
        {
            adjusted = false;
            Hero hero = Hero.Current;
            if (npc == null || npc.Template == null || hero == null)
            {
                return intrinsicQuality01;
            }

            return Grailwright.Shared.CorpseQualityBuckets.ApplyBoundedRelativeLevelAdjustment(
                intrinsicQuality01,
                npc.Template.ExpLevel,
                (float)hero.Level,
                Grailwright.Shared.CorpseQualityBuckets.DefaultLevelQualityPerLevel,
                Grailwright.Shared.CorpseQualityBuckets.DefaultMaximumLevelQualityAdjustment,
                out adjusted);
        }

        private Grailwright.Shared.CorpseQualityThreatClass ResolveCorpseQualityThreatClass(
            NpcElement npc)
        {
            if (npc == null || npc.Template == null)
            {
                return Grailwright.Shared.CorpseQualityThreatClass.Normal;
            }

            switch (npc.Template.NpcType)
            {
                case NpcType.Elite:
                    return Grailwright.Shared.CorpseQualityThreatClass.Elite;
                case NpcType.MiniBoss:
                    return Grailwright.Shared.CorpseQualityThreatClass.MiniBoss;
                case NpcType.Boss:
                    return Grailwright.Shared.CorpseQualityThreatClass.Boss;
                default:
                    return Grailwright.Shared.CorpseQualityThreatClass.Normal;
            }
        }

        private float TryReadKillXpDirect(object target)
        {
            if (target == null)
            {
                return 0.0f;
            }

            string[] propertyNames =
            {
                "ExpReward",
                "XPReward",
                "XpReward",
                "ExperienceReward"
            };
            for (int i = 0; i < propertyNames.Length; i++)
            {
                float value;
                if (TryGetFloatMemberValue(target, propertyNames[i], out value)
                    && value > 0.0f)
                {
                    return value;
                }
            }

            MethodInfo method = AccessTools.Method(target.GetType(), "GetExpReward", new Type[0]);
            if (method == null)
            {
                return 0.0f;
            }

            try
            {
                return ConvertNumericValue(method.Invoke(target, null), 0.0f);
            }
            catch
            {
                return 0.0f;
            }
        }

        private float TryReadMaxHealth(object target)
        {
            if (target == null)
            {
                return 0.0f;
            }

            float value = ReadStatValue(GetOptionalPropertyValue(target, "MaxHealth"));
            if (value > 0.0f)
            {
                return value;
            }

            object healthElement = GetOptionalPropertyValue(target, "HealthElement");
            return ReadStatValue(GetOptionalPropertyValue(healthElement, "MaxHealth"));
        }

        private float ReadStatValue(object stat)
        {
            float direct = ConvertNumericValue(stat, -1.0f);
            if (direct >= 0.0f)
            {
                return direct;
            }

            string[] propertyNames =
            {
                "ModifiedValue",
                "BaseValue",
                "ValueForSave",
                "PredictedValue",
                "Value",
                "CurrentValue"
            };
            for (int i = 0; i < propertyNames.Length; i++)
            {
                float value;
                if (TryGetFloatMemberValue(stat, propertyNames[i], out value)
                    && value >= 0.0f)
                {
                    return value;
                }
            }

            return 0.0f;
        }

        private float ConvertNumericValue(object value, float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return value is IConvertible
                    ? Convert.ToSingle(value, CultureInfo.InvariantCulture)
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private bool TryPeekDamageFeedback(object damage, out PendingDamageFeedback feedback)
        {
            feedback = null;
            if (damage == null)
            {
                return false;
            }

            PrunePendingDamageFeedback(Time.unscaledTime);

            int key = RuntimeHelpers.GetHashCode(damage);
            PendingDamageFeedback pending;
            if (!_pendingDamageFeedback.TryGetValue(key, out pending) || !ReferenceEquals(pending.Damage, damage))
            {
                return false;
            }

            feedback = pending;
            return true;
        }

        private bool TryGetDamageEffectivenessMultiplier(Damage damage, out float multiplier)
        {
            multiplier = 1.0f;
            if (damage == null)
            {
                return false;
            }

            if (IsFullyNativeImmune(damage))
            {
                multiplier = 0.0f;
                return true;
            }

            PendingDamageFeedback feedback;
            if (!TryPeekDamageFeedback(damage, out feedback)
                || float.IsNaN(feedback.Multiplier)
                || float.IsInfinity(feedback.Multiplier)
                || feedback.Multiplier < 0.0f)
            {
                return false;
            }

            multiplier = feedback.Multiplier;
            return true;
        }

        private bool IsFullyNativeImmune(Damage damage)
        {
            if (damage == null
                || damage.DamageTypeData == null
                || damage.DamageTypeData.Parts == null
                || damage.DamageReceivedMultiplierData == null)
            {
                return false;
            }

            float totalWeight = 0.0f;
            float weightedMultiplier = 0.0f;
            var partEnumerator = damage.DamageTypeData.Parts.GetEnumerator();
            try
            {
                while (partEnumerator.MoveNext())
                {
                    DamageTypeDataPart part = partEnumerator.Current;
                    float weight = Math.Max(0.0f, part.PercentageAsFloat);
                    if (weight <= 0.0001f)
                    {
                        continue;
                    }

                    float nativeMultiplier = damage.DamageReceivedMultiplierData.GetMultiplierForSubtype(part.SubType);
                    if (float.IsNaN(nativeMultiplier)
                        || float.IsInfinity(nativeMultiplier)
                        || nativeMultiplier < 0.0f)
                    {
                        return false;
                    }

                    totalWeight += weight;
                    weightedMultiplier += weight * nativeMultiplier;
                }
            }
            finally
            {
                partEnumerator.Dispose();
            }

            return totalWeight > 0.0001f
                && weightedMultiplier / totalWeight <= 0.0001f;
        }

        private bool TryConsumeDamageFeedback(object damage, out PendingDamageFeedback feedback)
        {
            if (!TryPeekDamageFeedback(damage, out feedback))
            {
                return false;
            }

            int key = RuntimeHelpers.GetHashCode(damage);
            _pendingDamageFeedback.Remove(key);
            return true;
        }

        private void PrunePendingDamageFeedback(float now)
        {
            if (_pendingDamageFeedback.Count == 0)
            {
                return;
            }

            List<int> expiredKeys = null;
            foreach (KeyValuePair<int, PendingDamageFeedback> pair in _pendingDamageFeedback)
            {
                if (now - pair.Value.CreatedAt <= PendingDamageFeedbackLifetimeSeconds)
                {
                    continue;
                }

                if (expiredKeys == null)
                {
                    expiredKeys = new List<int>();
                }

                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                _pendingDamageFeedback.Remove(expiredKeys[i]);
            }
        }

        private void TrimPendingDamageFeedback()
        {
            while (_pendingDamageFeedback.Count > MaxPendingDamageFeedback)
            {
                int oldestKey = 0;
                bool foundOldest = false;
                float oldestTime = float.MaxValue;
                foreach (KeyValuePair<int, PendingDamageFeedback> pair in _pendingDamageFeedback)
                {
                    if (!foundOldest || pair.Value.CreatedAt < oldestTime)
                    {
                        foundOldest = true;
                        oldestKey = pair.Key;
                        oldestTime = pair.Value.CreatedAt;
                    }
                }

                if (!foundOldest)
                {
                    return;
                }

                _pendingDamageFeedback.Remove(oldestKey);
            }
        }

        private DamageNumberVisual BuildDamageNumberVisual(
            float finalAmount,
            PendingDamageFeedback feedback,
            float visualEffectivenessMultiplier,
            bool critical,
            bool weakSpot,
            float precisionBonus,
            bool immune,
            bool damageOverTime,
            bool meleeDamage)
        {
            float multiplier = feedback == null ? 1.0f : feedback.Multiplier;
            float resistance = multiplier < 0.999f ? Mathf.Clamp01((1.0f - multiplier) / 0.95f) : 0.0f;
            float weakness = multiplier > 1.001f ? Mathf.Clamp01(multiplier - 1.0f) : 0.0f;
            float visualResistance = visualEffectivenessMultiplier < 0.999f
                ? Mathf.Clamp01((1.0f - visualEffectivenessMultiplier) / 0.95f)
                : 0.0f;
            float visualWeakness = visualEffectivenessMultiplier > 1.001f
                ? Mathf.Clamp01(visualEffectivenessMultiplier - 1.0f)
                : 0.0f;
            float sizeContrast = GetDamageNumberSizeContrast();
            float colorContrast = GetDamageNumberColorContrast();

            Color baseColor = GetDamageNumberBaseColor();
            Color color = baseColor;
            float scale = 1.0f;
            float duration = GetDamageNumberDurationSeconds();
            float fadeStart = 0.58f;
            float horizontalDistance = UnityEngine.Random.Range(18.0f, 42.0f);
            float verticalRise = UnityEngine.Random.Range(68.0f, 92.0f);
            float gravity = UnityEngine.Random.Range(12.0f, 24.0f);

            if (resistance > 0.0f)
            {
                if (visualResistance > 0.0f)
                {
                    float tone = Mathf.Clamp01(0.18f + (visualResistance * 0.82f));
                    color = Color.Lerp(baseColor, ResistedDamageNumberColor, Mathf.Clamp01(tone * colorContrast));
                }
                float resistedScale = Mathf.Lerp(0.96f, 0.68f, resistance);
                scale = 1.0f + ((resistedScale - 1.0f) * sizeContrast);
                duration = Mathf.Lerp(duration, 0.60f, resistance);
                fadeStart = 0.44f;
                horizontalDistance = UnityEngine.Random.Range(26.0f, 52.0f);
                verticalRise = UnityEngine.Random.Range(42.0f, 66.0f);
                gravity = UnityEngine.Random.Range(18.0f, 32.0f);
            }
            else if (weakness > 0.0f)
            {
                if (visualWeakness > 0.0f)
                {
                    float tone = Mathf.Clamp01(0.30f + (visualWeakness * 0.70f));
                    color = Color.Lerp(baseColor, WeaknessDamageNumberColor, Mathf.Clamp01(tone * colorContrast));
                }
                float weaknessScale = Mathf.Lerp(1.12f, 1.46f, weakness);
                scale = 1.0f + ((weaknessScale - 1.0f) * sizeContrast);
                duration = Mathf.Max(duration, Mathf.Lerp(duration, 1.05f, weakness));
                fadeStart = 0.62f;
                horizontalDistance = UnityEngine.Random.Range(8.0f, 24.0f);
                verticalRise = UnityEngine.Random.Range(82.0f, 122.0f);
                gravity = UnityEngine.Random.Range(8.0f, 18.0f);
            }

            if (immune)
            {
                color = ImmuneDamageNumberColor;
                scale = 0.82f;
                duration = 0.72f;
                fadeStart = 0.46f;
                horizontalDistance = UnityEngine.Random.Range(16.0f, 34.0f);
                verticalRise = UnityEngine.Random.Range(36.0f, 52.0f);
                gravity = UnityEngine.Random.Range(18.0f, 30.0f);
            }

            float precisionVisualScale = 1.0f - resistance;
            float precisionVisualBonus = immune
                ? 0.0f
                : Mathf.Clamp(precisionBonus, 0.0f, 0.50f) * precisionVisualScale;
            if (precisionVisualBonus > 0.0f)
            {
                color = Color.Lerp(color, Color.red, precisionVisualBonus);
                scale *= 1.0f + precisionVisualBonus;
            }

            if (critical)
            {
                duration = Mathf.Max(duration, GetDamageNumberCriticalDurationSeconds());
                fadeStart = Mathf.Max(fadeStart, 0.64f);
                horizontalDistance *= 0.58f;
                verticalRise += 26.0f;
                gravity *= 0.72f;
            }

            if (weakSpot)
            {
                verticalRise += 8.0f;
            }

            if (meleeDamage)
            {
                duration *= GetMeleeDamageNumberDurationMultiplier();
            }

            if (damageOverTime)
            {
                scale *= GetDamageOverTimeNumberScale();
            }

            return new DamageNumberVisual
            {
                Text = immune ? "IMMUNE" : FormatDamageAmount(finalAmount),
                Color = color,
                OutlineColor = DamageNumberOutlineColor,
                FontSize = GetDamageNumberFontSize(),
                StartScale = Mathf.Clamp(scale * (critical ? 1.18f : 1.05f), 0.55f, 2.45f),
                EndScale = Mathf.Clamp(scale, 0.55f, 2.20f),
                DurationSeconds = duration,
                FadeStart = Mathf.Clamp(fadeStart, 0.20f, 0.90f),
                Direction = UnityEngine.Random.value < 0.5f ? -1.0f : 1.0f,
                HorizontalDistance = horizontalDistance,
                VerticalRise = verticalRise,
                Gravity = gravity,
                WorldHeightMultiplier = damageOverTime ? GetDamageOverTimeNumberHeightMultiplier() : 1.0f,
                Critical = critical
            };
        }

        private string FormatDamageAmount(float finalAmount)
        {
            if (finalAmount >= 10.0f)
            {
                return Mathf.RoundToInt(finalAmount).ToString("N0", CultureInfo.InvariantCulture);
            }

            return finalAmount.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private Color GetDamageNumberBaseColor()
        {
            Color color;
            string configured = _damageNumberBaseColor == null ? null : _damageNumberBaseColor.Value;
            if (!string.IsNullOrWhiteSpace(configured) && ColorUtility.TryParseHtmlString(configured.Trim(), out color))
            {
                color.a = 1.0f;
                return color;
            }

            return DefaultDamageNumberColor;
        }

        private int GetDamageNumberFontSize()
        {
            int value = _damageNumberFontSize == null ? 34 : _damageNumberFontSize.Value;
            return Math.Max(12, Math.Min(80, value));
        }

        private FontAsset ResolveDamageNumberFontAsset()
        {
            DamageNumberFontMode mode = GetDamageNumberFontMode();
            if (mode == DamageNumberFontMode.ImguiDefault)
            {
                return ResolveImguiDefaultFontAsset();
            }

            try
            {
                if (mode == DamageNumberFontMode.Sans)
                {
                    return ResolveFontFamilyAsset(FontFamily.Sans, "Sans");
                }

                if (mode == DamageNumberFontMode.Serif)
                {
                    return ResolveFontFamilyAsset(FontFamily.Serif, "Serif");
                }

                FontChooseSetting setting = World.Any<FontChooseSetting>();
                if (setting == null)
                {
                    return TMP_Settings.defaultFontAsset;
                }

                FontFamily activeFont = setting.ActiveFont;
                return ResolveFontFamilyAsset(activeFont, activeFont == null ? "game" : "game " + activeFont.EnumName);
            }
            catch (Exception ex)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "ResolveDamageNumberFontAsset:" + mode.ToString() + ":" + ex.GetType().FullName,
                    "Could not resolve " + mode + " font asset for Steel and Bone damage numbers; using the TextMesh Pro default. "
                    + ex.GetBaseException().Message);
                return TMP_Settings.defaultFontAsset;
            }
        }

        private DamageNumberFontMode GetDamageNumberFontMode()
        {
            return _damageNumberFontMode == null ? DamageNumberFontMode.GameDefault : _damageNumberFontMode.Value;
        }

        private FontAsset ResolveFontFamilyAsset(FontFamily fontFamily, string label)
        {
            if (fontFamily == null)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "FontFamilyMissing:" + label,
                    "Could not resolve " + label + " font family for Steel and Bone damage numbers; using the TextMesh Pro default.");
                return TMP_Settings.defaultFontAsset;
            }

            FontAsset fontAsset = fontFamily.FontAsset;
            if (fontAsset == null)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "FontAssetMissing:" + fontFamily.EnumName,
                    "Could not resolve " + label + " FontAsset for Steel and Bone damage numbers; using the TextMesh Pro default.");
                return TMP_Settings.defaultFontAsset;
            }

            return fontAsset;
        }

        private FontAsset ResolveImguiDefaultFontAsset()
        {
            if (_imguiDefaultFontAsset != null)
            {
                return _imguiDefaultFontAsset;
            }

            try
            {
                Font sourceFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (sourceFont != null)
                {
                    _imguiDefaultFontAsset = FontAsset.CreateFontAsset(sourceFont);
                    if (_imguiDefaultFontAsset != null)
                    {
                        _imguiDefaultFontAsset.name = "SteelAndBone-ImguiDefault";
                        _imguiDefaultFontAsset.hideFlags = HideFlags.HideAndDontSave;
                        return _imguiDefaultFontAsset;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDamageNumberFontDiagnosticOnce(
                    "ResolveImguiDefaultFontAsset:" + ex.GetType().FullName,
                    "Could not create the legacy fallback font asset for Steel and Bone damage numbers; using the TextMesh Pro default. "
                    + ex.GetBaseException().Message);
            }

            return TMP_Settings.defaultFontAsset;
        }

        private void LogDamageNumberFontDiagnosticOnce(string key, string message)
        {
            if (!DiagnosticsEnabled() || string.Equals(_lastDamageNumberFontDiagnosticKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _lastDamageNumberFontDiagnosticKey = key;
            Logger.LogWarning(message);
        }

        private float GetDamageNumberDurationSeconds()
        {
            float value = _damageNumberDurationSeconds == null ? 0.85f : _damageNumberDurationSeconds.Value;
            return Clamp(value, 0.35f, 2.50f);
        }

        private float GetDamageNumberCriticalDurationSeconds()
        {
            float value = _damageNumberCriticalDurationSeconds == null ? 1.10f : _damageNumberCriticalDurationSeconds.Value;
            return Clamp(value, 0.45f, 3.00f);
        }

        private float GetMeleeDamageNumberDurationMultiplier()
        {
            float value = _meleeDamageNumberDurationMultiplier == null ? 2.0f : _meleeDamageNumberDurationMultiplier.Value;
            return Clamp(value, 1.0f, 3.0f);
        }

        private float GetDamageNumberHorizontalDrift()
        {
            float value = _damageNumberHorizontalDrift == null ? 1.0f : _damageNumberHorizontalDrift.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberVerticalDrift()
        {
            float value = _damageNumberVerticalDrift == null ? 1.0f : _damageNumberVerticalDrift.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageOverTimeNumberHeightMultiplier()
        {
            float value = _damageOverTimeNumberHeightMultiplier == null ? 3.0f : _damageOverTimeNumberHeightMultiplier.Value;
            return Clamp(value, 0.0f, 6.0f);
        }

        private float GetDamageOverTimeNumberScale()
        {
            float value = _damageOverTimeNumberScale == null ? 0.75f : _damageOverTimeNumberScale.Value;
            return Clamp(value, 0.5f, 2.0f);
        }

        private float GetDamageNumberSizeContrast()
        {
            float value = _damageNumberSizeContrast == null ? 1.0f : _damageNumberSizeContrast.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float GetDamageNumberColorContrast()
        {
            float value = _damageNumberColorContrast == null ? 1.0f : _damageNumberColorContrast.Value;
            return Clamp(value, 0.0f, 3.0f);
        }

        private float ApplyEffectivenessFeedbackSensitivity(float effectivenessMultiplier)
        {
            if (float.IsNaN(effectivenessMultiplier) || float.IsInfinity(effectivenessMultiplier))
            {
                return 1.0f;
            }

            float sensitivity = _effectivenessFeedbackSensitivity == null
                ? GetPresetEffectivenessFeedbackSensitivity(_preset == null ? Preset.Hardened : _preset.Value)
                : Clamp(_effectivenessFeedbackSensitivity.Value, 0.0f, 3.0f);
            return Clamp(1.0f + ((effectivenessMultiplier - 1.0f) * sensitivity), 0.0f, 3.0f);
        }

        private float GetDamageNumberMinimumAmount()
        {
            float value = _damageNumberMinimumAmount == null ? 0.10f : _damageNumberMinimumAmount.Value;
            return Clamp(value, 0.0f, 1000.0f);
        }

        private int GetDamageNumberMaximumActive()
        {
            int value = _damageNumberMaximumActive == null ? 36 : _damageNumberMaximumActive.Value;
            return Math.Max(1, Math.Min(128, value));
        }

        private object GetOptionalPropertyValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            try
            {
                PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private object GetOptionalMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            object value = GetOptionalPropertyValue(instance, memberName);
            if (value != null)
            {
                return value;
            }

            try
            {
                FieldInfo field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private int GetOptionalIntProperty(object instance, string propertyName, int fallback)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            if (value is int)
            {
                return (int)value;
            }
            if (value is uint)
            {
                uint uintValue = (uint)value;
                return uintValue > int.MaxValue ? int.MaxValue : (int)uintValue;
            }

            return fallback;
        }

        private bool GetOptionalBoolProperty(object instance, string propertyName)
        {
            object value = GetOptionalPropertyValue(instance, propertyName);
            return value is bool && (bool)value;
        }

        private bool IsTrueMember(object instance, string memberName)
        {
            bool value;
            return TryGetBoolMemberValue(instance, memberName, out value) && value;
        }

        private bool TryGetBoolMemberValue(object instance, string memberName, out bool value)
        {
            value = false;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            return false;
        }

        private bool TryGetFloatMemberValue(object instance, string memberName, out float value)
        {
            value = 0.0f;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw == null)
            {
                return false;
            }

            try
            {
                if (raw is float)
                {
                    value = (float)raw;
                    return true;
                }

                if (raw is double)
                {
                    value = (float)(double)raw;
                    return true;
                }

                if (raw is int)
                {
                    value = (int)raw;
                    return true;
                }

                if (raw is long)
                {
                    value = (long)raw;
                    return true;
                }

                if (raw is IConvertible)
                {
                    value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryGetVector3MemberValue(object instance, string memberName, out Vector3 value)
        {
            value = Vector3.zero;
            object raw = GetOptionalMemberValue(instance, memberName);
            if (raw is Vector3)
            {
                value = (Vector3)raw;
                return true;
            }

            return false;
        }

        private PropertyInfo GetIndexerProperty(Type type)
        {
            if (type == null)
            {
                return null;
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; i++)
            {
                ParameterInfo[] parameters = properties[i].GetIndexParameters();
                if (properties[i].Name == "Item"
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(int))
                {
                    return properties[i];
                }
            }

            return null;
        }

        private object GetIndexedValue(PropertyInfo indexer, object instance, int index)
        {
            if (indexer == null || instance == null)
            {
                return null;
            }

            try
            {
                return indexer.GetValue(instance, new object[] { index });
            }
            catch
            {
                return null;
            }
        }

        private bool ValueNameContains(object value, string expected)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return value.ToString().IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
        }

        private string DescribeObject(object value)
        {
            if (value == null)
            {
                return "null";
            }

            string displayName = GetOptionalPropertyValue(value, "DisplayName") as string;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "Name") as string;
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = GetOptionalPropertyValue(value, "DebugName") as string;
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            return value.GetType().Name;
        }

        private string DescribeValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            string text = value as string;
            if (text != null)
            {
                return text.Length == 0 ? "\"\"" : text;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                return value.ToString();
            }

            return DescribeObject(value);
        }

        private string DescribeTargetFamilies(TargetClassification classification)
        {
            if (classification == null)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            AppendDiagnosticLabel(builder, classification.IsBoneUndead, "BoneUndead");
            AppendDiagnosticLabel(builder, classification.IsConstruct, "Construct");
            AppendDiagnosticLabel(builder, IsEffectivelyArmoredHumanoid(classification), "ArmoredHumanoid");
            AppendDiagnosticLabel(builder, classification.IsFlesh, "Flesh");
            AppendDiagnosticLabel(builder, classification.IsFleshUndead, "FleshUndead");
            AppendDiagnosticLabel(builder, classification.IsWyrd, "Wyrd");
            AppendDiagnosticLabel(builder, classification.IsDrownedZombie, "DrownedZombie");
            AppendDiagnosticLabel(builder, classification.IsInfectedFlesh, "InfectedFlesh");
            AppendDiagnosticLabel(builder, classification.IsSeaFlesh, "SeaFlesh");
            AppendDiagnosticLabel(builder, classification.IsSpirit, "Spirit");
            AppendDiagnosticLabel(builder, classification.IsFlora, "Flora");
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private string DescribeTargetFlags(TargetClassification classification)
        {
            if (classification == null)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder();
            AppendDiagnosticLabel(builder, classification.IsEliteClass, "EliteClass");
            AppendDiagnosticLabel(builder, classification.IsConfirmedSkeleton, "ConfirmedSkeleton");
            AppendDiagnosticLabel(builder, classification.HasBoneBody, "BoneBody");
            AppendDiagnosticLabel(builder, classification.HasStoneBody, "StoneBody");
            AppendDiagnosticLabel(builder, classification.HasWoodBody, "WoodBody");
            AppendDiagnosticLabel(builder, classification.IsHumanoidFlesh, "HumanoidFlesh");
            AppendDiagnosticLabel(builder, classification.IsSwarm, "Swarm");
            AppendDiagnosticLabel(builder, classification.HasInheritedColdWeakness, "InheritedColdWeakness");
            AppendDiagnosticLabel(builder, classification.IsFlamegobbler, "Flamegobbler");
            AppendDiagnosticLabel(builder, classification.IsCrystalBody, "CrystalBody");
            AppendDiagnosticLabel(builder, classification.IsWyrdSlime, "WyrdSlime");
            if (classification.ExactTargets != ExactTarget.None)
            {
                AppendDiagnosticLabel(builder, true, "Exact:" + classification.ExactTargets);
            }
            return builder.Length == 0 ? "None" : builder.ToString();
        }

        private string DescribeDamageTags(DamageClassification classification)
        {
            if (classification == null || classification.Tags == DamageTag.None)
            {
                return "None";
            }

            StringBuilder builder = new StringBuilder(classification.Tags.ToString());
            AppendDiagnosticLabel(builder, classification.IsAxe, "Axe");
            AppendDiagnosticLabel(builder, classification.IsPommel, "Pommel");
            AppendDiagnosticLabel(builder, classification.IsHeavyAttack, "Heavy");
            AppendDiagnosticLabel(builder, classification.IsAreaAttack, "Area");
            return builder.ToString();
        }

        private void AppendDiagnosticLabel(StringBuilder builder, bool include, string label)
        {
            if (!include)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("|");
            }

            builder.Append(label);
        }

        private float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void LogDiagnostic(string message)
        {
            if (DiagnosticsEnabled())
            {
                Log.LogInfo(message);
            }
        }

        private void ShowDamageDecisionDiagnostic(
            string signature,
            string decision)
        {
            if (!DiagnosticsEnabled()
                || _showGrailFloatingTextDiagnostics == null
                || !_showGrailFloatingTextDiagnostics.Value
                || string.IsNullOrWhiteSpace(signature)
                || string.IsNullOrWhiteSpace(decision)
                || string.Equals(
                    signature,
                    _lastGftDamageDiagnosticSignature,
                    StringComparison.Ordinal))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextGftDamageDiagnosticTime)
            {
                return;
            }

            _lastGftDamageDiagnosticSignature = signature;
            _nextGftDamageDiagnosticTime = now + 2.0f;
            Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                .TryShowDiagnosticNotification(
                    PluginGuid,
                    "steel-and-bone-damage-decision",
                    "S&B: " + decision,
                    "steel-and-bone-diagnostics");
        }

        private bool DiagnosticsEnabled()
        {
            return _diagnostics != null && _diagnostics.Value;
        }

        private void Warn(string message)
        {
            if (_logPatchWarnings == null || _logPatchWarnings.Value)
            {
                Log.LogWarning(message);
            }
        }

        private enum Preset
        {
            Tempered,
            Hardened,
            Crucible
        }

        private enum DamageNumberFontMode
        {
            GameDefault,
            Sans,
            Serif,
            ImguiDefault
        }

        private enum DamageNumberMode
        {
            AllDamage,
            ResistAndImmuneOnly,
            ResistAndImmuneOnlyOnce
        }

        private enum TargetFamily
        {
            BoneUndead,
            BoneBody,
            Construct,
            StoneBody,
            ArmoredHumanoid,
            Flesh,
            FleshUndead,
            Wyrd,
            DrownedZombie,
            InfectedFlesh,
            SeaFlesh,
            Spirit,
            Flora
        }

        [Flags]
        private enum ExactTarget
        {
            None = 0,
            Rootambusher = 1,
            FrostbittenWarrior = 2,
            Wight = 4,
            Giant = 8,
            MissingCorpseEaterReaction = 16,
            ElectricStagfatherGolem = 32,
            Mistbearer = 64,
            WyrdheirChallenge = 128,
            Nivera = 256,
            Rimefiend = 512,
            FrostWolf = 1024,
            StrawParent = 2048,
            Wyrdspawn = 4096,
            Ogre = 8192,
            FireAligned = 16384,
            ElementalStagfatherGolem = 32768,
            DrownedSkeletonSailor = 65536,
            FrostAngel = 131072,
            IceWeaverChampion = 262144,
            IceWeaverWolf = 524288,
            IceTrialWyrd = 1048576,
            CharredConclaveWyrdspawn = 2097152,
            IceStatue = 4194304,
            AncientBeholder = 8388608,
            Singworm = 16777216,
            LirTentacle = 33554432,
            BloodAbomination = 67108864,
            WyrdSlime = 134217728,
            Tidewraith = 268435456,
            Frostgrot = 536870912
        }

        [Flags]
        private enum DamageTag
        {
            None = 0,
            BloodMagic = 1,
            Bleed = 2,
            Poison = 4,
            Wyrdness = 8,
            Slashing = 16,
            Piercing = 32,
            Bludgeoning = 64,
            GenericPhysical = 128,
            GenericMagical = 256,
            Fire = 512,
            Cold = 1024,
            Electric = 2048,
            Wet = 4096,
            Burn = 8192,
            Arrow = 16384,
            DirectSpell = 32768
        }

        private sealed class NativeSubtypeCheck
        {
            public readonly DamageTag Tag;
            public readonly string SubtypeName;

            public NativeSubtypeCheck(DamageTag tag, string subtypeName)
            {
                Tag = tag;
                SubtypeName = subtypeName;
            }
        }

        private sealed class DamageRule
        {
            public readonly TargetFamily TargetFamily;
            public readonly DamageTag DamageTags;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            public readonly float BaseMultiplier;
            public readonly int Priority;

            public DamageRule(
                TargetFamily targetFamily,
                DamageTag damageTags,
                string targetLabel,
                string damageLabel,
                float baseMultiplier,
                int priority)
            {
                TargetFamily = targetFamily;
                DamageTags = damageTags;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                BaseMultiplier = baseMultiplier;
                Priority = priority;
            }

            public bool MatchesDamageTag(DamageTag tag)
            {
                return (DamageTags & tag) != DamageTag.None;
            }
        }

        private sealed class ExactDamageRule
        {
            public readonly ExactTarget Target;
            public readonly DamageTag DamageTags;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            public readonly float BaseMultiplier;

            public ExactDamageRule(
                ExactTarget target,
                DamageTag damageTags,
                string targetLabel,
                string damageLabel,
                float baseMultiplier)
            {
                Target = target;
                DamageTags = damageTags;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                BaseMultiplier = baseMultiplier;
            }
        }

        private readonly struct DamageRuleMatch
        {
            public readonly float Multiplier;
            public readonly string TargetLabel;
            public readonly string DamageLabel;
            public readonly int Priority;
            public readonly float Impact;
            public readonly float PresetMultiplier;
            public readonly bool WasEliteClamped;

            public DamageRuleMatch(
                float multiplier,
                string targetLabel,
                string damageLabel,
                int priority,
                float impact,
                float presetMultiplier,
                bool wasEliteClamped)
            {
                Multiplier = multiplier;
                TargetLabel = targetLabel;
                DamageLabel = damageLabel;
                Priority = priority;
                Impact = impact;
                PresetMultiplier = presetMultiplier;
                WasEliteClamped = wasEliteClamped;
            }
        }

        private readonly struct VanillaMultiplierAmplification
        {
            public readonly string SubtypeName;
            public readonly float NativeMultiplier;
            public readonly float AmplifiedMultiplier;
            public readonly float AdjustmentMultiplier;

            public VanillaMultiplierAmplification(
                string subtypeName,
                float nativeMultiplier,
                float amplifiedMultiplier,
                float adjustmentMultiplier)
            {
                SubtypeName = subtypeName;
                NativeMultiplier = nativeMultiplier;
                AmplifiedMultiplier = amplifiedMultiplier;
                AdjustmentMultiplier = adjustmentMultiplier;
            }
        }

        private enum EnemyArmorTier
        {
            Unknown,
            Exposed,
            Light,
            Medium,
            Heavy
        }

        private enum EnemyArmorMaterial
        {
            Unknown,
            None,
            Fabric,
            Leather,
            Metal
        }

        private sealed class EnemyArmorProfile : Element<NpcElement>
        {
            private bool _dirty = true;
            private EnemyArmorTier _tier = EnemyArmorTier.Unknown;
            private EnemyArmorMaterial _material = EnemyArmorMaterial.Unknown;
            private string _evidence = "equipment:Pending";

            public sealed override bool IsNotSaved
            {
                get { return true; }
            }

            public EnemyArmorTier Tier
            {
                get
                {
                    RefreshIfNeeded();
                    return _tier;
                }
            }

            public string Evidence
            {
                get
                {
                    RefreshIfNeeded();
                    return _evidence;
                }
            }

            public EnemyArmorMaterial Material
            {
                get
                {
                    RefreshIfNeeded();
                    return _material;
                }
            }

            protected override void OnFullyInitialized()
            {
                ModelExtensions.ListenTo(
                    base.ParentModel.Inventory,
                    ICharacterInventory.Events.AfterEquipmentChanged,
                    OnEquipmentChanged,
                    this);
            }

            private void OnEquipmentChanged(ICharacterInventory inventory)
            {
                _dirty = true;
            }

            private void RefreshIfNeeded()
            {
                if (!_dirty)
                {
                    return;
                }

                if (!base.ParentModel.ItemsAddedToInventory)
                {
                    _tier = EnemyArmorTier.Unknown;
                    _material = EnemyArmorMaterial.Unknown;
                    _evidence = "equipment:Pending";
                    return;
                }

                _dirty = false;
                ItemInSlots slots = base.ParentModel.NpcItems.ItemInSlots;
                Item cuirass = slots[EquipmentSlotType.Cuirass];
                EnemyArmorTier declaredTier;
                if (TryGetDeclaredArmorTier(cuirass, out declaredTier))
                {
                    _tier = declaredTier;
                    _material = GetArmorMaterial(cuirass);
                    _evidence = "equipment:Cuirass:" + declaredTier + ":" + _material;
                    return;
                }

                if (cuirass != null && cuirass.IsArmor)
                {
                    _tier = EnemyArmorTier.Unknown;
                    _material = GetArmorMaterial(cuirass);
                    _evidence = "equipment:Cuirass:Unknown:" + _material;
                    return;
                }

                bool hasPartialArmor = false;
                bool hasUnknownArmor = false;
                EnemyArmorMaterial partialMaterial = EnemyArmorMaterial.Unknown;
                for (int i = 0; i < EquipmentSlotType.Armors.Length; i++)
                {
                    EquipmentSlotType slot = EquipmentSlotType.Armors[i];
                    if (slot == EquipmentSlotType.Cuirass)
                    {
                        continue;
                    }

                    Item item = slots[slot];
                    if (item == null || !item.IsArmor)
                    {
                        continue;
                    }

                    if (TryGetDeclaredArmorTier(item, out declaredTier))
                    {
                        hasPartialArmor = true;
                        EnemyArmorMaterial itemMaterial = GetArmorMaterial(item);
                        if ((int)itemMaterial > (int)partialMaterial)
                        {
                            partialMaterial = itemMaterial;
                        }
                    }
                    else
                    {
                        hasUnknownArmor = true;
                    }
                }

                if (hasPartialArmor)
                {
                    _tier = EnemyArmorTier.Light;
                    _material = partialMaterial;
                    _evidence = "equipment:PartialArmor:Light:" + _material;
                }
                else if (hasUnknownArmor)
                {
                    _tier = EnemyArmorTier.Unknown;
                    _material = EnemyArmorMaterial.Unknown;
                    _evidence = "equipment:PartialArmor:Unknown";
                }
                else
                {
                    _tier = EnemyArmorTier.Exposed;
                    _material = EnemyArmorMaterial.None;
                    _evidence = "equipment:NoArmor";
                }
            }

            private static EnemyArmorMaterial GetArmorMaterial(Item item)
            {
                ItemAudio itemAudio = item == null ? null : item.TryGetElement<ItemAudio>();
                SurfaceType surface = itemAudio == null ? null : itemAudio.ArmorSurfaceType;
                if (surface == SurfaceType.ArmorMetal)
                {
                    return EnemyArmorMaterial.Metal;
                }
                if (surface == SurfaceType.ArmorLeather)
                {
                    return EnemyArmorMaterial.Leather;
                }
                if (surface == SurfaceType.ArmorFabric)
                {
                    return EnemyArmorMaterial.Fabric;
                }

                return EnemyArmorMaterial.Unknown;
            }

            private static bool TryGetDeclaredArmorTier(Item item, out EnemyArmorTier tier)
            {
                tier = EnemyArmorTier.Unknown;
                if (item == null || !item.IsArmor || item.Template == null)
                {
                    return false;
                }

                if (item.Template.IsHeavyArmor)
                {
                    tier = EnemyArmorTier.Heavy;
                    return true;
                }
                if (item.Template.IsMediumArmor)
                {
                    tier = EnemyArmorTier.Medium;
                    return true;
                }
                if (item.Template.IsLightArmor)
                {
                    tier = EnemyArmorTier.Light;
                    return true;
                }

                return false;
            }
        }

        private sealed class TargetClassification
        {
            public static readonly TargetClassification Empty = new TargetClassification();

            public object Key;
            public int Revision;
            public bool HasMetadataEvidence;
            public string Evidence;
            public bool IsBoneUndead;
            public bool IsConstruct;
            public bool IsArmoredHumanoid;
            public bool IsFlesh;
            public bool IsFleshUndead;
            public bool IsWyrd;
            public bool IsDrownedZombie;
            public bool IsInfectedFlesh;
            public bool IsSeaFlesh;
            public bool IsSpirit;
            public bool IsFlora;
            public bool ResistanceNoticeShown;
            public bool ImmunityNoticeShown;
            public bool IsEliteClass;
            public bool IsBossClass;
            public bool IsBear;
            public bool IsBulkyMonster;
            public bool IsConfirmedSkeleton;
            public bool HasBoneBody;
            public bool HasStoneBody;
            public bool HasWoodBody;
            public bool IsHumanoidFlesh;
            public bool IsSwarm;
            public bool HasInheritedColdWeakness;
            public bool IsFlamegobbler;
            public bool IsCrystalBody;
            public bool IsWyrdSlime;
            public ExactTarget ExactTargets;
            public EnemyArmorProfile ArmorProfile;

            public bool HasAnyFamily()
            {
                return IsBoneUndead
                    || IsConstruct
                    || IsArmoredHumanoid
                    || IsFlesh
                    || IsFleshUndead
                    || IsWyrd
                    || IsDrownedZombie
                    || IsInfectedFlesh
                    || IsSeaFlesh
                    || IsSpirit
                    || IsFlora;
            }

            public bool HasMetadataFamily()
            {
                return HasMetadataEvidence && HasAnyFamily();
            }
        }

        private sealed class DamageClassification
        {
            public static readonly DamageClassification Empty = new DamageClassification();

            public bool IsBloodMagic;
            public bool IsBleed;
            public bool IsPoison;
            public bool IsWyrdness;
            public bool IsSlashing;
            public bool IsPiercing;
            public bool IsBludgeoning;
            public bool IsGenericPhysical;
            public bool IsGenericMagical;
            public bool IsFire;
            public bool IsCold;
            public bool IsElectric;
            public bool IsWet;
            public bool IsBurn;
            public bool IsArrow;
            public bool IsDirectSpell;
            public bool IsAxe;
            public bool IsMiningToolCombatHit;
            public bool IsPommel;
            public bool IsHeavyAttack;
            public bool IsAreaAttack;
            public DamageTag Tags;
            public string PhysicalTypeHint;

            public bool HasSpecificPhysicalType()
            {
                return IsSlashing || IsPiercing || IsBludgeoning;
            }

            public bool HasAny(DamageTag tags)
            {
                return (Tags & tags) != DamageTag.None;
            }
        }

        private sealed class PendingDamageFeedback
        {
            public object Damage;
            public float Multiplier;
            public string TargetLabel;
            public string DamageLabel;
            public bool IsMelee;
            public float CreatedAt;
        }

        private sealed class DamageNumberVisual
        {
            public string Text;
            public Color Color;
            public Color OutlineColor;
            public int FontSize;
            public float StartScale;
            public float EndScale;
            public float DurationSeconds;
            public float FadeStart;
            public float Direction;
            public float HorizontalDistance;
            public float VerticalRise;
            public float Gravity;
            public float WorldHeightMultiplier;
            public bool Critical;
        }

        private sealed class DamageNumberEntry
        {
            public Vector3 WorldPosition;
            public float StartTime;
            public DamageNumberVisual Visual;
            public TextMeshProUGUI Text;
        }

        private sealed class DamageNumberRenderer : MonoBehaviour
        {
            private readonly List<DamageNumberEntry> _entries = new List<DamageNumberEntry>();
            private SteelAndBonePlugin _plugin;
            private RectTransform _canvasRoot;

            public void Initialize(SteelAndBonePlugin plugin)
            {
                _plugin = plugin;
                hideFlags = HideFlags.HideAndDontSave;
                EnsureCanvas();
            }

            public void ShowDamageNumber(Vector3 worldPosition, DamageNumberVisual visual)
            {
                if (_plugin == null || visual == null || string.IsNullOrEmpty(visual.Text))
                {
                    return;
                }

                int maximumActive = _plugin.GetDamageNumberMaximumActive();
                while (_entries.Count >= maximumActive)
                {
                    DestroyEntry(_entries[0]);
                    _entries.RemoveAt(0);
                }

                TextMeshProUGUI text = CreateText(visual);
                if (text == null)
                {
                    return;
                }

                _entries.Add(new DamageNumberEntry
                {
                    WorldPosition = worldPosition
                        + (Vector3.up
                            * UnityEngine.Random.Range(0.25f, 0.65f)
                            * visual.WorldHeightMultiplier),
                    StartTime = Time.unscaledTime,
                    Visual = visual,
                    Text = text
                });
            }

            private void LateUpdate()
            {
                if (_plugin == null || _entries.Count == 0)
                {
                    return;
                }

                Camera camera = Camera.main;
                if (camera == null)
                {
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        if (_entries[i].Text != null)
                        {
                            _entries[i].Text.enabled = false;
                        }
                    }

                    return;
                }

                float now = Time.unscaledTime;
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    DamageNumberEntry entry = _entries[i];
                    DamageNumberVisual visual = entry.Visual;
                    float duration = Math.Max(0.05f, visual.DurationSeconds);
                    float elapsed = now - entry.StartTime;
                    if (elapsed >= duration)
                    {
                        DestroyEntry(entry);
                        _entries.RemoveAt(i);
                        continue;
                    }

                    Vector3 projected = camera.WorldToScreenPoint(entry.WorldPosition);
                    if (projected.z <= 0.0f)
                    {
                        entry.Text.enabled = false;
                        continue;
                    }

                    entry.Text.enabled = true;
                    float t = Mathf.Clamp01(elapsed / duration);
                    UpdateEntry(entry, projected, visual, t);
                }
            }

            private void EnsureCanvas()
            {
                if (_canvasRoot != null)
                {
                    return;
                }

                GameObject canvasObject = new GameObject(
                    "SteelAndBoneDamageNumberCanvas",
                    typeof(RectTransform),
                    typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.transform.SetParent(transform, false);

                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 30000;
                _canvasRoot = canvasObject.GetComponent<RectTransform>();
            }

            private TextMeshProUGUI CreateText(DamageNumberVisual visual)
            {
                EnsureCanvas();
                if (_canvasRoot == null)
                {
                    return null;
                }

                GameObject textObject = new GameObject(
                    "DamageNumber",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.hideFlags = HideFlags.HideAndDontSave;
                RectTransform rect = textObject.GetComponent<RectTransform>();
                rect.SetParent(_canvasRoot, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(640.0f, 220.0f);

                TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
                text.text = visual.Text;
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.richText = false;
                text.raycastTarget = false;
                text.outlineWidth = 0.18f;
                return text;
            }

            private void UpdateEntry(DamageNumberEntry entry, Vector3 projected, DamageNumberVisual visual, float t)
            {
                float smoothT = Mathf.SmoothStep(0.0f, 1.0f, t);
                float scale = Mathf.Lerp(visual.StartScale, visual.EndScale, smoothT);
                if (visual.Critical)
                {
                    scale *= Mathf.Lerp(1.16f, 1.0f, Mathf.Clamp01(t / 0.22f));
                }

                float fadeStart = Mathf.Clamp(visual.FadeStart, 0.01f, 0.99f);
                float alpha = t <= fadeStart ? 1.0f : 1.0f - Mathf.Clamp01((t - fadeStart) / (1.0f - fadeStart));
                if (alpha <= 0.01f)
                {
                    entry.Text.enabled = false;
                    return;
                }

                float xOffset = visual.Direction * visual.HorizontalDistance * Mathf.Sin(t * Mathf.PI * 0.75f) * _plugin.GetDamageNumberHorizontalDrift();
                float yOffset = ((-visual.VerticalRise * t) + (visual.Gravity * t * t)) * _plugin.GetDamageNumberVerticalDrift();
                float centerX = projected.x + xOffset;
                float centerY = projected.y - yOffset;

                FontAsset fontAsset = _plugin.ResolveDamageNumberFontAsset();
                if (fontAsset != null && !ReferenceEquals(entry.Text.font, fontAsset))
                {
                    entry.Text.font = fontAsset;
                }

                entry.Text.fontSize = Math.Max(8.0f, visual.FontSize * scale);
                entry.Text.color = WithAlpha(visual.Color, alpha);
                entry.Text.outlineColor = WithAlpha(visual.OutlineColor, alpha * 0.88f);
                entry.Text.rectTransform.anchoredPosition = new Vector2(centerX, centerY);
            }

            private void DestroyEntry(DamageNumberEntry entry)
            {
                if (entry != null && entry.Text != null)
                {
                    Destroy(entry.Text.gameObject);
                    entry.Text = null;
                }
            }

            private void OnDestroy()
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    DestroyEntry(_entries[i]);
                }

                _entries.Clear();
                if (_canvasRoot != null)
                {
                    Destroy(_canvasRoot.gameObject);
                    _canvasRoot = null;
                }
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a *= Mathf.Clamp01(alpha);
                return color;
            }
        }

        private static class ApplyDamageModifiersPatch
        {
            public static void Postfix(
                object __instance,
                object damage,
                DamageModifiersInfo __result,
                ref float dmgModifier)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null)
                {
                    plugin.ApplyDamageRuleModifier(
                        __instance,
                        damage,
                        __result,
                        ref dmgModifier);
                    plugin.ApplyProgressiveTenacityHealthDamage(
                        __instance,
                        damage as Damage,
                        ref dmgModifier);
                }
            }
        }

        private bool TryApplyWeightedDamageComposition(
            TargetClassification targetClass,
            DamageClassification damageClass,
            object damageObject,
            ref float damageModifier)
        {
            Damage damage = damageObject as Damage;
            if (damage == null || damage.DamageTypeData == null || damage.DamageTypeData.Parts == null)
            {
                return false;
            }

            int partCount = damage.DamageTypeData.Parts.Count;
            if (partCount <= 0)
            {
                return false;
            }

            EnsureDamagePartAdjustmentCapacity(partCount);

            float shareSum = 0.0f;
            var shareEnumerator = damage.DamageTypeData.Parts.GetEnumerator();
            try
            {
                while (shareEnumerator.MoveNext())
                {
                    float share = shareEnumerator.Current.TotalDamageMultiplier;
                    if (float.IsNaN(share) || float.IsInfinity(share) || share < 0.0f)
                    {
                        return false;
                    }

                    shareSum += share;
                }
            }
            finally
            {
                shareEnumerator.Dispose();
            }

            if (shareSum <= 0.0001f || float.IsNaN(shareSum) || float.IsInfinity(shareSum))
            {
                return false;
            }

            bool amplifyVanilla = _amplifyVanillaMultipliers != null
                && _amplifyVanillaMultipliers.Value;
            float amplificationStrength = amplifyVanilla ? GetVanillaAmplificationStrength() : 0.0f;
            float weightedAdjustment = 0.0f;
            float weightedFeedback = 0.0f;
            float feedbackWeight = 0.0f;
            float bestFeedbackScore = -1.0f;
            string feedbackTargetLabel = "";
            string feedbackDamageLabel = "";
            bool hasNativeResponse = false;
            bool hasCustomRule = false;
            bool compositionChanged = false;
            int partIndex = 0;

            var partEnumerator = damage.DamageTypeData.Parts.GetEnumerator();
            try
            {
                while (partEnumerator.MoveNext())
                {
                    ref DamageTypeDataPart part = ref partEnumerator.Current;
                    float postVanillaShare = part.TotalDamageMultiplier / shareSum;
                    PopulatePartDamageClassification(damageClass, part.SubType, _partDamageClassification);

                    float nativeMultiplier = damage.DamageReceivedMultiplierData == null
                        ? 1.0f
                        : damage.DamageReceivedMultiplierData.GetMultiplierForSubtype(part.SubType);
                    if (float.IsNaN(nativeMultiplier) || float.IsInfinity(nativeMultiplier) || nativeMultiplier < 0.0f)
                    {
                        nativeMultiplier = 1.0f;
                    }

                    float amplifiedMultiplier = nativeMultiplier;
                    float amplificationRatio = 1.0f;
                    if (amplificationStrength > 0.0001f
                        && nativeMultiplier > 0.0001f
                        && HasMeaningfulEffect(nativeMultiplier))
                    {
                        amplifiedMultiplier = AmplifyVanillaMultiplier(nativeMultiplier, amplificationStrength);
                        amplificationRatio = amplifiedMultiplier / nativeMultiplier;
                    }

                    if (HasMeaningfulEffect(nativeMultiplier))
                    {
                        hasNativeResponse = true;
                    }

                    DamageRuleMatch partMatch;
                    bool matchedPartRule = TryResolveDamagePartRule(
                        targetClass,
                        _partDamageClassification,
                        damage,
                        out partMatch);
                    float customMultiplier = matchedPartRule
                        ? Clamp(partMatch.Multiplier, 0.05f, 2.0f)
                        : 1.0f;
                    float partAdjustment = amplificationRatio * customMultiplier;
                    _damagePartAdjustments[partIndex++] = partAdjustment;
                    weightedAdjustment += postVanillaShare * partAdjustment;
                    compositionChanged |= Math.Abs(partAdjustment - 1.0f) > 0.001f;
                    hasCustomRule |= matchedPartRule;

                    float originalWeight = Math.Max(0.0f, part.PercentageAsFloat);
                    weightedFeedback += originalWeight * amplifiedMultiplier * customMultiplier;
                    feedbackWeight += originalWeight;

                    float customScore = matchedPartRule
                        ? postVanillaShare * GetRuleImpact(customMultiplier)
                        : -1.0f;
                    float nativeScore = postVanillaShare * GetRuleImpact(amplifiedMultiplier);
                    float feedbackScore = Math.Max(customScore, nativeScore);
                    if (feedbackScore > bestFeedbackScore)
                    {
                        bestFeedbackScore = feedbackScore;
                        feedbackTargetLabel = matchedPartRule ? partMatch.TargetLabel : "Vanilla";
                        feedbackDamageLabel = matchedPartRule ? partMatch.DamageLabel : part.SubType.ToString();
                    }

                    if (DiagnosticsEnabled())
                    {
                        LogDiagnostic(
                            "Damage part: subtype="
                            + part.SubType
                            + ", originalWeight="
                            + originalWeight.ToString("0.###", CultureInfo.InvariantCulture)
                            + ", postVanillaShare="
                            + postVanillaShare.ToString("0.###", CultureInfo.InvariantCulture)
                            + ", nativeMultiplier="
                            + nativeMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + ", amplifiedMultiplier="
                            + amplifiedMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + ", customMultiplier="
                            + customMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                            + ", adjustment="
                            + partAdjustment.ToString("0.###", CultureInfo.InvariantCulture)
                            + ".");
                    }
                }
            }
            finally
            {
                partEnumerator.Dispose();
            }

            if (weightedAdjustment <= 0.0001f
                || float.IsNaN(weightedAdjustment)
                || float.IsInfinity(weightedAdjustment))
            {
                return false;
            }

            float before = damageModifier;
            damageModifier *= weightedAdjustment;

            if (compositionChanged)
            {
                int adjustmentIndex = 0;
                var updateEnumerator = damage.DamageTypeData.Parts.GetEnumerator();
                try
                {
                    while (updateEnumerator.MoveNext())
                    {
                        ref DamageTypeDataPart part = ref updateEnumerator.Current;
                        float normalizedShare = part.TotalDamageMultiplier / shareSum;
                        float adjustedShare = normalizedShare
                            * _damagePartAdjustments[adjustmentIndex++]
                            / weightedAdjustment;
                        part.SetTotalDamageMultiplier(adjustedShare);
                    }
                }
                finally
                {
                    updateEnumerator.Dispose();
                }
            }

            if ((hasNativeResponse || hasCustomRule) && feedbackWeight > 0.0001f)
            {
                RememberDamageFeedback(
                    damage,
                    weightedFeedback / feedbackWeight,
                    string.IsNullOrEmpty(feedbackTargetLabel) ? "Mixed" : feedbackTargetLabel,
                    string.IsNullOrEmpty(feedbackDamageLabel) ? "Mixed" : feedbackDamageLabel);
            }

            if (DiagnosticsEnabled() && (compositionChanged || hasNativeResponse || hasCustomRule))
            {
                LogDiagnostic(
                    "Applied weighted damage composition: parts="
                    + partCount
                    + ", adjustment="
                    + weightedAdjustment.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", feedback="
                    + (feedbackWeight > 0.0001f
                        ? (weightedFeedback / feedbackWeight).ToString("0.###", CultureInfo.InvariantCulture)
                        : "neutral")
                    + ", damageModifier "
                    + before.ToString("0.###", CultureInfo.InvariantCulture)
                    + " -> "
                    + damageModifier.ToString("0.###", CultureInfo.InvariantCulture)
                    + ".");
                ShowDamageDecisionDiagnostic(
                    "weighted|"
                        + feedbackTargetLabel
                        + "|"
                        + feedbackDamageLabel
                        + "|"
                        + weightedAdjustment.ToString("0.###", CultureInfo.InvariantCulture),
                    (string.IsNullOrEmpty(feedbackTargetLabel)
                            ? "Mixed"
                            : feedbackTargetLabel)
                        + " + "
                        + (string.IsNullOrEmpty(feedbackDamageLabel)
                            ? "mixed damage"
                            : feedbackDamageLabel)
                        + " -> x"
                        + weightedAdjustment.ToString("0.###", CultureInfo.InvariantCulture));
            }

            return true;
        }

        private void EnsureDamagePartAdjustmentCapacity(int partCount)
        {
            if (_damagePartAdjustments.Length >= partCount)
            {
                return;
            }

            int capacity = _damagePartAdjustments.Length;
            while (capacity < partCount)
            {
                capacity *= 2;
            }

            Array.Resize(ref _damagePartAdjustments, capacity);
        }

        private bool TryResolveDamagePartRule(
            TargetClassification targetClass,
            DamageClassification partClass,
            Damage damage,
            out DamageRuleMatch match)
        {
            match = default(DamageRuleMatch);
            DamageTag physicalTags = DamageTag.Slashing
                | DamageTag.Piercing
                | DamageTag.Bludgeoning
                | DamageTag.GenericPhysical;
            bool physicalPart = partClass != null && partClass.HasAny(physicalTags);
            bool arrowRulesEnabled = partClass != null
                && partClass.IsArrow
                && (_arrowMaterialRulesEnabled == null || _arrowMaterialRulesEnabled.Value);

            bool skippedForVanilla;
            if (physicalPart
                && TryResolvePommelMaterialRule(
                    targetClass,
                    partClass,
                    damage,
                    out match,
                    out skippedForVanilla))
            {
                return true;
            }

            if (arrowRulesEnabled
                && physicalPart
                && TryResolveArrowMaterialRule(targetClass, damage, out match))
            {
                return true;
            }

            if (arrowRulesEnabled && !physicalPart)
            {
                bool payloadSkippedForVanilla;
                bool payloadSkippedForEliteClamp;
                return TryResolveDamageRule(
                    targetClass,
                    partClass,
                    damage,
                    physicalTags | DamageTag.Arrow | DamageTag.DirectSpell,
                    out match,
                    out payloadSkippedForVanilla,
                    out payloadSkippedForEliteClamp);
            }

            if (partClass != null
                && partClass.IsDirectSpell
                && TryResolveArmoredSpellRule(
                    targetClass,
                    partClass,
                    damage,
                    out match,
                    out skippedForVanilla))
            {
                return true;
            }

            bool skippedForEliteClamp;
            if (TryResolveDamageRule(
                targetClass,
                partClass,
                damage,
                out match,
                out skippedForVanilla,
                out skippedForEliteClamp))
            {
                TryApplyHeavyMaterialBreach(targetClass, partClass, damage, ref match);
                return true;
            }

            return TryResolveAreaSwarmRule(
                targetClass,
                partClass,
                damage,
                out match,
                out skippedForVanilla);
        }

        private void PopulatePartDamageClassification(
            DamageClassification overall,
            DamageSubType subtype,
            DamageClassification part)
        {
            part.IsBloodMagic = false;
            part.IsBleed = false;
            part.IsPoison = false;
            part.IsWyrdness = false;
            part.IsSlashing = subtype == DamageSubType.Slashing;
            part.IsPiercing = subtype == DamageSubType.Piercing;
            part.IsBludgeoning = subtype == DamageSubType.Bludgeoning;
            part.IsGenericPhysical = subtype == DamageSubType.GenericPhysical;
            part.IsGenericMagical = subtype == DamageSubType.GenericMagical;
            part.IsFire = subtype == DamageSubType.Fire;
            part.IsCold = subtype == DamageSubType.Cold;
            part.IsElectric = subtype == DamageSubType.Electric;
            part.IsWet = subtype == DamageSubType.Wet;
            part.IsBurn = false;
            part.IsArrow = overall != null && overall.IsArrow;
            part.IsDirectSpell = overall != null && overall.IsDirectSpell;
            part.IsAxe = false;
            part.IsPommel = overall != null && overall.IsPommel;
            part.IsHeavyAttack = overall != null && overall.IsHeavyAttack;
            part.IsAreaAttack = overall != null && overall.IsAreaAttack;
            part.Tags = DamageTag.None;
            part.PhysicalTypeHint = "";

            bool physicalPart = part.IsSlashing
                || part.IsPiercing
                || part.IsBludgeoning
                || part.IsGenericPhysical;
            if (physicalPart && overall != null && overall.IsMiningToolCombatHit)
            {
                part.IsSlashing = false;
                part.IsPiercing = true;
                part.IsBludgeoning = false;
                part.IsGenericPhysical = false;
                part.PhysicalTypeHint = overall.PhysicalTypeHint;
            }
            else if (part.IsGenericPhysical
                && overall != null
                && !string.IsNullOrEmpty(overall.PhysicalTypeHint))
            {
                part.IsSlashing = overall.IsSlashing;
                part.IsPiercing = overall.IsPiercing;
                part.IsBludgeoning = overall.IsBludgeoning;
                part.PhysicalTypeHint = overall.PhysicalTypeHint;
            }

            if (overall != null)
            {
                part.IsAxe = overall.IsAxe && physicalPart;
                bool contextualStatusPart = subtype == DamageSubType.Pure
                    || subtype == DamageSubType.Default;
                part.IsBloodMagic = overall.IsBloodMagic
                    && (contextualStatusPart || part.IsGenericMagical || part.IsWyrdness);
                part.IsBleed = contextualStatusPart && overall.IsBleed;
                part.IsPoison = subtype == DamageSubType.Poison
                    || (contextualStatusPart && overall.IsPoison);
                part.IsWyrdness = subtype == DamageSubType.Wyrdness
                    || (contextualStatusPart && overall.IsWyrdness);
                part.IsBurn = overall.IsBurn && (contextualStatusPart || part.IsFire);
                part.IsFire |= part.IsBurn;
            }

            if (part.IsBloodMagic) part.Tags |= DamageTag.BloodMagic;
            if (part.IsBleed) part.Tags |= DamageTag.Bleed;
            if (part.IsPoison) part.Tags |= DamageTag.Poison;
            if (part.IsWyrdness) part.Tags |= DamageTag.Wyrdness;
            if (part.IsSlashing) part.Tags |= DamageTag.Slashing;
            if (part.IsPiercing) part.Tags |= DamageTag.Piercing;
            if (part.IsBludgeoning) part.Tags |= DamageTag.Bludgeoning;
            if (part.IsGenericPhysical) part.Tags |= DamageTag.GenericPhysical;
            if (part.IsGenericMagical) part.Tags |= DamageTag.GenericMagical;
            if (part.IsFire) part.Tags |= DamageTag.Fire;
            if (part.IsCold) part.Tags |= DamageTag.Cold;
            if (part.IsElectric) part.Tags |= DamageTag.Electric;
            if (part.IsWet) part.Tags |= DamageTag.Wet;
            if (part.IsBurn) part.Tags |= DamageTag.Burn;
            if (part.IsArrow) part.Tags |= DamageTag.Arrow;
            if (part.IsDirectSpell) part.Tags |= DamageTag.DirectSpell;
        }

        private static class AfterHealthDecreaseEventsPatch
        {
            public static void Postfix(object __instance, object[] __args)
            {
                SteelAndBonePlugin plugin = Instance;
                if (plugin != null && __args != null && __args.Length > 0)
                {
                    plugin.HandleDamageOutcome(__instance, __args[0]);
                }
            }
        }
    }
}
