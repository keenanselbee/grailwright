using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.Main.AI;
using Awaken.TG.Main.AI.Fights.Projectiles;
using Awaken.TG.Main.AI.Idle;
using Awaken.TG.Main.AI.Movement.Controllers;
using Awaken.TG.Main.AI.Movement.States;
using Awaken.TG.Main.AI.SummonsAndAllies;
using Awaken.TG.Main.AI.Utils;
using Awaken.TG.Main.AudioSystem;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Heroes.Stats.Tweaks;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Setup;
using Awaken.TG.Main.Locations.Views;
using Awaken.TG.Main.VisualGraphUtils;
using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace SoulAndService
{
    internal static class SummonRuntime
    {
        private sealed class CollisionPair
        {
            internal Collider SummonCollider;
            internal Collider HeroCollider;
        }

        private sealed class ScalingTweaks
        {
            internal StatTweak Melee;
            internal StatTweak Ranged;
            internal StatTweak Magic;
            internal StatTweak Health;
        }

        private static readonly Dictionary<string, List<CollisionPair>> CollisionPairs =
            new Dictionary<string, List<CollisionPair>>();
        private static readonly Dictionary<string, StatTweak> SpeedTweaks =
            new Dictionary<string, StatTweak>();
        private static readonly Dictionary<string, ScalingTweaks> InvocationTweaks =
            new Dictionary<string, ScalingTweaks>();
        private static float _nextCollisionRefreshTime;

        private static readonly FieldInfo PatrolField =
            AccessTools.Field(typeof(NpcAlly), "_patrol");
        private static readonly MethodInfo TeleportToAllyMethod =
            AccessTools.Method(typeof(NpcAlly), "TeleportToAlly");
        private static readonly FieldInfo NpcDetectionField =
            AccessTools.Field(typeof(VCHeroRaycaster), "npcDetection");
        private static readonly Type CharacterLocationsType =
            RequireNestedType(typeof(CharacterLimitedLocations), "CharacterLocations");
        private static readonly FieldInfo LimitedLocationsField =
            AccessTools.Field(CharacterLocationsType, "_locations");
        private static readonly FieldInfo OldestIndexField =
            AccessTools.Field(CharacterLocationsType, "_oldestIndex");
        private static readonly FieldInfo EmptyCountField =
            AccessTools.Field(CharacterLocationsType, "_emptyCount");

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "UnityUpdate"),
                transpiler: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(TranspileAiTick)));
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "StayCloseToAlly"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeStayCloseToAlly)));
            harmony.Patch(
                RequireMethod(typeof(NpcAlly), "FindTarget"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterFindTarget)));

            MethodInfo preventMovement = RequireMethod(
                typeof(NpcHeroSummon),
                "PreventMovement");
            AsyncStateMachineAttribute asyncAttribute =
                preventMovement.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (asyncAttribute == null)
            {
                throw new MissingMethodException(
                    "NpcHeroSummon.PreventMovement async state machine was not found.");
            }
            harmony.Patch(
                RequireMethod(asyncAttribute.StateMachineType, "MoveNext"),
                transpiler: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(TranspileSpawnRecovery)));

            MethodInfo summonInit = RequireMethod(typeof(NpcHeroSummon), "Init");
            harmony.Patch(
                summonInit,
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterSummonInit)));
            harmony.Patch(
                RequireMethod(
                    CharacterLocationsType,
                    "AddLocation",
                    new[] { typeof(ICharacterLimitedLocation) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeAddLimitedLocation)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "ToggleWalkThroughColliders"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterToggleWalkThroughColliders)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "OnDiscard"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterSummonDiscard)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "get_DestroyOnRest"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeGetDestroyOnRest)));
            harmony.Patch(
                RequireMethod(typeof(NpcHeroSummon), "LimitForCharacter"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterGetSummonLimit)));

            harmony.Patch(
                RequireMethod(typeof(NpcController), "Update"),
                postfix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(AfterNpcControllerUpdate)));

            harmony.Patch(
                RequireMethod(
                    typeof(DamageDealingProjectile),
                    "CheckCastResult",
                    new[] { typeof(HitResult) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeProjectileHitResult)));
            harmony.Patch(
                RequireMethod(
                    typeof(DamageDealingProjectile),
                    "CheckCastResult",
                    new[] { typeof(Collider) }),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeProjectileCollider)));
            harmony.Patch(
                RequireMethod(
                    typeof(CharacterMagicGauntlet),
                    "OnBoxCastHit"),
                prefix: new HarmonyMethod(
                    typeof(SummonRuntime),
                    nameof(BeforeMagicGauntletHit)));
        }

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            if (!plugin.IsEnabled || !plugin.SummonPassThrough.Value)
            {
                RestoreAllCollisionPairs();
                return;
            }

            if (Time.unscaledTime < _nextCollisionRefreshTime)
            {
                return;
            }
            _nextCollisionRefreshTime = Time.unscaledTime + 1.0f;
            foreach (NpcHeroSummon summon in World.All<NpcHeroSummon>())
            {
                ApplyPlayerPassThrough(summon);
            }
        }

        internal static void Shutdown()
        {
            RestoreAllCollisionPairs();
            foreach (StatTweak tweak in SpeedTweaks.Values.ToArray())
            {
                DiscardTweak(tweak);
            }
            SpeedTweaks.Clear();
            foreach (ScalingTweaks tweaks in InvocationTweaks.Values.ToArray())
            {
                DiscardScalingTweaks(tweaks);
            }
            InvocationTweaks.Clear();
        }

        private static IEnumerable<CodeInstruction> TranspileAiTick(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> result = new List<CodeInstruction>();
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float
                    && Math.Abs((float)instruction.operand - 2.5f) < 0.0001f)
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction));
                    result.Add(new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(
                            typeof(SummonRuntime),
                            nameof(GetAiTickInterval))));
                    replaced++;
                }
                else
                {
                    result.Add(instruction);
                }
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException(
                    "Expected one NpcAlly.UnityUpdate AI tick literal but found "
                    + replaced + ".");
            }
            return result;
        }

        private static IEnumerable<CodeInstruction> TranspileSpawnRecovery(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFloatLiteral(
                instructions,
                1.5f,
                AccessTools.Method(typeof(SummonRuntime), nameof(GetSpawnRecoverySeconds)),
                "NpcHeroSummon.PreventMovement duration");
        }

        private static IEnumerable<CodeInstruction> ReplaceFloatLiteral(
            IEnumerable<CodeInstruction> instructions,
            float expected,
            MethodInfo replacement,
            string label)
        {
            List<CodeInstruction> result = instructions.ToList();
            int replaced = 0;
            for (int index = 0; index < result.Count; index++)
            {
                CodeInstruction instruction = result[index];
                if (instruction.opcode == OpCodes.Ldc_R4
                    && instruction.operand is float
                    && Math.Abs((float)instruction.operand - expected) < 0.0001f)
                {
                    result[index] = new CodeInstruction(OpCodes.Call, replacement)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                    replaced++;
                }
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException(
                    "Expected one " + label + " literal but found " + replaced + ".");
            }
            return result;
        }

        private static float GetAiTickInterval(NpcAlly ally)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null
                && plugin.IsEnabled
                && ally is NpcHeroSummon
                ? plugin.AiTickInterval.Value
                : 2.5f;
        }

        private static float GetSpawnRecoverySeconds()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return plugin != null && plugin.IsEnabled
                ? plugin.SpawnRecoverySeconds.Value
                : 1.5f;
        }

        private static bool BeforeStayCloseToAlly(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (plugin == null
                || !plugin.IsEnabled
                || summon == null
                || Time.timeScale == 0.0f
                || summon.Ally == null
                || summon.ParentModel == null
                || summon.ParentModel.Movement == null)
            {
                return true;
            }

            Patrol patrol = PatrolField.GetValue(__instance) as Patrol;
            if (patrol == null)
            {
                return true;
            }

            float distanceSqr =
                (summon.Ally.Coords - summon.ParentModel.Coords).sqrMagnitude;
            float teleportDistance = plugin.TeleportDistance.Value;
            if (distanceSqr > teleportDistance * teleportDistance)
            {
                object[] arguments =
                {
                    distanceSqr,
                    TeleportContext.AllyRanAway,
                    Vector3.zero
                };
                TeleportToAllyMethod.Invoke(__instance, arguments);
                return false;
            }

            float trotDistance = plugin.TrotDistance.Value;
            float runDistance = Math.Max(trotDistance, plugin.RunDistance.Value);
            if (distanceSqr <= trotDistance * trotDistance)
            {
                patrol.UpdateVelocityScheme(VelocityScheme.Walk);
            }
            else
            {
                patrol.UpdatePlace(summon.Ally.Coords);
                patrol.UpdateVelocityScheme(
                    distanceSqr <= runDistance * runDistance
                        ? VelocityScheme.Trot
                        : VelocityScheme.Run);
            }
            return false;
        }

        private static void AfterFindTarget(NpcAlly __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            NpcHeroSummon summon = __instance as NpcHeroSummon;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.ShareHeroTarget.Value
                || summon == null
                || summon.ParentModel == null
                || summon.ParentModel.HasBeenDiscarded
                || summon.ParentModel.GetCurrentTarget() != null
                || summon.HasElement<HeroSummonTargetOverride>())
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null || hero.VHeroController == null || hero.VHeroController.Raycaster == null)
            {
                return;
            }

            RaycastCheck detection = NpcDetectionField == null
                ? null
                : NpcDetectionField.GetValue(hero.VHeroController.Raycaster)
                    as RaycastCheck;
            if (detection == null)
            {
                return;
            }
            hero.VHeroController.Raycaster.GetViewRay(
                out Vector3 origin,
                out Vector3 direction);
            Collider targetCollider = detection.Detected(
                origin,
                direction,
                plugin.ShareTargetMaxDistance.Value);
            VLocation targetView = targetCollider == null
                ? null
                : targetCollider.GetComponentInParent<LocationParent>()
                    ?.GetComponentInChildren<VLocation>();
            Location targetLocation = targetView == null ? null : targetView.Target;
            NpcElement target = null;
            if (targetLocation == null
                || !targetLocation.TryGetElement<NpcElement>(out target)
                || target == null
                || target == summon.ParentModel
                || !target.IsAlive
                || target.IsUnconscious
                || !WithFactionUtils.WantToFight(summon.ParentModel, target))
            {
                return;
            }

            float maxDistance = plugin.ShareTargetMaxDistance.Value;
            if ((target.Coords - summon.ParentModel.Coords).sqrMagnitude
                > maxDistance * maxDistance)
            {
                return;
            }

            HeroSummonTargetOverride.AddSummonTargetOverrideElement(
                summon,
                target,
                10);
            plugin.LogDiagnostic(
                "Shared crosshair target " + ((Model)target).ID
                + " with summon " + ((Model)summon.ParentModel).ID + ".");
        }

        private static void BeforeAddLimitedLocation(
            object __instance,
            ICharacterLimitedLocation location)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.RepairInvocationScaling.Value
                || __instance == null
                || !(location is NpcHeroSummon))
            {
                return;
            }

            ICharacterLimitedLocation[] locations = LimitedLocationsField == null
                ? null
                : LimitedLocationsField.GetValue(__instance)
                    as ICharacterLimitedLocation[];
            int emptyCount = EmptyCountField == null
                ? 1
                : (int)EmptyCountField.GetValue(__instance);
            int oldestIndex = OldestIndexField == null
                ? -1
                : (int)OldestIndexField.GetValue(__instance);
            if (emptyCount != 0
                || locations == null
                || oldestIndex < 0
                || oldestIndex >= locations.Length)
            {
                return;
            }

            NpcHeroSummon outgoing = locations[oldestIndex] as NpcHeroSummon;
            if (outgoing != null)
            {
                RepairInvocationScaling(
                    (NpcHeroSummon)location,
                    outgoing,
                    plugin);
            }
        }

        private static void AfterSummonInit(NpcHeroSummon __instance)
        {
            if (__instance == null)
            {
                return;
            }
            __instance.ParentModel.OnCompletelyInitialized(
                delegate
                {
                    ApplyPlayerPassThrough(__instance);
                });

        }

        private static void RepairInvocationScaling(
            NpcHeroSummon incoming,
            NpcHeroSummon outgoing,
            SoulAndServicePlugin plugin)
        {
            Hero hero = Hero.Current;
            if (hero == null
                || incoming == null
                || outgoing == null
                || incoming.ParentModel == null
                || outgoing.ParentModel == null)
            {
                return;
            }

            float spirituality = hero.HeroRPGStats.Spirituality.ModifiedValue;
            if (spirituality <= 0.0f)
            {
                return;
            }

            NpcElement source = outgoing.ParentModel;
            NpcElement target = incoming.ParentModel;
            if (source.AliveStats == null
                || source.NpcStats == null
                || target.AliveStats == null
                || target.NpcStats == null)
            {
                return;
            }

            float multiplier = 1.0f + spirituality * 0.05f;
            if (!HasExpectedMultiplier(source.AliveStats.MaxHealth, multiplier))
            {
                plugin.LogDiagnostic(
                    "Skipped Invocation of Might repair for replacement summon "
                    + ((Model)target).ID
                    + " because the outgoing summon did not prove that the native scaling was active.");
                return;
            }

            ScalingTweaks tweaks = new ScalingTweaks();
            tweaks.Melee = AddMissingMultiplier(
                target.NpcStats.MeleeDamage,
                multiplier,
                target);
            tweaks.Ranged = AddMissingMultiplier(
                target.NpcStats.RangedDamage,
                multiplier,
                target);
            tweaks.Magic = AddMissingMultiplier(
                target.NpcStats.MagicDamage,
                multiplier,
                target);
            tweaks.Health = AddMissingMultiplier(
                target.AliveStats.MaxHealth,
                multiplier,
                target);
            if (tweaks.Melee == null
                && tweaks.Ranged == null
                && tweaks.Magic == null
                && tweaks.Health == null)
            {
                plugin.LogDiagnostic(
                    "Replacement summon " + ((Model)target).ID
                    + " already retained Invocation of Might scaling.");
                return;
            }

            InvocationTweaks[((Model)incoming).ID] = tweaks;
            if (tweaks.Health != null)
            {
                target.Health.SetToFull();
            }
            plugin.LogDiagnostic(
                "Repaired Invocation of Might scaling for replacement summon "
                + ((Model)target).ID
                + " after confirming it on the outgoing summon.");
        }

        private static bool HasExpectedMultiplier(Stat stat, float multiplier)
        {
            return stat != null
                && stat.BaseValue > 0.0001f
                && Math.Abs(stat.ModifiedValue / stat.BaseValue - multiplier) <= 0.02f;
        }

        private static StatTweak AddMissingMultiplier(
            Stat stat,
            float targetMultiplier,
            NpcElement owner)
        {
            if (stat == null
                || stat.BaseValue <= 0.0001f
                || targetMultiplier <= 1.0f)
            {
                return null;
            }
            float currentMultiplier = stat.ModifiedValue / stat.BaseValue;
            if (currentMultiplier >= targetMultiplier - 0.01f)
            {
                return null;
            }
            float missingMultiplier = targetMultiplier / Math.Max(currentMultiplier, 0.0001f);
            StatTweak tweak = StatTweak.Multi(
                stat,
                missingMultiplier,
                null,
                owner);
            ((Model)tweak).MarkedNotSaved = true;
            return tweak;
        }

        private static void AfterToggleWalkThroughColliders(NpcHeroSummon __instance)
        {
            ApplyPlayerPassThrough(__instance);
        }

        private static void ApplyPlayerPassThrough(NpcHeroSummon summon)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null
                || !plugin.IsEnabled
                || !plugin.SummonPassThrough.Value
                || summon == null
                || ((Model)summon).HasBeenDiscarded
                || summon.ParentModel == null
                || summon.ParentModel.Controller == null
                || summon.ParentModel.Controller.AlivePrefab == null
                || hero == null
                || hero.VHeroController == null
                || hero.VHeroController.Controller == null)
            {
                return;
            }

            string id = ((Model)summon).ID;
            List<CollisionPair> pairs;
            if (!CollisionPairs.TryGetValue(id, out pairs))
            {
                pairs = new List<CollisionPair>();
                CollisionPairs[id] = pairs;
            }

            Collider heroCollider = hero.VHeroController.Controller;
            foreach (Collider collider in summon.ParentModel.Controller.AlivePrefab
                .GetComponentsInChildren<Collider>(true))
            {
                if (collider == null
                    || collider.isTrigger
                    || ReferenceEquals(collider, heroCollider)
                    || pairs.Any(pair => pair.SummonCollider == collider
                        && pair.HeroCollider == heroCollider))
                {
                    continue;
                }
                Physics.IgnoreCollision(heroCollider, collider, true);
                pairs.Add(new CollisionPair
                {
                    HeroCollider = heroCollider,
                    SummonCollider = collider
                });
            }
        }

        private static void RestoreCollisionPairs(string id)
        {
            List<CollisionPair> pairs;
            if (!CollisionPairs.TryGetValue(id, out pairs))
            {
                return;
            }
            foreach (CollisionPair pair in pairs)
            {
                if (pair.HeroCollider != null && pair.SummonCollider != null)
                {
                    Physics.IgnoreCollision(
                        pair.HeroCollider,
                        pair.SummonCollider,
                        false);
                }
            }
            CollisionPairs.Remove(id);
        }

        private static void RestoreAllCollisionPairs()
        {
            foreach (string id in CollisionPairs.Keys.ToArray())
            {
                RestoreCollisionPairs(id);
            }
        }

        private static bool BeforeGetDestroyOnRest(ref bool __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.IsEnabled
                && plugin.PreventDismissOnRest.Value)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static void AfterGetSummonLimit(ref int __result)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null && plugin.IsEnabled)
            {
                __result += plugin.SummonLimitBonus.Value;
            }
        }

        private static void AfterNpcControllerUpdate(NpcController __instance)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (__instance == null || __instance.Npc == null || !__instance.Npc.IsHeroSummon)
            {
                return;
            }

            ApplyIdleVolume(__instance, plugin);
            UpdateCatchUpSpeed(__instance, plugin);
        }

        private static void ApplyIdleVolume(
            NpcController controller,
            SoulAndServicePlugin plugin)
        {
            ARFmodEventEmitter emitter = controller.IdleAudioEmitter;
            if (emitter == null || !emitter.EventInstance.isValid())
            {
                return;
            }
            float volume = plugin != null && plugin.IsEnabled
                ? plugin.IdleSoundVolumePercent.Value / 100.0f
                : 1.0f;
            emitter.EventInstance.setVolume(volume);
        }

        private static void UpdateCatchUpSpeed(
            NpcController controller,
            SoulAndServicePlugin plugin)
        {
            NpcElement npc = controller.Npc;
            string id = ((Model)npc).ID;
            bool shouldBoost = false;
            float multiplier = 1.0f;
            Hero hero = Hero.Current;
            if (plugin != null
                && plugin.IsEnabled
                && hero != null
                && npc.NpcAI != null
                && !npc.NpcAI.InCombat
                && npc.GetCurrentTarget() == null)
            {
                float threshold = plugin.TrotDistance.Value;
                shouldBoost = (hero.Coords - npc.Coords).sqrMagnitude
                    > threshold * threshold;
                multiplier = plugin.CatchUpSpeedMultiplier.Value;
            }

            if (!shouldBoost || multiplier <= 1.0f)
            {
                RemoveSpeedTweak(id);
                return;
            }

            if (!SpeedTweaks.ContainsKey(id)
                && npc.CharacterStats != null
                && npc.CharacterStats.MovementSpeedMultiplier != null)
            {
                StatTweak tweak = StatTweak.Multi(
                    npc.CharacterStats.MovementSpeedMultiplier,
                    multiplier,
                    null,
                    npc);
                ((Model)tweak).MarkedNotSaved = true;
                SpeedTweaks[id] = tweak;
            }
        }

        private static void AfterSummonDiscard(NpcHeroSummon __instance)
        {
            if (__instance == null)
            {
                return;
            }
            string id = ((Model)__instance).ID;
            RestoreCollisionPairs(id);
            RemoveSpeedTweak(id);
            ScalingTweaks tweaks;
            if (InvocationTweaks.TryGetValue(id, out tweaks))
            {
                DiscardScalingTweaks(tweaks);
                InvocationTweaks.Remove(id);
            }
            SoulSalvageRuntime.OnSummonDiscarded(__instance);
        }

        private static void RemoveSpeedTweak(string id)
        {
            StatTweak tweak;
            if (SpeedTweaks.TryGetValue(id, out tweak))
            {
                DiscardTweak(tweak);
                SpeedTweaks.Remove(id);
            }
        }

        private static void DiscardScalingTweaks(ScalingTweaks tweaks)
        {
            if (tweaks == null)
            {
                return;
            }
            DiscardTweak(tweaks.Melee);
            DiscardTweak(tweaks.Ranged);
            DiscardTweak(tweaks.Magic);
            DiscardTweak(tweaks.Health);
        }

        private static void DiscardTweak(StatTweak tweak)
        {
            if (tweak != null && !((Model)tweak).HasBeenDiscarded)
            {
                ((Model)tweak).Discard();
            }
        }

        private static bool BeforeProjectileHitResult(
            DamageDealingProjectile __instance,
            HitResult hitResult)
        {
            return !ShouldPassThrough(
                __instance,
                hitResult.Collider,
                __instance is MagicProjectile);
        }

        private static bool BeforeProjectileCollider(
            DamageDealingProjectile __instance,
            Collider collider)
        {
            return !ShouldPassThrough(
                __instance,
                collider,
                __instance is MagicProjectile);
        }

        private static bool BeforeMagicGauntletHit(
            CharacterMagicGauntlet __instance,
            ref RaycastHit other)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || __instance == null
                || __instance.Owner == null
                || __instance.Owner.Character != Hero.Current
                || plugin.PlayerAttackPassThrough.Value
                    == PlayerAttackPassThroughMode.Vanilla)
            {
                return true;
            }
            return !IsOwnedSummonCollider(other.collider);
        }

        private static bool ShouldPassThrough(
            DamageDealingProjectile projectile,
            Collider collider,
            bool isMagic)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || projectile == null
                || projectile.Owner != Hero.Current
                || collider == null)
            {
                return false;
            }

            PlayerAttackPassThroughMode mode =
                plugin.PlayerAttackPassThrough.Value;
            if (mode == PlayerAttackPassThroughMode.Vanilla
                || (mode == PlayerAttackPassThroughMode.MagicOnly && !isMagic))
            {
                return false;
            }
            return IsOwnedSummonCollider(collider);
        }

        private static bool IsOwnedSummonCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }
            NpcElement npc = VGUtils.TryGetModel<NpcElement>(collider.gameObject);
            return npc != null && npc.IsHeroSummon;
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

        private static Type RequireNestedType(Type type, string name)
        {
            Type nested = type.GetNestedType(name, BindingFlags.NonPublic);
            if (nested == null)
            {
                throw new MissingMemberException(type.FullName, name);
            }
            return nested;
        }
    }
}
