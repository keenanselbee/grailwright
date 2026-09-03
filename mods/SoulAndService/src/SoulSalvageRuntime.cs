using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Awaken.TG.Assets;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.AI.Utils;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Character.Features;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.Factions.Crimes;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Fights.NPCs.Presences;
using Awaken.TG.Main.General.Configs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.LootTables;
using Awaken.TG.Main.Heroes.Items.Tooltips;
using Awaken.TG.Main.Heroes.Items.Weapons;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Thievery;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Actions;
using Awaken.TG.Main.Locations.Actions.Attachments;
using Awaken.TG.Main.Locations.Attachments.Attachment;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Shops;
using Awaken.TG.Main.Locations.Views;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Scenes;
using Awaken.TG.Main.Templates;
using BepInEx;
using BepInEx.Bootstrap;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    [Serializable]
    public sealed class RaisedPersistencePayload
    {
        public int Version = 1;
        public RaisedPersistenceSnapshot[] Records =
            new RaisedPersistenceSnapshot[0];
    }

    [Serializable]
    public sealed class RaisedPersistenceSnapshot
    {
        public int Phase;
        public string SourceId;
        public string SourceInteractability;
        public string SpawnTemplateGuid;
        public string SourceDisplayName;
        public string CorpseFingerprint;
        public float Quality01;
        public int QualityTier;
        public float BindingManaCost;
        public int InvestedSoulVigor;
        public int NativeSoulVigor;
        public float HealthFraction;
        public float SoulforgedOriginalMaximumHealth;
        public float SoulforgedDamageDealt;
        public int SoulforgedRank;
        public float EmpowermentMultiplier;
        public bool RecoveryManaInitialized;
        public float RecoveryOriginalMana;
        public float RecoveryRemainingMana;
        public int RecoveryOriginalSoulVigor;
        public int EmpowermentSoulVigorInvestment;
        public int RecoveredSoulforgedRankMask;
        public bool IsMiniboss;
        public string HighSoulFingerprint;
        public int HighSoulExtractionValue;
        public int HighSoulServiceCycle;
    }

    internal static class SoulSalvageRuntime
    {
        private const string SoulSalvageTemplateGuid =
            "7bdd3a1b62fb53d46b8a28142c18a110";
        private const string SoulRendDisplayName = "Soul Rend";
        private const float SoulRendAssistRadius = 0.4f;
        private const int SoulRendAssistColliderBufferSize = 64;
        private const int SoulTargetRaycastBufferSize = 64;
        private const float SoulClaimPreparationCleanupIntervalSeconds = 1.0f;
        private const string GenericRaisedServantPortraitKey =
            "759a3e6e96ddae742ab8cde19fae42f0";
        private const string SkeletonSummonVfxKey =
            "0d139743aa2c21d4da0c81fb4e609890";
        private const string BloodRitualLesserVfxKey =
            "d858e5e33ccd9ec4ea9b3099ee02d32e";
        private const string BloodRitualGreaterVfxKey =
            "bfa9aa86addeec347877ffb0fc0b4315";
        private const float NativeManaRefundMultiplier = 0.75f;
        private const float HeavyCastManaCostMultiplier = 2.0f;
        private const float ServantEmpowerHealthThreshold = 0.95f;
        private const float ServantHealingPowerZeroFraction = 0.20f;
        private const float ServantHealingPowerMaximumFraction = 0.50f;
        private const float RaisedSalvageMinimumQualityFactor = 0.65f;
        private const float RaisedSalvageMaximumQualityFactor = 1.50f;
        private const float RaisedSalvageMaximumRefundFraction = 0.75f;
        private const float EmpowermentSeverRefundFraction = 0.75f;
        private const float SoulforgedRecoveryFractionPerRank = 0.03f;
        private const float SoulforgedRecoveryMinimumHealthFraction = 0.50f;
        private const float SoulSalvageRange = 50.0f;
        private const float ReanimationPositionRefreshSeconds = 0.10f;
        private const float ComparableLightSpellBaseDamage = 5.0f;
        private const float SoulRendPowerZeroMultiplier = 0.50f;
        private const float SoulRendPowerNormalMultiplier = 1.00f;
        private const float SoulRendPowerMaximumMultiplier = 2.00f;
        private const int SoulClaimMaximumPreparationHits = 5;
        private const float SoulClaimThresholdBonusPerHit = 0.02f;
        private const float SoulClaimMinimumHealthThreshold = 0.01f;
        private const float SoulClaimMaximumHealthThreshold = 0.40f;
        private const int OrdinarySummonVigorCostPerTier = 3;
        private const int HighSoulPoolPortions = 6;
        private const int MinibossReanimationBaseSoulVigor = 120;
        private const float MinibossBindingResistance = 340.0f;
        private const float ExecutedServantCleanupSeconds = 1.0f;
        private const int RaisedPersistenceLegacyVersion = 1;
        private const int RaisedPersistencePreviousVersion = 2;
        private const int RaisedPersistenceVersion = 3;
        private const int RaisedPersistenceMagic = 0x53525032;
        private const int RaisedPersistenceMaximumRecords = 256;
        private const int RaisedPersistenceMaximumPayloadCharacters = 2097152;
        private const int RaisedPersistenceMaximumStringCharacters = 16384;
        private const string RaisedPersistencePayloadPrefix = "SASRP2:";
        private const int RaisedPersistencePending = 0;
        private const int RaisedPersistenceActive = 1;
        private const string RaisedPersistencePayloadKey =
            "persistent_raised.payload";
        private static readonly string[] NonHumanoidSoulAudioTerms =
        {
            "Animal", "Animal_Prey", "Skeleton", "BoneMask", "HitBones",
            "Construct", "SarrasCreature", "ReefboundBody", "Wyrd", "Spirit",
            "Flora", "Plant", "Swarm", "Monster", "Undead", "Zombie",
            "Abomination", "Wolf", "Bear", "Boar", "Spider", "Wyrm",
            "Golem", "Grindleow", "Corpse Eater", "CorpseEater"
        };
        private static readonly string[] UnbindableHighSoulMessages =
        {
            "This spirit is too powerful to bind.",
            "This soul cannot be commanded.",
            "No mortal binding can contain this spirit.",
            "This spirit defies your service.",
            "Your binding cannot hold this soul."
        };
        private static readonly HashSet<string> ReanimatableMinibossTemplateGuids =
            new HashSet<string>(StringComparer.Ordinal)
            {
                // Generic non-scalable enemies. Individual scene instances still
                // have to pass every typed lifecycle protection below.
                "65d1cb395b9dfef45861bc23bbcc6ccb", // Foredweller Weaver
                "1bf99bc722b8c9a41abf0f0fd5aa7fb6", // Foredweller Weaver Mage
                "8a709501a390f4141baa2bc4dcfef4ab", // Blood Abomination
                "86fe83f56653eb44497ebc6526f8e93e", // Rootambusher
                "8ae71a8c2d8cd094489cd0f3bef79b43", // Forlorn Ogre
                "9f14199b71fc33847babc9df3ac610fd" // Sleepwalker
            };
        private const string BloodMagicPluginGuid =
            "ks.tgfoa.blood-magic-expansion";
        private const string BloodMagicApiTypeName =
            "BloodMagicExpansion.BloodMagicApi";
        private const string EyesInTheDarkPluginGuid =
            "ks.tgfoa.eyes-in-the-dark";
        private const string EyesInTheDarkCorpseDrainApiTypeName =
            "EyesInTheDark.EyesInTheDarkCorpseDrainApi";
        private const string VersatileWeaponsPluginGuid =
            "ks.tgfoa.versatile-weapons";
        private const string VersatileWeaponsApiTypeName =
            "VersatileWeapons.VersatileWeaponsApi";

        private sealed class ReanimationRecord
        {
            internal Location SourceCorpse;
            internal string SourceId;
            internal Location RaisedLocation;
            internal NpcElement RaisedNpc;
            internal LocationInteractability SourceInteractability;
            internal string SourceDisplayName;
            internal string CorpseFingerprint;
            internal float Quality01;
            internal Grailwright.Shared.CorpseQualityTier QualityTier;
            internal float BindingManaCost;
            internal int InvestedSoulVigor;
            internal int NativeSoulVigor;
            internal ServantRecoveryLedger Recovery =
                new ServantRecoveryLedger();
            internal float SalvageHealthFraction = 1.0f;
            internal float ManaReturnedOnSacrifice;
            internal StatTweak QualityHealthTweak;
            internal Vector3 OriginalCoords;
            internal Quaternion OriginalRotation;
            internal Vector3 LastSafeCoords;
            internal Quaternion LastSafeRotation;
            internal bool Sacrificed;
            internal bool DismissedAsRemains;
            internal bool ServiceInitialized;
            internal bool BloodRitualExecuted;
            internal float BloodRitualHoldUntil;
            internal float NextBloodRitualMovementHoldAt;
            internal int BloodRitualCommandSequence;
            internal string SpawnTemplateGuid;
            internal bool IsMiniboss;
            internal string HighSoulFingerprint;
            internal int HighSoulExtractionValue;
            internal int HighSoulServiceCycle;
        }

        private sealed class ServantRecoveryLedger
        {
            internal bool ManaInitialized;
            internal float OriginalMana;
            internal float RemainingMana;
            internal int OriginalSoulVigor;
            internal int EmpowermentSoulVigorInvestment;
            internal int RecoveredSoulforgedRankMask;
        }

        private sealed class OrdinarySummonInvestment
        {
            internal int InvestedSoulVigor;
            internal ServantRecoveryLedger Recovery =
                new ServantRecoveryLedger();
        }

        private sealed class SoulClaimPreparationState
        {
            internal NpcElement Target;
            internal int Hits;
        }

        private sealed class RaisedPersistenceRecoveryDiagnostics
        {
            internal int Loaded;
            internal int Attempted;
            internal int Scheduled;
            internal int Restored;
            internal int Rejected;
            internal int AlreadyServing;
            internal int SourcesRestored;
            internal int Deferred;
            internal int PendingCallbacks;
            internal bool ScenePassComplete;
        }

        private sealed class CurrentSessionCorpseState
        {
            internal bool IsSummon;
        }

        private sealed class RaiseAllCandidate
        {
            internal Location Source;
            internal float DistanceSqr;
        }

        private struct HighSoulCorpseInfo
        {
            internal Location Corpse;
            internal NpcTemplate NpcTemplate;
            internal NpcType NpcType;
            internal int NativeTier;
            internal int ExtractionValue;
            internal string Fingerprint;
            internal bool CanSafelySimplify;
            internal bool CanReanimate;
            internal LocationTemplate SpawnTemplate;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }

        private sealed class NecroticDamageMarker
        {
        }

        private struct CastState
        {
            internal bool IsSoulSalvage;
            internal bool IsHeavy;
            internal Item Item;
        }

        private static readonly Dictionary<string, ReanimationRecord> Reanimations =
            new Dictionary<string, ReanimationRecord>();
        private static readonly Dictionary<string, ReanimationRecord>
            ExecutedServantRemains =
                new Dictionary<string, ReanimationRecord>();
        private static readonly Dictionary<string, OrdinarySummonInvestment>
            OrdinarySummonInvestments =
                new Dictionary<string, OrdinarySummonInvestment>();
        private static readonly Dictionary<string, int> VanillaSummonTiers =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "984887087a27f3b4bacd13b76c3e2c33", 1 }, // Summon Corpse Eater
                { "c92e54c117a0c6f44aab42ec471d5ae4", 1 }, // Summon Grindylow
                { "3bd577472a0191c44bf298a82553cf3b", 1 }, // Wolf's Call
                { "2fd5cb39f6fd1824d9140dd0c9254b9a", 2 }, // Summon Redcap
                { "5aba3cd999b809445a4911f79fe38ebd", 2 }, // Skeleton Army
                { "3fda4c8c6e837a140b073135af120753", 3 }, // Summon Battlemage
                { "12d6c99da0f976c48be3b2a668e9072a", 3 }, // Skeleton Knight
                { "0bec60b30a30b904bacf180b9355c23c", 3 }, // Summon Crystal Crawler
                { "7a26e25196836554b88af907781341f3", 3 }, // Summon Keeper
                { "25461a54337b680499007ff4d56e4136", 3 }, // Summon Master Assassin
                { "1f621fba5096f354397c7c283ca26f9a", 3 }, // Call of the Depths
                { "7ab9829d6ebdcfd4e935fc658a6201f8", 4 }, // Ghost of Broc Meala
                { "ff98f521a7336aa44b5e953297ec9097", 4 }, // Summon Kamelot Spearman
                { "3cd88e22cc736294f9c5470e534878b9", 4 }, // Summon Sir Lancelot
                { "b0dbfbbad4fd39d4488d3e1eec8ed6e0", 4 }, // Summon Remor Archer
                { "9cb2498afdb7608469294b5a9c659435", 4 }, // Summon Remor Warrior
                { "cc2e07161c27b1f408632026ae14c961", 4 }, // Shoal Lancer
                { "0bc3c17f04146974b9c7c94eec737bda", 5 }, // Pyre Golem
                { "bdc07f76c5922634dbabfd54222c6615", 5 }, // Rime Golem
                { "0b828151247e64f43b043b2f1f93068f", 5 }, // Storm Golem
                { "419148188657ccd45b84ebbd7a7346d3", 5 }, // Mire Golem
                { "5638f9b619f60ec4f89ce6890eb885f9", 5 }, // Gawain
                { "dcd6ea6c09cccd5468e825da785f4b4b", 5 }, // Bertilak
                { "a339badda1efbe841ac49fcd62f13888", 5 }, // Sir Vast
                { "a4d083ffa4d64f143a7bb019e53b2d0d", 5 }, // Sea Bite
                { "b477a2b9fc1970244936cd5ccb096628", 6 } // Sir Galahad
            };
        private static readonly Dictionary<string, SoulClaimPreparationState>
            SoulClaimPreparations =
                new Dictionary<string, SoulClaimPreparationState>();
        private static readonly List<string> SoulClaimPreparationRemovalBuffer =
            new List<string>();
        private static readonly ConditionalWeakTable<Damage, NecroticDamageMarker>
            NecroticDamageMarkers =
                new ConditionalWeakTable<Damage, NecroticDamageMarker>();
        private static ConditionalWeakTable<Corpse, CurrentSessionCorpseState>
            CurrentSessionCorpses =
                new ConditionalWeakTable<Corpse, CurrentSessionCorpseState>();
        private static int _focusedTargetCacheFrame = -1;
        private static bool _focusedTargetCacheFound;
        private static Location _focusedTargetCacheLocation;
        private static NpcHeroSummon _focusedTargetCacheSummon;
        private static int _focusedHighSoulCacheFrame = -1;
        private static bool _focusedHighSoulCacheFound;
        private static HighSoulCorpseInfo _focusedHighSoulCacheInfo;
        private static readonly List<Location> PendingRaisedDiscards =
            new List<Location>();
        private static readonly List<NpcHeroSummon> PendingLegacyRaisedRestores =
            new List<NpcHeroSummon>();
        private static RaisedPersistenceSnapshot[] _loadedRaisedPersistence;
        private static IEventListener _raisedPersistenceSceneListener;
        private static bool _raisedPersistenceListenerWarningLogged;
        private static readonly Dictionary<string, ItemStats> SoulSalvageItems =
            new Dictionary<string, ItemStats>();
        private static readonly List<string> SoulSalvageItemRemovalBuffer =
            new List<string>();
        private static readonly List<string> ExecutedServantRemovalBuffer =
            new List<string>();
        private static readonly Dictionary<string, StatTweak> HeavyCostTweaks =
            new Dictionary<string, StatTweak>();
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>>
            OptionalPropertyCache =
                new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>>
            OptionalFieldCache =
                new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly HashSet<MagicItemTemplateInfo> LightCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly HashSet<MagicItemTemplateInfo> HeavyCastInfos =
            new HashSet<MagicItemTemplateInfo>();
        private static readonly Dictionary<MagicItemTemplateInfo, int>
            OrdinarySummonCastTiers =
                new Dictionary<MagicItemTemplateInfo, int>();
        private static readonly Collider[] SoulRendAssistColliderBuffer =
            new Collider[SoulRendAssistColliderBufferSize];
        private static readonly RaycastHit[] SoulTargetRaycastBuffer =
            new RaycastHit[SoulTargetRaycastBufferSize];
        private static readonly RaycastHitDistanceComparer SoulTargetHitComparer =
            new RaycastHitDistanceComparer();
        private static readonly FieldInfo SimplifiedDeadBodyReplacementField =
            AccessTools.Field(typeof(NpcDummy), "_simplifiedDeadBodyReplacementRef");
        private static readonly FieldInfo LocationInitializerField =
            AccessTools.Field(typeof(Location), "_initializer");
        private static readonly MethodInfo PreventSummonMovementMethod =
            AccessTools.Method(typeof(NpcHeroSummon), "PreventMovement");

        private static bool _lightCastActive;
        private static float _nextExecutedServantCleanupTime;
        private static bool _heavyCastActive;
        private static bool _lightHarvestCompleted;
        private static bool _lightPreserveTarget;
        private static NpcHeroSummon _lightTarget;
        private static NpcHeroSummon _heavyTarget;
        private static float _lightOriginalMana;
        private static float _lightHealthFraction;
        private static float _lightMaximumManaReturn;
        private static float _lightResolvedManaReturn;
        private static float _itemRefreshDelay;
        private static float _nextSoulClaimPreparationCleanupAt;
        private static float _nextReanimationPositionRefreshTime;
        private static int _raiseAllEligibilityFrame = -1;
        private static Hero _raiseAllEligibilityHero;
        private static float _raiseAllEligibilityRadius;
        private static bool _raiseAllEligibilityResult;
        private static bool _creatingRaisedServant;
        private static MethodInfo _bloodMagicGetExsanguinationSeverityMethod;
        private static bool _bloodMagicApiUnavailable;
        private static MethodInfo _eyesInTheDarkRegisterCorpseDrainMethod;
        private static bool _eyesInTheDarkBridgeResolved;
        private static bool _eyesInTheDarkFailureLogged;
        private static Func<bool> _versatileWeaponsIsMainHandSuppressed;
        private static Func<bool> _versatileWeaponsIsOffHandSuppressed;
        private static bool _versatileWeaponsApiUnavailable;

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                RequireMethod(typeof(GameplayMemory), nameof(GameplayMemory.OnBeforeSerialize)),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGameplayMemorySerialize)));
            harmony.Patch(
                RequireMethod(typeof(GameplayMemory), nameof(GameplayMemory.OnAfterDeserialize)),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterGameplayMemoryDeserialize)));
            harmony.Patch(
                RequireMethod(typeof(Location), "OnInitialize"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterLocationInitialized)));
            ConstructorInfo corpseConstructor = AccessTools.Constructor(
                typeof(Corpse),
                new[] { typeof(NpcElement), typeof(ICharacter) });
            if (corpseConstructor == null)
            {
                throw new MissingMethodException(
                    typeof(Corpse).FullName,
                    ".ctor(NpcElement, ICharacter)");
            }
            harmony.Patch(
                corpseConstructor,
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterCorpseConstructed)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingBegun",
                    new[] { typeof(CastingHand), typeof(bool) }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingBegun)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingCanceled",
                    new[]
                    {
                        typeof(CastingHand),
                        typeof(Item),
                        typeof(bool),
                        typeof(bool)
                    }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingCanceled)));
            harmony.Patch(
                RequireMethod(
                    typeof(VHeroController),
                    "CastingEnded",
                    new[] { typeof(CastingHand), typeof(bool) }),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeCastingEnded)),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterCastingEnded)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "get_ManaExpended"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGetManaExpended)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "Destroy"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeDestroySummon)));
            harmony.Patch(
                RequireMethod(typeof(NpcElement), "Destroy"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeDestroyHeavyTarget)));
            harmony.Patch(
                RequireMethod(typeof(HealthElement), "Kill"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeKillHeavyTarget)));
            harmony.Patch(
                RequireMethod(typeof(ItemStats), "OnInitialize"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterItemStatsInitialized)));
            harmony.Patch(
                RequireMethod(typeof(Item), "get_DisplayName"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterGetItemDisplayName)));
            harmony.Patch(
                RequireMethod(typeof(SearchAction), "get_ActionFrame"),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterGetSearchActionFrame)));
            harmony.Patch(
                RequireMethod(typeof(NpcDummy), "TryReplaceWithSimplifiedLocation"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeReplaceHighSoulCorpse)));
            harmony.Patch(
                RequireMethod(typeof(MagicItemTemplateInfo), "get_MagicDescription"),
                prefix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(BeforeGetMagicDescription)),
                postfix: new HarmonyMethod(
                    typeof(SoulSalvageRuntime),
                    nameof(AfterGetMagicDescription)));
        }

        private static bool BeforeReplaceHighSoulCorpse(
            NpcDummy __instance,
            ref bool __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Location location = __instance == null
                ? null
                : __instance.ParentModel;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || location == null
                || location.HasBeenDiscarded)
            {
                return true;
            }

            ReanimationRecord activeRecord = Reanimations.Values.FirstOrDefault(
                record => record != null
                    && record.IsMiniboss
                    && ReferenceEquals(record.SourceCorpse, location));
            if (activeRecord != null)
            {
                __result = false;
                return false;
            }

            Hero hero = Hero.Current;
            Corpse sourceCorpse = location.TryGetElement<Corpse>();
            if (IsPotentialSavedHighSoulCorpse(location)
                && (hero == null
                    || hero.Faction == null
                    || sourceCorpse == null
                    || sourceCorpse.Faction == null))
            {
                __result = false;
                return false;
            }
            if (hero == null
                || !TryResolveHighSoulCorpse(
                    hero,
                    location,
                    out HighSoulCorpseInfo highSoul))
            {
                return true;
            }
            int remaining = SoulProgressionRuntime
                .GetOrInitializeHighSoulVigorPool(
                    highSoul.Fingerprint,
                    highSoul.ExtractionValue);
            if (remaining <= 0 && highSoul.CanSafelySimplify)
            {
                return true;
            }
            __result = false;
            return false;
        }

        private static void AfterCorpseConstructed(
            Corpse __instance,
            NpcElement npc)
        {
            if (__instance == null || npc == null)
            {
                return;
            }

            CurrentSessionCorpses.Remove(__instance);
            CurrentSessionCorpses.Add(
                __instance,
                new CurrentSessionCorpseState
                {
                    IsSummon = npc.IsSummon
                        || npc.HasElement<NpcHeroSummon>()
                });
        }

        private static bool IsPotentialSavedHighSoulCorpse(Location candidate)
        {
            NpcTemplate npcTemplate = candidate == null
                ? null
                : NpcTemplate.FromNpcOrDummy(candidate);
            return candidate != null
                && !candidate.HasBeenDiscarded
                && (candidate.Initializer is SceneLocationInitializer
                    || (candidate.Initializer is RuntimeLocationInitializer
                        && IsAuthoredSourceDiscarded(((Model)candidate).ID)))
                && !((Model)candidate).IsNotSaved
                && candidate.TryGetElement<Corpse>() != null
                && npcTemplate != null
                && !npcTemplate.IsSummon
                && (npcTemplate.NpcType == NpcType.MiniBoss
                    || npcTemplate.NpcType == NpcType.Boss);
        }

        internal static void Update()
        {
            UpdateSoulSalvageItems();
            UpdateReanimationPositions();
            ReanimationGlyphRuntime.Update();
            UpdateExecutedServantRemains();
            RemoveStaleSoulClaimPreparations();

            if (PendingRaisedDiscards.Count > 0)
            {
                Location[] pending = PendingRaisedDiscards.ToArray();
                PendingRaisedDiscards.Clear();
                foreach (Location location in pending)
                {
                    if (location != null && !location.HasBeenDiscarded)
                    {
                        location.Discard();
                    }
                }
            }
        }

        private static void AfterLocationInitialized(Location __instance)
        {
            try
            {
                Location location = __instance;
                if (location == null || location.HasBeenDiscarded)
                {
                    return;
                }
                if (_loadedRaisedPersistence != null
                    && _raisedPersistenceSceneListener == null)
                {
                    EnsureRaisedPersistenceSceneListener();
                }
                string sourceId = ((Model)location).ID;
                if (ReadDeferredSourceInt(sourceId, "restore") != 0)
                {
                    if (!TryRestoreDeferredSource(location, out string failure))
                    {
                        SoulAndServicePlugin.Instance?.LogWarning(
                            "Deferred Soul Rend source recovery remains pending: "
                            + failure + ".");
                        EnsureRaisedPersistenceSceneListener();
                    }
                    return;
                }

                string unsafeSummonId = null;
                foreach (KeyValuePair<string, ReanimationRecord> pair
                    in Reanimations)
                {
                    ReanimationRecord record = pair.Value;
                    if (record == null
                        || record.SourceCorpse != null
                        || !string.Equals(
                            record.SourceId,
                            sourceId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    TemplatesProvider templates = World.Services == null
                        ? null
                        : World.Services.TryGet<TemplatesProvider>();
                    LocationTemplate spawnTemplate = templates == null
                        || string.IsNullOrEmpty(record.SpawnTemplateGuid)
                            ? null
                            : templates.Get<LocationTemplate>(
                                record.SpawnTemplateGuid);
                    if (!IsMatchingPersistentSource(location, spawnTemplate))
                    {
                        record.SourceCorpse = location;
                        unsafeSummonId = pair.Key;
                        break;
                    }
                    record.SourceCorpse = location;
                    record.OriginalCoords = location.Coords;
                    record.OriginalRotation = location.Rotation;
                    location.SetInteractability(LocationInteractability.Hidden);
                    SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                    if (plugin != null)
                    {
                        plugin.LogDiagnostic(
                            "Reconnected persistent raised servant to source corpse "
                            + sourceId + ".");
                    }
                    break;
                }
                if (!string.IsNullOrEmpty(unsafeSummonId))
                {
                    RestoreSourceCorpse(
                        unsafeSummonId,
                        discardRaisedCopy: true,
                        showDiagnostic: false);
                    SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                    if (plugin != null)
                    {
                        plugin.LogWarning(
                            "Rejected a raised-servant source identity mismatch safely.");
                    }
                }
            }
            catch (Exception exception)
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Soul Rend source lifecycle recovery failed safely: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        internal static void Shutdown()
        {
            CurrentSessionCorpses =
                new ConditionalWeakTable<Corpse, CurrentSessionCorpseState>();
            RemoveRaisedPersistenceSceneListener();
            _loadedRaisedPersistence = null;
            _raisedPersistenceListenerWarningLogged = false;
            PendingLegacyRaisedRestores.Clear();
            SoulAndServicePlugin activePlugin = SoulAndServicePlugin.Instance;
            if (activePlugin != null
                && activePlugin.PersistentServants != null
                && activePlugin.PersistentServants.Value)
            {
                foreach (ReanimationRecord record
                    in ExecutedServantRemains.Values.ToArray())
                {
                    RestoreExecutedServantCorpse(record);
                }
                foreach (string id in Reanimations.Keys.ToArray())
                {
                    ReanimationGlyphRuntime.Remove(id);
                }
                Reanimations.Clear();
                ExecutedServantRemains.Clear();
                OrdinarySummonInvestments.Clear();
                ReanimationGlyphRuntime.Shutdown();
                SoulSalvageAudioRuntime.Shutdown();
                return;
            }
            foreach (string id in Reanimations.Keys.ToArray())
            {
                RestoreSourceCorpse(
                    id,
                    discardRaisedCopy: true,
                    showDiagnostic: false);
            }
            foreach (ReanimationRecord record in ExecutedServantRemains.Values.ToArray())
            {
                RestoreExecutedServantCorpse(record);
            }
            ExecutedServantRemains.Clear();
            Update();
            foreach (StatTweak tweak in HeavyCostTweaks.Values.ToArray())
            {
                if (tweak != null && !((Model)tweak).HasBeenDiscarded)
                {
                    tweak.Discard();
                }
            }
            HeavyCostTweaks.Clear();
            SoulSalvageItems.Clear();
            LightCastInfos.Clear();
            HeavyCastInfos.Clear();
            OrdinarySummonCastTiers.Clear();
            SoulClaimPreparations.Clear();
            SoulClaimPreparationRemovalBuffer.Clear();
            SoulSalvageItemRemovalBuffer.Clear();
            ExecutedServantRemovalBuffer.Clear();
            _nextSoulClaimPreparationCleanupAt = 0.0f;
            OrdinarySummonInvestments.Clear();
            ClearLightCastState();
            ReanimationGlyphRuntime.Shutdown();
            SoulSalvageAudioRuntime.Shutdown();
        }

        internal static void OnSummonDiscarded(
            NpcHeroSummon summon,
            bool fromDomainDrop)
        {
            if (summon == null)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            if (fromDomainDrop)
            {
                ReanimationGlyphRuntime.Remove(summonId);
                Reanimations.Remove(summonId);
                OrdinarySummonInvestments.Remove(summonId);
                return;
            }
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                OrdinarySummonInvestments.Remove(summonId);
                ClearPersistedServant(summonId);
                return;
            }
            if (record.BloodRitualExecuted && !record.IsMiniboss)
            {
                ReanimationGlyphRuntime.Remove(summonId);
                Reanimations.Remove(summonId);
                ClearPersistedServant(summonId);
                if (record.RaisedLocation != null
                    && !record.RaisedLocation.HasBeenDiscarded)
                {
                    ExecutedServantRemains[((Model)record.RaisedLocation).ID] = record;
                }
                else
                {
                    RestoreExecutedServantCorpse(record);
                }
                if (record.QualityHealthTweak != null
                    && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
                {
                    ((Model)record.QualityHealthTweak).Discard();
                }
                return;
            }
            bool endedInWorld = record.Sacrificed
                || record.DismissedAsRemains
                || (record.RaisedNpc != null && !record.RaisedNpc.IsAlive);
            if (endedInWorld)
            {
                EndServiceAsRemains(summonId, showDiagnostic: true);
            }
            else
            {
                RestoreSourceCorpse(
                    summonId,
                    discardRaisedCopy: true,
                    showDiagnostic: false);
            }
        }

        internal static void OnSummonInitialized(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (_creatingRaisedServant
                || summon == null
                || plugin == null
                || !plugin.IsEnabled
                || !ReferenceEquals(summon.Ally, Hero.Current))
            {
                return;
            }

            string summonId = ((Model)summon).ID;
            bool persistent = plugin.PersistentServants != null
                && plugin.PersistentServants.Value;
            SetSummonSavedState(summon, persistent);
            if (persistent
                && ReadPersistedInt(summonId, "raised_active") != 0)
            {
                if (!PendingLegacyRaisedRestores.Contains(summon))
                {
                    PendingLegacyRaisedRestores.Add(summon);
                }
                EnsureRaisedPersistenceSceneListener();
                return;
            }
            if (persistent
                && ReadPersistedInt(summonId, "ordinary_active") != 0)
            {
                int investedVigor = Math.Max(
                    0,
                    ReadPersistedInt(summonId, "ordinary_investment"));
                OrdinarySummonInvestment investment =
                    new OrdinarySummonInvestment
                    {
                        InvestedSoulVigor = investedVigor
                    };
                if (ReadPersistedInt(
                        summonId,
                        "ordinary_recovery_version") > 0)
                {
                    investment.Recovery.ManaInitialized =
                        ReadPersistedInt(
                            summonId,
                            "ordinary_recovery_mana_initialized") != 0;
                    investment.Recovery.OriginalMana = Math.Max(
                        0.0f,
                        ReadPersistedFloat(
                            summonId,
                            "ordinary_recovery_original_mana"));
                    investment.Recovery.RemainingMana = Mathf.Clamp(
                        ReadPersistedFloat(
                            summonId,
                            "ordinary_recovery_remaining_mana"),
                        0.0f,
                        investment.Recovery.OriginalMana);
                    investment.Recovery.OriginalSoulVigor = Math.Max(
                        investedVigor,
                        ReadPersistedInt(
                            summonId,
                            "ordinary_recovery_original_vigor"));
                    investment.Recovery.EmpowermentSoulVigorInvestment =
                        Mathf.Clamp(
                            ReadPersistedInt(
                                summonId,
                                "ordinary_empowerment_investment"),
                            0,
                            investedVigor);
                    investment.Recovery.RecoveredSoulforgedRankMask =
                        ReadPersistedInt(
                            summonId,
                            "ordinary_recovered_rank_mask")
                        & ((1 << SoulforgedRuntime.MaximumRank) - 1);
                }
                else
                {
                    investment.Recovery.OriginalSoulVigor = investedVigor;
                }
                OrdinarySummonInvestments[summonId] = investment;
                return;
            }
            if (!plugin.SoulSalvageOverhaul.Value)
            {
                OrdinarySummonInvestments[summonId] =
                    new OrdinarySummonInvestment();
                if (persistent)
                {
                    WritePersistedInt(summonId, "ordinary_active", 1);
                    WritePersistedInt(summonId, "ordinary_investment", 0);
                }
                return;
            }
            if (OrdinarySummonInvestments.ContainsKey(summonId))
            {
                return;
            }
            int summonTier = GetOrdinarySummonTier(summon.Item);
            int vigorCost = GetOrdinarySummonSoulVigorCost(
                summonTier,
                SoulProgressionRuntime.GetNecromanticPower());
            RegisterOrdinarySummonCastInfo(summon.Item, summonTier);
            if (SoulProgressionRuntime.TrySpendSoulVigor(
                vigorCost,
                out int before,
                out int after))
            {
                int committedVigor = after < before ? vigorCost : 0;
                OrdinarySummonInvestment investment =
                    new OrdinarySummonInvestment
                    {
                        InvestedSoulVigor = committedVigor
                    };
                investment.Recovery.OriginalSoulVigor = committedVigor;
                OrdinarySummonInvestments[summonId] = investment;
                if (persistent)
                {
                    SaveOrdinarySummonInvestment(summonId, investment);
                }
                SoulProgressionRuntime.ShowSoulVigorWanesAfterSpend(before, after);
                if (committedVigor > 0)
                {
                    SoulProgressionRuntime.ShowSummonCreated(
                        GetSummonDisplayName(summon),
                        committedVigor);
                }
                plugin.LogDiagnostic(
                    "Invested " + committedVigor.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor in tier "
                    + summonTier.ToString(CultureInfo.InvariantCulture)
                    + " ordinary summon " + summonId
                    + "; balance=" + before + " -> " + after + ".");
                return;
            }

            plugin.LogDiagnostic(
                "Rejected ordinary summon " + summonId
                + " because it requires "
                + vigorCost.ToString(CultureInfo.InvariantCulture)
                + " Soul Vigor.");
            SoulProgressionRuntime.ShowInsufficientSoulVigor(vigorCost);
            summon.ParentModel.OnCompletelyInitialized(
                delegate
                {
                    if (!summon.HasBeenDiscarded)
                    {
                        summon.Destroy();
                    }
                });
        }

        internal static bool IsReanimatedSummon(string summonId)
        {
            return !string.IsNullOrEmpty(summonId)
                && Reanimations.ContainsKey(summonId);
        }

        private static void SetSummonSavedState(
            NpcHeroSummon summon,
            bool persistent)
        {
            if (summon == null || summon.ParentModel == null)
            {
                return;
            }
            Location location = summon.ParentModel.ParentModel;
            if (!persistent && location != null)
            {
                ((Model)location).MarkedNotSaved = true;
            }
        }

        private static bool TryRestorePersistentReanimation(
            NpcHeroSummon summon)
        {
            string summonId = ((Model)summon).ID;
            if (ReadPersistedInt(summonId, "raised_active") == 0)
            {
                return false;
            }
            string sourceId = ReadPersistedString(summonId, "source_id");
            Location source = World.All<Location>().FirstOrDefault(location =>
                location != null
                && !location.HasBeenDiscarded
                && string.Equals(
                    ((Model)location).ID,
                    sourceId,
                    StringComparison.Ordinal));
            NpcElement npc = summon.ParentModel;
            ReanimationRecord record = new ReanimationRecord
            {
                SourceCorpse = source,
                SourceId = sourceId,
                RaisedLocation = npc.ParentModel,
                RaisedNpc = npc,
                SourceInteractability = ResolvePersistedInteractability(
                    ReadPersistedString(
                        summonId,
                        "source_interactability")),
                SourceDisplayName = ReadPersistedString(
                    summonId,
                    "source_name"),
                CorpseFingerprint = ReadPersistedString(
                    summonId,
                    "fingerprint"),
                Quality01 = Mathf.Clamp01(ReadPersistedFloat(
                    summonId,
                    "quality")),
                QualityTier = (Grailwright.Shared.CorpseQualityTier)Mathf.Clamp(
                    ReadPersistedInt(summonId, "quality_tier"),
                    (int)Grailwright.Shared.CorpseQualityTier.Meager,
                    (int)Grailwright.Shared.CorpseQualityTier.Prime),
                BindingManaCost = Math.Max(0.0f, ReadPersistedFloat(
                    summonId,
                    "binding_mana")),
                InvestedSoulVigor = Math.Max(0, ReadPersistedInt(
                    summonId,
                    "invested_vigor")),
                NativeSoulVigor = Math.Max(0, ReadPersistedInt(
                    summonId,
                    "native_vigor")),
                OriginalCoords = source == null ? npc.Coords : source.Coords,
                OriginalRotation = source == null
                    ? npc.ParentModel.Rotation
                    : source.Rotation,
                LastSafeCoords = npc.Coords,
                LastSafeRotation = npc.ParentModel == null
                    ? Quaternion.identity
                    : npc.ParentModel.Rotation,
                ServiceInitialized = true
            };
            EnsureRaisedRecoveryLedger(record);
            if (string.IsNullOrEmpty(record.SourceDisplayName))
            {
                record.SourceDisplayName = GetCorpseDisplayName(source);
            }
            Reanimations[summonId] = record;
            if (source != null)
            {
                source.SetInteractability(LocationInteractability.Hidden);
            }
            else
            {
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Persistent raised servant " + summonId
                        + " is waiting to reconnect to source corpse "
                        + sourceId + ".");
                }
            }
            record.RaisedLocation.RemoveElementsOfType<AliveLocationDeathReward>();
            record.RaisedLocation.RemoveElementsOfType<SearchAction>();
            record.RaisedLocation.RemoveElementsOfType<PickpocketAction>();
            npc.AddMarkerElement<PreventExpRewardMarker>();
            npc.RemoveElementsOfType<NpcHealthRegeneration>();
            npc.OnCompletelyInitialized(
                delegate
                {
                    if (npc.AliveStats != null
                        && npc.AliveStats.MaxHealth != null)
                    {
                        float multiplier = SoulProgressionRuntime
                            .GetQualityHealthMultiplier(record.QualityTier);
                        if (Math.Abs(multiplier - 1.0f) > 0.0001f)
                        {
                            record.QualityHealthTweak = StatTweak.Multi(
                                npc.AliveStats.MaxHealth,
                                multiplier,
                                null,
                                npc);
                            ((Model)record.QualityHealthTweak).MarkedNotSaved = true;
                        }
                    }
                    SoulforgedRuntime.RefreshOriginalMaximumHealth(summon, false);
                    ReanimationGlyphRuntime.Attach(summonId, npc);
                });
            return true;
        }

        private static void SavePersistentReanimation(
            string summonId,
            ReanimationRecord record)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || plugin.PersistentServants == null
                || !plugin.PersistentServants.Value
                || record == null)
            {
                return;
            }
            string sourceId = record.SourceCorpse == null
                ? record.SourceId
                : ((Model)record.SourceCorpse).ID;
            if (string.IsNullOrEmpty(sourceId))
            {
                return;
            }
            record.SourceId = sourceId;
            WriteRaisedPersistencePayload();
        }

        private static void BeforeGameplayMemorySerialize(
            GameplayMemory __instance)
        {
            try
            {
                if (_loadedRaisedPersistence == null)
                {
                    WriteRaisedPersistencePayload(__instance);
                }
            }
            catch (Exception exception)
            {
                LogRaisedPersistenceWarning(
                    "Could not refresh the raised-servant save snapshot; "
                    + "the previous valid snapshot was retained: ",
                    exception);
            }
        }

        private static void AfterGameplayMemoryDeserialize(
            GameplayMemory __instance)
        {
            try
            {
                _raisedPersistenceListenerWarningLogged = false;
                ContextualFacts facts = GetPersistenceFacts(__instance);
                string storedPayload = facts == null
                    ? string.Empty
                    : facts.Get(RaisedPersistencePayloadKey, string.Empty);
                if (string.IsNullOrWhiteSpace(storedPayload))
                {
                    _loadedRaisedPersistence = null;
                    LogRaisedPersistenceDiagnostic(
                        "load: snapshot=missing; deferredSources="
                        + HasDeferredSourceRestoration() + ".");
                    if (HasDeferredSourceRestoration())
                    {
                        EnsureRaisedPersistenceSceneListener();
                    }
                    return;
                }
                int loadedVersion;
                RaisedPersistenceSnapshot[] records;
                if (storedPayload.StartsWith(
                    RaisedPersistencePayloadPrefix,
                    StringComparison.Ordinal))
                {
                    records = DeserializeRaisedPersistencePayload(
                        storedPayload,
                        out loadedVersion);
                }
                else
                {
                    RaisedPersistencePayload legacyPayload =
                        JsonUtility.FromJson<RaisedPersistencePayload>(
                            storedPayload);
                    if (legacyPayload == null
                        || legacyPayload.Version
                            != RaisedPersistenceLegacyVersion
                        || legacyPayload.Records == null)
                    {
                        throw new InvalidOperationException(
                            "unsupported raised-servant snapshot version");
                    }
                    loadedVersion = legacyPayload.Version;
                    records = legacyPayload.Records;
                }
                if (loadedVersion != RaisedPersistenceLegacyVersion
                    && loadedVersion != RaisedPersistencePreviousVersion
                    && loadedVersion != RaisedPersistenceVersion)
                {
                    throw new InvalidOperationException(
                        "unsupported raised-servant snapshot version");
                }
                _loadedRaisedPersistence = records;
                EnsureRaisedPersistenceSceneListener();
                LogRaisedPersistenceDiagnostic(
                    "load: snapshot=loaded; version="
                    + loadedVersion
                    + "; format=" + (loadedVersion
                        == RaisedPersistenceLegacyVersion
                            ? "legacy-json"
                            : "binary-base64")
                    + "; payloadChars=" + storedPayload.Length
                    + "; records=" + records.Length
                    + "; active=" + records.Count(snapshot =>
                        snapshot != null
                        && snapshot.Phase == RaisedPersistenceActive)
                    + "; pending=" + records.Count(snapshot =>
                        snapshot != null
                        && snapshot.Phase == RaisedPersistencePending)
                    + "; recoveryScheduled=true.");
            }
            catch (Exception exception)
            {
                _loadedRaisedPersistence = new RaisedPersistenceSnapshot[0];
                EnsureRaisedPersistenceSceneListener();
                LogRaisedPersistenceWarning(
                    "Ignored an invalid raised-servant snapshot safely: ",
                    exception);
            }
        }

        private static void EnsureRaisedPersistenceSceneListener()
        {
            if (_raisedPersistenceSceneListener != null)
            {
                return;
            }
            try
            {
                _raisedPersistenceSceneListener = World.EventSystem.ListenTo(
                    "*",
                    SceneLifetimeEvents.Events.AfterSceneFullyInitialized,
                    AfterSceneFullyInitializedForRaisedPersistence);
                _raisedPersistenceListenerWarningLogged = false;
            }
            catch (Exception exception)
            {
                if (!_raisedPersistenceListenerWarningLogged)
                {
                    _raisedPersistenceListenerWarningLogged = true;
                    LogRaisedPersistenceWarning(
                        "Could not schedule raised-servant recovery safely: ",
                        exception);
                }
            }
        }

        private static void RemoveRaisedPersistenceSceneListener()
        {
            if (_raisedPersistenceSceneListener == null)
            {
                return;
            }
            try
            {
                World.EventSystem.RemoveListener(_raisedPersistenceSceneListener);
            }
            catch
            {
            }
            _raisedPersistenceSceneListener = null;
        }

        private static void AfterSceneFullyInitializedForRaisedPersistence(
            SceneLifetimeEventData data)
        {
            if (!data.IsMainScene)
            {
                return;
            }
            RemoveRaisedPersistenceSceneListener();
            RaisedPersistenceSnapshot[] loaded = _loadedRaisedPersistence;
            _loadedRaisedPersistence = null;
            RaisedPersistenceRecoveryDiagnostics recovery =
                new RaisedPersistenceRecoveryDiagnostics
                {
                    Loaded = loaded == null ? 0 : loaded.Length
                };
            try
            {
                MigrateLegacyRaisedServants();
                if (loaded != null)
                {
                    HashSet<string> handledSources =
                        new HashSet<string>(StringComparer.Ordinal);
                    List<Vector3> restoredPlacementReservations =
                        new List<Vector3>();
                    int restoredPlacementSlot = 0;
                    foreach (RaisedPersistenceSnapshot snapshot in loaded)
                    {
                        if (snapshot == null)
                        {
                            recovery.Rejected++;
                            LogRaisedPersistenceRejection(
                                string.Empty,
                                "snapshot entry is null");
                            continue;
                        }
                        if (string.IsNullOrEmpty(snapshot.SourceId))
                        {
                            recovery.Rejected++;
                            LogRaisedPersistenceRejection(
                                string.Empty,
                                "source identity is missing");
                            continue;
                        }
                        if (!handledSources.Add(snapshot.SourceId))
                        {
                            recovery.Rejected++;
                            LogRaisedPersistenceRejection(
                                snapshot.SourceId,
                                "duplicate source identity");
                            continue;
                        }
                        recovery.Attempted++;
                        bool validSnapshot = false;
                        try
                        {
                            bool sourceAlreadyServing = Reanimations.Values.Any(
                                record => record != null
                                    && string.Equals(
                                        record.SourceId,
                                        snapshot.SourceId,
                                        StringComparison.Ordinal));
                            if (sourceAlreadyServing)
                            {
                                recovery.AlreadyServing++;
                                continue;
                            }
                            validSnapshot =
                                IsValidRaisedPersistenceSnapshot(snapshot);
                            if (!validSnapshot)
                            {
                                recovery.Rejected++;
                                LogRaisedPersistenceRejection(
                                    snapshot.SourceId,
                                    "snapshot fields are invalid");
                                RestoreLoadedRaisedSource(
                                    snapshot,
                                    refundVigor: validSnapshot,
                                    trustedSnapshot: validSnapshot,
                                    diagnostics: recovery);
                                continue;
                            }
                            if (snapshot.Phase != RaisedPersistenceActive)
                            {
                                recovery.Rejected++;
                                LogRaisedPersistenceRejection(
                                    snapshot.SourceId,
                                    "snapshot is pending source restoration");
                                RestoreLoadedRaisedSource(
                                    snapshot,
                                    refundVigor: true,
                                    trustedSnapshot: true,
                                    diagnostics: recovery);
                                continue;
                            }
                            int placementSlot = restoredPlacementSlot++;
                            if (!TryRehydrateRaisedServant(
                                    snapshot,
                                    recovery,
                                    placementSlot,
                                    restoredPlacementReservations,
                                    out string rejectionReason))
                            {
                                recovery.Rejected++;
                                LogRaisedPersistenceRejection(
                                    snapshot.SourceId,
                                    rejectionReason);
                                RestoreLoadedRaisedSource(
                                    snapshot,
                                    refundVigor: true,
                                    trustedSnapshot: true,
                                    diagnostics: recovery);
                            }
                        }
                        catch (Exception exception)
                        {
                            recovery.Rejected++;
                            RestoreLoadedRaisedSource(
                                snapshot,
                                refundVigor: validSnapshot,
                                trustedSnapshot: validSnapshot,
                                diagnostics: recovery);
                            LogRaisedPersistenceWarning(
                                "A raised servant failed to rehydrate and its source "
                                + "was restored safely: ",
                                exception);
                        }
                    }
                }
                RestoreDeferredSourcesAfterSceneInitialized();
                WriteRaisedPersistencePayload();
                recovery.ScenePassComplete = true;
                LogRaisedPersistenceRecoverySummary(recovery, "scene-pass");
            }
            catch (Exception exception)
            {
                recovery.ScenePassComplete = true;
                LogRaisedPersistenceRecoverySummary(recovery, "stopped");
                LogRaisedPersistenceWarning(
                    "Raised-servant recovery stopped safely after scene load: ",
                    exception);
            }
        }

        private static void MigrateLegacyRaisedServants()
        {
            if (PendingLegacyRaisedRestores.Count == 0)
            {
                return;
            }
            NpcHeroSummon[] legacy = PendingLegacyRaisedRestores.ToArray();
            PendingLegacyRaisedRestores.Clear();
            foreach (NpcHeroSummon summon in legacy)
            {
                if (summon == null
                    || summon.HasBeenDiscarded
                    || summon.ParentModel == null)
                {
                    continue;
                }
                string summonId = ((Model)summon).ID;
                try
                {
                    if (!TryRestorePersistentReanimation(summon))
                    {
                        continue;
                    }
                    ReanimationRecord record = Reanimations[summonId];
                    Location raised = summon.ParentModel.ParentModel;
                    LocationTemplate canonical;
                    if (!TryResolveCanonicalPersistentSpawnTemplate(
                            record.SourceCorpse ?? raised,
                            raised == null ? null : raised.Template,
                            out canonical))
                    {
                        RestoreSourceCorpse(
                            summonId,
                            discardRaisedCopy: true,
                            showDiagnostic: false);
                        continue;
                    }
                    record.SpawnTemplateGuid = canonical.GUID;
                    ((Model)raised).MarkedNotSaved = true;
                    ClearLegacyRaisedPersistence(summonId);
                    SavePersistentReanimation(summonId, record);
                }
                catch (Exception exception)
                {
                    ReanimationRecord record;
                    if (Reanimations.TryGetValue(summonId, out record))
                    {
                        RestoreSourceCorpse(
                            summonId,
                            discardRaisedCopy: true,
                            showDiagnostic: false);
                    }
                    LogRaisedPersistenceWarning(
                        "A legacy raised servant could not be migrated safely: ",
                        exception);
                }
            }
        }

        private static bool TryRehydrateRaisedServant(
            RaisedPersistenceSnapshot snapshot,
            RaisedPersistenceRecoveryDiagnostics diagnostics,
            int placementSlot,
            ICollection<Vector3> restoredPlacementReservations,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            TemplatesProvider templates = World.Services == null
                ? null
                : World.Services.TryGet<TemplatesProvider>();
            if (plugin == null || !plugin.IsEnabled)
            {
                rejectionReason = "plugin is unavailable or disabled";
                return false;
            }
            if (plugin.PersistentServants == null
                || !plugin.PersistentServants.Value)
            {
                rejectionReason = "Persistent Servants is disabled";
                return false;
            }
            if (hero == null)
            {
                rejectionReason = "hero is unavailable after scene load";
                return false;
            }
            if (templates == null || !templates.AllLoaded)
            {
                rejectionReason = "location templates are not ready";
                return false;
            }
            if (string.IsNullOrEmpty(snapshot.SpawnTemplateGuid))
            {
                rejectionReason = "spawn template identity is missing";
                return false;
            }
            LocationTemplate spawnTemplate =
                templates.Get<LocationTemplate>(snapshot.SpawnTemplateGuid);
            if (!IsEligiblePersistentSpawnTemplate(spawnTemplate))
            {
                rejectionReason = "spawn template is not persistence-safe";
                return false;
            }
            RepetitiveNpcAttachment spawnAttachment =
                spawnTemplate.GetComponent<RepetitiveNpcAttachment>();
            NpcTemplate spawnNpcTemplate = spawnAttachment == null
                ? null
                : spawnAttachment.NpcTemplate;
            bool templateIsMiniboss = spawnNpcTemplate != null
                && spawnNpcTemplate.NpcType == NpcType.MiniBoss;
            if (templateIsMiniboss != snapshot.IsMiniboss)
            {
                rejectionReason = "spawn template miniboss identity changed";
                return false;
            }
            Location source = FindLocationById(snapshot.SourceId);
            if (source != null
                && !IsMatchingPersistentSource(source, spawnTemplate))
            {
                rejectionReason = "source corpse no longer matches its spawn template";
                return false;
            }
            if (snapshot.IsMiniboss
                && !IsMatchingPersistentMinibossSource(
                    snapshot,
                    source,
                    spawnTemplate,
                    spawnNpcTemplate))
            {
                rejectionReason = "miniboss source validation failed";
                return false;
            }
            if (snapshot.IsMiniboss && source.CurrentDomain != Domain.Gameplay)
            {
                rejectionReason = "miniboss source is outside the Gameplay domain";
                return false;
            }

            Location raised = null;
            string summonId = string.Empty;
            try
            {
                Pathfinding.GraphNode heroNode = AstarPath.active == null
                    ? null
                    : AstarPath.active.GetNearest(
                        hero.Coords,
                        Pathfinding.NNConstraint.Walkable).node;
                Vector3 position = hero.Coords
                    + ((hero.Rotation * Vector3.forward) * 2.0f);
                bool usedReservedPlacement = false;
                try
                {
                    if (SummonFormationCoordinator.TryReserveRestoredPlacement(
                            hero,
                            heroNode,
                            placementSlot,
                            restoredPlacementReservations,
                            out Vector3 reservedPosition))
                    {
                        position = reservedPosition;
                        usedReservedPlacement = true;
                    }
                }
                catch (Exception exception)
                {
                    LogRaisedPersistenceWarning(
                        "Could not reserve an ideal raised-servant arrival; "
                        + "using the safe recovery fallback: ",
                        exception);
                }
                LogRaisedPersistenceDiagnostic(
                    "arrival placement: source=" + snapshot.SourceId
                    + "; slot=" + placementSlot
                    + "; reserved=" + usedReservedPlacement
                    + "; distance="
                    + Vector3.Distance(hero.Coords, position).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + ".");
                raised = spawnTemplate.SpawnLocation(position, hero.Rotation);
                if (raised == null || raised.HasBeenDiscarded)
                {
                    rejectionReason = "spawn template did not create a location";
                    return false;
                }
                ((Model)raised).MarkedNotSaved = true;
                NpcElement raisedNpc = raised.Element<NpcElement>();
                EnsureRaisedServantPortrait(raisedNpc);
                NpcElement npc;
                _creatingRaisedServant = true;
                try
                {
                    npc = SummonUtils.InitializeSummon(
                        raised,
                        hero,
                        null,
                        0.0f,
                        0.0f,
                        null);
                }
                finally
                {
                    _creatingRaisedServant = false;
                }
                NpcHeroSummon summon = npc.Element<NpcHeroSummon>();
                summonId = ((Model)summon).ID;
                ReanimationRecord record = new ReanimationRecord
                {
                    SourceCorpse = source,
                    SourceId = snapshot.SourceId,
                    RaisedLocation = raised,
                    RaisedNpc = npc,
                    SourceInteractability = ResolvePersistedInteractability(
                        snapshot.SourceInteractability),
                    SourceDisplayName = snapshot.SourceDisplayName,
                    CorpseFingerprint = snapshot.CorpseFingerprint,
                    Quality01 = Mathf.Clamp01(snapshot.Quality01),
                    QualityTier = (Grailwright.Shared.CorpseQualityTier)Mathf.Clamp(
                        snapshot.QualityTier,
                        (int)Grailwright.Shared.CorpseQualityTier.Meager,
                        (int)Grailwright.Shared.CorpseQualityTier.Prime),
                    BindingManaCost = Math.Max(0.0f, snapshot.BindingManaCost),
                    InvestedSoulVigor = Math.Max(0, snapshot.InvestedSoulVigor),
                    NativeSoulVigor = Math.Max(0, snapshot.NativeSoulVigor),
                    Recovery = new ServantRecoveryLedger
                    {
                        ManaInitialized = snapshot.RecoveryManaInitialized,
                        OriginalMana = Math.Max(
                            0.0f,
                            snapshot.RecoveryOriginalMana),
                        RemainingMana = Mathf.Clamp(
                            snapshot.RecoveryRemainingMana,
                            0.0f,
                            Math.Max(0.0f, snapshot.RecoveryOriginalMana)),
                        OriginalSoulVigor = Math.Max(
                            0,
                            snapshot.RecoveryOriginalSoulVigor),
                        EmpowermentSoulVigorInvestment = Mathf.Clamp(
                            snapshot.EmpowermentSoulVigorInvestment,
                            0,
                            Math.Max(0, snapshot.InvestedSoulVigor)),
                        RecoveredSoulforgedRankMask =
                            snapshot.RecoveredSoulforgedRankMask
                            & ((1 << SoulforgedRuntime.MaximumRank) - 1)
                    },
                    OriginalCoords = source == null ? position : source.Coords,
                    OriginalRotation = source == null ? hero.Rotation : source.Rotation,
                    LastSafeCoords = position,
                    LastSafeRotation = hero.Rotation,
                    SpawnTemplateGuid = snapshot.SpawnTemplateGuid,
                    IsMiniboss = snapshot.IsMiniboss,
                    HighSoulFingerprint = snapshot.HighSoulFingerprint,
                    HighSoulExtractionValue = snapshot.HighSoulExtractionValue,
                    HighSoulServiceCycle = snapshot.HighSoulServiceCycle
                };
                EnsureRaisedRecoveryLedger(record);
                Reanimations[summonId] = record;
                if (source != null)
                {
                    source.SetInteractability(LocationInteractability.Hidden);
                }
                diagnostics.Scheduled++;
                diagnostics.PendingCallbacks++;
                try
                {
                    npc.OnCompletelyInitialized(delegate
                    {
                        CompleteRehydratedRaisedServant(
                            summon,
                            summonId,
                            record,
                            snapshot,
                            diagnostics);
                    });
                }
                catch
                {
                    diagnostics.Scheduled--;
                    diagnostics.PendingCallbacks--;
                    throw;
                }
                return true;
            }
            catch
            {
                if (!string.IsNullOrEmpty(summonId))
                {
                    Reanimations.Remove(summonId);
                }
                if (raised != null && !raised.HasBeenDiscarded)
                {
                    raised.Discard();
                }
                throw;
            }
        }

        private static bool IsValidRaisedPersistenceSnapshot(
            RaisedPersistenceSnapshot snapshot)
        {
            if (snapshot == null
                || (snapshot.Phase != RaisedPersistencePending
                    && snapshot.Phase != RaisedPersistenceActive))
            {
                return false;
            }
            if (string.IsNullOrEmpty(snapshot.SourceId)
                || !IsPersistedInteractability(snapshot.SourceInteractability)
                || snapshot.InvestedSoulVigor < 0
                || !IsFinite(snapshot.RecoveryOriginalMana)
                || snapshot.RecoveryOriginalMana < 0.0f
                || !IsFinite(snapshot.RecoveryRemainingMana)
                || snapshot.RecoveryRemainingMana < 0.0f
                || snapshot.RecoveryRemainingMana
                    > snapshot.RecoveryOriginalMana
                || snapshot.RecoveryOriginalSoulVigor < 0
                || snapshot.EmpowermentSoulVigorInvestment < 0
                || snapshot.EmpowermentSoulVigorInvestment
                    > snapshot.InvestedSoulVigor
                || (snapshot.RecoveredSoulforgedRankMask
                    & ~((1 << SoulforgedRuntime.MaximumRank) - 1)) != 0)
            {
                return false;
            }
            if (snapshot.Phase == RaisedPersistencePending)
            {
                return true;
            }
            return !string.IsNullOrEmpty(snapshot.SpawnTemplateGuid)
                && IsFinite(snapshot.Quality01)
                && snapshot.Quality01 >= 0.0f
                && snapshot.Quality01 <= 1.0f
                && snapshot.QualityTier
                    >= (int)Grailwright.Shared.CorpseQualityTier.Meager
                && snapshot.QualityTier
                    <= (int)Grailwright.Shared.CorpseQualityTier.Prime
                && IsFinite(snapshot.BindingManaCost)
                && snapshot.BindingManaCost >= 0.0f
                && snapshot.NativeSoulVigor >= 0
                && IsFinite(snapshot.HealthFraction)
                && snapshot.HealthFraction >= 0.0f
                && snapshot.HealthFraction <= 1.0f
                && IsFinite(snapshot.SoulforgedOriginalMaximumHealth)
                && snapshot.SoulforgedOriginalMaximumHealth >= 0.0f
                && IsFinite(snapshot.SoulforgedDamageDealt)
                && snapshot.SoulforgedDamageDealt >= 0.0f
                && snapshot.SoulforgedRank >= 0
                && snapshot.SoulforgedRank <= 17
                && IsFinite(snapshot.EmpowermentMultiplier)
                && snapshot.EmpowermentMultiplier >= 0.0f
                && snapshot.EmpowermentMultiplier <= 1.50f
                && (!snapshot.IsMiniboss
                    || (!string.IsNullOrEmpty(snapshot.HighSoulFingerprint)
                        && snapshot.HighSoulExtractionValue > 0
                        && snapshot.HighSoulExtractionValue <= 1000
                        && snapshot.HighSoulServiceCycle >= 0
                        && snapshot.HighSoulServiceCycle
                            < HighSoulPoolPortions));
        }

        private static bool IsEligiblePersistentSpawnTemplate(
            LocationTemplate template)
        {
            RepetitiveNpcAttachment attachment = template == null
                ? null
                : template.GetComponent<RepetitiveNpcAttachment>();
            NpcTemplate npcTemplate = attachment == null
                ? null
                : attachment.NpcTemplate;
            if (npcTemplate == null
                || (attachment.StoryOnDeath != null
                    && attachment.StoryOnDeath.IsSet))
            {
                return false;
            }
            CrimeReactionArchetype crimeReaction =
                npcTemplate.CrimeReactionArchetype;
            if (crimeReaction == CrimeReactionArchetype.Guard
                || crimeReaction == CrimeReactionArchetype.Defender
                || crimeReaction == CrimeReactionArchetype.Vigilante)
            {
                return false;
            }
            return npcTemplate.NpcType == NpcType.Critter
                || npcTemplate.NpcType == NpcType.Trash
                || npcTemplate.NpcType == NpcType.Normal
                || npcTemplate.NpcType == NpcType.Elite
                || (npcTemplate.NpcType == NpcType.MiniBoss
                    && ReanimatableMinibossTemplateGuids.Contains(
                        npcTemplate.GUID));
        }

        private static bool IsMatchingPersistentSource(
            Location source,
            LocationTemplate spawnTemplate)
        {
            if (source == null || !IsEligiblePersistentSpawnTemplate(spawnTemplate))
            {
                return false;
            }
            RepetitiveNpcAttachment attachment =
                spawnTemplate.GetComponent<RepetitiveNpcAttachment>();
            NpcTemplate sourceNpc = NpcTemplate.FromNpcOrDummy(source);
            return sourceNpc != null
                && attachment != null
                && attachment.NpcTemplate != null
                && string.Equals(
                    sourceNpc.GUID,
                    attachment.NpcTemplate.GUID,
                    StringComparison.Ordinal);
        }

        private static bool IsMatchingPersistentMinibossSource(
            RaisedPersistenceSnapshot snapshot,
            Location source,
            LocationTemplate spawnTemplate,
            NpcTemplate npcTemplate)
        {
            if (snapshot == null
                || source == null
                || npcTemplate == null
                || !(source.Initializer is RuntimeLocationInitializer)
                || !IsAuthoredSourceDiscarded(((Model)source).ID)
                || !CanReanimateMiniboss(
                    source,
                    npcTemplate,
                    spawnTemplate,
                    CanSafelySimplifyHighSoulCorpse(source, npcTemplate),
                    out LocationTemplate durableTemplate)
                || durableTemplate == null
                || !string.Equals(
                    durableTemplate.GUID,
                    spawnTemplate.GUID,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string expectedFingerprint = GetHighSoulCorpseFingerprint(
                source,
                npcTemplate);
            int expectedExtraction = CalculateHighSoulExtractionValue(
                npcTemplate,
                source.Template);
            if (!string.Equals(
                    expectedFingerprint,
                    snapshot.HighSoulFingerprint,
                    StringComparison.Ordinal)
                || snapshot.HighSoulExtractionValue != expectedExtraction
                || !SoulProgressionRuntime.TryGetExistingHighSoulVigorPool(
                    expectedFingerprint,
                    expectedExtraction,
                    out int remaining)
                || remaining <= 0)
            {
                return false;
            }
            return snapshot.HighSoulServiceCycle
                == SoulProgressionRuntime.GetHighSoulServiceCycle(
                    expectedFingerprint,
                    expectedExtraction);
        }

        private static bool IsPersistedInteractability(string value)
        {
            return string.Equals(value, "Active", StringComparison.Ordinal)
                || string.Equals(value, "Inactive", StringComparison.Ordinal)
                || string.Equals(value, "Hidden", StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void CompleteRehydratedRaisedServant(
            NpcHeroSummon summon,
            string summonId,
            ReanimationRecord record,
            RaisedPersistenceSnapshot snapshot,
            RaisedPersistenceRecoveryDiagnostics diagnostics)
        {
            try
            {
                NpcElement npc = record.RaisedNpc;
                Location raised = record.RaisedLocation;
                if (npc == null || npc.HasBeenDiscarded
                    || raised == null || raised.HasBeenDiscarded)
                {
                    RestoreSourceCorpse(
                        summonId,
                        discardRaisedCopy: true,
                        showDiagnostic: false);
                    diagnostics.Rejected++;
                    LogRaisedPersistenceRejection(
                        snapshot == null ? string.Empty : snapshot.SourceId,
                        "spawned servant was discarded before initialization completed");
                    return;
                }
                raised.RemoveElementsOfType<AliveLocationDeathReward>();
                raised.RemoveElementsOfType<SearchAction>();
                raised.RemoveElementsOfType<PickpocketAction>();
                npc.AddMarkerElement<PreventExpRewardMarker>();
                npc.RemoveElementsOfType<NpcHealthRegeneration>();
                if (npc.AliveStats != null && npc.AliveStats.MaxHealth != null)
                {
                    float multiplier = record.IsMiniboss
                        ? 0.50f
                        : SoulProgressionRuntime.GetQualityHealthMultiplier(
                            record.QualityTier);
                    if (Math.Abs(multiplier - 1.0f) > 0.0001f)
                    {
                        record.QualityHealthTweak = StatTweak.Multi(
                            npc.AliveStats.MaxHealth,
                            multiplier,
                            null,
                            npc);
                        ((Model)record.QualityHealthTweak).MarkedNotSaved = true;
                    }
                }
                npc.Health.SetToFull();
                float healthFraction = Mathf.Clamp01(snapshot.HealthFraction);
                if (healthFraction < 1.0f)
                {
                    npc.Health.DecreaseBy(
                        npc.Health.ModifiedValue * (1.0f - healthFraction));
                }
                SoulforgedRuntime.RestorePersistenceState(
                    summon,
                    snapshot.SoulforgedOriginalMaximumHealth,
                    snapshot.SoulforgedDamageDealt,
                    snapshot.SoulforgedRank,
                    record.IsMiniboss
                        ? 1.0f
                        : snapshot.EmpowermentMultiplier);
                record.ServiceInitialized = true;
                ReanimationGlyphRuntime.Attach(summonId, npc);
                SavePersistentReanimation(summonId, record);
                try
                {
                    SpawnNecromanticSummonVfx(npc);
                }
                catch (Exception exception)
                {
                    LogRaisedPersistenceWarning(
                        "Could not play a restored servant's arrival effect: ",
                        exception);
                }
                diagnostics.Restored++;
                LogRaisedPersistenceDiagnostic(
                    "rehydrated: source=" + snapshot.SourceId
                    + "; summon=" + summonId
                    + "; health="
                    + (Mathf.Clamp01(snapshot.HealthFraction) * 100.0f)
                        .ToString("0.##", CultureInfo.InvariantCulture)
                    + "%.");
            }
            catch (Exception exception)
            {
                diagnostics.Rejected++;
                RestoreSourceCorpse(
                    summonId,
                    discardRaisedCopy: true,
                    showDiagnostic: false);
                LogRaisedPersistenceWarning(
                    "A raised servant failed to finish rehydrating and its source "
                    + "was restored safely: ",
                    exception);
            }
            finally
            {
                diagnostics.PendingCallbacks = Math.Max(
                    0,
                    diagnostics.PendingCallbacks - 1);
                if (diagnostics.ScenePassComplete
                    && diagnostics.PendingCallbacks == 0)
                {
                    LogRaisedPersistenceRecoverySummary(
                        diagnostics,
                        "complete");
                }
            }
        }

        private static void RestoreLoadedRaisedSource(
            RaisedPersistenceSnapshot snapshot,
            bool refundVigor,
            bool trustedSnapshot,
            RaisedPersistenceRecoveryDiagnostics diagnostics = null)
        {
            if (snapshot == null)
            {
                return;
            }
            LocationInteractability interactability = trustedSnapshot
                && IsPersistedInteractability(snapshot.SourceInteractability)
                    ? ResolvePersistedInteractability(snapshot.SourceInteractability)
                    : LocationInteractability.Active;
            bool restored = false;
            try
            {
                Location source = FindLocationById(snapshot.SourceId);
                if (source != null)
                {
                    if (snapshot.IsMiniboss
                        && !TryReturnMinibossSourceToCurrentScene(
                            source,
                            out string returnFailure))
                    {
                        throw new InvalidOperationException(returnFailure);
                    }
                    source.SetInteractability(interactability);
                    if (!snapshot.IsMiniboss)
                    {
                        TriggerRuntimeCorpseVisualEvent(source, "OnDeath");
                    }
                    restored = true;
                    if (diagnostics != null)
                    {
                        diagnostics.SourcesRestored++;
                    }
                }
            }
            catch (Exception exception)
            {
                LogRaisedPersistenceWarning(
                    "Could not restore a loaded raised-servant source immediately: ",
                    exception);
            }
            if (!restored && !string.IsNullOrEmpty(snapshot.SourceId))
            {
                try
                {
                    WriteDeferredSourceString(
                        snapshot.SourceId,
                        "interactability",
                        GetPersistedInteractability(interactability));
                    WriteDeferredSourceInt(
                        snapshot.SourceId,
                        "scene_safe_visual",
                        snapshot.IsMiniboss ? 0 : 1);
                    WriteDeferredSourceInt(snapshot.SourceId, "restore", 1);
                    if (diagnostics != null)
                    {
                        diagnostics.Deferred++;
                    }
                }
                catch (Exception exception)
                {
                    LogRaisedPersistenceWarning(
                        "Could not defer raised-servant source restoration: ",
                        exception);
                }
            }
            if (refundVigor && snapshot.InvestedSoulVigor > 0)
            {
                try
                {
                    SoulProgressionRuntime.RestoreSoulVigor(
                        snapshot.InvestedSoulVigor);
                }
                catch (Exception exception)
                {
                    LogRaisedPersistenceWarning(
                        "Could not refund a failed raised-servant recovery: ",
                        exception);
                }
            }
        }

        private static Location FindLocationById(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return null;
            }
            return World.All<Location>().FirstOrDefault(location =>
                location != null
                && !location.HasBeenDiscarded
                && string.Equals(
                    ((Model)location).ID,
                    locationId,
                    StringComparison.Ordinal));
        }

        private static void WriteRaisedPersistencePayload(
            GameplayMemory memory = null)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            ContextualFacts facts = GetPersistenceFacts(memory);
            if (facts == null)
            {
                return;
            }
            List<RaisedPersistenceSnapshot> records =
                new List<RaisedPersistenceSnapshot>();
            bool preserveHost = plugin != null
                && plugin.PersistentServants != null
                && plugin.PersistentServants.Value;
            foreach (ReanimationRecord record in Reanimations.Values)
            {
                RaisedPersistenceSnapshot snapshot =
                    CaptureRaisedPersistenceSnapshot(record, preserveHost);
                if (!IsValidRaisedPersistenceSnapshot(snapshot))
                {
                    throw new InvalidOperationException(
                        "a hidden raised-servant source could not be captured safely");
                }
                records.Add(snapshot);
            }
            RaisedPersistenceSnapshot[] snapshots = records.ToArray();
            string serializedPayload =
                SerializeRaisedPersistencePayload(snapshots);
            RaisedPersistenceSnapshot[] roundTrip =
                DeserializeRaisedPersistencePayload(
                    serializedPayload,
                    out int roundTripVersion);
            if (roundTripVersion != RaisedPersistenceVersion
                || roundTrip.Length != snapshots.Length
                || roundTrip.Any(snapshot =>
                    !IsValidRaisedPersistenceSnapshot(snapshot))
                || roundTrip.Where((snapshot, index) =>
                    !AreEquivalentRaisedPersistenceSnapshots(
                        snapshots[index],
                        snapshot)).Any())
            {
                throw new InvalidOperationException(
                    "the raised-servant snapshot failed payload round-trip validation");
            }
            facts.Set(RaisedPersistencePayloadKey, serializedPayload);
            string storedPayload = facts.Get(
                RaisedPersistencePayloadKey,
                string.Empty);
            LogRaisedPersistenceDiagnostic(
                "snapshot write: version=" + RaisedPersistenceVersion
                + "; format=binary-base64"
                + "; preserveHost=" + preserveHost
                + "; memory="
                + (memory == null ? "service" : "serialized-instance")
                + "; payloadChars=" + serializedPayload.Length
                + "; storedMatches="
                + string.Equals(
                    serializedPayload,
                    storedPayload,
                    StringComparison.Ordinal)
                + "; roundTripRecords=" + roundTrip.Length
                + "; records=" + snapshots.Length
                + "; active=" + snapshots.Count(snapshot =>
                    snapshot != null
                    && snapshot.Phase == RaisedPersistenceActive)
                + "; pending=" + snapshots.Count(snapshot =>
                    snapshot != null
                    && snapshot.Phase == RaisedPersistencePending)
                + ".");
        }

        private static string SerializeRaisedPersistencePayload(
            RaisedPersistenceSnapshot[] snapshots)
        {
            if (snapshots == null
                || snapshots.Length > RaisedPersistenceMaximumRecords)
            {
                throw new InvalidOperationException(
                    "raised-servant snapshot record count is invalid");
            }
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(
                stream,
                Encoding.UTF8))
            {
                writer.Write(RaisedPersistenceMagic);
                writer.Write(RaisedPersistenceVersion);
                writer.Write(snapshots.Length);
                foreach (RaisedPersistenceSnapshot snapshot in snapshots)
                {
                    if (!IsValidRaisedPersistenceSnapshot(snapshot))
                    {
                        throw new InvalidOperationException(
                            "raised-servant snapshot record is invalid");
                    }
                    writer.Write(snapshot.Phase);
                    WriteRaisedPersistenceString(writer, snapshot.SourceId);
                    WriteRaisedPersistenceString(
                        writer,
                        snapshot.SourceInteractability);
                    WriteRaisedPersistenceString(
                        writer,
                        snapshot.SpawnTemplateGuid);
                    WriteRaisedPersistenceString(
                        writer,
                        snapshot.SourceDisplayName);
                    WriteRaisedPersistenceString(
                        writer,
                        snapshot.CorpseFingerprint);
                    writer.Write(snapshot.Quality01);
                    writer.Write(snapshot.QualityTier);
                    writer.Write(snapshot.BindingManaCost);
                    writer.Write(snapshot.InvestedSoulVigor);
                    writer.Write(snapshot.NativeSoulVigor);
                    writer.Write(snapshot.HealthFraction);
                    writer.Write(snapshot.SoulforgedOriginalMaximumHealth);
                    writer.Write(snapshot.SoulforgedDamageDealt);
                    writer.Write(snapshot.SoulforgedRank);
                    writer.Write(snapshot.EmpowermentMultiplier);
                    writer.Write(snapshot.RecoveryManaInitialized);
                    writer.Write(snapshot.RecoveryOriginalMana);
                    writer.Write(snapshot.RecoveryRemainingMana);
                    writer.Write(snapshot.RecoveryOriginalSoulVigor);
                    writer.Write(snapshot.EmpowermentSoulVigorInvestment);
                    writer.Write(snapshot.RecoveredSoulforgedRankMask);
                    writer.Write(snapshot.IsMiniboss);
                    WriteRaisedPersistenceString(
                        writer,
                        snapshot.HighSoulFingerprint);
                    writer.Write(snapshot.HighSoulExtractionValue);
                    writer.Write(snapshot.HighSoulServiceCycle);
                }
                writer.Flush();
                string payload = RaisedPersistencePayloadPrefix
                    + Convert.ToBase64String(stream.ToArray());
                if (payload.Length > RaisedPersistenceMaximumPayloadCharacters)
                {
                    throw new InvalidOperationException(
                        "raised-servant snapshot payload is too large");
                }
                return payload;
            }
        }

        private static RaisedPersistenceSnapshot[]
            DeserializeRaisedPersistencePayload(
                string payload,
                out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(payload)
                || payload.Length > RaisedPersistenceMaximumPayloadCharacters
                || !payload.StartsWith(
                    RaisedPersistencePayloadPrefix,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "raised-servant snapshot payload header is invalid");
            }
            byte[] bytes = Convert.FromBase64String(
                payload.Substring(RaisedPersistencePayloadPrefix.Length));
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(
                stream,
                Encoding.UTF8))
            {
                if (reader.ReadInt32() != RaisedPersistenceMagic)
                {
                    throw new InvalidDataException(
                        "raised-servant snapshot payload magic is invalid");
                }
                version = reader.ReadInt32();
                if (version != RaisedPersistencePreviousVersion
                    && version != RaisedPersistenceVersion)
                {
                    throw new InvalidDataException(
                        "raised-servant snapshot payload version is unsupported");
                }
                int recordCount = reader.ReadInt32();
                if (recordCount < 0
                    || recordCount > RaisedPersistenceMaximumRecords)
                {
                    throw new InvalidDataException(
                        "raised-servant snapshot record count is invalid");
                }
                RaisedPersistenceSnapshot[] snapshots =
                    new RaisedPersistenceSnapshot[recordCount];
                for (int index = 0; index < recordCount; index++)
                {
                    RaisedPersistenceSnapshot snapshot =
                        new RaisedPersistenceSnapshot
                    {
                        Phase = reader.ReadInt32(),
                        SourceId = ReadRaisedPersistenceString(reader),
                        SourceInteractability =
                            ReadRaisedPersistenceString(reader),
                        SpawnTemplateGuid =
                            ReadRaisedPersistenceString(reader),
                        SourceDisplayName =
                            ReadRaisedPersistenceString(reader),
                        CorpseFingerprint =
                            ReadRaisedPersistenceString(reader),
                        Quality01 = reader.ReadSingle(),
                        QualityTier = reader.ReadInt32(),
                        BindingManaCost = reader.ReadSingle(),
                        InvestedSoulVigor = reader.ReadInt32(),
                        NativeSoulVigor = reader.ReadInt32(),
                        HealthFraction = reader.ReadSingle(),
                        SoulforgedOriginalMaximumHealth = reader.ReadSingle(),
                        SoulforgedDamageDealt = reader.ReadSingle(),
                        SoulforgedRank = reader.ReadInt32(),
                        EmpowermentMultiplier = reader.ReadSingle()
                    };
                    if (version >= RaisedPersistenceVersion)
                    {
                        snapshot.RecoveryManaInitialized =
                            reader.ReadBoolean();
                        snapshot.RecoveryOriginalMana = reader.ReadSingle();
                        snapshot.RecoveryRemainingMana = reader.ReadSingle();
                        snapshot.RecoveryOriginalSoulVigor = reader.ReadInt32();
                        snapshot.EmpowermentSoulVigorInvestment =
                            reader.ReadInt32();
                        snapshot.RecoveredSoulforgedRankMask =
                            reader.ReadInt32();
                    }
                    else
                    {
                        snapshot.RecoveryOriginalSoulVigor = Math.Max(
                            0,
                            snapshot.NativeSoulVigor
                                + snapshot.InvestedSoulVigor);
                    }
                    snapshot.IsMiniboss = reader.ReadBoolean();
                    snapshot.HighSoulFingerprint =
                        ReadRaisedPersistenceString(reader);
                    snapshot.HighSoulExtractionValue = reader.ReadInt32();
                    snapshot.HighSoulServiceCycle = reader.ReadInt32();
                    snapshots[index] = snapshot;
                }
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException(
                        "raised-servant snapshot payload has trailing data");
                }
                return snapshots;
            }
        }

        private static void WriteRaisedPersistenceString(
            BinaryWriter writer,
            string value)
        {
            writer.Write(value != null);
            if (value != null)
            {
                if (value.Length > RaisedPersistenceMaximumStringCharacters)
                {
                    throw new InvalidDataException(
                        "raised-servant snapshot string is too long");
                }
                writer.Write(value);
            }
        }

        private static string ReadRaisedPersistenceString(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }
            string value = reader.ReadString();
            if (value.Length > RaisedPersistenceMaximumStringCharacters)
            {
                throw new InvalidDataException(
                    "raised-servant snapshot string is too long");
            }
            return value;
        }

        private static bool AreEquivalentRaisedPersistenceSnapshots(
            RaisedPersistenceSnapshot expected,
            RaisedPersistenceSnapshot actual)
        {
            return expected != null
                && actual != null
                && expected.Phase == actual.Phase
                && string.Equals(
                    expected.SourceId,
                    actual.SourceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected.SourceInteractability,
                    actual.SourceInteractability,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected.SpawnTemplateGuid,
                    actual.SpawnTemplateGuid,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected.SourceDisplayName,
                    actual.SourceDisplayName,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected.CorpseFingerprint,
                    actual.CorpseFingerprint,
                    StringComparison.Ordinal)
                && expected.Quality01 == actual.Quality01
                && expected.QualityTier == actual.QualityTier
                && expected.BindingManaCost == actual.BindingManaCost
                && expected.InvestedSoulVigor == actual.InvestedSoulVigor
                && expected.NativeSoulVigor == actual.NativeSoulVigor
                && expected.HealthFraction == actual.HealthFraction
                && expected.SoulforgedOriginalMaximumHealth
                    == actual.SoulforgedOriginalMaximumHealth
                && expected.SoulforgedDamageDealt
                    == actual.SoulforgedDamageDealt
                && expected.SoulforgedRank == actual.SoulforgedRank
                && expected.EmpowermentMultiplier
                    == actual.EmpowermentMultiplier
                && expected.RecoveryManaInitialized
                    == actual.RecoveryManaInitialized
                && expected.RecoveryOriginalMana
                    == actual.RecoveryOriginalMana
                && expected.RecoveryRemainingMana
                    == actual.RecoveryRemainingMana
                && expected.RecoveryOriginalSoulVigor
                    == actual.RecoveryOriginalSoulVigor
                && expected.EmpowermentSoulVigorInvestment
                    == actual.EmpowermentSoulVigorInvestment
                && expected.RecoveredSoulforgedRankMask
                    == actual.RecoveredSoulforgedRankMask
                && expected.IsMiniboss == actual.IsMiniboss
                && string.Equals(
                    expected.HighSoulFingerprint,
                    actual.HighSoulFingerprint,
                    StringComparison.Ordinal)
                && expected.HighSoulExtractionValue
                    == actual.HighSoulExtractionValue
                && expected.HighSoulServiceCycle
                    == actual.HighSoulServiceCycle;
        }

        private static RaisedPersistenceSnapshot CaptureRaisedPersistenceSnapshot(
            ReanimationRecord record,
            bool preserveHost)
        {
            if (record == null || string.IsNullOrEmpty(record.SourceId))
            {
                return null;
            }
            EnsureRaisedRecoveryLedger(record);
            RaisedPersistenceSnapshot snapshot = new RaisedPersistenceSnapshot
            {
                Phase = RaisedPersistencePending,
                SourceId = record.SourceId,
                SourceInteractability = GetPersistedInteractability(
                    record.SourceInteractability),
                InvestedSoulVigor = Math.Max(0, record.InvestedSoulVigor),
                RecoveryManaInitialized = record.Recovery != null
                    && record.Recovery.ManaInitialized,
                RecoveryOriginalMana = record.Recovery == null
                    ? 0.0f
                    : Math.Max(0.0f, record.Recovery.OriginalMana),
                RecoveryRemainingMana = record.Recovery == null
                    ? 0.0f
                    : Mathf.Clamp(
                        record.Recovery.RemainingMana,
                        0.0f,
                        Math.Max(0.0f, record.Recovery.OriginalMana)),
                RecoveryOriginalSoulVigor = record.Recovery == null
                    ? 0
                    : Math.Max(0, record.Recovery.OriginalSoulVigor),
                EmpowermentSoulVigorInvestment = record.Recovery == null
                    ? 0
                    : Mathf.Clamp(
                        record.Recovery.EmpowermentSoulVigorInvestment,
                        0,
                        Math.Max(0, record.InvestedSoulVigor)),
                RecoveredSoulforgedRankMask = record.Recovery == null
                    ? 0
                    : record.Recovery.RecoveredSoulforgedRankMask
                        & ((1 << SoulforgedRuntime.MaximumRank) - 1),
                IsMiniboss = record.IsMiniboss,
                HighSoulFingerprint = record.HighSoulFingerprint,
                HighSoulExtractionValue = record.HighSoulExtractionValue,
                HighSoulServiceCycle = record.HighSoulServiceCycle
            };
            if (!preserveHost
                || !record.ServiceInitialized
                || string.IsNullOrEmpty(record.SpawnTemplateGuid)
                || record.RaisedNpc == null
                || record.RaisedNpc.HasBeenDiscarded)
            {
                return snapshot;
            }
            NpcHeroSummon summon = record.RaisedNpc.TryGetElement<NpcHeroSummon>();
            if (summon == null || summon.HasBeenDiscarded)
            {
                return snapshot;
            }
            SoulforgedRuntime.GetPersistenceState(
                summon,
                out float originalMaximumHealth,
                out float damageDealt,
                out int earnedRank);
            snapshot.Phase = RaisedPersistenceActive;
            snapshot.SpawnTemplateGuid = record.SpawnTemplateGuid;
            snapshot.SourceDisplayName = record.SourceDisplayName;
            snapshot.CorpseFingerprint = record.CorpseFingerprint;
            snapshot.Quality01 = record.Quality01;
            snapshot.QualityTier = (int)record.QualityTier;
            snapshot.BindingManaCost = record.BindingManaCost;
            snapshot.NativeSoulVigor = record.NativeSoulVigor;
            snapshot.HealthFraction = record.RaisedNpc.Health == null
                ? 1.0f
                : Mathf.Clamp01(record.RaisedNpc.Health.Percentage);
            snapshot.SoulforgedOriginalMaximumHealth = originalMaximumHealth;
            snapshot.SoulforgedDamageDealt = damageDealt;
            snapshot.SoulforgedRank = earnedRank;
            snapshot.EmpowermentMultiplier = SummonRuntime
                .GetEmpowermentCombatMultiplier(((Model)summon).ID);
            if (!IsValidRaisedPersistenceSnapshot(snapshot))
            {
                snapshot = new RaisedPersistenceSnapshot
                {
                    Phase = RaisedPersistencePending,
                    SourceId = record.SourceId,
                    SourceInteractability = GetPersistedInteractability(
                        record.SourceInteractability),
                    InvestedSoulVigor = Math.Max(0, record.InvestedSoulVigor),
                    RecoveryManaInitialized = record.Recovery != null
                        && record.Recovery.ManaInitialized,
                    RecoveryOriginalMana = record.Recovery == null
                        ? 0.0f
                        : Math.Max(0.0f, record.Recovery.OriginalMana),
                    RecoveryRemainingMana = record.Recovery == null
                        ? 0.0f
                        : Mathf.Clamp(
                            record.Recovery.RemainingMana,
                            0.0f,
                            Math.Max(0.0f, record.Recovery.OriginalMana)),
                    RecoveryOriginalSoulVigor = record.Recovery == null
                        ? 0
                        : Math.Max(0, record.Recovery.OriginalSoulVigor),
                    EmpowermentSoulVigorInvestment = record.Recovery == null
                        ? 0
                        : Mathf.Clamp(
                            record.Recovery.EmpowermentSoulVigorInvestment,
                            0,
                            Math.Max(0, record.InvestedSoulVigor)),
                    RecoveredSoulforgedRankMask = record.Recovery == null
                        ? 0
                        : record.Recovery.RecoveredSoulforgedRankMask
                            & ((1 << SoulforgedRuntime.MaximumRank) - 1),
                    IsMiniboss = record.IsMiniboss,
                    HighSoulFingerprint = record.HighSoulFingerprint,
                    HighSoulExtractionValue = record.HighSoulExtractionValue,
                    HighSoulServiceCycle = record.HighSoulServiceCycle
                };
            }
            return snapshot;
        }

        private static void ClearLegacyRaisedPersistence(string summonId)
        {
            WritePersistedInt(summonId, "raised_active", 0);
            WritePersistedString(summonId, "source_id", string.Empty);
        }

        private static void LogRaisedPersistenceWarning(
            string prefix,
            Exception exception)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogWarning(
                    prefix + (exception == null
                        ? "unknown error"
                        : exception.GetBaseException().Message));
            }
        }

        private static void LogRaisedPersistenceRejection(
            string sourceId,
            string reason)
        {
            LogRaisedPersistenceDiagnostic(
                "rejected: source="
                + (string.IsNullOrEmpty(sourceId) ? "<missing>" : sourceId)
                + "; reason=" + reason + ".");
        }

        private static void LogRaisedPersistenceRecoverySummary(
            RaisedPersistenceRecoveryDiagnostics diagnostics,
            string phase)
        {
            if (diagnostics == null)
            {
                return;
            }
            LogRaisedPersistenceDiagnostic(
                "recovery " + phase
                + ": loaded=" + diagnostics.Loaded
                + "; attempted=" + diagnostics.Attempted
                + "; scheduled=" + diagnostics.Scheduled
                + "; restored=" + diagnostics.Restored
                + "; rejected=" + diagnostics.Rejected
                + "; alreadyServing=" + diagnostics.AlreadyServing
                + "; sourcesRestored=" + diagnostics.SourcesRestored
                + "; deferred=" + diagnostics.Deferred
                + "; pendingCallbacks=" + diagnostics.PendingCallbacks
                + ".");
        }

        private static void LogRaisedPersistenceDiagnostic(string message)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic("Raised persistence " + message);
            }
        }

        private static ContextualFacts GetPersistenceFacts(
            GameplayMemory memory = null)
        {
            if (memory == null)
            {
                memory = World.Services == null
                    ? null
                    : World.Services.TryGet<GameplayMemory>();
            }
            return memory == null ? null : memory.Context("SoulAndService");
        }

        private static string PersistedKey(string summonId, string value)
        {
            return "persistent_servant." + summonId + "." + value;
        }

        private static string DeferredSourceKey(string sourceId, string value)
        {
            return "persistent_source." + sourceId + "." + value;
        }

        private static int ReadPersistedInt(string summonId, string value)
        {
            ContextualFacts facts = GetPersistenceFacts();
            return facts == null
                ? 0
                : facts.Get(PersistedKey(summonId, value), 0);
        }

        private static float ReadPersistedFloat(string summonId, string value)
        {
            ContextualFacts facts = GetPersistenceFacts();
            return facts == null
                ? 0.0f
                : facts.Get(PersistedKey(summonId, value), 0.0f);
        }

        private static string ReadPersistedString(string summonId, string value)
        {
            ContextualFacts facts = GetPersistenceFacts();
            return facts == null
                ? string.Empty
                : facts.Get(PersistedKey(summonId, value), string.Empty);
        }

        private static void WritePersistedInt(
            string summonId,
            string value,
            int amount)
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts != null)
            {
                facts.Set(PersistedKey(summonId, value), amount);
            }
        }

        private static void WritePersistedFloat(
            string summonId,
            string value,
            float amount)
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts != null)
            {
                facts.Set(PersistedKey(summonId, value), amount);
            }
        }

        private static void WritePersistedString(
            string summonId,
            string value,
            string text)
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts != null)
            {
                facts.Set(PersistedKey(summonId, value), text ?? string.Empty);
            }
        }

        private static void EnsureRaisedRecoveryLedger(
            ReanimationRecord record)
        {
            if (record == null)
            {
                return;
            }
            if (record.Recovery == null)
            {
                record.Recovery = new ServantRecoveryLedger();
            }
            int baseVigor = Math.Max(
                0,
                record.NativeSoulVigor
                    + record.InvestedSoulVigor
                    - record.Recovery.EmpowermentSoulVigorInvestment);
            if (record.Recovery.OriginalSoulVigor <= 0 && baseVigor > 0)
            {
                record.Recovery.OriginalSoulVigor = baseVigor;
            }
        }

        private static void EnsureOrdinaryRecoveryLedger(
            OrdinarySummonInvestment investment)
        {
            if (investment == null)
            {
                return;
            }
            if (investment.Recovery == null)
            {
                investment.Recovery = new ServantRecoveryLedger();
            }
            int baseVigor = Math.Max(
                0,
                investment.InvestedSoulVigor
                    - investment.Recovery.EmpowermentSoulVigorInvestment);
            if (investment.Recovery.OriginalSoulVigor <= 0 && baseVigor > 0)
            {
                investment.Recovery.OriginalSoulVigor = baseVigor;
            }
        }

        private static void SaveOrdinarySummonInvestment(
            string summonId,
            OrdinarySummonInvestment investment)
        {
            if (string.IsNullOrEmpty(summonId) || investment == null)
            {
                return;
            }
            EnsureOrdinaryRecoveryLedger(investment);
            WritePersistedInt(summonId, "ordinary_active", 1);
            WritePersistedInt(
                summonId,
                "ordinary_investment",
                Math.Max(0, investment.InvestedSoulVigor));
            WritePersistedInt(summonId, "ordinary_recovery_version", 1);
            WritePersistedInt(
                summonId,
                "ordinary_recovery_mana_initialized",
                investment.Recovery.ManaInitialized ? 1 : 0);
            WritePersistedFloat(
                summonId,
                "ordinary_recovery_original_mana",
                Math.Max(0.0f, investment.Recovery.OriginalMana));
            WritePersistedFloat(
                summonId,
                "ordinary_recovery_remaining_mana",
                Mathf.Clamp(
                    investment.Recovery.RemainingMana,
                    0.0f,
                    Math.Max(0.0f, investment.Recovery.OriginalMana)));
            WritePersistedInt(
                summonId,
                "ordinary_recovery_original_vigor",
                Math.Max(0, investment.Recovery.OriginalSoulVigor));
            WritePersistedInt(
                summonId,
                "ordinary_empowerment_investment",
                Mathf.Clamp(
                    investment.Recovery.EmpowermentSoulVigorInvestment,
                    0,
                    Math.Max(0, investment.InvestedSoulVigor)));
            WritePersistedInt(
                summonId,
                "ordinary_recovered_rank_mask",
                investment.Recovery.RecoveredSoulforgedRankMask
                    & ((1 << SoulforgedRuntime.MaximumRank) - 1));
        }

        private static int ReadDeferredSourceInt(string sourceId, string value)
        {
            ContextualFacts facts = GetPersistenceFacts();
            return facts == null
                ? 0
                : facts.Get(DeferredSourceKey(sourceId, value), 0);
        }

        private static string ReadDeferredSourceString(string sourceId, string value)
        {
            ContextualFacts facts = GetPersistenceFacts();
            return facts == null
                ? string.Empty
                : facts.Get(DeferredSourceKey(sourceId, value), string.Empty);
        }

        private static bool HasDeferredSourceRestoration()
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts == null)
            {
                return false;
            }
            foreach (KeyValuePair<string, object> pair in facts.GetAll())
            {
                if (pair.Key.StartsWith("persistent_source.", StringComparison.Ordinal)
                    && pair.Key.EndsWith(".restore", StringComparison.Ordinal)
                    && facts.Get(pair.Key, 0) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryRestoreDeferredSource(
            Location source,
            out string failure)
        {
            failure = string.Empty;
            if (source == null || source.HasBeenDiscarded)
            {
                failure = "the saved source corpse was unavailable";
                return false;
            }
            string sourceId = ((Model)source).ID;
            if (string.IsNullOrEmpty(sourceId)
                || ReadDeferredSourceInt(sourceId, "restore") == 0)
            {
                return true;
            }

            bool sceneSafeVisual = ReadDeferredSourceInt(
                sourceId,
                "scene_safe_visual") != 0;
            string persistedInteractability = ReadDeferredSourceString(
                sourceId,
                "interactability");
            try
            {
                if (!sceneSafeVisual
                    && !TryReturnMinibossSourceToCurrentScene(
                        source,
                        out failure))
                {
                    return false;
                }
                source.SetInteractability(IsPersistedInteractability(
                        persistedInteractability)
                    ? ResolvePersistedInteractability(persistedInteractability)
                    : LocationInteractability.Active);
                if (sceneSafeVisual)
                {
                    TriggerRuntimeCorpseVisualEvent(source, "OnDeath");
                }
                WriteDeferredSourceInt(sourceId, "restore", 0);
                WriteDeferredSourceInt(sourceId, "scene_safe_visual", 0);
                WriteDeferredSourceString(sourceId, "interactability", string.Empty);
                SoulAndServicePlugin.Instance?.LogDiagnostic(
                    "Restored deferred Soul Rend source corpse " + sourceId + ".");
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetBaseException().Message;
                return false;
            }
        }

        private static void RestoreDeferredSourcesAfterSceneInitialized()
        {
            if (!HasDeferredSourceRestoration())
            {
                return;
            }
            Location[] sources = World.All<Location>()
                .Where(source => source != null
                    && !source.HasBeenDiscarded
                    && ReadDeferredSourceInt(((Model)source).ID, "restore") != 0)
                .ToArray();
            foreach (Location source in sources)
            {
                if (!TryRestoreDeferredSource(source, out string failure))
                {
                    SoulAndServicePlugin.Instance?.LogWarning(
                        "Deferred Soul Rend source recovery remains pending after scene initialization: "
                        + failure + ".");
                }
            }
            if (HasDeferredSourceRestoration())
            {
                EnsureRaisedPersistenceSceneListener();
            }
        }

        private static void WriteDeferredSourceInt(
            string sourceId,
            string value,
            int amount)
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts != null)
            {
                facts.Set(DeferredSourceKey(sourceId, value), amount);
            }
        }

        private static void WriteDeferredSourceString(
            string sourceId,
            string value,
            string text)
        {
            ContextualFacts facts = GetPersistenceFacts();
            if (facts != null)
            {
                facts.Set(
                    DeferredSourceKey(sourceId, value),
                    text ?? string.Empty);
            }
        }

        private static void ScheduleDeferredSourceRestoration(
            ReanimationRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.SourceId))
            {
                return;
            }
            WriteDeferredSourceString(
                record.SourceId,
                "interactability",
                GetPersistedInteractability(record.SourceInteractability));
            WriteDeferredSourceInt(
                record.SourceId,
                "scene_safe_visual",
                record.IsMiniboss ? 0 : 1);
            WriteDeferredSourceInt(record.SourceId, "restore", 1);
            EnsureRaisedPersistenceSceneListener();
        }

        private static void ClearPersistedServant(string summonId)
        {
            WritePersistedInt(summonId, "ordinary_active", 0);
            WritePersistedInt(summonId, "ordinary_investment", 0);
            WritePersistedInt(summonId, "ordinary_recovery_version", 0);
            WritePersistedInt(
                summonId,
                "ordinary_recovery_mana_initialized",
                0);
            WritePersistedFloat(
                summonId,
                "ordinary_recovery_original_mana",
                0.0f);
            WritePersistedFloat(
                summonId,
                "ordinary_recovery_remaining_mana",
                0.0f);
            WritePersistedInt(
                summonId,
                "ordinary_recovery_original_vigor",
                0);
            WritePersistedInt(
                summonId,
                "ordinary_empowerment_investment",
                0);
            WritePersistedInt(
                summonId,
                "ordinary_recovered_rank_mask",
                0);
            WritePersistedInt(summonId, "raised_active", 0);
            WritePersistedString(summonId, "source_id", string.Empty);
            WriteRaisedPersistencePayload();
        }

        private static string GetPersistedInteractability(
            LocationInteractability interactability)
        {
            return ReferenceEquals(interactability, LocationInteractability.Active)
                ? "Active"
                : ReferenceEquals(interactability, LocationInteractability.Inactive)
                    ? "Inactive"
                    : "Hidden";
        }

        private static LocationInteractability ResolvePersistedInteractability(
            string value)
        {
            return string.Equals(value, "Active", StringComparison.Ordinal)
                ? LocationInteractability.Active
                : string.Equals(value, "Inactive", StringComparison.Ordinal)
                    ? LocationInteractability.Inactive
                    : LocationInteractability.Hidden;
        }

        private static void AfterItemStatsInitialized(ItemStats __instance)
        {
            Item item = __instance == null ? null : __instance.ParentModel;
            if (item == null)
            {
                return;
            }

            int summonTier;
            if (item.Template != null
                && VanillaSummonTiers.TryGetValue(item.Template.GUID, out summonTier))
            {
                RegisterOrdinarySummonCastInfo(item, summonTier);
            }
            if (!IsSoulSalvageItem(item))
            {
                return;
            }

            string itemId = ((Model)item).ID;
            RemoveHeavyCostTweak(itemId);
            SoulSalvageItems[itemId] = __instance;
            LightCastInfos.Add(item.LightCastInfo);
            HeavyCastInfos.Add(item.HeavyCastInfo);
            EnsureHeavyCostTweak(itemId, __instance);
        }

        private static void AfterGetItemDisplayName(Item __instance, ref string __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value
                && IsSoulSalvageItem(__instance))
            {
                __result = SoulRendDisplayName;
            }
        }

        private static void AfterGetSearchActionFrame(
            SearchAction __instance,
            ref InfoFrame __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Location location = __instance == null ? null : __instance.ParentModel;
            if (_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || !IsSoulSalvageEquipped()
                || !TryResolveHighSoulCorpse(
                    Hero.Current,
                    location,
                    out HighSoulCorpseInfo highSoul))
            {
                return;
            }

            int remaining = SoulProgressionRuntime
                .GetOrInitializeHighSoulVigorPool(
                    highSoul.Fingerprint,
                    highSoul.ExtractionValue);
            __result = new InfoFrame(
                remaining.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor",
                false);
        }

        private static bool BeforeGetMagicDescription(
            MagicItemTemplateInfo __instance,
            ref string __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value)
            {
                return true;
            }

            if (LightCastInfos.Contains(__instance))
            {
                __result = "Corpses: Harvest for Soul Vigor.\n"
                    + "Servants: Strip Empowerment, then two Soulforged ranks per cast; unbind at rank 0."
                    + (plugin.LivingTargetSoulSalvage.Value
                        ? "\nEnemies: Deal Necrotic damage. Each surviving hit raises that enemy's Soul Claim threshold by 2%, up to 10%."
                        : string.Empty);
                return false;
            }

            if (!HeavyCastInfos.Contains(__instance))
            {
                return true;
            }

            __result = "Corpses: Bind and reanimate; cost scales with soul quality."
                + (plugin.LivingTargetSoulSalvage.Value
                    ? "\nWounded enemies: Claim at or below their Power- and soul-quality threshold."
                    : string.Empty)
                + "\nServants: Restore Health; at 95%, Empower at 1,000 Soul Vigor for twice base soul value.";
            return false;
        }

        private static void AfterGetMagicDescription(
            MagicItemTemplateInfo __instance,
            ref string __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            int summonTier;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || __instance == null
                || !OrdinarySummonCastTiers.TryGetValue(__instance, out summonTier))
            {
                return;
            }

            int vigorCost = GetOrdinarySummonSoulVigorCost(
                summonTier,
                SoulProgressionRuntime.GetNecromanticPower());
            string costLine = "Soul Vigor Cost: "
                + vigorCost.ToString(CultureInfo.InvariantCulture);
            __result = string.IsNullOrWhiteSpace(__result)
                ? costLine
                : __result.TrimEnd() + "\n" + costLine;
        }

        private static void RegisterOrdinarySummonCastInfo(Item item, int summonTier)
        {
            if (item != null && item.HeavyCastInfo != null && summonTier > 0)
            {
                OrdinarySummonCastTiers[item.HeavyCastInfo] = summonTier;
            }
        }

        private static int GetOrdinarySummonTier(Item item)
        {
            int summonTier;
            if (item != null
                && item.Template != null
                && VanillaSummonTiers.TryGetValue(item.Template.GUID, out summonTier))
            {
                return summonTier;
            }
            if (item != null && item.Tags != null)
            {
                for (int tier = 6; tier >= 1; tier--)
                {
                    if (item.Tags.Contains(
                            "item:tier" + tier.ToString(CultureInfo.InvariantCulture)))
                    {
                        return tier;
                    }
                }
            }
            return 1;
        }

        private static int GetOrdinarySummonSoulVigorCost(int summonTier, float power)
        {
            return GetPowerScaledSoulVigorCost(
                Math.Max(1, summonTier) * OrdinarySummonVigorCostPerTier,
                power);
        }

        private static int GetEmpowermentSoulVigorCost(
            NpcHeroSummon summon,
            float power)
        {
            int baseSoulVigor = Math.Max(
                    1,
                    GetOrdinarySummonTier(summon == null ? null : summon.Item))
                * OrdinarySummonVigorCostPerTier;
            if (summon != null)
            {
                ReanimationRecord record;
                if (Reanimations.TryGetValue(
                        ((Model)summon).ID,
                        out record)
                    && record != null
                    && record.NativeSoulVigor > 0)
                {
                    baseSoulVigor = record.NativeSoulVigor;
                }
            }
            return GetPowerScaledSoulVigorCost(baseSoulVigor * 2, power);
        }

        private static void AddEmpowermentSoulVigorInvestment(
            NpcHeroSummon summon,
            int committedVigor)
        {
            if (summon == null || committedVigor <= 0)
            {
                return;
            }
            string summonId = ((Model)summon).ID;
            ReanimationRecord record;
            if (Reanimations.TryGetValue(summonId, out record)
                && record != null)
            {
                EnsureRaisedRecoveryLedger(record);
                record.InvestedSoulVigor += committedVigor;
                record.Recovery.EmpowermentSoulVigorInvestment +=
                    committedVigor;
                SavePersistentReanimation(summonId, record);
                return;
            }
            OrdinarySummonInvestment investment;
            if (!OrdinarySummonInvestments.TryGetValue(
                    summonId,
                    out investment)
                || investment == null)
            {
                investment = new OrdinarySummonInvestment();
                OrdinarySummonInvestments[summonId] = investment;
            }
            EnsureOrdinaryRecoveryLedger(investment);
            investment.InvestedSoulVigor += committedVigor;
            investment.Recovery.EmpowermentSoulVigorInvestment +=
                committedVigor;
            SaveOrdinarySummonInvestment(summonId, investment);
        }

        private static void UpdateSoulSalvageItems()
        {
            _itemRefreshDelay -= Time.deltaTime;
            if (_itemRefreshDelay <= 0.0f)
            {
                _itemRefreshDelay = 2.0f;
                foreach (ItemStats stats in World.All<ItemStats>())
                {
                    if (stats != null && IsSoulSalvageItem(stats.ParentModel))
                    {
                        Item item = stats.ParentModel;
                        string itemId = ((Model)item).ID;
                        if (!SoulSalvageItems.ContainsKey(itemId))
                        {
                            SoulSalvageItems[itemId] = stats;
                            LightCastInfos.Add(item.LightCastInfo);
                            HeavyCastInfos.Add(item.HeavyCastInfo);
                        }
                    }
                }
            }

            SoulSalvageItemRemovalBuffer.Clear();
            foreach (KeyValuePair<string, ItemStats> pair in SoulSalvageItems)
            {
                string itemId = pair.Key;
                ItemStats stats = pair.Value;
                if (stats == null
                    || ((Model)stats).HasBeenDiscarded
                    || stats.ParentModel == null
                    || ((Model)stats.ParentModel).HasBeenDiscarded)
                {
                    SoulSalvageItemRemovalBuffer.Add(itemId);
                    continue;
                }
                EnsureHeavyCostTweak(itemId, stats);
            }
            foreach (string itemId in SoulSalvageItemRemovalBuffer)
            {
                RemoveHeavyCostTweak(itemId);
                SoulSalvageItems.Remove(itemId);
            }
        }

        private static void EnsureHeavyCostTweak(string itemId, ItemStats stats)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            bool shouldApply = plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value;
            StatTweak existing;
            if (HeavyCostTweaks.TryGetValue(itemId, out existing))
            {
                if (existing != null
                    && !((Model)existing).HasBeenDiscarded
                    && shouldApply)
                {
                    return;
                }
                RemoveHeavyCostTweak(itemId);
            }
            if (!shouldApply || stats == null || stats.HeavyCastManaCost == null)
            {
                return;
            }

            StatTweak tweak = StatTweak.Multi(
                stats.HeavyCastManaCost,
                HeavyCastManaCostMultiplier,
                null,
                stats.ParentModel);
            ((Model)tweak).MarkedNotSaved = true;
            HeavyCostTweaks[itemId] = tweak;
        }

        private static void RemoveHeavyCostTweak(string itemId)
        {
            StatTweak tweak;
            if (HeavyCostTweaks.TryGetValue(itemId, out tweak))
            {
                HeavyCostTweaks.Remove(itemId);
                if (tweak != null && !((Model)tweak).HasBeenDiscarded)
                {
                    tweak.Discard();
                }
            }
        }

        internal static bool IsSoulSalvageItem(Item item)
        {
            return item != null
                && item.Template != null
                && string.Equals(
                    item.Template.GUID,
                    SoulSalvageTemplateGuid,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNecroticDamageForInterop(object damage)
        {
            Damage typedDamage = damage as Damage;
            if (typedDamage == null)
            {
                return false;
            }

            NecroticDamageMarker marker;
            return NecroticDamageMarkers.TryGetValue(typedDamage, out marker);
        }

        private static void MarkNecroticDamage(Damage damage)
        {
            if (damage != null)
            {
                NecroticDamageMarkers.GetValue(
                    damage,
                    ignored => new NecroticDamageMarker());
            }
        }

        private static void BeforeCastingBegun(
            CastingHand hand,
            bool lightCast)
        {
            BeginSoulRendCast(hand, lightCast);
        }

        private static void BeforeCastingCanceled(Item castingItem)
        {
            if (_lightCastActive
                || _heavyCastActive
                || IsSoulSalvageItem(castingItem))
            {
                ClearLightCastState();
            }
        }

        private static void BeforeCastingEnded(
            CastingHand hand,
            bool lightCast,
            out CastState __state)
        {
            __state = default(CastState);
            Item item;
            if (!TryGetSoulRendCastItem(hand, out item))
            {
                ClearLightCastState();
                return;
            }
            if (_lightCastActive != lightCast
                || _heavyCastActive == lightCast)
            {
                BeginSoulRendCast(hand, lightCast);
            }

            __state = new CastState
            {
                IsSoulSalvage = true,
                IsHeavy = !lightCast,
                Item = item
            };
            if (!lightCast && _heavyTarget == null)
            {
                TryCaptureFocusedHeavyTarget();
            }
        }

        private static void BeginSoulRendCast(
            CastingHand hand,
            bool lightCast)
        {
            Item item;
            if (!TryGetSoulRendCastItem(hand, out item))
            {
                ClearLightCastState();
                return;
            }
            _lightCastActive = lightCast;
            _heavyCastActive = !lightCast;
            _lightHarvestCompleted = false;
            _lightPreserveTarget = false;
            _lightTarget = null;
            _heavyTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
            _lightMaximumManaReturn = float.PositiveInfinity;
            _lightResolvedManaReturn = 0.0f;
            if (!lightCast)
            {
                TryCaptureFocusedHeavyTarget();
            }
        }

        private static bool TryGetSoulRendCastItem(
            CastingHand hand,
            out Item item)
        {
            item = null;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || hero == null)
            {
                return false;
            }
            EquipmentSlotType slot = hand == CastingHand.OffHand
                ? EquipmentSlotType.OffHand
                : EquipmentSlotType.MainHand;
            item = hero.HeroItems.EquippedItem(slot);
            return IsSoulSalvageItem(item);
        }

        private static void TryCaptureFocusedHeavyTarget()
        {
            Location focusedLocation;
            NpcHeroSummon focusedSummon;
            if (TryFindFocusedSoulTargetCached(
                    out focusedLocation,
                    out focusedSummon)
                && focusedSummon != null)
            {
                _heavyTarget = focusedSummon;
            }
        }

        private static void AfterCastingEnded(CastState __state)
        {
            try
            {
                if (__state.IsSoulSalvage && __state.IsHeavy)
                {
                    if (_heavyTarget != null)
                    {
                        TryServeHeavyTarget(_heavyTarget);
                    }
                    else
                    {
                        TryUseHeavyCast(__state.Item);
                    }
                }
                else if (__state.IsSoulSalvage
                    && !_lightHarvestCompleted
                    && _lightTarget != null)
                {
                    CompleteLightSummonHarvest(_lightTarget);
                }
                else if (__state.IsSoulSalvage
                    && !_lightHarvestCompleted
                    && _lightTarget == null)
                {
                    TryUseLightCast(__state.Item);
                }
            }
            finally
            {
                ClearLightCastState();
            }
        }

        private static bool BeforeGetManaExpended(
            NpcHeroSummon __instance,
            ref float __result,
            float ____manaExpended)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (_heavyCastActive
                && plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value)
            {
                _heavyTarget = __instance;
                __result = 0.0f;
                return false;
            }
            if (!_lightCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || __instance == null)
            {
                return true;
            }

            _lightTarget = __instance;
            ReanimationRecord raisedRecord;
            if (Reanimations.TryGetValue(((Model)__instance).ID, out raisedRecord))
            {
                if (raisedRecord.IsMiniboss)
                {
                    _lightOriginalMana = 0.0f;
                    _lightMaximumManaReturn = 0.0f;
                }
                else
                {
                    float corpseQuality = Mathf.Lerp(
                        RaisedSalvageMinimumQualityFactor,
                        RaisedSalvageMaximumQualityFactor,
                        Mathf.Clamp01(raisedRecord.Quality01));
                    _lightOriginalMana = Math.Max(
                        0.0f,
                        raisedRecord.BindingManaCost * corpseQuality);
                    _lightMaximumManaReturn = Math.Max(
                        0.0f,
                        raisedRecord.BindingManaCost
                        * RaisedSalvageMaximumRefundFraction);
                }
            }
            else
            {
                _lightOriginalMana = Math.Max(0.0f, ____manaExpended);
                _lightMaximumManaReturn = float.PositiveInfinity;
            }
            _lightHealthFraction = __instance.ParentModel != null
                && __instance.ParentModel.Health != null
                    ? Mathf.Clamp01(__instance.ParentModel.Health.Percentage)
                    : 0.0f;

            CompleteLightSummonHarvest(__instance);
            __result = NativeManaRefundMultiplier > 0.0f
                ? _lightResolvedManaReturn / NativeManaRefundMultiplier
                : 0.0f;
            return false;
        }

        private static bool BeforeDestroySummon(NpcHeroSummon __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (_heavyCastActive
                && plugin != null
                && plugin.IsEnabled
                && plugin.SoulSalvageOverhaul.Value
                && __instance != null)
            {
                if (_heavyTarget == null)
                {
                    TryCaptureFocusedHeavyTarget();
                }
                if (ReferenceEquals(_heavyTarget, __instance))
                {
                    plugin.LogDiagnostic(
                        "Blocked vanilla heavy Soul Rend summon sacrifice; resolving friendly Empower instead.");
                    return false;
                }
            }
            if (_lightCastActive
                && plugin != null
                && hero != null
                && __instance != null
                && ReferenceEquals(__instance, _lightTarget))
            {
                if (!_lightHarvestCompleted)
                {
                    CompleteLightSummonHarvest(__instance);
                }
                if (_lightPreserveTarget)
                {
                    plugin.LogDiagnostic(
                        "Blocked native light Soul Rend destruction after resolving one servant layer.");
                    return false;
                }
            }
            if (__instance != null)
            {
                ReanimationRecord endingRecord;
                if (Reanimations.TryGetValue(
                        ((Model)__instance).ID,
                        out endingRecord))
                {
                    endingRecord.DismissedAsRemains = true;
                }
            }
            return true;
        }

        private static bool BeforeDestroyHeavyTarget(NpcElement __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement target = _heavyTarget == null
                ? null
                : _heavyTarget.ParentModel;
            if (!_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || target == null
                || !ReferenceEquals(target, __instance))
            {
                return true;
            }

            plugin.LogDiagnostic(
                "Blocked vanilla heavy Soul Rend NPC destroy; resolving friendly Empower instead.");
            return false;
        }

        private static bool BeforeKillHeavyTarget(HealthElement __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement target = _heavyTarget == null
                ? null
                : _heavyTarget.ParentModel;
            if (!_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || target == null
                || !ReferenceEquals(target.HealthElement, __instance))
            {
                return true;
            }

            plugin.LogDiagnostic(
                "Blocked vanilla heavy Soul Rend servant kill; resolving friendly Empower instead.");
            return false;
        }

        private static void CompleteLightSummonHarvest(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (_lightHarvestCompleted
                || plugin == null
                || hero == null
                || summon == null
                || !ReferenceEquals(summon, _lightTarget))
            {
                return;
            }

            _lightHarvestCompleted = true;
            _lightPreserveTarget = false;
            _lightResolvedManaReturn = 0.0f;
            string summonId = ((Model)summon).ID;
            ReanimationRecord raisedRecord;
            bool raisedServant = Reanimations.TryGetValue(
                summonId,
                out raisedRecord);
            OrdinarySummonInvestment ordinaryInvestment = null;
            if (!raisedServant
                && (!OrdinarySummonInvestments.TryGetValue(
                        summonId,
                        out ordinaryInvestment)
                    || ordinaryInvestment == null))
            {
                ordinaryInvestment = new OrdinarySummonInvestment();
                OrdinarySummonInvestments[summonId] = ordinaryInvestment;
            }

            ServantRecoveryLedger recovery = EnsureLightRecoveryLedger(
                summonId,
                raisedRecord,
                ordinaryInvestment,
                plugin);
            string displayName = raisedServant
                ? raisedRecord.SourceDisplayName
                : GetSummonDisplayName(summon);

            if (SummonRuntime.IsEmpoweredSummon(summon))
            {
                _lightPreserveTarget = true;
                if (!SummonRuntime.TryRemoveEmpowerment(summon))
                {
                    plugin.LogWarning(
                        "Soul Rend preserved " + summonId
                        + " because its Empowerment could not be removed safely.");
                    return;
                }

                int empowermentInvestment = recovery == null
                    ? 0
                    : Math.Max(
                        0,
                        recovery.EmpowermentSoulVigorInvestment);
                int requestedRefund = Mathf.Clamp(
                    Mathf.RoundToInt(
                        empowermentInvestment
                            * EmpowermentSeverRefundFraction),
                    0,
                    empowermentInvestment);
                if (raisedServant)
                {
                    raisedRecord.InvestedSoulVigor = Math.Max(
                        0,
                        raisedRecord.InvestedSoulVigor
                            - empowermentInvestment);
                    recovery.EmpowermentSoulVigorInvestment = 0;
                    SavePersistentReanimation(summonId, raisedRecord);
                }
                else
                {
                    ordinaryInvestment.InvestedSoulVigor = Math.Max(
                        0,
                        ordinaryInvestment.InvestedSoulVigor
                            - empowermentInvestment);
                    recovery.EmpowermentSoulVigorInvestment = 0;
                    SaveOrdinarySummonInvestment(
                        summonId,
                        ordinaryInvestment);
                }
                int actualRefund = SoulProgressionRuntime.RestoreSoulVigor(
                    requestedRefund);
                SoulProgressionRuntime.ShowServantSoulRendStage(
                    "servant-empowerment-severed",
                    summonId,
                    displayName + ": Empowerment severed"
                    + (actualRefund > 0
                        ? " | +" + actualRefund.ToString(
                            CultureInfo.InvariantCulture)
                            + " Soul Vigor"
                        : string.Empty));
                SoulProgressionRuntime.ShowCommandUnlocksAfterSummonHarvest(
                    actualRefund);
                PlayServantSoulRendFeedback(summon, raisedRecord);
                plugin.LogDiagnostic(
                    "Soul Rend severed Empowerment from " + summonId
                    + ": investedVigor=" + empowermentInvestment
                    + "; refund=" + actualRefund + ".");
                return;
            }

            int realRank = SoulforgedRuntime.GetRealRank(summon);
            if (realRank > 0)
            {
                _lightPreserveTarget = true;
                if (!SoulforgedRuntime.TryReduceRealRanks(
                        summon,
                        2,
                        out int previousRank,
                        out int currentRank))
                {
                    plugin.LogWarning(
                        "Soul Rend preserved " + summonId
                        + " because its Soulforged rank could not be reduced safely.");
                    return;
                }

                int vigorAward = 0;
                if (!raisedServant || !raisedRecord.IsMiniboss)
                {
                    ApplySoulforgedRecovery(
                        raisedRecord,
                        ordinaryInvestment,
                        recovery,
                        previousRank,
                        currentRank,
                        _lightHealthFraction,
                        out _lightResolvedManaReturn,
                        out vigorAward);
                    if (raisedServant)
                    {
                        SavePersistentReanimation(summonId, raisedRecord);
                    }
                    else
                    {
                        SaveOrdinarySummonInvestment(
                            summonId,
                            ordinaryInvestment);
                    }
                }
                string recoveryText = string.Empty;
                if (_lightResolvedManaReturn > 0.0f)
                {
                    recoveryText += " | +"
                        + _lightResolvedManaReturn.ToString(
                            "0",
                            CultureInfo.InvariantCulture)
                        + " Mana";
                }
                if (vigorAward > 0)
                {
                    recoveryText += " | +"
                        + vigorAward.ToString(CultureInfo.InvariantCulture)
                        + " Soul Vigor";
                }
                SoulProgressionRuntime.ShowServantSoulRendStage(
                    "servant-rank-reduced",
                    summonId,
                    displayName + ": Soulforged "
                    + SoulforgedRuntime.GetRankLabel(previousRank)
                    + " -> "
                    + SoulforgedRuntime.GetRankLabel(currentRank)
                    + recoveryText);
                SoulProgressionRuntime.ShowCommandUnlocksAfterSummonHarvest(
                    vigorAward);
                PlayServantSoulRendFeedback(summon, raisedRecord);
                plugin.LogDiagnostic(
                    "Soul Rend reduced " + summonId + " from rank "
                    + previousRank + " to " + currentRank
                    + ": healthFraction="
                    + _lightHealthFraction.ToString("0.###")
                    + "; mana="
                    + _lightResolvedManaReturn.ToString("0.##")
                    + "; vigor=" + vigorAward + ".");
                return;
            }

            float manaReturned = recovery == null
                ? CalculateFullHealthLightManaReturn(plugin)
                    * _lightHealthFraction
                : Mathf.Round(
                    Math.Max(0.0f, recovery.RemainingMana)
                        * _lightHealthFraction);
            _lightResolvedManaReturn = manaReturned;
            if (recovery != null)
            {
                recovery.RemainingMana = 0.0f;
            }
            int soulVigorAward = 0;
            Grailwright.Shared.CorpseQualityTier qualityTier;
            if (raisedServant)
            {
                raisedRecord.Sacrificed = true;
                raisedRecord.SalvageHealthFraction = _lightHealthFraction;
                raisedRecord.ManaReturnedOnSacrifice = manaReturned;
                qualityTier = raisedRecord.QualityTier;
                SavePersistentReanimation(summonId, raisedRecord);
            }
            else
            {
                int investedVigor = Math.Max(
                    0,
                    ordinaryInvestment.InvestedSoulVigor);
                OrdinarySummonInvestments.Remove(summonId);
                ClearPersistedServant(summonId);
                soulVigorAward = SoulProgressionRuntime.RestoreSoulVigor(
                    Mathf.Clamp(
                        Mathf.RoundToInt(
                            investedVigor * _lightHealthFraction),
                        0,
                        investedVigor));
                qualityTier = Grailwright.Shared.CorpseQualityTier.None;
                SoulProgressionRuntime.ShowSoulVigorHarvest(
                    displayName,
                    qualityTier,
                    soulVigorAward,
                    manaReturned);
                SoulProgressionRuntime.ShowCommandUnlocksAfterSummonHarvest(
                    soulVigorAward);
            }
            PlayServantSoulRendFeedback(summon, raisedRecord);
            plugin.LogDiagnostic(
                "Soul Rend unbound " + summonId
                + ": remainingMana=" + manaReturned.ToString("0.##")
                + "; healthFraction=" + _lightHealthFraction.ToString("0.###")
                + "; soulVigor=" + (raisedServant
                    ? "pending remains"
                    : soulVigorAward.ToString(CultureInfo.InvariantCulture))
                + ".");
        }

        private static ServantRecoveryLedger EnsureLightRecoveryLedger(
            string summonId,
            ReanimationRecord raisedRecord,
            OrdinarySummonInvestment ordinaryInvestment,
            SoulAndServicePlugin plugin)
        {
            ServantRecoveryLedger recovery;
            if (raisedRecord != null)
            {
                EnsureRaisedRecoveryLedger(raisedRecord);
                recovery = raisedRecord.Recovery;
            }
            else
            {
                EnsureOrdinaryRecoveryLedger(ordinaryInvestment);
                recovery = ordinaryInvestment.Recovery;
            }
            if (!recovery.ManaInitialized)
            {
                recovery.ManaInitialized = true;
                recovery.OriginalMana = CalculateFullHealthLightManaReturn(
                    plugin);
                recovery.RemainingMana = recovery.OriginalMana;
                if (raisedRecord != null)
                {
                    SavePersistentReanimation(summonId, raisedRecord);
                }
                else
                {
                    SaveOrdinarySummonInvestment(
                        summonId,
                        ordinaryInvestment);
                }
            }
            return recovery;
        }

        private static void ApplySoulforgedRecovery(
            ReanimationRecord raisedRecord,
            OrdinarySummonInvestment ordinaryInvestment,
            ServantRecoveryLedger recovery,
            int previousRank,
            int currentRank,
            float healthFraction,
            out float manaAward,
            out int vigorAward)
        {
            manaAward = 0.0f;
            vigorAward = 0;
            if (recovery == null || previousRank <= currentRank)
            {
                return;
            }

            int previousMask = recovery.RecoveredSoulforgedRankMask;
            int currentMask = previousMask;
            for (int rank = previousRank; rank > currentRank; rank--)
            {
                if (rank >= 1 && rank <= SoulforgedRuntime.MaximumRank)
                {
                    currentMask |= 1 << (rank - 1);
                }
            }
            recovery.RecoveredSoulforgedRankMask = currentMask;
            int previousRecovered = CountRecoveredSoulforgedRanks(previousMask);
            int currentRecovered = CountRecoveredSoulforgedRanks(currentMask);
            if (currentRecovered <= previousRecovered)
            {
                return;
            }

            float targetManaConsumed = Mathf.Round(
                recovery.OriginalMana
                    * SoulforgedRecoveryFractionPerRank
                    * currentRecovered);
            float previousManaConsumed = Math.Max(
                0.0f,
                recovery.OriginalMana - recovery.RemainingMana);
            float grossMana = Mathf.Clamp(
                targetManaConsumed - previousManaConsumed,
                0.0f,
                recovery.RemainingMana);
            recovery.RemainingMana = Math.Max(
                0.0f,
                recovery.RemainingMana - grossMana);

            int currentBaseVigor = raisedRecord != null
                ? Math.Max(
                    0,
                    raisedRecord.NativeSoulVigor
                        + raisedRecord.InvestedSoulVigor)
                : Math.Max(0, ordinaryInvestment.InvestedSoulVigor);
            int targetVigorConsumed = Mathf.Clamp(
                Mathf.RoundToInt(
                    recovery.OriginalSoulVigor
                        * SoulforgedRecoveryFractionPerRank
                        * currentRecovered),
                0,
                recovery.OriginalSoulVigor);
            int previousVigorConsumed = Math.Max(
                0,
                recovery.OriginalSoulVigor - currentBaseVigor);
            int grossVigor = Mathf.Clamp(
                targetVigorConsumed - previousVigorConsumed,
                0,
                currentBaseVigor);
            if (raisedRecord != null)
            {
                int investedReduction = Math.Min(
                    raisedRecord.InvestedSoulVigor,
                    grossVigor);
                raisedRecord.InvestedSoulVigor -= investedReduction;
                raisedRecord.NativeSoulVigor = Math.Max(
                    0,
                    raisedRecord.NativeSoulVigor
                        - (grossVigor - investedReduction));
            }
            else
            {
                ordinaryInvestment.InvestedSoulVigor = Math.Max(
                    0,
                    ordinaryInvestment.InvestedSoulVigor - grossVigor);
            }

            float recoveryEfficiency = Math.Max(
                SoulforgedRecoveryMinimumHealthFraction,
                Mathf.Clamp01(healthFraction));
            manaAward = Mathf.Round(grossMana * recoveryEfficiency);
            vigorAward = SoulProgressionRuntime.RestoreSoulVigor(
                Mathf.Clamp(
                    Mathf.RoundToInt(grossVigor * recoveryEfficiency),
                    0,
                    grossVigor));
        }

        private static int CountRecoveredSoulforgedRanks(int mask)
        {
            int count = 0;
            int remaining = mask
                & ((1 << SoulforgedRuntime.MaximumRank) - 1);
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }
            return count;
        }

        private static float CalculateFullHealthLightManaReturn(
            SoulAndServicePlugin plugin)
        {
            if (plugin == null || plugin.SoulSalvageManaReturnPercent == null)
            {
                return 0.0f;
            }
            float rawReturn = _lightOriginalMana
                * (plugin.SoulSalvageManaReturnPercent.Value / 100.0f);
            return Mathf.Round(Math.Min(rawReturn, _lightMaximumManaReturn));
        }

        private static void PlayServantSoulRendFeedback(
            NpcHeroSummon summon,
            ReanimationRecord raisedRecord)
        {
            Location summonLocation = summon == null
                || summon.ParentModel == null
                    ? null
                    : summon.ParentModel.ParentModel;
            bool hasAudioPosition = raisedRecord != null
                || summonLocation != null;
            Vector3 audioPosition = raisedRecord != null
                ? raisedRecord.LastSafeCoords
                : summonLocation == null
                    ? Vector3.zero
                    : summonLocation.Coords;
            Grailwright.Shared.CorpseQualityTier audioTier =
                raisedRecord != null
                    ? raisedRecord.QualityTier
                    : Grailwright.Shared.CorpseQualityBuckets.GetTier(
                        CalculateQuality01(
                            summonLocation,
                            summon == null ? null : summon.ParentModel),
                        true);
            SoulSalvageAudioRuntime.Play(
                audioTier,
                hasAudioPosition,
                audioPosition,
                GetSoulSalvageAudioTargetClass(
                    raisedRecord == null
                        ? summonLocation
                        : raisedRecord.SourceCorpse,
                    raisedRecord == null
                        ? summon == null ? null : summon.ParentModel
                        : raisedRecord.RaisedNpc));
            SoulSalvageAudioRuntime.PlayImpact(
                false,
                hasAudioPosition,
                audioPosition);
        }

        private static void TryServeHeavyTarget(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcElement npc = summon == null ? null : summon.ParentModel;
            if (plugin == null
                || npc == null
                || npc.HasBeenDiscarded
                || !npc.IsAlive
                || npc.Health == null)
            {
                return;
            }

            float power = SoulProgressionRuntime.GetNecromanticPower();
            bool isMiniboss = IsReanimatedMiniboss(summon);
            float maximumHealth = npc.AliveStats != null
                    && npc.AliveStats.MaxHealth != null
                ? Math.Max(
                    npc.Health.UpperLimit,
                    npc.AliveStats.MaxHealth.ModifiedValue)
                : npc.Health.UpperLimit;
            if (maximumHealth <= 0.0f)
            {
                plugin.LogDiagnostic(
                    "Heavy Soul Rend could not resolve the servant's maximum Health.");
                return;
            }

            float beforeHealth = Mathf.Clamp(
                npc.Health.ModifiedValue,
                0.0f,
                maximumHealth);
            float beforeFraction = beforeHealth / maximumHealth;
            bool empowerEligibleHealth = beforeFraction
                >= ServantEmpowerHealthThreshold;
            float healingFraction = Mathf.Lerp(
                ServantHealingPowerZeroFraction,
                ServantHealingPowerMaximumFraction,
                Mathf.Clamp01(power / 200.0f))
                * (isMiniboss ? 0.50f : 1.0f);
            float missingHealth = Math.Max(0.0f, maximumHealth - beforeHealth);
            float requestedHealing = empowerEligibleHealth
                ? missingHealth
                : Math.Min(missingHealth, maximumHealth * healingFraction);
            if (requestedHealing > 0.001f)
            {
                npc.Health.IncreaseBy(requestedHealing);
            }
            float appliedHealing = Math.Max(
                0.0f,
                Math.Min(maximumHealth, npc.Health.ModifiedValue) - beforeHealth);

            bool alreadyEmpowered = SummonRuntime.IsEmpoweredSummon(summon);
            bool empowered = false;
            float multiplier = 1.0f;
            int empowermentCost = 0;
            int committedVigor = 0;
            if (empowerEligibleHealth
                && !isMiniboss
                && power >= SoulProgressionRuntime.EmpowermentPower
                && !alreadyEmpowered)
            {
                empowermentCost = GetEmpowermentSoulVigorCost(summon, power);
                if (!SoulProgressionRuntime.TrySpendSoulVigor(
                        empowermentCost,
                        out int beforeVigor,
                        out int afterVigor))
                {
                    plugin.LogDiagnostic(
                        "Heavy Soul Rend could not Empower servant "
                        + ((Model)summon).ID
                        + " because it requires "
                        + empowermentCost.ToString(CultureInfo.InvariantCulture)
                        + " Soul Vigor.");
                    if (appliedHealing > 0.001f)
                    {
                        SpawnNecromanticSummonVfx(summon.ParentModel);
                    }
                    SoulProgressionRuntime.ShowInsufficientSoulVigor(
                        empowermentCost);
                    return;
                }
                committedVigor = afterVigor < beforeVigor
                    ? empowermentCost
                    : 0;
                float roll = UnityEngine.Random.value;
                multiplier = 1.20f + (0.30f * roll * roll);
                empowered = SummonRuntime.TryEmpowerSummon(
                    summon,
                    multiplier);
                if (!empowered)
                {
                    if (committedVigor > 0)
                    {
                        SoulProgressionRuntime.RestoreSoulVigor(committedVigor);
                        committedVigor = 0;
                    }
                }
                else
                {
                    AddEmpowermentSoulVigorInvestment(
                        summon,
                        committedVigor);
                    SoulProgressionRuntime.ShowSoulVigorWanesAfterSpend(
                        beforeVigor,
                        afterVigor);
                }
            }

            plugin.LogDiagnostic(
                "Heavy Soul Rend servant service: summon="
                + ((Model)summon).ID
                + "; power=" + power.ToString("0.##", CultureInfo.InvariantCulture)
                + "; health=" + beforeHealth.ToString("0.##", CultureInfo.InvariantCulture)
                + "/" + maximumHealth.ToString("0.##", CultureInfo.InvariantCulture)
                + "; thresholdEligible=" + empowerEligibleHealth
                + "; requestedHealing=" + requestedHealing.ToString("0.##", CultureInfo.InvariantCulture)
                + "; appliedHealing=" + appliedHealing.ToString("0.##", CultureInfo.InvariantCulture)
                + "; alreadyEmpowered=" + alreadyEmpowered
                + "; empowermentCost=" + empowermentCost
                + "; committedVigor=" + committedVigor
                + "; empowered=" + empowered + ".");

            if (appliedHealing <= 0.001f && !empowered)
            {
                if (!empowerEligibleHealth)
                {
                    plugin.LogDiagnostic(
                        "Heavy Soul Rend found an injured servant, but no Health could be restored.");
                }
                else if (power < SoulProgressionRuntime.EmpowermentPower)
                {
                    plugin.LogDiagnostic(
                        "The servant is whole, but Empower requires 100 Necromantic Power.");
                }
                else if (alreadyEmpowered)
                {
                    plugin.LogDiagnostic("The servant is already Empowered.");
                }
                else if (isMiniboss)
                {
                    plugin.LogDiagnostic(
                        "Miniboss servants cannot be Empowered.");
                }
                return;
            }

            SpawnNecromanticSummonVfx(summon.ParentModel);
            SoulSalvageAudioRuntime.PlayImpact(
                true,
                true,
                summon.ParentModel.Coords);
            float appliedPercent = 100.0f * appliedHealing / maximumHealth;
            string costSuffix = committedVigor > 0
                ? " | -" + committedVigor.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor"
                : string.Empty;
            if (empowered && appliedHealing > 0.001f)
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Restored and Empowered: "
                    + multiplier.ToString("0.00", CultureInfo.InvariantCulture)
                    + "x" + costSuffix);
            }
            else if (empowered)
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Empowered: "
                    + multiplier.ToString("0.00", CultureInfo.InvariantCulture)
                    + "x" + costSuffix);
            }
            else
            {
                SoulProgressionRuntime.ShowSummonCommand(
                    "Servant Restored: +"
                    + appliedPercent.ToString("0.#", CultureInfo.InvariantCulture)
                    + "% Health");
            }
        }

        internal static void SpawnNecromanticSummonVfx(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }
            Location location = npc.ParentModel;
            PrefabPool.InstantiateAndReturn(
                new ShareableARAssetReference(SkeletonSummonVfxKey),
                npc.Coords,
                location == null ? Quaternion.identity : location.Rotation).Forget();
        }

        private static void SpawnBloodRitualVfx(ReanimationRecord record)
        {
            if (record == null
                || record.RaisedNpc == null
                || record.RaisedNpc.HasBeenDiscarded)
            {
                return;
            }

            string vfxKey;
            switch (record.QualityTier)
            {
                case Grailwright.Shared.CorpseQualityTier.Meager:
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    vfxKey = BloodRitualLesserVfxKey;
                    break;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    vfxKey = BloodRitualGreaterVfxKey;
                    break;
                default:
                    return;
            }

            Vector3 vfxPosition = record.RaisedNpc.Coords;
            if (record.RaisedNpc.VFXBodyMarker != null
                && record.RaisedNpc.VFXBodyMarker.Mesh != null)
            {
                var localBounds =
                    record.RaisedNpc.VFXBodyMarker.Mesh.localBoundingSphere;
                vfxPosition = record.RaisedNpc.VFXBodyMarker.transform.TransformPoint(
                    new Vector3(localBounds.x, localBounds.y, localBounds.z));
            }
            PrefabPool.InstantiateAndReturn(
                new ShareableARAssetReference(vfxKey),
                vfxPosition,
                Quaternion.identity).Forget();
        }

        private static void TryUseLightCast(Item sourceItem)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null || hero == null)
            {
                return;
            }
            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo highSoul))
            {
                TryDrainHighSoulCorpse(highSoul);
                return;
            }
            Location corpse;
            LocationTemplate ignoredSpawnTemplate;
            string corpseRejection;
            if (TryFindEligibleCorpse(
                    hero,
                    needsSpawnTemplate: false,
                    out corpse,
                    out ignoredSpawnTemplate,
                    out corpseRejection))
            {
                TryHarvestCorpse(corpse);
                return;
            }

            if (plugin.LivingTargetSoulSalvage.Value)
            {
                Location targetLocation;
                NpcElement target;
                Collider hitCollider;
                LocationTemplate ignoredLivingSpawnTemplate;
                string livingRejection;
                if (TryFindEligibleLivingTarget(
                        hero,
                        needsSpawnTemplate: false,
                        out targetLocation,
                        out target,
                        out hitCollider,
                        out ignoredLivingSpawnTemplate,
                        out livingRejection))
                {
                    ApplySoulRend(hero, target, sourceItem, hitCollider);
                    return;
                }
                plugin.LogDiagnostic(
                    "Soul Rend light cast found no eligible target: corpse="
                    + corpseRejection + "; living=" + livingRejection + ".");
                ShowSpentCorpseFeedback(plugin, corpseRejection);
                return;
            }

            plugin.LogDiagnostic(
                "Soul Rend light cast harvested nothing: " + corpseRejection);
            ShowSpentCorpseFeedback(plugin, corpseRejection);
        }

        private static void TryDrainHighSoulCorpse(HighSoulCorpseInfo highSoul)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Location corpse = highSoul.Corpse;
            if (plugin == null
                || corpse == null
                || corpse.HasBeenDiscarded)
            {
                return;
            }
            string displayName = GetCorpseDisplayName(corpse);
            Vector3 corpseCoords = corpse.Coords;
            Quaternion corpseRotation = corpse.Rotation;
            SoulSalvageAudioTargetClass audioTargetClass =
                GetSoulSalvageAudioTargetClass(corpse, null);

            int remainingBefore = SoulProgressionRuntime
                .GetOrInitializeHighSoulVigorPool(
                    highSoul.Fingerprint,
                    highSoul.ExtractionValue);
            if (remainingBefore <= 0)
            {
                plugin.LogDiagnostic(
                    "Soul Rend found no Soul Vigor remaining in "
                    + displayName + ".");
                return;
            }

            if (!SoulProgressionRuntime.TryDrainHighSoulVigorPool(
                    highSoul.Fingerprint,
                    highSoul.ExtractionValue,
                    out SoulProgressionRuntime.HighSoulDrainReceipt receipt))
            {
                plugin.LogWarning(
                    "Soul Rend could not save the high-soul drain for "
                    + displayName
                    + "; the corpse was left unchanged.");
                return;
            }

            int remainingAfter = Math.Max(
                0,
                receipt.BeforeRemaining - receipt.Award);
            if (remainingAfter == 0
                && highSoul.CanSafelySimplify
                && !TryCreateRemains(
                    corpse,
                    corpseCoords,
                    corpseRotation,
                    out string failure))
            {
                SoulProgressionRuntime.RollbackHighSoulDrain(receipt);
                plugin.LogWarning(
                    "Soul Rend could not simplify "
                    + displayName
                    + ": " + failure);
                return;
            }

            Grailwright.Shared.CorpseQualityTier presentationTier =
                Grailwright.Shared.CorpseQualityTier.Prime;
            SoulProgressionRuntime.ShowSoulVigorHarvest(
                displayName,
                presentationTier,
                receipt.Award,
                0.0f);
            SoulProgressionRuntime.ShowCommandUnlocksAfterSummonHarvest(
                receipt.Award);
            SoulSalvageAudioRuntime.Play(
                presentationTier,
                true,
                corpseCoords,
                audioTargetClass);
            SoulSalvageAudioRuntime.PlayImpact(
                false,
                true,
                corpseCoords);
            plugin.LogDiagnostic(
                "Soul Rend drained " + displayName
                + "; type=" + highSoul.NpcType
                + "; tier=" + highSoul.NativeTier.ToString(
                    CultureInfo.InvariantCulture)
                + "; soulVigor=" + receipt.Award.ToString(
                    CultureInfo.InvariantCulture)
                + "; remaining=" + remainingAfter.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
        }

        private static void TryHarvestCorpse(Location corpse)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null || corpse == null)
            {
                return;
            }
            ReanimationRecord executedRecord;
            bool executedServant = ExecutedServantRemains.TryGetValue(
                ((Model)corpse).ID,
                out executedRecord);
            float quality01 = executedServant
                ? executedRecord.Quality01
                : CalculateQuality01(corpse, null);
            Grailwright.Shared.CorpseQualityTier tier =
                executedServant
                    ? executedRecord.QualityTier
                    : Grailwright.Shared.CorpseQualityBuckets.GetTier(
                        quality01,
                        true);
            string fingerprint = executedServant
                ? executedRecord.CorpseFingerprint
                : GetCorpseFingerprint(corpse);
            string harvestIdentity = executedServant
                ? executedRecord.CorpseFingerprint
                : GetCorpseHarvestIdentity(corpse);
            string displayName = executedServant
                ? executedRecord.SourceDisplayName
                : GetCorpseDisplayName(corpse);
            SoulSalvageAudioTargetClass audioTargetClass =
                GetSoulSalvageAudioTargetClass(
                executedServant ? executedRecord.SourceCorpse : corpse,
                executedServant ? executedRecord.RaisedNpc : null);
            SoulProgressionRuntime.CorpseHarvestReceipt harvestReceipt;
            int executedAward = executedServant
                ? Mathf.Clamp(
                    Mathf.RoundToInt(
                        (executedRecord.NativeSoulVigor
                            + executedRecord.InvestedSoulVigor)
                        * Mathf.Clamp01(executedRecord.SalvageHealthFraction)),
                    0,
                    executedRecord.NativeSoulVigor
                        + executedRecord.InvestedSoulVigor)
                : 0;
            bool harvested = executedServant
                ? SoulProgressionRuntime.TryHarvestCorpse(
                    fingerprint,
                    tier,
                    executedAward,
                    out harvestReceipt)
                : SoulProgressionRuntime.TryHarvestCorpse(
                    fingerprint,
                    harvestIdentity,
                    tier,
                    quality01,
                    out harvestReceipt);
            if (!harvested)
            {
                plugin.LogWarning(
                    "Soul Rend could not save Soul Vigor for " + displayName
                    + "; the corpse was left unchanged.");
                return;
            }
            Location remainsSource = executedServant
                ? executedRecord.SourceCorpse
                : corpse;
            bool broadCurrentSessionHarvest = !executedServant
                && IsCurrentSessionCorpseHarvestEligible(
                    corpse.TryGetElement<Corpse>());
            bool canSafelySimplify = executedServant
                || CanSafelySimplifyOrdinaryCorpse(corpse);
            string failure = string.Empty;
            bool simplified = canSafelySimplify
                && TryCreateRemains(
                    remainsSource,
                    corpse.Coords,
                    corpse.Rotation,
                    out failure);
            if (canSafelySimplify
                && !simplified
                && !broadCurrentSessionHarvest)
            {
                SoulProgressionRuntime.RollbackCorpseHarvest(harvestReceipt);
                plugin.LogWarning(
                    "Soul Rend could not simplify " + displayName + ": " + failure);
                return;
            }
            if (!simplified)
            {
                plugin.LogDiagnostic(
                    "Soul Rend retained the intact body of " + displayName
                    + (canSafelySimplify
                        ? " because safe simplification failed: " + failure
                        : " because its source identity is protected")
                    + "; its soul remains spent.");
            }
            if (executedServant)
            {
                ExecutedServantRemains.Remove(((Model)corpse).ID);
                if (!corpse.HasBeenDiscarded
                    && !PendingRaisedDiscards.Contains(corpse))
                {
                    PendingRaisedDiscards.Add(corpse);
                }
            }
            else
            {
                ReportCorpseHarvestThreatToEyes(quality01);
            }
            SoulProgressionRuntime.ShowSoulVigorHarvest(
                displayName,
                tier,
                harvestReceipt.Award,
                0.0f);
            SoulProgressionRuntime.ShowCommandUnlocksAfterCorpseHarvest(
                harvestReceipt);
            SoulSalvageAudioRuntime.Play(
                tier,
                true,
                corpse.Coords,
                audioTargetClass);
            SoulSalvageAudioRuntime.PlayImpact(
                false,
                true,
                corpse.Coords);
            plugin.LogDiagnostic(
                "Soul Rend harvested " + displayName
                + "; quality=" + tier
                + "; soulVigor=" + harvestReceipt.Award.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + ".");
        }

        private static void ApplySoulRend(
            Hero hero,
            NpcElement target,
            Item sourceItem,
            Collider hitCollider)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || target == null
                || target.HealthElement == null
                || sourceItem == null
                || sourceItem.ItemStats == null)
            {
                return;
            }

            float power = SoulProgressionRuntime.GetNecromanticPower();
            float powerMultiplier = GetSoulRendPowerMultiplier(power);
            Model targetModel = (Model)target;
            string targetId = targetModel.ID;
            string targetDisplayName = GetCorpseDisplayName(target.ParentModel);
            Vector3 targetCoords = target.Coords;
            int itemLevel = sourceItem.Level == null
                ? 0
                : Math.Max(0, sourceItem.Level.ModifiedInt);
            float comparableDamage = ComparableLightSpellBaseDamage + itemLevel;
            Damage.GetStatModifiers(
                hero,
                sourceItem.ItemStats,
                out float statMultiplier,
                out float linearModifier);
            DamageParameters parameters = DamageParameters.Default;
            parameters.CanBeCritical = false;
            parameters.Critical = false;
            parameters.DamageTypeData = new RuntimeDamageTypeData(
                DamageType.MagicalHitSource,
                DamageSubType.GenericMagical);
            parameters.PoiseDamage = 0.0f;
            parameters.ForceDamage = 0.0f;
            parameters.RagdollForce = 0.0f;
            Vector3 impactPosition = hitCollider == null
                ? targetCoords
                : hitCollider.ClosestPoint(targetCoords);
            parameters.Position = impactPosition;
            Vector3 direction = targetCoords - hero.Coords;
            parameters.Direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;

            Damage damage = new Damage(
                    parameters,
                    hero,
                    target,
                    new RawDamageData(
                        comparableDamage,
                        statMultiplier * powerMultiplier,
                        linearModifier))
                .WithItem(sourceItem)
                .WithHitCollider(hitCollider);
            MarkNecroticDamage(damage);
            target.HealthElement.TakeDamage(damage);
            SoulSalvageAudioRuntime.PlayImpact(
                false,
                true,
                impactPosition);

            int preparationHits = 0;
            if (!targetModel.HasBeenDiscarded && target.IsAlive)
            {
                SoulClaimPreparationState state;
                if (!SoulClaimPreparations.TryGetValue(targetId, out state)
                    || state == null
                    || state.Target != target)
                {
                    state = new SoulClaimPreparationState
                    {
                        Target = target
                    };
                    SoulClaimPreparations[targetId] = state;
                }
                state.Hits = Math.Min(
                    SoulClaimMaximumPreparationHits,
                    state.Hits + 1);
                preparationHits = state.Hits;
            }
            else
            {
                SoulClaimPreparations.Remove(targetId);
            }

            plugin.LogDiagnostic(
                "Soul Rend hit " + targetDisplayName
                + "; comparableDamage="
                + comparableDamage.ToString("0.##", CultureInfo.InvariantCulture)
                + "; power=" + power.ToString("0.##", CultureInfo.InvariantCulture)
                + "; multiplier="
                + powerMultiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + "; finalDamage="
                + damage.Amount.ToString("0.##", CultureInfo.InvariantCulture)
                + "; claimPreparationHits="
                + preparationHits.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        private static float GetSoulRendPowerMultiplier(float power)
        {
            float safePower = Mathf.Clamp(power, 0.0f, 200.0f);
            return safePower <= 100.0f
                ? Mathf.Lerp(
                    SoulRendPowerZeroMultiplier,
                    SoulRendPowerNormalMultiplier,
                    safePower / 100.0f)
                : Mathf.Lerp(
                    SoulRendPowerNormalMultiplier,
                    SoulRendPowerMaximumMultiplier,
                    (safePower - 100.0f) / 100.0f);
        }

        private static void TryUseHeavyCast(Item sourceItem)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null)
            {
                return;
            }

            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo highSoul))
            {
                int remaining = SoulProgressionRuntime
                    .GetOrInitializeHighSoulVigorPool(
                        highSoul.Fingerprint,
                        highSoul.ExtractionValue);
                if (remaining <= 0)
                {
                    SoulProgressionRuntime.ShowBindingFailure(
                        "No soul remains to bind.");
                }
                else if (!highSoul.CanReanimate)
                {
                    SoulProgressionRuntime.ShowBindingFailure(
                        GetUnbindableHighSoulMessage(highSoul.Fingerprint));
                }
                else
                {
                    TryRaiseMiniboss(sourceItem, highSoul);
                }
                return;
            }

            Location source;
            LocationTemplate spawnTemplate;
            string corpseRejection;
            if (TryFindEligibleCorpse(
                    hero,
                    needsSpawnTemplate: true,
                    out source,
                    out spawnTemplate,
                    out corpseRejection))
            {
                TryRaiseCorpse(
                    sourceItem,
                    source,
                    spawnTemplate,
                    bindingAlreadyWon: false,
                    summonLimitAlreadyChecked: false);
                return;
            }

            if (plugin.LivingTargetSoulSalvage.Value)
            {
                Location targetLocation;
                NpcElement target;
                Collider hitCollider;
                LocationTemplate livingSpawnTemplate;
                string livingRejection;
                if (TryFindEligibleLivingTarget(
                        hero,
                        needsSpawnTemplate: true,
                        out targetLocation,
                        out target,
                        out hitCollider,
                        out livingSpawnTemplate,
                        out livingRejection))
                {
                    TryClaimLivingTarget(
                        hero,
                        targetLocation,
                        target,
                        sourceItem,
                        hitCollider,
                        livingSpawnTemplate);
                    return;
                }
                plugin.LogDiagnostic(
                    "Soul Rend heavy cast found no eligible target: corpse="
                    + corpseRejection + "; living=" + livingRejection + ".");
                if (!ShowCorpseBindingRejectionFeedback(
                        plugin,
                        corpseRejection))
                {
                    plugin.ShowSoulSalvageHeavyCastDiagnostic(
                        "targeting",
                        "Soul Rend: no eligible target - "
                        + livingRejection + ".");
                }
                return;
            }

            plugin.LogDiagnostic(
                "Soul Rend heavy cast raised nothing: " + corpseRejection);
            if (!ShowCorpseBindingRejectionFeedback(plugin, corpseRejection))
            {
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "targeting",
                    "Soul Rend: no eligible corpse - " + corpseRejection + ".");
            }
        }

        private static void ShowSpentCorpseFeedback(
            SoulAndServicePlugin plugin,
            string rejection)
        {
            if (plugin != null
                && string.Equals(
                    rejection,
                    "no Soul Vigor remains in that corpse",
                    StringComparison.Ordinal))
            {
                plugin.ShowSoulSalvageHeavyCastFeedback(
                    "soul-rend-corpse-spent",
                    "Soul Rend: no Soul Vigor remains in that corpse.");
            }
        }

        private static bool ShowCorpseBindingRejectionFeedback(
            SoulAndServicePlugin plugin,
            string rejection)
        {
            if (plugin == null
                || string.IsNullOrEmpty(rejection)
                || string.Equals(
                    rejection,
                    "no corpse was under the crosshair",
                    StringComparison.Ordinal)
                || string.Equals(
                    rejection,
                    "the line of sight was blocked before a corpse",
                    StringComparison.Ordinal)
                || string.Equals(
                    rejection,
                    "the targeted location is not a corpse",
                    StringComparison.Ordinal))
            {
                return false;
            }

            bool spent = string.Equals(
                rejection,
                "no Soul Vigor remains in that corpse",
                StringComparison.Ordinal);
            plugin.ShowSoulSalvageHeavyCastFeedback(
                spent
                    ? "soul-rend-corpse-spent-binding"
                    : "soul-rend-corpse-resistant",
                spent
                    ? "Soul Rend: no soul remains to bind."
                    : "Soul Rend: this soul is too resistant to bind.",
                warning: true);
            return true;
        }

        private static void TryClaimLivingTarget(
            Hero hero,
            Location targetLocation,
            NpcElement target,
            Item sourceItem,
            Collider hitCollider,
            LocationTemplate spawnTemplate)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || targetLocation == null
                || target == null
                || target.Health == null
                || target.HealthElement == null)
            {
                return;
            }

            if (!TryGetSummonCapacity(
                    hero,
                    plugin,
                    out int summonCount,
                    out int summonLimit))
            {
                plugin.ShowSoulSalvageHeavyCastFeedback(
                    "soul-rend-servant-limit",
                    "Soul Rend: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").",
                    warning: true);
                return;
            }

            float healthFraction = Mathf.Clamp01(target.Health.Percentage);
            string displayName = GetCorpseDisplayName(targetLocation);
            string targetId = ((Model)target).ID;
            int preparationHits = GetSoulClaimPreparationHits(targetId, target);
            float power = SoulProgressionRuntime.GetNecromanticPower();
            float quality01 = CalculateQuality01(targetLocation, target);
            Grailwright.Shared.CorpseQualityTier qualityTier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality01, true);
            float claimThreshold = CalculateSoulClaimThreshold(
                power,
                qualityTier,
                preparationHits);
            if (healthFraction > claimThreshold)
            {
                SoulProgressionRuntime.ShowSoulClaimFeedback(
                    "The soul is still too firmly bound to " + displayName + ".",
                    highPriority: false);
                plugin.LogDiagnostic(
                    "Soul Claim rejected " + displayName + "; health="
                    + (healthFraction * 100.0f).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + "% exceeds its "
                    + (claimThreshold * 100.0f).ToString(
                        "0.##",
                        CultureInfo.InvariantCulture)
                    + "% threshold; power="
                    + power.ToString("0.##", CultureInfo.InvariantCulture)
                    + "; tier=" + qualityTier
                    + "; claimPreparationHits="
                    + preparationHits.ToString(CultureInfo.InvariantCulture)
                    + ".");
                return;
            }

            string corpseFingerprint = GetCorpseFingerprint(targetLocation);
            int nativeSoulVigor =
                SoulProgressionRuntime.GetOrRollCorpseSoulVigorValue(
                    corpseFingerprint,
                    qualityTier,
                    quality01);
            int vigorCost = GetReanimationSoulVigorCost(
                nativeSoulVigor,
                power);
            if (SoulProgressionRuntime.GetSoulVigor() + 0.001f < vigorCost)
            {
                SoulProgressionRuntime.ShowInsufficientSoulVigor(vigorCost);
                return;
            }
            SoulSalvageAudioRuntime.PlayImpact(
                true,
                true,
                target.Coords);

            DamageParameters parameters = DamageParameters.Default;
            parameters.CanBeCritical = false;
            parameters.Critical = false;
            parameters.IgnoreArmor = true;
            parameters.Inevitable = true;
            parameters.IsHeavyAttack = true;
            parameters.PoiseDamage = 0.0f;
            parameters.ForceDamage = 0.0f;
            parameters.RagdollForce = 0.0f;
            parameters.DamageTypeData = new RuntimeDamageTypeData(
                DamageType.MagicalHitSource,
                DamageSubType.GenericMagical);
            parameters.Position = hitCollider == null
                ? target.Coords
                : hitCollider.ClosestPoint(target.Coords);
            Vector3 direction = target.Coords - hero.Coords;
            parameters.Direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            float lethalDamage = Math.Max(
                    1.0f,
                    Math.Max(target.Health.ModifiedValue, target.Health.UpperLimit))
                * 100.0f;
            Damage claimDamage = new Damage(
                    parameters,
                    hero,
                    target,
                    new RawDamageData(lethalDamage))
                .WithItem(sourceItem)
                .WithHitCollider(hitCollider);
            target.HealthElement.TakeDamage(claimDamage);
            if (target.IsAlive || !targetLocation.HasElement<Corpse>())
            {
                SoulProgressionRuntime.ShowSoulClaimFeedback(
                    "The soul tore loose, but the body refused your command.",
                    highPriority: false);
                plugin.LogWarning(
                    "Soul Claim executed " + displayName
                    + " but the native killing-damage path did not produce a corpse.");
                return;
            }

            SoulClaimPreparations.Remove(targetId);
            SoulProgressionRuntime.ShowSoulClaimFeedback(
                displayName + "'s soul is yours.",
                highPriority: true);
            plugin.LogDiagnostic(
                "Soul Claim succeeded against " + displayName
                + "; tier=" + qualityTier
                + "; health="
                + (healthFraction * 100.0f).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "%; threshold="
                + (claimThreshold * 100.0f).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "%. Reanimating through the protected corpse lifecycle.");
            TryRaiseCorpse(
                sourceItem,
                targetLocation,
                spawnTemplate,
                bindingAlreadyWon: true,
                summonLimitAlreadyChecked: true,
                preparedCorpseFingerprint: corpseFingerprint,
                preparedNativeSoulVigor: nativeSoulVigor,
                preparedVigorCost: vigorCost);
        }

        private static float GetSoulClaimPowerThreshold(float power)
        {
            float safePower = Mathf.Clamp(power, 0.0f, 200.0f);
            if (safePower <= 25.0f)
            {
                return Mathf.Lerp(0.00f, 0.05f, safePower / 25.0f);
            }
            if (safePower <= 50.0f)
            {
                return Mathf.Lerp(0.05f, 0.10f, (safePower - 25.0f) / 25.0f);
            }
            if (safePower <= 75.0f)
            {
                return Mathf.Lerp(0.10f, 0.15f, (safePower - 50.0f) / 25.0f);
            }
            if (safePower <= 100.0f)
            {
                return Mathf.Lerp(0.15f, 0.20f, (safePower - 75.0f) / 25.0f);
            }
            if (safePower <= 150.0f)
            {
                return Mathf.Lerp(0.20f, 0.25f, (safePower - 100.0f) / 50.0f);
            }
            return Mathf.Lerp(0.25f, 0.30f, (safePower - 150.0f) / 50.0f);
        }

        private static float GetSoulClaimQualityResistance(
            Grailwright.Shared.CorpseQualityTier tier)
        {
            switch (tier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return 0.03f;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return 0.06f;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return 0.09f;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return 0.0f;
            }
        }

        private static float CalculateSoulClaimThreshold(
            float power,
            Grailwright.Shared.CorpseQualityTier qualityTier,
            int preparationHits)
        {
            float presetAdjustment = SoulAndServicePlugin
                    .GetEffectiveBalanceTuning()
                    .SoulClaimThresholdAdjustment
                / 100.0f;
            return Mathf.Clamp(
                GetSoulClaimPowerThreshold(power)
                    + (SoulClaimThresholdBonusPerHit * Math.Min(
                        SoulClaimMaximumPreparationHits,
                        Math.Max(0, preparationHits)))
                    - GetSoulClaimQualityResistance(qualityTier)
                    + presetAdjustment,
                SoulClaimMinimumHealthThreshold,
                SoulClaimMaximumHealthThreshold);
        }

        private static bool TryGetSummonCapacity(
            Hero hero,
            SoulAndServicePlugin plugin,
            out int summonCount,
            out int summonLimit)
        {
            summonCount = 0;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                summonCount += IsReanimatedMiniboss(summon) ? 2 : 1;
            }
            summonLimit = hero.HeroStats.SummonLimit.ModifiedInt
                + SoulProgressionRuntime.GetProgressionSummonLimitBonus()
                + plugin.SummonLimitBonus.Value;
            return summonCount < summonLimit;
        }

        private static bool TryGetMinibossSummonCapacity(
            Hero hero,
            SoulAndServicePlugin plugin,
            out int occupiedSlots,
            out int summonLimit)
        {
            occupiedSlots = 0;
            bool hasActiveMiniboss = false;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                if (summon == null || summon.HasBeenDiscarded)
                {
                    continue;
                }
                ReanimationRecord record;
                bool isMiniboss = Reanimations.TryGetValue(
                        ((Model)summon).ID,
                        out record)
                    && record != null
                    && record.IsMiniboss;
                occupiedSlots += isMiniboss ? 2 : 1;
                hasActiveMiniboss |= isMiniboss;
            }
            summonLimit = hero.HeroStats.SummonLimit.ModifiedInt
                + SoulProgressionRuntime.GetProgressionSummonLimitBonus()
                + plugin.SummonLimitBonus.Value;
            return !hasActiveMiniboss
                && occupiedSlots <= Math.Max(0, summonLimit - 2);
        }

        private static int GetSoulClaimPreparationHits(
            string targetId,
            NpcElement target)
        {
            SoulClaimPreparationState state;
            if (string.IsNullOrEmpty(targetId)
                || target == null
                || !SoulClaimPreparations.TryGetValue(targetId, out state)
                || state == null
                || state.Target != target)
            {
                return 0;
            }
            return Math.Min(
                SoulClaimMaximumPreparationHits,
                Math.Max(0, state.Hits));
        }

        private static void RemoveStaleSoulClaimPreparations()
        {
            if (SoulClaimPreparations.Count == 0)
            {
                _nextSoulClaimPreparationCleanupAt = 0.0f;
                return;
            }
            float now = Time.unscaledTime;
            if (now < _nextSoulClaimPreparationCleanupAt)
            {
                return;
            }
            _nextSoulClaimPreparationCleanupAt = now
                + SoulClaimPreparationCleanupIntervalSeconds;
            SoulClaimPreparationRemovalBuffer.Clear();
            foreach (KeyValuePair<string, SoulClaimPreparationState> pair
                in SoulClaimPreparations)
            {
                NpcElement target = pair.Value == null ? null : pair.Value.Target;
                if (target == null
                    || ((Model)target).HasBeenDiscarded
                    || !target.IsAlive)
                {
                    SoulClaimPreparationRemovalBuffer.Add(pair.Key);
                }
            }
            foreach (string targetId in SoulClaimPreparationRemovalBuffer)
            {
                SoulClaimPreparations.Remove(targetId);
            }
        }

        private static RaycastHit[] GetSortedSoulTargetHits(
            Vector3 origin,
            Vector3 direction,
            out int hitCount)
        {
            hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                SoulTargetRaycastBuffer,
                SoulSalvageRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (hitCount < SoulTargetRaycastBuffer.Length)
            {
                Array.Sort(
                    SoulTargetRaycastBuffer,
                    0,
                    hitCount,
                    SoulTargetHitComparer);
                return SoulTargetRaycastBuffer;
            }

            RaycastHit[] overflowHits = Physics.RaycastAll(
                origin,
                direction,
                SoulSalvageRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(overflowHits, SoulTargetHitComparer);
            hitCount = overflowHits.Length;
            return overflowHits;
        }

        private static bool TryRaiseMiniboss(
            Item sourceItem,
            HighSoulCorpseInfo highSoul)
        {
            return highSoul.CanReanimate
                && highSoul.Corpse != null
                && highSoul.SpawnTemplate != null
                && TryRaiseCorpse(
                    sourceItem,
                    highSoul.Corpse,
                    highSoul.SpawnTemplate,
                    bindingAlreadyWon: false,
                    summonLimitAlreadyChecked: false,
                    highSoul: highSoul);
        }

        private static bool TryRaiseCorpse(
            Item sourceItem,
            Location source,
            LocationTemplate spawnTemplate,
            bool bindingAlreadyWon,
            bool summonLimitAlreadyChecked,
            string preparedCorpseFingerprint = null,
            int preparedNativeSoulVigor = 0,
            int preparedVigorCost = 0,
            HighSoulCorpseInfo? highSoul = null)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || hero == null
                || source == null
                || spawnTemplate == null)
            {
                return false;
            }

            bool isMiniboss = highSoul.HasValue;
            LocationTemplate durableMinibossTemplate = null;
            if (isMiniboss
                && !TryResolveCanonicalPersistentSpawnTemplate(
                    source,
                    spawnTemplate,
                    out durableMinibossTemplate))
            {
                plugin.LogWarning(
                    "Soul Rend rejected a miniboss whose reusable spawn template could not be resolved safely.");
                return false;
            }
            if (isMiniboss)
            {
                spawnTemplate = durableMinibossTemplate;
            }

            if (!summonLimitAlreadyChecked
                && !(isMiniboss
                    ? TryGetMinibossSummonCapacity(
                        hero,
                        plugin,
                        out int summonCount,
                        out int summonLimit)
                    : TryGetSummonCapacity(
                        hero,
                        plugin,
                        out summonCount,
                        out summonLimit)))
            {
                plugin.LogWarning(
                    "Soul Rend could not raise " + source.DebugName
                    + " because the summon limit is full.");
                plugin.ShowSoulSalvageHeavyCastFeedback(
                    "soul-rend-servant-limit",
                    "Soul Rend: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").",
                    warning: true);
                return false;
            }

            float quality01 = isMiniboss
                ? 1.0f
                : CalculateQuality01(source, null);
            Grailwright.Shared.CorpseQualityTier qualityTier =
                isMiniboss
                    ? Grailwright.Shared.CorpseQualityTier.Prime
                    : Grailwright.Shared.CorpseQualityBuckets.GetTier(
                        quality01,
                        true);
            string corpseFingerprint = string.IsNullOrEmpty(
                preparedCorpseFingerprint)
                    ? isMiniboss
                        ? highSoul.Value.Fingerprint
                        : GetCorpseFingerprint(source)
                    : preparedCorpseFingerprint;
            int nativeSoulVigor = preparedNativeSoulVigor > 0
                ? preparedNativeSoulVigor
                : isMiniboss
                    ? highSoul.Value.ExtractionValue
                    : SoulProgressionRuntime.GetOrRollCorpseSoulVigorValue(
                        corpseFingerprint,
                        qualityTier,
                        quality01);
            float reanimationPower = SoulProgressionRuntime.GetNecromanticPower();
            int vigorCost = preparedVigorCost > 0
                ? preparedVigorCost
                : isMiniboss
                    ? GetMinibossReanimationSoulVigorCost(
                        SoulProgressionRuntime.GetOrInitializeHighSoulVigorPool(
                            highSoul.Value.Fingerprint,
                            highSoul.Value.ExtractionValue),
                        highSoul.Value.ExtractionValue,
                        reanimationPower)
                    : GetReanimationSoulVigorCost(
                        nativeSoulVigor,
                        reanimationPower);
            if (SoulProgressionRuntime.GetSoulVigor() + 0.001f < vigorCost)
            {
                SoulProgressionRuntime.ShowInsufficientSoulVigor(vigorCost);
                return false;
            }
            float bindingProgress01;
            float bindingResistance;
            if (!bindingAlreadyWon)
            {
                int serviceCycle = isMiniboss
                    ? SoulProgressionRuntime.GetHighSoulServiceCycle(
                        highSoul.Value.Fingerprint,
                        highSoul.Value.ExtractionValue)
                    : 0;
                bool bindingWon = isMiniboss
                    ? SoulProgressionRuntime.ApplyHighSoulBindingAttempt(
                        highSoul.Value.Fingerprint,
                        serviceCycle,
                        MinibossBindingResistance,
                        out bindingProgress01,
                        out bindingResistance)
                    : SoulProgressionRuntime.ApplyBindingAttempt(
                        corpseFingerprint,
                        qualityTier,
                        out bindingProgress01,
                        out bindingResistance);
                SoulSalvageAudioRuntime.PlayImpact(
                    true,
                    true,
                    source.Coords);
                if (!bindingWon)
                {
                    string flavor = SoulProgressionRuntime.GetBindingFailureMessage(
                        corpseFingerprint,
                        Mathf.RoundToInt(bindingProgress01 * 1000.0f));
                    SoulProgressionRuntime.ShowBindingFailure(flavor);
                    plugin.LogDiagnostic(
                        "Soul binding resisted by " + source.DebugName
                        + "; tier=" + qualityTier
                        + "; progress="
                        + bindingProgress01.ToString("0.###", CultureInfo.InvariantCulture)
                        + "; resistance="
                        + bindingResistance.ToString("0.##", CultureInfo.InvariantCulture)
                        + "; power="
                        + SoulProgressionRuntime.GetNecromanticPower().ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                        + ".");
                    return false;
                }
            }

            if (!SoulProgressionRuntime.TrySpendSoulVigor(
                vigorCost,
                out int vigorBefore,
                out int vigorAfter))
            {
                SoulProgressionRuntime.ShowInsufficientSoulVigor(vigorCost);
                return false;
            }
            int committedVigor = vigorAfter < vigorBefore ? vigorCost : 0;

            LocationInteractability previousInteractability = source.Interactability;
            float bindingManaCost = sourceItem == null
                ? 0.0f
                : GetHeavyCastManaCost(sourceItem);
            Location raised = null;
            string summonId = string.Empty;
            try
            {
                if (!isMiniboss)
                {
                    TriggerRuntimeCorpseVisualEvent(source, "OnResurrectStarted");
                }
                raised = spawnTemplate.SpawnLocation(source.Coords, source.Rotation);
                bool persistent = plugin.PersistentServants != null
                    && plugin.PersistentServants.Value;
                ((Model)raised).MarkedNotSaved = true;
                NpcElement raisedNpc = raised.Element<NpcElement>();
                bool usedFallbackPortrait = EnsureRaisedServantPortrait(raisedNpc);
                NpcElement npc;
                _creatingRaisedServant = true;
                try
                {
                    npc = SummonUtils.InitializeSummon(
                        raised,
                        hero,
                        sourceItem,
                        0.0f,
                        0.0f,
                        null);
                }
                finally
                {
                    _creatingRaisedServant = false;
                }
                npc.AddMarkerElement<PreventExpRewardMarker>();
                raised.RemoveElementsOfType<AliveLocationDeathReward>();
                raised.RemoveElementsOfType<SearchAction>();
                raised.RemoveElementsOfType<PickpocketAction>();

                NpcHeroSummon summon = npc.Element<NpcHeroSummon>();
                summonId = ((Model)summon).ID;
                Reanimations[summonId] = new ReanimationRecord
                {
                    SourceCorpse = source,
                    SourceId = ((Model)source).ID,
                    RaisedLocation = raised,
                    RaisedNpc = npc,
                    SourceInteractability = previousInteractability,
                    SourceDisplayName = GetCorpseDisplayName(source),
                    CorpseFingerprint = corpseFingerprint,
                    Quality01 = quality01,
                    QualityTier = qualityTier,
                    BindingManaCost = bindingManaCost,
                    InvestedSoulVigor = committedVigor,
                    NativeSoulVigor = nativeSoulVigor,
                    Recovery = new ServantRecoveryLedger
                    {
                        OriginalSoulVigor = Math.Max(
                            0,
                            committedVigor + nativeSoulVigor)
                    },
                    OriginalCoords = source.Coords,
                    OriginalRotation = source.Rotation,
                    LastSafeCoords = raised.Coords,
                    LastSafeRotation = raised.Rotation,
                    SpawnTemplateGuid = persistent
                        ? spawnTemplate.GUID
                        : string.Empty,
                    IsMiniboss = isMiniboss,
                    HighSoulFingerprint = isMiniboss
                        ? highSoul.Value.Fingerprint
                        : string.Empty,
                    HighSoulExtractionValue = isMiniboss
                        ? highSoul.Value.ExtractionValue
                        : 0,
                    HighSoulServiceCycle = isMiniboss
                        ? SoulProgressionRuntime.GetHighSoulServiceCycle(
                            highSoul.Value.Fingerprint,
                            highSoul.Value.ExtractionValue)
                        : 0
                };
                if (isMiniboss
                    && !TryPrepareMinibossSourceForService(
                        source,
                        spawnTemplate,
                        out string sourceFailure))
                {
                    throw new InvalidOperationException(sourceFailure);
                }
                source.SetInteractability(LocationInteractability.Hidden);
                if (persistent)
                {
                    SavePersistentReanimation(
                        summonId,
                        Reanimations[summonId]);
                }
                PrefabPool.InstantiateAndReturn(
                    new ShareableARAssetReference(SkeletonSummonVfxKey),
                    source.Coords,
                    source.Rotation).Forget();

                npc.OnCompletelyInitialized(
                    delegate
                    {
                        try
                        {
                            if (npc.HasBeenDiscarded)
                            {
                                RestoreSourceCorpse(
                                    summonId,
                                    discardRaisedCopy: true,
                                    showDiagnostic: false);
                                return;
                            }
                            ReanimationRecord record;
                            if (!Reanimations.TryGetValue(summonId, out record))
                            {
                                return;
                            }
                            raised.RemoveElementsOfType<AliveLocationDeathReward>();
                            raised.RemoveElementsOfType<SearchAction>();
                            raised.RemoveElementsOfType<PickpocketAction>();
                            npc.RemoveElementsOfType<NpcHealthRegeneration>();
                            float qualityHealthMultiplier = record.IsMiniboss
                                ? 0.50f
                                : SoulProgressionRuntime.GetQualityHealthMultiplier(
                                    record.QualityTier);
                            if (npc.AliveStats != null
                                && npc.AliveStats.MaxHealth != null
                                && Math.Abs(qualityHealthMultiplier - 1.0f) > 0.0001f)
                            {
                                record.QualityHealthTweak = StatTweak.Multi(
                                    npc.AliveStats.MaxHealth,
                                    qualityHealthMultiplier,
                                    null,
                                    npc);
                                ((Model)record.QualityHealthTweak).MarkedNotSaved = true;
                            }
                            npc.Health.SetToFull();
                            raised.TriggerVisualScriptingEvent("OnResurrect");

                            float maximumHealth = npc.Health.UpperLimit;
                            float necromanticPower =
                                SoulProgressionRuntime.GetNecromanticPower();
                            float retainedHealthFraction = record.IsMiniboss
                                ? 1.0f
                                : SoulProgressionRuntime.RollRaisedHealthFraction(
                                    necromanticPower);
                            float retainedHealth = maximumHealth * retainedHealthFraction;
                            if (npc.Health.ModifiedValue > retainedHealth)
                            {
                                npc.Health.DecreaseBy(
                                    npc.Health.ModifiedValue - retainedHealth);
                            }
                            float exsanguinationSeverity = record.IsMiniboss
                                ? 0.0f
                                : GetBloodExsanguinationSeverity(
                                    record.SourceCorpse);
                            if (exsanguinationSeverity > 0.0001f)
                            {
                                npc.Health.DecreaseBy(
                                    npc.Health.ModifiedValue
                                    * exsanguinationSeverity);
                            }
                            float actualStartingHealthFraction = maximumHealth > 0.0001f
                                ? Mathf.Clamp01(npc.Health.ModifiedValue / maximumHealth)
                                : 0.0f;
                            plugin.LogDiagnostic(
                                "Raised a restricted runtime copy of " + source.DebugName
                                + "; quality=" + record.QualityTier
                                + " (" + record.Quality01.ToString("0.###", CultureInfo.InvariantCulture) + ")"
                                + "; maximumHealth="
                                + maximumHealth.ToString("0.##", CultureInfo.InvariantCulture)
                                + "; retainedHealth="
                                + retainedHealthFraction.ToString("0.###", CultureInfo.InvariantCulture)
                                + "; exsanguination="
                                + exsanguinationSeverity.ToString("0.###", CultureInfo.InvariantCulture)
                                + "; power="
                                + necromanticPower.ToString("0.##", CultureInfo.InvariantCulture)
                                + "; upkeep="
                                + SummonRuntime.GetUpkeepPercentPerMinute(
                                    (int)World.All<NpcHeroSummon>().Count(),
                                    SoulProgressionRuntime.GetNecromanticPower())
                                    .ToString("0.###", CultureInfo.InvariantCulture)
                                + "% max health per minute"
                                + "; portrait="
                                + (usedFallbackPortrait
                                    ? "generic-skeleton-summon"
                                    : "native")
                                + ".");
                            string outcome = "Soul Rend: raised "
                                + record.SourceDisplayName
                                + " at "
                                + (actualStartingHealthFraction * 100.0f).ToString(
                                    "0",
                                    CultureInfo.InvariantCulture)
                                + "% health (Power "
                                + necromanticPower.ToString(
                                    "0.##",
                                    CultureInfo.InvariantCulture)
                                + ")"
                                + (usedFallbackPortrait
                                    ? " (generic portrait used)"
                                    : string.Empty)
                                + ".";
                            plugin.ShowSoulSalvageHeavyCastDiagnostic(
                                "binding",
                                outcome);
                            if (record.IsMiniboss)
                            {
                                SoulProgressionRuntime.CommitHighSoulBinding(
                                    record.HighSoulFingerprint,
                                    record.HighSoulServiceCycle);
                            }
                            else
                            {
                                SoulProgressionRuntime.CommitSuccessfulBinding(
                                    record.CorpseFingerprint);
                            }
                            record.ServiceInitialized = true;
                            SavePersistentReanimation(summonId, record);
                            SoulforgedRuntime.RefreshOriginalMaximumHealth(
                                summon,
                                true);
                            ReanimationGlyphRuntime.Attach(summonId, npc);
                            SoulProgressionRuntime.ShowSoulVigorWanesAfterSpend(
                                vigorBefore,
                                vigorAfter);
                            SoulProgressionRuntime.ShowResurrection(
                                record.SourceDisplayName,
                                record.QualityTier,
                                record.InvestedSoulVigor);
                        }
                        catch (Exception exception)
                        {
                            ReanimationRecord failedRecord;
                            if (Reanimations.TryGetValue(
                                    summonId,
                                    out failedRecord)
                                && failedRecord != null)
                            {
                                failedRecord.ServiceInitialized = false;
                            }
                            RestoreSourceCorpse(
                                summonId,
                                discardRaisedCopy: true,
                                showDiagnostic: false);
                            plugin.LogWarning(
                                "Soul Rend could not finish initializing a raised servant: "
                                + exception.GetBaseException().Message);
                            plugin.ShowSoulSalvageHeavyCastFeedback(
                                "soul-rend-reanimation-failed",
                                "Soul Rend: reanimation failed - source corpse restored; see BepInEx log.",
                                warning: true);
                        }
                    });
                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(summonId)
                    && Reanimations.ContainsKey(summonId))
                {
                    RestoreSourceCorpse(
                        summonId,
                        discardRaisedCopy: true,
                        showDiagnostic: false);
                    committedVigor = 0;
                }
                else
                {
                    if (raised != null && !raised.HasBeenDiscarded)
                    {
                        try
                        {
                            raised.Discard();
                        }
                        catch (Exception discardException)
                        {
                            plugin.LogWarning(
                                "Soul Rend could not discard a failed raised copy: "
                                + discardException.GetBaseException().Message);
                        }
                    }
                    if (source != null && !source.HasBeenDiscarded)
                    {
                        try
                        {
                            source.SetInteractability(previousInteractability);
                        }
                        catch (Exception restoreException)
                        {
                            plugin.LogWarning(
                                "Soul Rend could not restore the failed source presentation: "
                                + restoreException.GetBaseException().Message);
                        }
                    }
                }
                SoulProgressionRuntime.RestoreSoulVigor(committedVigor);
                plugin.LogWarning(
                    "Soul Rend could not create a raised servant: "
                    + exception.GetBaseException().Message);
                plugin.ShowSoulSalvageHeavyCastFeedback(
                    "soul-rend-reanimation-failed",
                    "Soul Rend: reanimation failed - see BepInEx log.",
                    warning: true);
                return false;
            }
        }

        internal static bool HasEligibleRaiseAllCorpse(
            Hero hero,
            float radius)
        {
            int frame = Time.frameCount;
            if (_raiseAllEligibilityFrame == frame
                && ReferenceEquals(_raiseAllEligibilityHero, hero)
                && Math.Abs(_raiseAllEligibilityRadius - radius) < 0.001f)
            {
                return _raiseAllEligibilityResult;
            }
            _raiseAllEligibilityFrame = frame;
            _raiseAllEligibilityHero = hero;
            _raiseAllEligibilityRadius = radius;
            _raiseAllEligibilityResult = false;

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || !TryGetSummonCapacity(
                    hero,
                    plugin,
                    out int ignoredSummonCount,
                    out int ignoredSummonLimit))
            {
                return false;
            }

            float radiusSqr = Math.Max(0.0f, radius) * Math.Max(0.0f, radius);
            foreach (Location candidate in World.All<Location>())
            {
                LocationTemplate ignoredSpawnTemplate;
                string rejection;
                if (candidate != null
                    && !candidate.HasBeenDiscarded
                    && (candidate.Coords - hero.Coords).sqrMagnitude <= radiusSqr
                    && TryValidateEligibleCorpse(
                        hero,
                        candidate,
                        needsSpawnTemplate: true,
                        out ignoredSpawnTemplate,
                        out rejection))
                {
                    _raiseAllEligibilityResult = true;
                    break;
                }
            }
            return _raiseAllEligibilityResult;
        }

        internal static int RaiseAll(Hero hero, float radius)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || hero == null
                || hero.HasBeenDiscarded
                || !hero.IsAlive
                || SoulProgressionRuntime.GetNecromanticPower()
                    < SoulProgressionRuntime.RaiseAllPower
                || World.All<NpcHeroSummon>().Any(summon =>
                    summon != null
                    && !summon.HasBeenDiscarded
                    && summon.ParentModel != null
                    && !summon.ParentModel.HasBeenDiscarded
                    && summon.ParentModel.IsAlive
                    && ReferenceEquals(summon.Ally, hero)))
            {
                return 0;
            }

            if (!TryGetSummonCapacity(
                    hero,
                    plugin,
                    out int summonCount,
                    out int summonLimit))
            {
                plugin.ShowSoulSalvageHeavyCastFeedback(
                    "raise-all-servant-limit",
                    "Raise All: servant limit full ("
                    + summonCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + summonLimit.ToString(CultureInfo.InvariantCulture)
                    + ").",
                    warning: true);
                return 0;
            }

            float safeRadius = Math.Max(0.0f, radius);
            float radiusSqr = safeRadius * safeRadius;
            List<RaiseAllCandidate> candidates = new List<RaiseAllCandidate>();
            foreach (Location candidate in World.All<Location>())
            {
                LocationTemplate ignoredSpawnTemplate;
                string rejection;
                if (candidate == null
                    || candidate.HasBeenDiscarded
                    || (candidate.Coords - hero.Coords).sqrMagnitude > radiusSqr
                    || !TryValidateEligibleCorpse(
                        hero,
                        candidate,
                        needsSpawnTemplate: true,
                        out ignoredSpawnTemplate,
                        out rejection))
                {
                    continue;
                }
                candidates.Add(new RaiseAllCandidate
                {
                    Source = candidate,
                    DistanceSqr = (candidate.Coords - hero.Coords).sqrMagnitude
                });
            }
            candidates.Sort((left, right) =>
                left.DistanceSqr.CompareTo(right.DistanceSqr));

            int remainingCapacity = Math.Max(0, summonLimit - summonCount);
            int raisedCount = 0;
            foreach (RaiseAllCandidate candidate in candidates)
            {
                if (remainingCapacity <= 0)
                {
                    break;
                }

                Location source = candidate.Source;
                LocationTemplate spawnTemplate;
                string rejection;
                if (source == null
                    || source.HasBeenDiscarded
                    || !TryValidateEligibleCorpse(
                        hero,
                        source,
                        needsSpawnTemplate: true,
                        out spawnTemplate,
                        out rejection))
                {
                    continue;
                }

                float quality01 = CalculateQuality01(source, null);
                Grailwright.Shared.CorpseQualityTier qualityTier =
                    Grailwright.Shared.CorpseQualityBuckets.GetTier(
                        quality01,
                        true);
                string corpseFingerprint = GetCorpseFingerprint(source);
                int nativeSoulVigor =
                    SoulProgressionRuntime.GetOrRollCorpseSoulVigorValue(
                        corpseFingerprint,
                        qualityTier,
                        quality01);
                int vigorCost = GetReanimationSoulVigorCost(
                    nativeSoulVigor,
                    SoulProgressionRuntime.GetNecromanticPower());
                if (SoulProgressionRuntime.GetSoulVigor() + 0.001f < vigorCost)
                {
                    SoulProgressionRuntime.ShowInsufficientSoulVigor(vigorCost);
                    break;
                }

                if (!TryRaiseCorpse(
                    null,
                    source,
                    spawnTemplate,
                    bindingAlreadyWon: true,
                    summonLimitAlreadyChecked: true,
                    preparedCorpseFingerprint: corpseFingerprint,
                    preparedNativeSoulVigor: nativeSoulVigor,
                    preparedVigorCost: vigorCost))
                {
                    continue;
                }
                raisedCount++;
                remainingCapacity--;
            }

            if (raisedCount > 0)
            {
                PrefabPool.InstantiateAndReturn(
                    new ShareableARAssetReference(SkeletonSummonVfxKey),
                    hero.Coords,
                    hero.Rotation).Forget();
            }
            return raisedCount;
        }

        private static int GetReanimationSoulVigorCost(
            int nativeSoulVigor,
            float power)
        {
            return GetPowerScaledSoulVigorCost(nativeSoulVigor, power);
        }

        private static int GetMinibossReanimationSoulVigorCost(
            int remainingSoulVigor,
            int extractionValue,
            float power)
        {
            extractionValue = Math.Max(1, extractionValue);
            int remainingPortions = Mathf.Clamp(
                Mathf.CeilToInt(
                    Math.Max(0, remainingSoulVigor)
                    / (float)extractionValue),
                1,
                HighSoulPoolPortions);
            float depletionMultiplier = 1.0f
                + (0.10f * (HighSoulPoolPortions - remainingPortions));
            int pristineCost = GetPowerScaledSoulVigorCost(
                MinibossReanimationBaseSoulVigor,
                power);
            return Math.Max(
                1,
                Mathf.CeilToInt(pristineCost * depletionMultiplier));
        }

        private static string GetUnbindableHighSoulMessage(string fingerprint)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safe = fingerprint ?? string.Empty;
                for (int i = 0; i < safe.Length; i++)
                {
                    hash ^= safe[i];
                    hash *= 16777619u;
                }
                return UnbindableHighSoulMessages[
                    (int)(hash % (uint)UnbindableHighSoulMessages.Length)];
            }
        }

        private static int GetPowerScaledSoulVigorCost(int baseCost, float power)
        {
            float safePower = Mathf.Clamp(power, 0.0f, 200.0f);
            float multiplier = safePower <= 100.0f
                ? Mathf.Lerp(2.0f, 1.0f, safePower / 100.0f)
                : Mathf.Lerp(
                    1.0f,
                    0.5f,
                    (safePower - 100.0f) / 100.0f);
            return Math.Max(
                1,
                Mathf.CeilToInt(
                    Math.Max(0, baseCost)
                    * multiplier
                    * SoulAndServicePlugin.GetEffectiveBalanceTuning()
                        .SoulVigorCostMultiplier));
        }

        private static float GetBloodExsanguinationSeverity(object sourceCorpse)
        {
            Location sourceLocation = sourceCorpse as Location;
            if (sourceLocation != null)
            {
                sourceCorpse = GetBloodMagicCorpseIdentity(sourceLocation);
            }
            if (sourceCorpse == null || _bloodMagicApiUnavailable)
            {
                return 0.0f;
            }
            if (_bloodMagicGetExsanguinationSeverityMethod == null)
            {
                PluginInfo pluginInfo;
                if (!Chainloader.PluginInfos.TryGetValue(
                        BloodMagicPluginGuid,
                        out pluginInfo)
                    || pluginInfo == null
                    || pluginInfo.Instance == null)
                {
                    return 0.0f;
                }
                Type api = pluginInfo.Instance.GetType().Assembly.GetType(
                    BloodMagicApiTypeName,
                    false);
                _bloodMagicGetExsanguinationSeverityMethod = api == null
                    ? null
                    : api.GetMethod(
                        "GetCorpseExsanguinationSeverity",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(object) },
                        null);
                if (_bloodMagicGetExsanguinationSeverityMethod == null)
                {
                    _bloodMagicApiUnavailable = true;
                    return 0.0f;
                }
            }
            try
            {
                object result = _bloodMagicGetExsanguinationSeverityMethod.Invoke(
                    null,
                    new[] { sourceCorpse });
                return result is float ? Mathf.Clamp01((float)result) : 0.0f;
            }
            catch
            {
                _bloodMagicGetExsanguinationSeverityMethod = null;
                return 0.0f;
            }
        }

        private static void ReportCorpseHarvestThreatToEyes(float quality)
        {
            ResolveEyesInTheDarkBridge();
            if (_eyesInTheDarkRegisterCorpseDrainMethod == null)
            {
                return;
            }

            try
            {
                _eyesInTheDarkRegisterCorpseDrainMethod.Invoke(
                    null,
                    new object[] { Mathf.Clamp01(quality) });
            }
            catch (Exception exception)
            {
                if (!_eyesInTheDarkFailureLogged)
                {
                    _eyesInTheDarkFailureLogged = true;
                    SoulAndServicePlugin.Instance?.LogWarning(
                        "Eyes in the Dark corpse-harvest threat integration failed: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static void ResolveEyesInTheDarkBridge()
        {
            if (_eyesInTheDarkBridgeResolved)
            {
                return;
            }

            _eyesInTheDarkBridgeResolved = true;
            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    EyesInTheDarkPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                EyesInTheDarkCorpseDrainApiTypeName,
                false);
            _eyesInTheDarkRegisterCorpseDrainMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "TryRegisterCorpseDrain",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(float) },
                    null);
            if (_eyesInTheDarkRegisterCorpseDrainMethod == null
                || _eyesInTheDarkRegisterCorpseDrainMethod.ReturnType
                    != typeof(bool))
            {
                _eyesInTheDarkRegisterCorpseDrainMethod = null;
                if (!_eyesInTheDarkFailureLogged)
                {
                    _eyesInTheDarkFailureLogged = true;
                    SoulAndServicePlugin.Instance?.LogWarning(
                        "Eyes in the Dark is loaded, but its corpse-drain threat API could not be found.");
                }
            }
        }

        internal static bool TryResolveOwnedReanimatedServantForInterop(
            object candidate,
            out object sourceCorpse)
        {
            sourceCorpse = null;
            ReanimationRecord record;
            Hero hero = Hero.Current;
            if (hero == null
                || !TryResolveReanimationRecord(candidate, out record)
                || record.IsMiniboss
                || record.SourceCorpse == null
                || record.RaisedNpc == null
                || !record.RaisedNpc.IsAlive
                || (record.BloodRitualHoldUntil > Time.unscaledTime
                    && record.BloodRitualCommandSequence
                        != SummonRuntime.GetCommandSequenceForInterop()))
            {
                return false;
            }
            sourceCorpse = GetBloodMagicCorpseIdentity(record.SourceCorpse);
            return true;
        }

        internal static bool TryResolveOwnedBloodServantForInterop(
            object candidate,
            out object sourceCorpse,
            out object servantNpc)
        {
            object sourceLocation;
            object sourceCorpseElement;
            bool resolved = TryResolveOwnedBloodServantIdentityForInterop(
                candidate,
                out sourceLocation,
                out sourceCorpseElement,
                out servantNpc);
            sourceCorpse = sourceCorpseElement ?? sourceLocation;
            return resolved;
        }

        internal static bool TryResolveOwnedBloodServantIdentityForInterop(
            object candidate,
            out object sourceLocation,
            out object sourceCorpse,
            out object servantNpc)
        {
            sourceLocation = null;
            sourceCorpse = null;
            servantNpc = null;
            ReanimationRecord record;
            if (TryResolveReanimationRecord(candidate, out record))
            {
                if (record.IsMiniboss
                    || record.SourceCorpse == null
                    || record.RaisedNpc == null
                    || !record.RaisedNpc.IsAlive
                    || (record.BloodRitualHoldUntil > Time.unscaledTime
                        && record.BloodRitualCommandSequence
                            != SummonRuntime.GetCommandSequenceForInterop()))
                {
                    return false;
                }
                sourceLocation = record.SourceCorpse;
                sourceCorpse = record.SourceCorpse.TryGetElement<Corpse>();
                servantNpc = record.RaisedNpc;
                return true;
            }

            NpcHeroSummon summon;
            NpcElement npc;
            if (!TryResolveOwnedLivingSummon(candidate, out summon, out npc))
            {
                return false;
            }
            servantNpc = npc;
            return true;
        }

        private static object GetBloodMagicCorpseIdentity(Location sourceCorpse)
        {
            if (sourceCorpse == null)
            {
                return null;
            }
            Corpse corpse = sourceCorpse.TryGetElement<Corpse>();
            return corpse ?? (object)sourceCorpse;
        }

        internal static bool SetOwnedBloodServantRitualStateForInterop(
            object candidate,
            bool channeling,
            bool completed)
        {
            ReanimationRecord record;
            if (TryResolveReanimationRecord(candidate, out record))
            {
                if (record.IsMiniboss)
                {
                    return false;
                }
                return SetOwnedReanimatedServantBloodRitualStateForInterop(
                    candidate,
                    channeling,
                    completed);
            }

            NpcHeroSummon summon;
            NpcElement ignoredNpc;
            if (!TryResolveOwnedLivingSummon(
                    candidate,
                    out summon,
                    out ignoredNpc))
            {
                return false;
            }
            if (channeling && PreventSummonMovementMethod != null)
            {
                PreventSummonMovementMethod.Invoke(summon, null);
            }
            return true;
        }

        internal static bool TryExsanguinateOwnedBloodServantForInterop(
            object candidate,
            float severity,
            out bool killed)
        {
            ReanimationRecord record;
            if (TryResolveReanimationRecord(candidate, out record))
            {
                if (record.IsMiniboss)
                {
                    killed = false;
                    return false;
                }
                return TryExsanguinateOwnedReanimatedServantForInterop(
                    candidate,
                    severity,
                    out killed);
            }

            killed = false;
            NpcHeroSummon summon;
            NpcElement npc;
            if (!TryResolveOwnedLivingSummon(candidate, out summon, out npc)
                || npc.Health == null)
            {
                return false;
            }
            float maximumHealth = Math.Max(
                npc.Health.UpperLimit,
                npc.Health.ModifiedValue);
            if (maximumHealth <= 0.0f)
            {
                return false;
            }
            if (Mathf.Clamp01(npc.Health.ModifiedValue / maximumHealth) <= 0.20f)
            {
                killed = true;
                npc.HealthElement.Kill();
                return true;
            }
            npc.Health.DecreaseBy(
                npc.Health.ModifiedValue
                * Mathf.Clamp(severity, 0.20f, 0.30f));
            return true;
        }

        internal static bool TryMaterializeOwnedBloodServantCorpseForAbhartachForInterop(
            object candidate,
            out object corpseLocation)
        {
            corpseLocation = null;
            ReanimationRecord record;
            if (TryResolveReanimationRecord(candidate, out record))
            {
                if (record.IsMiniboss
                    || record.SourceCorpse == null
                    || record.SourceCorpse.HasBeenDiscarded
                    || record.SourceCorpse.TryGetElement<NpcDummy>() == null
                    || record.RaisedLocation == null
                    || record.RaisedLocation.HasBeenDiscarded
                    || record.RaisedNpc == null
                    || !record.RaisedNpc.IsAlive)
                {
                    return false;
                }

                string summonId = ((Model)record.RaisedNpc.Element<NpcHeroSummon>()).ID;
                Vector3 coords = record.RaisedLocation.Coords;
                Quaternion rotation = record.RaisedLocation.Rotation;
                ReanimationGlyphRuntime.Remove(summonId);
                Reanimations.Remove(summonId);
                ClearPersistedServant(summonId);
                record.SourceCorpse.MoveAndRotateTo(coords, rotation, true);
                record.SourceCorpse.SetInteractability(record.SourceInteractability);
                TriggerRuntimeCorpseVisualEvent(record.SourceCorpse, "OnDeath");
                if (!PendingRaisedDiscards.Contains(record.RaisedLocation))
                {
                    PendingRaisedDiscards.Add(record.RaisedLocation);
                }
                if (record.QualityHealthTweak != null
                    && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
                {
                    ((Model)record.QualityHealthTweak).Discard();
                }
                corpseLocation = record.SourceCorpse;
                return true;
            }

            NpcHeroSummon summon;
            NpcElement npc;
            if (!TryResolveOwnedLivingSummon(candidate, out summon, out npc)
                || npc.HealthElement == null)
            {
                return false;
            }
            Location location = npc.ParentModel;
            if (location == null || location.HasBeenDiscarded)
            {
                return false;
            }
            OrdinarySummonInvestments.Remove(((Model)summon).ID);
            npc.HealthElement.Kill();
            if (location.HasBeenDiscarded
                || location.TryGetElement<NpcDummy>() == null)
            {
                return false;
            }
            corpseLocation = location;
            return true;
        }

        private static bool TryResolveOwnedLivingSummon(
            object candidate,
            out NpcHeroSummon summon,
            out NpcElement npc)
        {
            summon = candidate as NpcHeroSummon;
            npc = candidate as NpcElement;
            Location location = candidate as Location;
            Component component = candidate as Component;
            if (component != null)
            {
                VLocation view = component.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
                location = view == null ? null : view.Target;
            }
            if (npc == null && location != null)
            {
                npc = location.TryGetElement<NpcElement>();
            }
            if (summon == null && npc != null)
            {
                summon = npc.TryGetElement<NpcHeroSummon>();
            }
            if (summon == null && component != null)
            {
                Hero currentHero = Hero.Current;
                foreach (NpcHeroSummon candidateSummon
                    in World.All<NpcHeroSummon>())
                {
                    NpcElement candidateNpc = candidateSummon == null
                        ? null
                        : candidateSummon.ParentModel;
                    if (candidateNpc != null
                        && ReferenceEquals(candidateSummon.Ally, currentHero)
                        && IsComponentWithinNpcVisual(component, candidateNpc))
                    {
                        summon = candidateSummon;
                        npc = candidateNpc;
                        break;
                    }
                }
            }
            Hero hero = Hero.Current;
            return hero != null
                && summon != null
                && !summon.HasBeenDiscarded
                && npc != null
                && !npc.HasBeenDiscarded
                && npc.IsAlive
                && ReferenceEquals(summon.Ally, hero);
        }

        internal static bool SetOwnedReanimatedServantBloodRitualStateForInterop(
            object candidate,
            bool channeling,
            bool completed)
        {
            ReanimationRecord record;
            if (!TryResolveReanimationRecord(candidate, out record))
            {
                return false;
            }
            if (record.IsMiniboss)
            {
                return false;
            }
            if (completed)
            {
                record.BloodRitualHoldUntil = Time.unscaledTime + 8.0f;
                SpawnBloodRitualVfx(record);
            }
            else if (channeling)
            {
                if (record.BloodRitualHoldUntil <= Time.unscaledTime)
                {
                    record.BloodRitualCommandSequence =
                        SummonRuntime.GetCommandSequenceForInterop();
                }
                record.BloodRitualHoldUntil = Time.unscaledTime + 0.35f;
            }
            else
            {
                record.BloodRitualHoldUntil = 0.0f;
            }
            record.NextBloodRitualMovementHoldAt = 0.0f;
            return true;
        }

        internal static bool TryExsanguinateOwnedReanimatedServantForInterop(
            object candidate,
            float severity,
            out bool killed)
        {
            killed = false;
            ReanimationRecord record;
            if (!TryResolveReanimationRecord(candidate, out record)
                || record.IsMiniboss
                || record.RaisedNpc == null
                || !record.RaisedNpc.IsAlive
                || record.RaisedNpc.Health == null)
            {
                return false;
            }
            float maximumHealth = Math.Max(
                record.RaisedNpc.Health.UpperLimit,
                record.RaisedNpc.Health.ModifiedValue);
            if (maximumHealth <= 0.0f)
            {
                return false;
            }
            float healthFraction = Mathf.Clamp01(
                record.RaisedNpc.Health.ModifiedValue / maximumHealth);
            record.SalvageHealthFraction = healthFraction;
            if (healthFraction <= 0.20f)
            {
                killed = true;
                record.BloodRitualExecuted = true;
                record.RaisedNpc.HealthElement.Kill();
                return true;
            }
            record.RaisedNpc.Health.DecreaseBy(
                record.RaisedNpc.Health.ModifiedValue
                * Mathf.Clamp(severity, 0.20f, 0.30f));
            return true;
        }

        private static bool TryResolveReanimationRecord(
            object candidate,
            out ReanimationRecord record)
        {
            record = null;
            if (candidate == null)
            {
                return false;
            }
            NpcHeroSummon summon = candidate as NpcHeroSummon;
            NpcElement npc = candidate as NpcElement;
            Location location = candidate as Location;
            Component component = candidate as Component;
            if (component != null)
            {
                VLocation view = component.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
                location = view == null ? null : view.Target;
                npc = location == null ? null : location.TryGetElement<NpcElement>();
                summon = npc == null ? null : npc.TryGetElement<NpcHeroSummon>();
            }
            if (summon != null
                && Reanimations.TryGetValue(((Model)summon).ID, out record))
            {
                return true;
            }
            foreach (ReanimationRecord candidateRecord in Reanimations.Values)
            {
                if (ReferenceEquals(candidateRecord.RaisedNpc, npc)
                    || ReferenceEquals(candidateRecord.RaisedLocation, location)
                    || (component != null
                        && IsComponentWithinNpcVisual(
                            component,
                            candidateRecord.RaisedNpc)))
                {
                    record = candidateRecord;
                    return true;
                }
            }
            return false;
        }

        private static bool IsComponentWithinNpcVisual(
            Component component,
            NpcElement npc)
        {
            if (component == null
                || npc == null
                || npc.Controller == null
                || npc.Controller.AlivePrefab == null)
            {
                return false;
            }
            Transform root = npc.Controller.AlivePrefab.transform;
            Transform candidate = component.transform;
            return candidate != null
                && (ReferenceEquals(candidate, root) || candidate.IsChildOf(root));
        }

        private static bool EnsureRaisedServantPortrait(NpcElement npc)
        {
            SpriteReference portrait = npc == null ? null : npc.NpcIcon;
            if (portrait == null || portrait.IsSet)
            {
                return false;
            }

            portrait.arSpriteReference =
                new ARAssetReference(GenericRaisedServantPortraitKey);
            return true;
        }

        private static string GetCorpseDisplayName(Location source)
        {
            try
            {
                string displayName = source == null ? string.Empty : source.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Replace("\r", " ").Replace("\n", " ").Trim();
                }
            }
            catch
            {
            }

            return source == null || string.IsNullOrWhiteSpace(source.DebugName)
                ? "corpse"
                : source.DebugName;
        }

        internal static string GetSummonDisplayName(NpcHeroSummon summon)
        {
            Location location = summon == null || summon.ParentModel == null
                ? null
                : summon.ParentModel.ParentModel;
            if (location != null)
            {
                return GetCorpseDisplayName(location);
            }
            return summon == null
                || summon.Item == null
                || string.IsNullOrWhiteSpace(summon.Item.DisplayName)
                    ? "Summon"
                    : summon.Item.DisplayName.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static SoulSalvageAudioTargetClass GetSoulSalvageAudioTargetClass(
            Location source,
            NpcElement npc)
        {
            object template = npc != null
                ? (object)npc.Template
                : source == null ? null : NpcTemplate.FromNpcOrDummy(source);
            object[] owners =
            {
                source,
                source == null ? null : source.Template,
                npc,
                template
            };
            bool isMonster = false;
            foreach (object owner in owners)
            {
                if (SoulAudioValueContainsAny(owner, NonHumanoidSoulAudioTerms, 0))
                {
                    isMonster = true;
                    break;
                }
            }

            NpcElement sourceNpc = npc;
            if (sourceNpc == null && source != null)
            {
                sourceNpc = source.TryGetElement<NpcElement>();
            }
            Gender gender = sourceNpc == null
                ? Gender.None
                : sourceNpc.GetGender();
            if (gender == Gender.None && source != null)
            {
                NpcDummy dummy = source.TryGetElement<NpcDummy>();
                if (dummy != null)
                {
                    gender = dummy.GetGender();
                }
            }

            if (isMonster)
            {
                return gender == Gender.Female
                    ? SoulSalvageAudioTargetClass.FemaleMonster
                    : gender == Gender.Male
                        ? SoulSalvageAudioTargetClass.MaleMonster
                        : SoulSalvageAudioTargetClass.UnknownMonster;
            }
            return gender == Gender.Female
                ? SoulSalvageAudioTargetClass.Female
                : gender == Gender.Male
                    ? SoulSalvageAudioTargetClass.Male
                    : SoulSalvageAudioTargetClass.Unknown;
        }

        private static bool SoulAudioValueContainsAny(
            object value,
            string[] terms,
            int depth)
        {
            if (value == null || depth > 2)
            {
                return false;
            }
            string text = value as string;
            if (text == null
                && (value.GetType().IsEnum
                    || value.GetType().IsPrimitive
                    || value is decimal))
            {
                text = value.ToString();
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                return terms.Any(term => text.IndexOf(
                    term,
                    StringComparison.OrdinalIgnoreCase) >= 0);
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ >= 32)
                    {
                        break;
                    }
                    if (SoulAudioValueContainsAny(item, terms, depth + 1))
                    {
                        return true;
                    }
                }
                return false;
            }
            if (depth >= 2)
            {
                return false;
            }
            string[] members =
            {
                "Name", "DisplayName", "DebugName", "TechnicalName",
                "SurfaceType", "Tags", "AbstractTypes", "_abstractTypes"
            };
            foreach (string member in members)
            {
                if (SoulAudioValueContainsAny(
                    GetMemberValue(value, member),
                    terms,
                    depth + 1))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryFindFocusedHighSoulCorpse(
            out HighSoulCorpseInfo highSoul)
        {
            highSoul = default(HighSoulCorpseInfo);
            Hero hero = Hero.Current;
            if (hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null)
            {
                return false;
            }

            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = GetSortedSoulTargetHits(
                origin,
                direction,
                out int hitCount);
            if (hitCount <= 0)
            {
                return false;
            }

            RaycastHit hit = hits[0];
            VLocation view = hit.collider == null
                ? null
                : hit.collider.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
            Location candidate = view == null ? null : view.Target;
            if (IsSoulRendAssistSurface(hit, candidate))
            {
                return TryFindNearestHighSoulCorpse(
                    hero,
                    hit.point,
                    hit.normal,
                    out highSoul);
            }
            return TryResolveHighSoulCorpse(hero, candidate, out highSoul);
        }

        private static bool TryFindFocusedHighSoulCorpseCached(
            out HighSoulCorpseInfo highSoul)
        {
            int frame = Time.frameCount;
            if (_focusedHighSoulCacheFrame != frame)
            {
                _focusedHighSoulCacheFrame = frame;
                _focusedHighSoulCacheFound = TryFindFocusedHighSoulCorpse(
                    out _focusedHighSoulCacheInfo);
            }
            highSoul = _focusedHighSoulCacheInfo;
            return _focusedHighSoulCacheFound;
        }

        private static bool TryFindNearestHighSoulCorpse(
            Hero hero,
            Vector3 impactPoint,
            Vector3 surfaceNormal,
            out HighSoulCorpseInfo highSoul)
        {
            highSoul = default(HighSoulCorpseInfo);
            int count = Physics.OverlapSphereNonAlloc(
                impactPoint,
                SoulRendAssistRadius,
                SoulRendAssistColliderBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = SoulRendAssistColliderBuffer[i];
                SoulRendAssistColliderBuffer[i] = null;
                VLocation view = collider == null
                    ? null
                    : collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (!TryResolveHighSoulCorpse(
                        hero,
                        candidate,
                        out HighSoulCorpseInfo candidateHighSoul))
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(impactPoint);
                Vector3 offset = closestPoint - impactPoint;
                if (Vector3.Dot(offset, surfaceNormal) < -0.10f)
                {
                    continue;
                }
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }
                nearestDistanceSqr = distanceSqr;
                highSoul = candidateHighSoul;
            }
            return highSoul.Corpse != null;
        }

        private static bool TryResolveHighSoulCorpse(
            Hero hero,
            Location candidate,
            out HighSoulCorpseInfo highSoul)
        {
            highSoul = default(HighSoulCorpseInfo);
            if (hero == null
                || candidate == null
                || candidate.HasBeenDiscarded
                || (!(candidate.Initializer is SceneLocationInitializer)
                    && (!(candidate.Initializer is RuntimeLocationInitializer)
                        || !IsAuthoredSourceDiscarded(((Model)candidate).ID)))
                || ((Model)candidate).IsNotSaved
                || candidate.TryGetElement<Corpse>() == null
                || PendingRaisedDiscards.Contains(candidate)
                || ExecutedServantRemains.ContainsKey(((Model)candidate).ID)
                || Reanimations.Values.Any(record =>
                    record.SourceCorpse == candidate
                    || record.RaisedLocation == candidate))
            {
                return false;
            }

            Corpse corpse = candidate.TryGetElement<Corpse>();
            NpcTemplate npcTemplate = NpcTemplate.FromNpcOrDummy(candidate);
            if (corpse == null
                || npcTemplate == null
                || npcTemplate.IsSummon
                || (npcTemplate.NpcType != NpcType.MiniBoss
                    && npcTemplate.NpcType != NpcType.Boss)
                || corpse.Faction == null
                || hero.Faction == null
                || !corpse.Faction.IsHostileTo(hero.Faction))
            {
                return false;
            }

            LocationSpec spec = candidate.Spec;
            if (spec != null
                && (spec.GetComponent<TemporaryDeathAttachment>() != null
                    || spec.GetComponent<KillPreventionAttachment>() != null
                    || spec.GetComponent<NpcKillOnSpawnAttachment>() != null))
            {
                return false;
            }

            int nativeTier = GetHighSoulNativeTier(
                npcTemplate,
                candidate.Template);
            int extractionValue = CalculateHighSoulExtractionValue(
                npcTemplate,
                candidate.Template);
            string fingerprint = GetHighSoulCorpseFingerprint(
                candidate,
                npcTemplate);
            if (string.IsNullOrEmpty(fingerprint))
            {
                return false;
            }

            LocationTemplate spawnTemplate = ResolveSoulTargetSpawnTemplate(
                candidate);
            bool canSafelySimplify = CanSafelySimplifyHighSoulCorpse(
                candidate,
                npcTemplate);
            bool canReanimate = CanReanimateMiniboss(
                candidate,
                npcTemplate,
                spawnTemplate,
                canSafelySimplify,
                out LocationTemplate durableTemplate);
            if (canReanimate)
            {
                spawnTemplate = durableTemplate;
            }
            highSoul = new HighSoulCorpseInfo
            {
                Corpse = candidate,
                NpcTemplate = npcTemplate,
                NpcType = npcTemplate.NpcType,
                NativeTier = nativeTier,
                ExtractionValue = extractionValue,
                Fingerprint = fingerprint,
                CanSafelySimplify = canSafelySimplify,
                CanReanimate = canReanimate,
                SpawnTemplate = spawnTemplate
            };
            return true;
        }

        private static bool CanReanimateMiniboss(
            Location candidate,
            NpcTemplate npcTemplate,
            LocationTemplate spawnTemplate,
            bool canSafelySimplify,
            out LocationTemplate durableTemplate)
        {
            durableTemplate = null;
            if (!canSafelySimplify
                || candidate == null
                || candidate.Initializer == null
                || (!(candidate.Initializer is SceneLocationInitializer)
                    && !(candidate.Initializer is RuntimeLocationInitializer))
                || candidate.TryGetElement<NpcDummy>() == null
                || npcTemplate == null
                || npcTemplate.NpcType != NpcType.MiniBoss
                || !ReanimatableMinibossTemplateGuids.Contains(npcTemplate.GUID)
                || spawnTemplate == null
                || string.IsNullOrEmpty(spawnTemplate.GUID))
            {
                return false;
            }

            if (!TryResolveCanonicalPersistentSpawnTemplate(
                    candidate,
                    spawnTemplate,
                    out durableTemplate))
            {
                return false;
            }

            RepetitiveNpcAttachment reusable =
                durableTemplate.GetComponent<RepetitiveNpcAttachment>();
            bool initializerSafe = candidate.Initializer is SceneLocationInitializer
                || (candidate.Initializer is RuntimeLocationInitializer
                    && IsAuthoredSourceDiscarded(((Model)candidate).ID)
                    && candidate.Template != null
                    && string.Equals(
                        candidate.Template.GUID,
                        durableTemplate.GUID,
                        StringComparison.Ordinal));
            return initializerSafe
                && reusable != null
                && reusable.NpcTemplate != null
                && string.Equals(
                    reusable.NpcTemplate.GUID,
                    npcTemplate.GUID,
                    StringComparison.Ordinal)
                && (reusable.StoryOnDeath == null
                    || !reusable.StoryOnDeath.IsSet);
        }

        private static ContextualFacts GetDiscardedPlacesFacts()
        {
            GameplayMemory memory = World.Services == null
                ? null
                : World.Services.TryGet<GameplayMemory>();
            return memory == null
                ? null
                : memory.Context(Location.DiscardedPlacesKey);
        }

        private static bool IsAuthoredSourceDiscarded(string sourceId)
        {
            ContextualFacts facts = GetDiscardedPlacesFacts();
            return facts != null
                && !string.IsNullOrEmpty(sourceId)
                && facts.Get(sourceId, false);
        }

        private static bool TryPrepareMinibossSourceForService(
            Location source,
            LocationTemplate durableTemplate,
            out string failure)
        {
            failure = string.Empty;
            if (source == null
                || source.HasBeenDiscarded
                || durableTemplate == null
                || string.IsNullOrEmpty(durableTemplate.GUID))
            {
                failure = "the canonical miniboss corpse or spawn template was unavailable";
                return false;
            }

            string sourceId = ((Model)source).ID;
            if (source.Initializer is RuntimeLocationInitializer)
            {
                if (!IsAuthoredSourceDiscarded(sourceId)
                    || source.Template == null
                    || !string.Equals(
                        source.Template.GUID,
                        durableTemplate.GUID,
                        StringComparison.Ordinal))
                {
                    failure = "the persistent miniboss corpse identity did not match its saved runtime template";
                    return false;
                }
                if (source.CurrentDomain != Domain.Gameplay)
                {
                    source.MoveToDomain(Domain.Gameplay);
                }
                if (source.CurrentDomain != Domain.Gameplay)
                {
                    failure = "the persistent miniboss corpse did not enter the gameplay domain";
                    return false;
                }
                return true;
            }
            if (!(source.Initializer is SceneLocationInitializer)
                || LocationInitializerField == null
                || string.IsNullOrEmpty(sourceId))
            {
                failure = "the scene-authored miniboss corpse could not be promoted safely";
                return false;
            }

            ContextualFacts discarded = GetDiscardedPlacesFacts();
            if (discarded == null)
            {
                failure = "the game's discarded-location memory was unavailable";
                return false;
            }

            LocationInitializer previousInitializer = source.Initializer;
            Domain previousDomain = source.CurrentDomain;
            bool hadDiscardedValue = discarded.HasValue(sourceId);
            bool previousDiscardedValue = discarded.Get(sourceId, false);
            bool initializerReplaced = false;
            try
            {
                RuntimeLocationData data = new RuntimeLocationData(
                    durableTemplate,
                    source.Coords,
                    source.Rotation,
                    source.SpecInitialScale,
                    previousInitializer.OverridenLocationPrefab,
                    source.DisplayName,
                    null,
                    previousInitializer.OverridenNewGamePlusLevel);
                RuntimeLocationInitializer runtimeInitializer =
                    new RuntimeLocationInitializer(in data);
                runtimeInitializer.PrepareSpec(source);
                LocationInitializerField.SetValue(source, runtimeInitializer);
                initializerReplaced = true;
                if (!(source.Initializer is RuntimeLocationInitializer)
                    || source.Spec == null
                    || source.Template == null
                    || !string.Equals(
                        source.Template.GUID,
                        durableTemplate.GUID,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "the runtime initializer did not retain the canonical template");
                }

                discarded.Set(sourceId, true);
                source.MoveToDomain(Domain.Gameplay);
                if (source.CurrentDomain != Domain.Gameplay)
                {
                    throw new InvalidOperationException(
                        "the promoted corpse did not enter the persistent gameplay domain");
                }
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    if (source.CurrentDomain != previousDomain)
                    {
                        source.MoveToDomain(previousDomain);
                    }
                    if (source.CurrentDomain != previousDomain)
                    {
                        throw new InvalidOperationException(
                            "the promoted corpse could not return to its original scene during rollback");
                    }
                    if (initializerReplaced)
                    {
                        LocationInitializerField.SetValue(source, previousInitializer);
                    }
                    if (hadDiscardedValue)
                    {
                        discarded.Set(sourceId, previousDiscardedValue);
                    }
                    else
                    {
                        discarded.Remove(sourceId);
                    }
                }
                catch (Exception rollbackException)
                {
                    failure = exception.GetBaseException().Message
                        + "; rollback also failed: "
                        + rollbackException.GetBaseException().Message;
                    return false;
                }
                failure = exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryReturnMinibossSourceToCurrentScene(
            Location source,
            out string failure)
        {
            failure = string.Empty;
            string sourceId = source == null ? string.Empty : ((Model)source).ID;
            if (source == null
                || source.HasBeenDiscarded
                || !(source.Initializer is RuntimeLocationInitializer)
                || !IsAuthoredSourceDiscarded(sourceId))
            {
                failure = "the persistent miniboss corpse identity was unavailable";
                return false;
            }
            SceneService sceneService = World.Services == null
                ? null
                : World.Services.TryGet<SceneService>();
            if (sceneService == null || sceneService.ActiveSceneRef == null)
            {
                failure = "the current scene domain was unavailable";
                return false;
            }
            Domain currentScene = sceneService.ActiveDomain;
            if (source.CurrentDomain != currentScene)
            {
                source.MoveToDomain(currentScene);
            }
            if (source.CurrentDomain != currentScene)
            {
                failure = "the persistent miniboss corpse did not enter the current scene";
                return false;
            }
            return true;
        }

        private static int GetHighSoulNativeTier(
            NpcTemplate npcTemplate,
            LocationTemplate fallbackTemplate)
        {
            int nativeTier;
            if (!TryReadNativeTier(npcTemplate, out nativeTier)
                && !TryReadNativeTier(fallbackTemplate, out nativeTier))
            {
                nativeTier = 1;
            }
            return Mathf.Clamp(nativeTier, 1, 7);
        }

        private static int CalculateHighSoulExtractionValue(
            NpcTemplate npcTemplate,
            LocationTemplate fallbackTemplate)
        {
            int nativeTier = GetHighSoulNativeTier(
                npcTemplate,
                fallbackTemplate);
            return npcTemplate != null
                && npcTemplate.NpcType == NpcType.Boss
                    ? Mathf.Clamp(nativeTier + 5, 6, 12)
                    : Mathf.Clamp(nativeTier + 3, 5, 10);
        }

        private static bool CanSafelySimplifyHighSoulCorpse(
            Location candidate,
            NpcTemplate npcTemplate)
        {
            if (candidate == null
                || candidate.Spec == null
                || npcTemplate == null)
            {
                return false;
            }

            NpcAttachment attachment = candidate.Spec
                .GetComponent<NpcAttachment>();
            if (attachment == null && candidate.Template != null)
            {
                attachment = candidate.Template.GetComponent<NpcAttachment>();
            }
            RepetitiveNpcAttachment repetitive =
                attachment as RepetitiveNpcAttachment;
            if (repetitive == null
                || repetitive.NpcTemplate == null
                || !string.Equals(
                    npcTemplate.GUID,
                    repetitive.NpcTemplate.GUID,
                    StringComparison.Ordinal))
            {
                return false;
            }

            CrimeReactionArchetype crimeReaction =
                npcTemplate.CrimeReactionArchetype;
            return crimeReaction != CrimeReactionArchetype.Guard
                && crimeReaction != CrimeReactionArchetype.Defender
                && crimeReaction != CrimeReactionArchetype.Vigilante
                && !candidate.HasElement<GameplayUniqueLocation>()
                && !candidate.HasElement<NpcPresence>()
                && !candidate.HasElement<NpcAlly>()
                && !candidate.HasElement<Shop>()
                && !candidate.HasElement<DialogueAction>()
                && (attachment.StoryOnDeath == null
                    || !attachment.StoryOnDeath.IsSet)
                && !HasProtectedSoulTargetAttachment(candidate.Spec);
        }

        private static string GetHighSoulCorpseFingerprint(
            Location corpse,
            NpcTemplate npcTemplate)
        {
            if (corpse == null || npcTemplate == null)
            {
                return string.Empty;
            }
            string locationId = ((Model)corpse).ID;
            return !string.IsNullOrEmpty(locationId)
                ? "high|" + npcTemplate.GUID + "|" + locationId
                : "high|" + GetCorpseFingerprint(corpse);
        }

        private static bool TryFindEligibleCorpse(
            Hero hero,
            bool needsSpawnTemplate,
            out Location source,
            out LocationTemplate spawnTemplate,
            out string rejection)
        {
            source = null;
            spawnTemplate = null;
            rejection = "no corpse was under the crosshair";
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = GetSortedSoulTargetHits(
                origin,
                direction,
                out int hitCount);
            if (hitCount > 0)
            {
                RaycastHit hit = hits[0];
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (IsSoulRendAssistSurface(hit, candidate)
                    && TryFindNearestEligibleCorpse(
                        hero,
                        hit.point,
                        hit.normal,
                        needsSpawnTemplate,
                        out source,
                        out spawnTemplate))
                {
                    rejection = string.Empty;
                    return true;
                }
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    rejection = "the line of sight was blocked before a corpse";
                    return false;
                }
                if (!TryValidateEligibleCorpse(
                        hero,
                        candidate,
                        needsSpawnTemplate,
                        out spawnTemplate,
                        out rejection))
                {
                    return false;
                }
                source = candidate;
                rejection = string.Empty;
                return true;
            }
            return false;
        }

        private static bool IsSoulRendAssistSurface(
            RaycastHit hit,
            Location candidate)
        {
            if (hit.collider == null)
            {
                return false;
            }
            if (candidate == null || candidate.HasBeenDiscarded)
            {
                return true;
            }
            return candidate.TryGetElement<Corpse>() == null
                && candidate.TryGetElement<NpcElement>() == null;
        }

        private static bool TryFindNearestEligibleCorpse(
            Hero hero,
            Vector3 impactPoint,
            Vector3 surfaceNormal,
            bool needsSpawnTemplate,
            out Location source,
            out LocationTemplate spawnTemplate)
        {
            source = null;
            spawnTemplate = null;
            int count = Physics.OverlapSphereNonAlloc(
                impactPoint,
                SoulRendAssistRadius,
                SoulRendAssistColliderBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = SoulRendAssistColliderBuffer[i];
                SoulRendAssistColliderBuffer[i] = null;
                VLocation view = collider == null
                    ? null
                    : collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                LocationTemplate candidateSpawnTemplate;
                string rejection;
                if (candidate == null
                    || candidate.HasBeenDiscarded
                    || !TryValidateEligibleCorpse(
                        hero,
                        candidate,
                        needsSpawnTemplate,
                        out candidateSpawnTemplate,
                        out rejection))
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(impactPoint);
                Vector3 offset = closestPoint - impactPoint;
                if (Vector3.Dot(offset, surfaceNormal) < -0.10f)
                {
                    continue;
                }
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }
                nearestDistanceSqr = distanceSqr;
                source = candidate;
                spawnTemplate = candidateSpawnTemplate;
            }
            return source != null;
        }

        private static bool TryFindEligibleLivingTarget(
            Hero hero,
            bool needsSpawnTemplate,
            out Location source,
            out NpcElement npc,
            out Collider hitCollider,
            out LocationTemplate spawnTemplate,
            out string rejection)
        {
            source = null;
            npc = null;
            hitCollider = null;
            spawnTemplate = null;
            rejection = "no living enemy was under the crosshair";
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = GetSortedSoulTargetHits(
                origin,
                direction,
                out int hitCount);
            if (hitCount > 0)
            {
                RaycastHit hit = hits[0];
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    rejection = "the line of sight was blocked before a living enemy";
                    return false;
                }
                if (!TryValidateEligibleLivingTarget(
                        hero,
                        candidate,
                        needsSpawnTemplate,
                        out NpcElement candidateNpc,
                        out spawnTemplate,
                        out rejection))
                {
                    return false;
                }
                source = candidate;
                npc = candidateNpc;
                hitCollider = hit.collider;
                rejection = string.Empty;
                return true;
            }
            return false;
        }

        private static bool TryValidateEligibleLivingTarget(
            Hero hero,
            Location candidate,
            bool needsSpawnTemplate,
            out NpcElement npc,
            out LocationTemplate spawnTemplate,
            out string rejection)
        {
            spawnTemplate = null;
            npc = candidate == null ? null : candidate.TryGetElement<NpcElement>();
            if (npc == null || !npc.IsAlive || npc.HealthElement == null)
            {
                rejection = "the targeted location is not a living enemy";
                return false;
            }
            if (npc.IsSummon || npc.HasElement<NpcHeroSummon>())
            {
                rejection = "living summons use Soul Rend's sacrifice effect";
                return false;
            }
            if (!TryResolveEligibleSoulTargetIdentity(
                    candidate,
                    npc,
                    needsSpawnTemplate,
                    out spawnTemplate,
                    out rejection))
            {
                return false;
            }
            if (!npc.IsHostileTo(hero))
            {
                rejection = "only ordinary hostile living enemies are eligible";
                return false;
            }
            rejection = string.Empty;
            return true;
        }

        private static bool TryValidateEligibleCorpse(
            Hero hero,
            Location candidate,
            bool needsSpawnTemplate,
            out LocationTemplate spawnTemplate,
            out string rejection)
        {
            spawnTemplate = null;
            Corpse corpse;
            if (!candidate.TryGetElement<Corpse>(out corpse) || corpse == null)
            {
                rejection = "the targeted location is not a corpse";
                return false;
            }
            if (ExecutedServantRemains.ContainsKey(((Model)candidate).ID))
            {
                spawnTemplate = needsSpawnTemplate
                    ? ResolveSoulTargetSpawnTemplate(candidate)
                    : null;
                if (needsSpawnTemplate && spawnTemplate == null)
                {
                    rejection = "that servant's remains have no reusable summon data";
                    return false;
                }
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                bool persistent = plugin != null
                    && plugin.PersistentServants != null
                    && plugin.PersistentServants.Value;
                LocationTemplate canonicalTemplate = null;
                if (needsSpawnTemplate
                    && persistent
                    && (((Model)candidate).IsNotSaved
                        || !TryResolveCanonicalPersistentSpawnTemplate(
                            candidate,
                            spawnTemplate,
                            out canonicalTemplate)))
                {
                    rejection = "that servant's remains cannot be preserved durably";
                    return false;
                }
                if (needsSpawnTemplate && persistent)
                {
                    spawnTemplate = canonicalTemplate;
                }
                rejection = string.Empty;
                return true;
            }
            if (Reanimations.Values.Any(record => record.SourceCorpse == candidate))
            {
                rejection = "that corpse is already serving";
                return false;
            }
            string harvestIdentity = GetCorpseHarvestIdentity(candidate);
            if (SoulProgressionRuntime.IsCorpseHarvested(harvestIdentity))
            {
                rejection = "no Soul Vigor remains in that corpse";
                return false;
            }
            if (!needsSpawnTemplate
                && IsCurrentSessionCorpseHarvestEligible(corpse))
            {
                rejection = string.Empty;
                return true;
            }
            if (!TryResolveEligibleSoulTargetIdentity(
                    candidate,
                    null,
                    needsSpawnTemplate,
                    out spawnTemplate,
                    out rejection))
            {
                return false;
            }
            if (corpse.Faction == null
                || hero.Faction == null
                || !corpse.Faction.IsHostileTo(hero.Faction))
            {
                rejection = "only ordinary hostile corpses are eligible";
                return false;
            }
            rejection = string.Empty;
            return true;
        }

        private static bool IsCurrentSessionCorpseHarvestEligible(Corpse corpse)
        {
            CurrentSessionCorpseState state;
            return corpse != null
                && CurrentSessionCorpses.TryGetValue(corpse, out state)
                && state != null
                && !state.IsSummon;
        }

        private static bool CanSafelySimplifyOrdinaryCorpse(Location candidate)
        {
            LocationTemplate ignoredSpawnTemplate;
            string ignoredRejection;
            return TryResolveEligibleSoulTargetIdentity(
                candidate,
                null,
                needsSpawnTemplate: false,
                out ignoredSpawnTemplate,
                out ignoredRejection);
        }

        private static bool TryResolveEligibleSoulTargetIdentity(
            Location candidate,
            NpcElement livingNpc,
            bool needsSpawnTemplate,
            out LocationTemplate spawnTemplate,
            out string rejection)
        {
            spawnTemplate = null;
            if (candidate == null || candidate.Spec == null)
            {
                rejection = "that target has no stable NPC source data";
                return false;
            }

            NpcAttachment attachment = candidate.Spec.GetComponent<NpcAttachment>();
            if (attachment == null && candidate.Template != null)
            {
                attachment = candidate.Template.GetComponent<NpcAttachment>();
            }
            if (!(attachment is RepetitiveNpcAttachment))
            {
                rejection = "named and unique NPC identities are protected";
                return false;
            }

            NpcTemplate npcTemplate = livingNpc == null
                ? NpcTemplate.FromNpcOrDummy(candidate)
                : livingNpc.Template ?? NpcTemplate.FromNpcOrDummy(candidate);
            NpcTemplate attachmentTemplate = attachment.NpcTemplate;
            if (npcTemplate == null
                || attachmentTemplate == null
                || !string.Equals(
                    npcTemplate.GUID,
                    attachmentTemplate.GUID,
                    StringComparison.Ordinal))
            {
                rejection = "that target's NPC identity could not be matched safely";
                return false;
            }

            switch (npcTemplate.NpcType)
            {
                case NpcType.Critter:
                case NpcType.Trash:
                case NpcType.Normal:
                case NpcType.Elite:
                    break;
                default:
                    rejection = "bosses, minibosses, and summons are protected";
                    return false;
            }

            CrimeReactionArchetype crimeReaction =
                npcTemplate.CrimeReactionArchetype;
            if (crimeReaction == CrimeReactionArchetype.Guard
                || crimeReaction == CrimeReactionArchetype.Defender
                || crimeReaction == CrimeReactionArchetype.Vigilante)
            {
                rejection = "guards and protected settlement defenders are ineligible";
                return false;
            }

            if ((livingNpc != null
                    && (livingNpc.IsUnique
                        || livingNpc.StoryOnDeath != null
                        || livingNpc.NpcPresence != null
                        || livingNpc.HasElement<NpcAlly>()))
                || candidate.HasElement<GameplayUniqueLocation>()
                || candidate.HasElement<NpcPresence>()
                || candidate.HasElement<Shop>()
                || candidate.HasElement<DialogueAction>()
                || (attachment.StoryOnDeath != null
                    && attachment.StoryOnDeath.IsSet)
                || HasProtectedSoulTargetAttachment(candidate.Spec))
            {
                rejection = "quest, scripted, merchant, guard, and companion NPCs are protected";
                return false;
            }

            if (needsSpawnTemplate)
            {
                spawnTemplate = ResolveSoulTargetSpawnTemplate(candidate);
                if (spawnTemplate == null)
                {
                    rejection = "that target has no reusable summon data";
                    return false;
                }
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                bool persistent = plugin != null
                    && plugin.PersistentServants != null
                    && plugin.PersistentServants.Value;
                if (persistent && ((Model)candidate).IsNotSaved)
                {
                    rejection = "that target cannot preserve its source safely";
                    return false;
                }
                LocationTemplate canonicalTemplate = null;
                if (persistent
                    && !TryResolveCanonicalPersistentSpawnTemplate(
                        candidate,
                        spawnTemplate,
                        out canonicalTemplate))
                {
                    rejection = "that target has no durable summon template";
                    return false;
                }
                if (persistent)
                {
                    spawnTemplate = canonicalTemplate;
                }
            }
            rejection = string.Empty;
            return true;
        }

        private static bool HasProtectedSoulTargetAttachment(LocationSpec spec)
        {
            return spec != null
                && (spec.GetComponent<NpcPresenceAttachment>() != null
                    || spec.GetComponent<ShopAttachment>() != null
                    || spec.GetComponent<DialogueAttachment>() != null
                    || spec.GetComponent<TemporaryDeathAttachment>() != null
                    || spec.GetComponent<KillPreventionAttachment>() != null
                    || spec.GetComponent<NpcKillOnSpawnAttachment>() != null);
        }

        private static LocationTemplate ResolveSoulTargetSpawnTemplate(
            Location candidate)
        {
            return candidate == null
                ? null
                : candidate.Template
                     ?? candidate.Spec?.GetComponent<LocationTemplate>();
        }

        private static bool TryResolveCanonicalPersistentSpawnTemplate(
            Location identitySource,
            LocationTemplate immediateTemplate,
            out LocationTemplate canonicalTemplate)
        {
            canonicalTemplate = null;
            TemplatesProvider templates = World.Services == null
                ? null
                : World.Services.TryGet<TemplatesProvider>();
            if (templates == null || !templates.AllLoaded)
            {
                return false;
            }
            if (immediateTemplate != null
                && !string.IsNullOrEmpty(immediateTemplate.GUID))
            {
                LocationTemplate loaded =
                    templates.Get<LocationTemplate>(immediateTemplate.GUID);
                if (IsMatchingPersistentSource(identitySource, loaded))
                {
                    canonicalTemplate = loaded;
                    return true;
                }
            }

            NpcTemplate npcTemplate = identitySource == null
                ? null
                : NpcTemplate.FromNpcOrDummy(identitySource);
            if (npcTemplate == null || string.IsNullOrEmpty(npcTemplate.GUID))
            {
                return false;
            }
            string preferredName = immediateTemplate == null
                ? identitySource.Spec?.name
                : immediateTemplate.name;
            List<LocationTemplate> matches = templates
                .GetAllOfType<LocationTemplate>()
                .Where(template =>
                {
                    if (template == null || string.IsNullOrEmpty(template.GUID))
                    {
                        return false;
                    }
                    RepetitiveNpcAttachment attachment =
                        template.GetComponent<RepetitiveNpcAttachment>();
                    return attachment != null
                        && attachment.NpcTemplate != null
                        && string.Equals(
                            attachment.NpcTemplate.GUID,
                            npcTemplate.GUID,
                            StringComparison.Ordinal);
                })
                .ToList();
            if (!string.IsNullOrEmpty(preferredName))
            {
                List<LocationTemplate> named = matches.Where(template =>
                    string.Equals(
                        template.name,
                        preferredName,
                        StringComparison.Ordinal)).ToList();
                if (named.Count == 1)
                {
                    canonicalTemplate = named[0];
                    return true;
                }
            }
            if (matches.Count == 1)
            {
                canonicalTemplate = matches[0];
                return true;
            }
            return false;
        }

        private static void TriggerRuntimeCorpseVisualEvent(
            Location source,
            string eventName)
        {
            if (source != null
                && source.Initializer is RuntimeLocationInitializer)
            {
                source.TriggerVisualScriptingEvent(eventName);
            }
        }

        private static void UpdateReanimationPositions()
        {
            if (Time.unscaledTime < _nextReanimationPositionRefreshTime)
            {
                return;
            }
            _nextReanimationPositionRefreshTime = Time.unscaledTime
                + ReanimationPositionRefreshSeconds;
            foreach (ReanimationRecord record in Reanimations.Values)
            {
                Location raised = record.RaisedLocation;
                if (raised == null || raised.HasBeenDiscarded)
                {
                    continue;
                }
                Vector3 coords = raised.Coords;
                if (float.IsNaN(coords.x)
                    || float.IsNaN(coords.y)
                    || float.IsNaN(coords.z)
                    || float.IsInfinity(coords.x)
                    || float.IsInfinity(coords.y)
                    || float.IsInfinity(coords.z))
                {
                    continue;
                }
                record.LastSafeCoords = coords;
                record.LastSafeRotation = raised.Rotation;
                if (record.BloodRitualHoldUntil > Time.unscaledTime
                    && Time.unscaledTime >= record.NextBloodRitualMovementHoldAt
                    && record.RaisedNpc != null
                    && record.RaisedNpc.IsAlive
                    && PreventSummonMovementMethod != null)
                {
                    record.NextBloodRitualMovementHoldAt = Time.unscaledTime + 1.0f;
                    PreventSummonMovementMethod.Invoke(
                        record.RaisedNpc.Element<NpcHeroSummon>(),
                        null);
                }
            }
        }

        private static void EndServiceAsRemains(
            string summonId,
            bool showDiagnostic)
        {
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                return;
            }
            if (record.IsMiniboss)
            {
                EndMinibossService(summonId, record, showDiagnostic);
                return;
            }

            SoulProgressionRuntime.CorpseHarvestReceipt harvestReceipt = null;
            string failure = string.Empty;
            int salvageAward = Mathf.Clamp(
                Mathf.RoundToInt(
                    (record.NativeSoulVigor + record.InvestedSoulVigor)
                    * Mathf.Clamp01(record.SalvageHealthFraction)),
                0,
                record.NativeSoulVigor + record.InvestedSoulVigor);
            bool harvestReady = !record.Sacrificed
                || SoulProgressionRuntime.TryHarvestCorpse(
                    record.CorpseFingerprint,
                    record.QualityTier,
                    salvageAward,
                    out harvestReceipt);
            bool sourceDeferred = record.SourceCorpse == null
                || record.SourceCorpse.HasBeenDiscarded;
            bool simplified = harvestReady && TryCreateRemains(
                    record.SourceCorpse,
                    record.LastSafeCoords,
                    record.LastSafeRotation,
                    out failure);
            if (!harvestReady)
            {
                failure = "Soul Vigor could not be saved";
            }
            if (!simplified
                && harvestReady
                && (record.LastSafeCoords - record.OriginalCoords).sqrMagnitude
                    > 0.01f)
            {
                simplified = TryCreateRemains(
                    record.SourceCorpse,
                    record.OriginalCoords,
                    record.OriginalRotation,
                    out failure);
            }
            if (!simplified && harvestReceipt != null && !sourceDeferred)
            {
                SoulProgressionRuntime.RollbackCorpseHarvest(harvestReceipt);
            }
            if (sourceDeferred)
            {
                ScheduleDeferredSourceRestoration(record);
            }

            ReanimationGlyphRuntime.Remove(summonId);
            Reanimations.Remove(summonId);
            ClearPersistedServant(summonId);
            if (!simplified
                && record.SourceCorpse != null
                && !record.SourceCorpse.HasBeenDiscarded)
            {
                record.SourceCorpse.SetInteractability(
                    record.SourceInteractability);
                TriggerRuntimeCorpseVisualEvent(record.SourceCorpse, "OnDeath");
            }
            if (record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
            if (record.QualityHealthTweak != null
                && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
            {
                ((Model)record.QualityHealthTweak).Discard();
            }

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                if (simplified)
                {
                    if (harvestReceipt != null)
                    {
                        SoulProgressionRuntime.ShowSoulVigorHarvest(
                            record.SourceDisplayName,
                            record.QualityTier,
                            harvestReceipt.Award,
                            record.ManaReturnedOnSacrifice);
                        SoulProgressionRuntime.ShowCommandUnlocksAfterCorpseHarvest(
                            harvestReceipt);
                    }
                    plugin.LogDiagnostic(
                        "Soul Rend ended " + record.SourceDisplayName
                        + "'s service as simplified remains at its last position.");
                    if (showDiagnostic)
                    {
                        plugin.ShowSoulSalvageHeavyCastDiagnostic(
                            "lifecycle",
                            "Soul Rend: " + record.SourceDisplayName
                            + "'s service ended; remains were left behind.");
                    }
                }
                else if (sourceDeferred)
                {
                    if (harvestReceipt != null)
                    {
                        SoulProgressionRuntime.ShowSoulVigorHarvest(
                            record.SourceDisplayName,
                            record.QualityTier,
                            harvestReceipt.Award,
                            record.ManaReturnedOnSacrifice);
                        SoulProgressionRuntime.ShowCommandUnlocksAfterCorpseHarvest(
                            harvestReceipt);
                    }
                    plugin.LogWarning(
                        "Soul Rend ended " + record.SourceDisplayName
                        + "'s service while its source scene was unavailable; "
                        + "the original corpse will be restored when that scene loads.");
                }
                else
                {
                    plugin.LogWarning(
                        "Soul Rend could not create remains for "
                        + record.SourceDisplayName + ": " + failure
                        + ". The original corpse was restored.");
                }
            }
        }

        private static void EndMinibossService(
            string summonId,
            ReanimationRecord record,
            bool showDiagnostic)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            SoulProgressionRuntime.HighSoulDrainReceipt depletionReceipt = null;
            bool depleted = SoulProgressionRuntime
                .TryConsumeHighSoulServicePortion(
                    record.HighSoulFingerprint,
                    record.HighSoulExtractionValue,
                    out depletionReceipt);
            Location source = record.SourceCorpse;
            string failure = string.Empty;
            bool terminalRemains = false;
            bool sourceRestored = false;
            int remaining = -1;

            try
            {
                if (!depleted)
                {
                    failure = "the saved soul pool was unavailable or inconsistent";
                }
                else if (source == null || source.HasBeenDiscarded)
                {
                    failure = "the canonical corpse was unavailable";
                }
                else if (!SoulProgressionRuntime.TryGetExistingHighSoulVigorPool(
                        record.HighSoulFingerprint,
                        record.HighSoulExtractionValue,
                        out remaining))
                {
                    failure = "the saved soul pool became inconsistent";
                }
                else
                {
                    source.MoveAndRotateTo(
                        record.LastSafeCoords,
                        record.LastSafeRotation,
                        teleport: true);
                    if (!TryReturnMinibossSourceToCurrentScene(
                            source,
                            out failure))
                    {
                        sourceRestored = false;
                    }
                    else if (remaining <= 0)
                    {
                        terminalRemains = TryCreateRemains(
                            source,
                            record.LastSafeCoords,
                            record.LastSafeRotation,
                            out failure);
                    }
                    if (!terminalRemains)
                    {
                        if (string.IsNullOrEmpty(failure))
                        {
                            source.SetInteractability(record.SourceInteractability);
                            sourceRestored = true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception.GetBaseException().Message;
            }

            bool serviceCommitted = depleted
                && (terminalRemains
                    || (sourceRestored && string.IsNullOrEmpty(failure)));
            if (!serviceCommitted && depletionReceipt != null)
            {
                SoulProgressionRuntime.RollbackHighSoulDrain(depletionReceipt);
                SoulProgressionRuntime.TryGetExistingHighSoulVigorPool(
                    record.HighSoulFingerprint,
                    record.HighSoulExtractionValue,
                    out remaining);
            }
            if (!terminalRemains && !sourceRestored)
            {
                try
                {
                    if (source != null && !source.HasBeenDiscarded)
                    {
                        if (!TryReturnMinibossSourceToCurrentScene(
                                source,
                                out string returnFailure))
                        {
                            failure = string.IsNullOrEmpty(failure)
                                ? returnFailure
                                : failure;
                        }
                        else
                        {
                            source.SetInteractability(record.SourceInteractability);
                            sourceRestored = true;
                        }
                    }
                }
                catch (Exception exception)
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? exception.GetBaseException().Message
                        : failure;
                }
                if (!sourceRestored)
                {
                    try
                    {
                        ScheduleDeferredSourceRestoration(record);
                    }
                    catch (Exception exception)
                    {
                        plugin?.LogWarning(
                            "Soul Rend could not record deferred miniboss corpse recovery: "
                            + exception.GetBaseException().Message);
                    }
                }
            }

            try
            {
                ReanimationGlyphRuntime.Remove(summonId);
            }
            catch (Exception exception)
            {
                plugin?.LogWarning(
                    "Soul Rend could not remove a miniboss servant glyph: "
                    + exception.GetBaseException().Message);
            }
            Reanimations.Remove(summonId);
            try
            {
                ClearPersistedServant(summonId);
            }
            catch (Exception exception)
            {
                plugin?.LogWarning(
                    "Soul Rend could not clear a completed miniboss snapshot immediately: "
                    + exception.GetBaseException().Message);
            }
            if (record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
            if (record.QualityHealthTweak != null
                && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
            {
                try
                {
                    ((Model)record.QualityHealthTweak).Discard();
                }
                catch (Exception exception)
                {
                    plugin?.LogWarning(
                        "Soul Rend could not discard a completed miniboss health modifier: "
                        + exception.GetBaseException().Message);
                }
            }

            if (plugin == null)
            {
                return;
            }
            if (terminalRemains)
            {
                plugin.LogDiagnostic(
                    "Miniboss service exhausted " + record.SourceDisplayName
                    + " and left one loot-preserving set of simplified remains.");
            }
            else if (serviceCommitted)
            {
                plugin.LogDiagnostic(
                    "Miniboss service ended for " + record.SourceDisplayName
                    + "; remaining Soul Vigor="
                    + remaining.ToString(CultureInfo.InvariantCulture)
                    + ". Canonical full corpse restored at the servant's last position.");
            }
            else
            {
                plugin.LogWarning(
                    "Miniboss service ended without consuming a soul portion: "
                    + failure + ". The canonical corpse was retained for recovery.");
            }
            if (showDiagnostic)
            {
                plugin.ShowSoulSalvageHeavyCastDiagnostic(
                    "lifecycle",
                    terminalRemains
                        ? "Soul Rend: the miniboss soul is exhausted; remains were left behind."
                        : remaining >= 0
                            ? "Soul Rend: miniboss service ended; "
                                + remaining.ToString(CultureInfo.InvariantCulture)
                                + " Soul Vigor remains."
                            : "Soul Rend: miniboss service ended; its soul portion was retained for recovery.");
            }
        }

        private static bool TryCreateRemains(
            Location source,
            Vector3 coords,
            Quaternion rotation,
            out string failure)
        {
            failure = string.Empty;
            if (source == null || source.HasBeenDiscarded)
            {
                failure = "the source corpse was unavailable";
                return false;
            }
            NpcDummy dummy = source.TryGetElement<NpcDummy>();
            GameConstants constants = World.Services == null
                ? null
                : World.Services.TryGet<GameConstants>();
            if (dummy == null || constants == null
                || constants.DefaultDeadBodyReplacedPrefab == null)
            {
                failure = "the native corpse replacement data was unavailable";
                return false;
            }

            Location remains = null;
            try
            {
                List<ItemSpawningDataRuntime> items =
                    new List<ItemSpawningDataRuntime>();
                SearchAction search = source.TryGetElement<SearchAction>();
                if (search != null)
                {
                    search.GetAllItems(items);
                }
                ShareableARAssetReference replacement =
                    SimplifiedDeadBodyReplacementField == null
                        ? null
                        : SimplifiedDeadBodyReplacementField.GetValue(dummy)
                            as ShareableARAssetReference;
                ARAssetReference visual = replacement != null && replacement.IsSet
                    ? replacement.Get()
                    : constants.DefaultDeadBodyReplacedVisualPrefab;
                remains = constants.DefaultDeadBodyReplacedPrefab.SpawnLocation(
                    coords,
                    Quaternion.Euler(0.0f, rotation.eulerAngles.y, 0.0f),
                    Vector3.one,
                    visual,
                    source.DisplayName);
                if (remains == null || remains.HasBeenDiscarded)
                {
                    failure = "the native remains prefab did not spawn";
                    return false;
                }
                remains.AddElement(new SearchAction(items, true));
                if (dummy.Template != null
                    && dummy.Template.corpseVFX != null
                    && dummy.Template.corpseVFX.IsSet)
                {
                    remains.AddElement(new DeadBodyMarkerVFX(dummy.Template.corpseVFX));
                }
                IWithFaction faction = source.TryGetElement<IWithFaction>();
                if (faction != null && faction.Faction != null)
                {
                    remains.AddElement(
                        new SimpleFactionProvider(faction.Faction.Template));
                }
                remains.AddElement<DiscardReplacementBodyElement>();
                source.Discard();
                return true;
            }
            catch (Exception exception)
            {
                if (remains != null && !remains.HasBeenDiscarded)
                {
                    remains.Discard();
                }
                failure = exception.GetBaseException().Message;
                return false;
            }
        }

        private static void RestoreSourceCorpse(
            string summonId,
            bool discardRaisedCopy,
            bool showDiagnostic)
        {
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(summonId, out record))
            {
                return;
            }
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            try
            {
                ReanimationGlyphRuntime.Remove(summonId);
            }
            catch (Exception exception)
            {
                plugin?.LogWarning(
                    "Soul Rend could not remove a failed servant glyph: "
                    + exception.GetBaseException().Message);
            }
            Reanimations.Remove(summonId);
            try
            {
                ClearPersistedServant(summonId);
            }
            catch (Exception exception)
            {
                plugin?.LogWarning(
                    "Soul Rend could not clear a failed servant snapshot immediately: "
                    + exception.GetBaseException().Message);
            }
            if (!record.ServiceInitialized && record.InvestedSoulVigor > 0)
            {
                try
                {
                    SoulProgressionRuntime.RestoreSoulVigor(
                        record.InvestedSoulVigor);
                }
                catch (Exception exception)
                {
                    plugin?.LogWarning(
                        "Soul Rend could not refund failed reanimation Vigor: "
                        + exception.GetBaseException().Message);
                }
            }
            bool sourceRestored = false;
            if (record.SourceCorpse != null && !record.SourceCorpse.HasBeenDiscarded)
            {
                try
                {
                    if (record.IsMiniboss
                        && !TryReturnMinibossSourceToCurrentScene(
                            record.SourceCorpse,
                            out string returnFailure))
                    {
                        throw new InvalidOperationException(returnFailure);
                    }
                    record.SourceCorpse.SetInteractability(
                        record.SourceInteractability);
                    if (!record.IsMiniboss)
                    {
                        TriggerRuntimeCorpseVisualEvent(
                            record.SourceCorpse,
                            "OnDeath");
                    }
                    sourceRestored = true;
                }
                catch (Exception exception)
                {
                    plugin?.LogWarning(
                        "Soul Rend could not restore a source corpse immediately: "
                        + exception.GetBaseException().Message);
                }
            }
            if (!sourceRestored)
            {
                try
                {
                    ScheduleDeferredSourceRestoration(record);
                }
                catch (Exception exception)
                {
                    plugin?.LogWarning(
                        "Soul Rend could not record deferred source restoration: "
                        + exception.GetBaseException().Message);
                }
            }
            else if (plugin != null)
            {
                plugin.LogDiagnostic("Soul Rend restored the source corpse.");
                if (showDiagnostic)
                {
                    plugin.ShowSoulSalvageHeavyCastDiagnostic(
                        "lifecycle",
                        "Soul Rend: " + record.SourceDisplayName
                        + "'s service ended; source corpse restored.");
                }
            }
            if (discardRaisedCopy
                && record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
            if (record.QualityHealthTweak != null
                && !((Model)record.QualityHealthTweak).HasBeenDiscarded)
            {
                try
                {
                    ((Model)record.QualityHealthTweak).Discard();
                }
                catch (Exception exception)
                {
                    plugin?.LogWarning(
                        "Soul Rend could not discard a failed health modifier: "
                        + exception.GetBaseException().Message);
                }
            }
        }

        private static void RestoreExecutedServantCorpse(
            ReanimationRecord record)
        {
            if (record == null)
            {
                return;
            }
            if (record.SourceCorpse != null && !record.SourceCorpse.HasBeenDiscarded)
            {
                if (record.IsMiniboss
                    && !TryReturnMinibossSourceToCurrentScene(
                        record.SourceCorpse,
                        out string returnFailure))
                {
                    throw new InvalidOperationException(returnFailure);
                }
                record.SourceCorpse.SetInteractability(record.SourceInteractability);
                if (!record.IsMiniboss)
                {
                    TriggerRuntimeCorpseVisualEvent(record.SourceCorpse, "OnDeath");
                }
            }
            else
            {
                ScheduleDeferredSourceRestoration(record);
            }
            if (record.RaisedLocation != null
                && !record.RaisedLocation.HasBeenDiscarded
                && !PendingRaisedDiscards.Contains(record.RaisedLocation))
            {
                PendingRaisedDiscards.Add(record.RaisedLocation);
            }
        }

        private static void UpdateExecutedServantRemains()
        {
            if (ExecutedServantRemains.Count == 0
                || Time.unscaledTime < _nextExecutedServantCleanupTime)
            {
                return;
            }
            _nextExecutedServantCleanupTime = Time.unscaledTime
                + ExecutedServantCleanupSeconds;
            ExecutedServantRemovalBuffer.Clear();
            foreach (KeyValuePair<string, ReanimationRecord> pair
                in ExecutedServantRemains)
            {
                ReanimationRecord record = pair.Value;
                if (record != null
                    && record.RaisedLocation != null
                    && !record.RaisedLocation.HasBeenDiscarded)
                {
                    continue;
                }
                ExecutedServantRemovalBuffer.Add(pair.Key);
            }
            foreach (string locationId in ExecutedServantRemovalBuffer)
            {
                ReanimationRecord record = ExecutedServantRemains[locationId];
                ExecutedServantRemains.Remove(locationId);
                RestoreExecutedServantCorpse(record);
            }
        }

        private static void ClearLightCastState()
        {
            _lightCastActive = false;
            _heavyCastActive = false;
            _lightHarvestCompleted = false;
            _lightPreserveTarget = false;
            _lightTarget = null;
            _heavyTarget = null;
            _lightOriginalMana = 0.0f;
            _lightHealthFraction = 0.0f;
            _lightMaximumManaReturn = float.PositiveInfinity;
            _lightResolvedManaReturn = 0.0f;
        }

        internal static int GetFocusedTargetStateForInterop(
            bool requireRelevantSpell)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || (requireRelevantSpell && !IsSoulSalvageEquipped()))
            {
                return (int)SoulSalvageFocusedTargetState.None;
            }

            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo ignoredHighSoul))
            {
                return (int)SoulSalvageFocusedTargetState.Corpse;
            }

            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon))
            {
                return (int)SoulSalvageFocusedTargetState.None;
            }
            return summon == null
                ? (int)SoulSalvageFocusedTargetState.Corpse
                : (int)SoulSalvageFocusedTargetState.ActiveSummon;
        }

        internal static int GetHeavySoulRendHoverStateForInterop()
        {
            string ignoredText;
            GameObject ignoredView;
            return TryGetHeavySoulRendHover(
                out int state,
                out ignoredText,
                out ignoredView)
                    ? state
                    : (int)HeavySoulRendHoverState.None;
        }

        internal static string GetHeavySoulRendHoverTextForInterop()
        {
            int ignoredState;
            GameObject ignoredView;
            return TryGetHeavySoulRendHover(
                out ignoredState,
                out string text,
                out ignoredView)
                    ? text
                    : string.Empty;
        }

        internal static bool TryGetHeavySoulRendHoverForInteraction(
            out string text,
            out GameObject viewObject)
        {
            int state;
            return TryGetHeavySoulRendHover(
                out state,
                out text,
                out viewObject)
                && state != (int)HeavySoulRendHoverState.ServantFullyRestored;
        }

        private static bool TryGetHeavySoulRendHover(
            out int state,
            out string text,
            out GameObject viewObject)
        {
            state = (int)HeavySoulRendHoverState.None;
            text = string.Empty;
            viewObject = null;
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (!_heavyCastActive
                || plugin == null
                || !plugin.IsEnabled
                || !plugin.SoulSalvageOverhaul.Value
                || hero == null)
            {
                return false;
            }

            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo focusedHighSoul))
            {
                Location highSoulCorpse = focusedHighSoul.Corpse;
                viewObject = highSoulCorpse == null
                    || highSoulCorpse.Spec == null
                        ? null
                        : highSoulCorpse.Spec.gameObject;
                int remaining = SoulProgressionRuntime
                    .GetOrInitializeHighSoulVigorPool(
                        focusedHighSoul.Fingerprint,
                        focusedHighSoul.ExtractionValue);
                if (remaining <= 0)
                {
                    state = (int)HeavySoulRendHoverState.RequiresSoulVigor;
                    text = "No soul remains to bind.";
                    return true;
                }
                if (!focusedHighSoul.CanReanimate)
                {
                    state = (int)HeavySoulRendHoverState.RequiresSoulVigor;
                    text = GetUnbindableHighSoulMessage(
                        focusedHighSoul.Fingerprint);
                    return true;
                }

                int highSoulCost = GetMinibossReanimationSoulVigorCost(
                    remaining,
                    SoulProgressionRuntime.GetHighSoulExtractionValue(
                        focusedHighSoul.Fingerprint,
                        focusedHighSoul.ExtractionValue),
                    SoulProgressionRuntime.GetNecromanticPower());
                bool highSoulAffordable = SoulProgressionRuntime.GetSoulVigor()
                    + 0.001f >= highSoulCost;
                state = highSoulAffordable
                    ? (int)HeavySoulRendHoverState.Reanimate
                    : (int)HeavySoulRendHoverState.RequiresSoulVigor;
                text = (highSoulAffordable ? "Reanimate: " : "Requires ")
                    + highSoulCost.ToString(CultureInfo.InvariantCulture)
                    + " Soul Vigor";
                return true;
            }

            Location location;
            NpcHeroSummon summon;
            if (TryFindFocusedSoulTargetCached(out location, out summon))
            {
                viewObject = location == null || location.Spec == null
                    ? null
                    : location.Spec.gameObject;
                if (summon == null)
                {
                    float quality01 = CalculateQuality01(location, null);
                    Grailwright.Shared.CorpseQualityTier tier =
                        Grailwright.Shared.CorpseQualityBuckets.GetTier(
                            quality01,
                            true);
                    int nativeSoulVigor =
                        SoulProgressionRuntime.GetOrRollCorpseSoulVigorValue(
                            GetCorpseFingerprint(location),
                            tier,
                            quality01);
                    int cost = GetReanimationSoulVigorCost(
                        nativeSoulVigor,
                        SoulProgressionRuntime.GetNecromanticPower());
                    bool affordable = SoulProgressionRuntime.GetSoulVigor()
                        + 0.001f >= cost;
                    state = affordable
                        ? (int)HeavySoulRendHoverState.Reanimate
                        : (int)HeavySoulRendHoverState.RequiresSoulVigor;
                    text = (affordable ? "Reanimate: " : "Requires ")
                        + cost.ToString(CultureInfo.InvariantCulture)
                        + " Soul Vigor";
                    return true;
                }

                NpcElement servant = summon.ParentModel;
                float maximumHealth = servant == null || servant.Health == null
                    ? 0.0f
                    : Math.Max(
                        servant.Health.UpperLimit,
                        servant.Health.ModifiedValue);
                float healthFraction = maximumHealth <= 0.0f
                    ? 1.0f
                    : Mathf.Clamp01(servant.Health.ModifiedValue / maximumHealth);
                if (healthFraction < ServantEmpowerHealthThreshold)
                {
                    state = (int)HeavySoulRendHoverState.RestoreServant;
                    text = "Restore Servant";
                }
                else if (SoulProgressionRuntime.GetNecromanticPower()
                        >= SoulProgressionRuntime.EmpowermentPower
                    && !IsReanimatedMiniboss(summon)
                    && !SummonRuntime.IsEmpoweredSummon(summon))
                {
                    int cost = GetEmpowermentSoulVigorCost(
                        summon,
                        SoulProgressionRuntime.GetNecromanticPower());
                    bool affordable = SoulProgressionRuntime.GetSoulVigor()
                        + 0.001f >= cost;
                    state = affordable
                        ? (int)HeavySoulRendHoverState.EmpowerServant
                        : (int)HeavySoulRendHoverState.RequiresSoulVigor;
                    text = (affordable ? "Empower: " : "Requires ")
                        + cost.ToString(CultureInfo.InvariantCulture)
                        + " Soul Vigor";
                }
                else
                {
                    state = (int)HeavySoulRendHoverState.ServantFullyRestored;
                    text = string.Empty;
                }
                return true;
            }

            if (!plugin.LivingTargetSoulSalvage.Value)
            {
                return false;
            }
            Location targetLocation;
            NpcElement target;
            Collider hitCollider;
            LocationTemplate ignoredLivingSpawnTemplate;
            string rejection;
            if (!TryFindEligibleLivingTarget(
                    hero,
                    needsSpawnTemplate: true,
                    out targetLocation,
                    out target,
                    out hitCollider,
                    out ignoredLivingSpawnTemplate,
                    out rejection)
                || target == null
                || target.Health == null)
            {
                return false;
            }
            Grailwright.Shared.CorpseQualityTier qualityTier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(
                    CalculateQuality01(targetLocation, target),
                    true);
            float threshold = CalculateSoulClaimThreshold(
                SoulProgressionRuntime.GetNecromanticPower(),
                qualityTier,
                GetSoulClaimPreparationHits(((Model)target).ID, target));
            int claimThresholdPercent = Mathf.Clamp(
                Mathf.RoundToInt(threshold * 100.0f),
                0,
                100);
            int targetHealthPercent = Mathf.Clamp(
                Mathf.RoundToInt(target.Health.Percentage * 100.0f),
                0,
                100);
            viewObject = hitCollider == null ? null : hitCollider.gameObject;
            state = (int)HeavySoulRendHoverState.ClaimSoul;
            text = "Claim Soul at "
                + claimThresholdPercent.ToString(CultureInfo.InvariantCulture)
                + "% ("
                + targetHealthPercent.ToString(CultureInfo.InvariantCulture)
                + "%)";
            return true;
        }

        internal static float GetFocusedTargetQuality01ForInterop()
        {
            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo ignoredHighSoul))
            {
                return 1.0f;
            }
            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon))
            {
                return 0.0f;
            }
            if (summon != null)
            {
                ReanimationRecord record;
                if (Reanimations.TryGetValue(((Model)summon).ID, out record))
                {
                    return record.Quality01;
                }
                return CalculateQuality01(location, summon.ParentModel);
            }
            return CalculateQuality01(location, null);
        }

        internal static int GetFocusedTargetQualityTierForInterop()
        {
            if (GetFocusedTargetStateForInterop(true)
                == (int)SoulSalvageFocusedTargetState.None)
            {
                return (int)Grailwright.Shared.CorpseQualityTier.None;
            }
            return (int)Grailwright.Shared.CorpseQualityBuckets.GetTier(
                GetFocusedTargetQuality01ForInterop(),
                true);
        }

        internal static float GetFocusedBindingProgress01ForInterop()
        {
            if (TryFindFocusedHighSoulCorpseCached(
                    out HighSoulCorpseInfo focusedHighSoul))
            {
                return focusedHighSoul.CanReanimate
                    ? SoulProgressionRuntime.GetHighSoulBindingProgress01(
                        focusedHighSoul.Fingerprint,
                        SoulProgressionRuntime.GetHighSoulServiceCycle(
                            focusedHighSoul.Fingerprint,
                            focusedHighSoul.ExtractionValue),
                        MinibossBindingResistance)
                    : 0.0f;
            }
            Location location;
            NpcHeroSummon summon;
            if (!TryFindFocusedSoulTargetCached(out location, out summon)
                || summon != null)
            {
                return 0.0f;
            }
            float quality = CalculateQuality01(location, null);
            Grailwright.Shared.CorpseQualityTier tier =
                Grailwright.Shared.CorpseQualityBuckets.GetTier(quality, true);
            return SoulProgressionRuntime.GetBindingProgress01(
                GetCorpseFingerprint(location),
                tier);
        }

        internal static bool IsReanimatedMiniboss(NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return false;
            }
            ReanimationRecord record;
            return Reanimations.TryGetValue(((Model)summon).ID, out record)
                && record != null
                && record.IsMiniboss;
        }

        internal static bool IsReanimatedServant(NpcHeroSummon summon)
        {
            return summon != null
                && Reanimations.ContainsKey(((Model)summon).ID);
        }

        internal static float GetReanimatedHealthMultiplier(
            NpcHeroSummon summon)
        {
            if (summon == null)
            {
                return 1.0f;
            }
            ReanimationRecord record;
            if (!Reanimations.TryGetValue(((Model)summon).ID, out record)
                || record == null)
            {
                return 1.0f;
            }
            return record.IsMiniboss
                ? 0.50f
                : SoulProgressionRuntime.GetQualityHealthMultiplier(
                    record.QualityTier);
        }

        private static bool IsSoulSalvageEquipped()
        {
            Hero hero = Hero.Current;
            if (hero == null || hero.HeroItems == null)
            {
                return false;
            }
            Item mainHandItem = hero.HeroItems.EquippedItem(
                EquipmentSlotType.MainHand);
            Item offHandItem = hero.HeroItems.EquippedItem(
                EquipmentSlotType.OffHand);
            return (IsSoulSalvageItem(mainHandItem)
                    && !IsVersatileWeaponsHandSuppressed(
                        EquipmentSlotType.MainHand))
                || (IsSoulSalvageItem(offHandItem)
                    && !IsVersatileWeaponsHandSuppressed(
                        EquipmentSlotType.OffHand));
        }

        internal static bool IsVersatileWeaponsHandSuppressed(
            EquipmentSlotType slot)
        {
            if (!TryResolveVersatileWeaponsApi())
            {
                return false;
            }
            try
            {
                return slot == EquipmentSlotType.MainHand
                    ? _versatileWeaponsIsMainHandSuppressed()
                    : _versatileWeaponsIsOffHandSuppressed();
            }
            catch (Exception exception)
            {
                _versatileWeaponsIsMainHandSuppressed = null;
                _versatileWeaponsIsOffHandSuppressed = null;
                _versatileWeaponsApiUnavailable = true;
                SoulAndServicePlugin.Instance?.LogWarning(
                    "Versatile Weapons hand-suppression integration failed and is disabled for this session: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private static bool TryResolveVersatileWeaponsApi()
        {
            if (_versatileWeaponsIsMainHandSuppressed != null
                && _versatileWeaponsIsOffHandSuppressed != null)
            {
                return true;
            }
            if (_versatileWeaponsApiUnavailable)
            {
                return false;
            }

            PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    VersatileWeaponsPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                _versatileWeaponsApiUnavailable = true;
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                VersatileWeaponsApiTypeName,
                false);
            MethodInfo mainMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "IsMainHandSuppressed",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
            MethodInfo offMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "IsOffHandSuppressed",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
            if (mainMethod == null
                || mainMethod.ReturnType != typeof(bool)
                || offMethod == null
                || offMethod.ReturnType != typeof(bool))
            {
                _versatileWeaponsApiUnavailable = true;
                SoulAndServicePlugin.Instance?.LogWarning(
                    "Versatile Weapons is loaded, but its hand-suppression API is unavailable; suppressed Soul Rend hands will use ordinary equipment-slot detection.");
                return false;
            }

            try
            {
                _versatileWeaponsIsMainHandSuppressed =
                    (Func<bool>)Delegate.CreateDelegate(
                        typeof(Func<bool>),
                        mainMethod);
                _versatileWeaponsIsOffHandSuppressed =
                    (Func<bool>)Delegate.CreateDelegate(
                        typeof(Func<bool>),
                        offMethod);
                return true;
            }
            catch (Exception exception)
            {
                _versatileWeaponsApiUnavailable = true;
                SoulAndServicePlugin.Instance?.LogWarning(
                    "Versatile Weapons hand-suppression API binding failed; suppressed Soul Rend hands will use ordinary equipment-slot detection: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private static bool TryFindFocusedSoulTarget(
            out Location location,
            out NpcHeroSummon summon)
        {
            location = null;
            summon = null;
            Hero hero = Hero.Current;
            if (hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Raycaster == null)
            {
                return false;
            }

            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            RaycastHit[] hits = GetSortedSoulTargetHits(
                origin,
                direction,
                out int hitCount);
            if (hitCount > 0)
            {
                RaycastHit hit = hits[0];
                VLocation view = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<LocationParent>()
                        ?.GetComponentInChildren<VLocation>();
                Location candidate = view == null ? null : view.Target;
                if (IsSoulRendAssistSurface(hit, candidate)
                    && TryFindNearestEligibleCorpse(
                        hero,
                        hit.point,
                        hit.normal,
                        needsSpawnTemplate: true,
                        out location,
                        out LocationTemplate ignoredAssistSpawnTemplate))
                {
                    return true;
                }
                if (candidate == null || candidate.HasBeenDiscarded)
                {
                    return false;
                }

                NpcElement npc = candidate.TryGetElement<NpcElement>();
                NpcHeroSummon candidateSummon = npc == null
                    ? null
                    : npc.TryGetElement<NpcHeroSummon>();
                if (candidateSummon != null
                    && candidateSummon.Ally == hero
                    && npc.IsAlive)
                {
                    location = candidate;
                    summon = candidateSummon;
                    return true;
                }

                Corpse corpse = candidate.TryGetElement<Corpse>();
                if (corpse == null)
                {
                    return false;
                }
                string rejection;
                if (TryValidateEligibleCorpse(
                        hero,
                        candidate,
                        needsSpawnTemplate: true,
                        out LocationTemplate ignoredSpawnTemplate,
                        out rejection))
                {
                    location = candidate;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static bool TryFindFocusedSoulTargetCached(
            out Location location,
            out NpcHeroSummon summon)
        {
            int frame = Time.frameCount;
            if (_focusedTargetCacheFrame != frame)
            {
                _focusedTargetCacheFrame = frame;
                _focusedTargetCacheFound = TryFindFocusedSoulTarget(
                    out _focusedTargetCacheLocation,
                    out _focusedTargetCacheSummon);
            }
            location = _focusedTargetCacheLocation;
            summon = _focusedTargetCacheSummon;
            return _focusedTargetCacheFound;
        }

        private static float GetHeavyCastManaCost(Item item)
        {
            return item != null
                && item.ItemStats != null
                && item.ItemStats.HeavyCastManaCost != null
                    ? Math.Max(0.0f, item.ItemStats.HeavyCastManaCost.ModifiedValue)
                    : 30.0f;
        }

        private static string GetCorpseFingerprint(Location source)
        {
            LocationTemplate spawnTemplate = ResolveSoulTargetSpawnTemplate(source);
            NpcTemplate npcTemplate = source == null
                ? null
                : NpcTemplate.FromNpcOrDummy(source);
            string templateGuid = spawnTemplate != null
                ? spawnTemplate.GUID
                : npcTemplate == null ? string.Empty : npcTemplate.GUID;
            Vector3 position = source == null ? Vector3.zero : source.Coords;
            return string.Join(
                "|",
                templateGuid ?? string.Empty,
                Mathf.RoundToInt(position.x * 4.0f).ToString(CultureInfo.InvariantCulture),
                Mathf.RoundToInt(position.y * 4.0f).ToString(CultureInfo.InvariantCulture),
                Mathf.RoundToInt(position.z * 4.0f).ToString(CultureInfo.InvariantCulture),
                GetCorpseDisplayName(source));
        }

        private static string GetCorpseHarvestIdentity(Location source)
        {
            string modelId = source == null ? string.Empty : ((Model)source).ID;
            return string.IsNullOrEmpty(modelId)
                ? GetCorpseFingerprint(source)
                : "corpse-model|" + modelId;
        }

        private static float CalculateQuality01(Location source, NpcElement knownNpc)
        {
            NpcElement npc = knownNpc;
            if (npc == null && source != null)
            {
                npc = source.TryGetElement<NpcElement>();
            }
            object template = npc != null
                ? (object)npc.Template
                : source == null ? null : NpcTemplate.FromNpcOrDummy(source);
            int nativeTier;
            bool hasNativeTier = TryReadNativeTier(template, out nativeTier)
                || TryReadNativeTier(source == null ? null : source.Template, out nativeTier);
            float killXp = ReadFirstPositiveFloat(
                npc,
                template,
                source,
                "ExpReward",
                "KillExp",
                "KillXp",
                "ExperienceReward");
            float maximumHealth = npc != null && npc.Health != null
                ? Math.Max(0.0f, npc.Health.UpperLimit)
                : ReadFirstPositiveFloat(
                    source,
                    template,
                    null,
                    "MaxHealth",
                    "MaximumHealth",
                    "Health");
            bool hasEvidence;
            bool usedNativeTier;
            float quality = Grailwright.Shared.CorpseQualityBuckets
                .CalculateIntrinsicQuality01(
                    hasNativeTier ? nativeTier : -1,
                    killXp,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceKillXp,
                    maximumHealth,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultReferenceMaxHealth,
                    out hasEvidence,
                    out usedNativeTier);
            if (!hasEvidence)
            {
                quality = 0.20f;
            }

            Grailwright.Shared.CorpseQualityThreatClass threat =
                ReadThreatClass(template);
            quality = Grailwright.Shared.CorpseQualityBuckets
                .ApplyThreatClassAdjustment(quality, threat);
            float enemyLevel = ReadFirstPositiveFloat(
                npc,
                template,
                source,
                "ExpLevel",
                "Level",
                "CharacterLevel");
            float heroLevel = Hero.Current == null
                ? -1.0f
                : ReadFirstPositiveFloat(
                    Hero.Current.HeroStats,
                    Hero.Current,
                    null,
                    "CharacterLevel",
                    "Level",
                    "ExpLevel");
            bool adjusted;
            return Grailwright.Shared.CorpseQualityBuckets
                .ApplyBoundedRelativeLevelAdjustment(
                    quality,
                    enemyLevel <= 0.0f ? -1.0f : enemyLevel,
                    heroLevel <= 0.0f ? -1.0f : heroLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultLevelQualityPerLevel,
                    Grailwright.Shared.CorpseQualityBuckets.DefaultMaximumLevelQualityAdjustment,
                    out adjusted);
        }

        private static bool TryReadNativeTier(object owner, out int nativeTier)
        {
            nativeTier = -1;
            IEnumerable tags = GetMemberValue(owner, "Tags") as IEnumerable;
            if (tags == null || tags is string)
            {
                return false;
            }
            foreach (object value in tags)
            {
                string tag = value as string;
                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }
                for (int tier = 0; tier <= 7; tier++)
                {
                    if (string.Equals(
                        tag,
                        "Tier:" + tier.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                    {
                        nativeTier = tier;
                        return true;
                    }
                }
            }
            return false;
        }

        private static Grailwright.Shared.CorpseQualityThreatClass ReadThreatClass(
            object owner)
        {
            object value = GetMemberValue(owner, "NpcType");
            string name = value == null ? string.Empty : value.ToString();
            if (string.Equals(name, "Boss", StringComparison.Ordinal))
            {
                return Grailwright.Shared.CorpseQualityThreatClass.Boss;
            }
            if (string.Equals(name, "MiniBoss", StringComparison.Ordinal))
            {
                return Grailwright.Shared.CorpseQualityThreatClass.MiniBoss;
            }
            return string.Equals(name, "Elite", StringComparison.Ordinal)
                ? Grailwright.Shared.CorpseQualityThreatClass.Elite
                : Grailwright.Shared.CorpseQualityThreatClass.Normal;
        }

        private static float ReadFirstPositiveFloat(
            object first,
            object second,
            object third,
            params string[] names)
        {
            object[] owners = { first, second, third };
            foreach (object owner in owners)
            {
                foreach (string name in names)
                {
                    object value = GetMemberValue(owner, name);
                    if (value is Stat)
                    {
                        float statValue = ((Stat)value).ModifiedValue;
                        if (statValue > 0.0f)
                        {
                            return statValue;
                        }
                    }
                    try
                    {
                        float numeric = value == null
                            ? 0.0f
                            : Convert.ToSingle(value, CultureInfo.InvariantCulture);
                        if (numeric > 0.0f)
                        {
                            return numeric;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return 0.0f;
        }

        private static object GetMemberValue(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }
            Type type = owner.GetType();
            PropertyInfo property = GetPropertySilent(type, name);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(owner, null);
                }
                catch
                {
                }
            }
            FieldInfo field = GetFieldSilent(type, name);
            if (field != null)
            {
                try
                {
                    return field.GetValue(owner);
                }
                catch
                {
                }
            }
            return null;
        }

        private static PropertyInfo GetPropertySilent(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            Dictionary<string, PropertyInfo> members;
            if (!OptionalPropertyCache.TryGetValue(type, out members))
            {
                members = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
                OptionalPropertyCache[type] = members;
            }
            PropertyInfo property;
            if (members.TryGetValue(name, out property))
            {
                return property;
            }
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;
            Type current = type;
            while (current != null)
            {
                property = current.GetProperty(name, flags);
                if (property != null)
                {
                    break;
                }
                current = current.BaseType;
            }
            members[name] = property;
            return property;
        }

        private static FieldInfo GetFieldSilent(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            Dictionary<string, FieldInfo> members;
            if (!OptionalFieldCache.TryGetValue(type, out members))
            {
                members = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
                OptionalFieldCache[type] = members;
            }
            FieldInfo field;
            if (members.TryGetValue(name, out field))
            {
                return field;
            }
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;
            Type current = type;
            while (current != null)
            {
                field = current.GetField(name, flags);
                if (field != null)
                {
                    break;
                }
                current = current.BaseType;
            }
            members[name] = field;
            return field;
        }

        private static MethodInfo RequireMethod(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type[] arguments)
        {
            MethodInfo method = AccessTools.Method(type, name, arguments);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }
    }
}
