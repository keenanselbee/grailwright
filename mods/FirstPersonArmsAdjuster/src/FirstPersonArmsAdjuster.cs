using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Awaken.ECS.Authoring.LinkedEntities;
using Awaken.ECS.Components;
using Awaken.ECS.Systems;
using Awaken.Kandra;
using Awaken.Kandra.Data;
using Awaken.Kandra.Managers;
using Awaken.TG.Main.Crafting.Fireplace;
using Awaken.TG.Assets;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.Utility.LowLevel;
using Awaken.Utility.LowLevel.Collections;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[assembly: AssemblyTitle("First Person Arms Adjuster")]
[assembly: AssemblyDescription("Moves the rendered first-person arms and weapons without changing world FOV.")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("First Person Arms Adjuster")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.3.1.0")]
[assembly: AssemblyFileVersion("0.3.1.0")]

namespace FirstPersonArmsAdjuster
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FirstPersonArmsAdjusterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.first-person-arms-adjuster";
        public const string PluginName = "First Person Arms Adjuster";
        public const string PluginVersion = "0.3.1";

        private const int ConfigSchemaVersion = 7;
        private const int ConfigRecoveryBaselineSchema = 1;
        private const int SceneTransitionSuspensionFrames = 45;
        private const float FireplaceBlendOutSeconds = 0.25f;
        private const float FireplaceStandFallbackSeconds = 1.15f;
        private const float FireplaceBlendInSeconds = 0.40f;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[0];
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];

        private struct DrakeOffsetState
        {
            public bool HadOriginalOffset;
            public float4x4 OriginalOffset;
        }

        private struct OffsetKandraBonesJob : IJobParallelFor
        {
            public NativeArray<Bone> Bones;
            public int StartIndex;
            public float3 Translation;

            public void Execute(int index)
            {
                int boneIndex = StartIndex + index;
                Bone bone = Bones[boneIndex];
                bone.boneTransform.c3 =
                    bone.boneTransform.c3 + Translation;
                Bones[boneIndex] = bone;
            }
        }

        private struct OffsetKandraCullingJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<uint> Slots;
            public UnsafeArray<float4x4> RootBones;
            public UnsafeArray<float> Xs;
            public UnsafeArray<float> Ys;
            public UnsafeArray<float> Zs;
            public float3 Translation;

            public void Execute(int index)
            {
                uint slot = Slots[index];
                float4x4 rootBone = RootBones[slot];
                rootBone.c3 = new float4(
                    rootBone.c3.xyz + Translation,
                    rootBone.c3.w);
                RootBones[slot] = rootBone;
                Xs[slot] += Translation.x;
                Ys[slot] += Translation.y;
                Zs[slot] += Translation.z;
            }
        }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _forwardOffset;
        private ConfigEntry<float> _horizontalOffset;
        private ConfigEntry<float> _verticalOffset;
        private ConfigEntry<bool> _useCategoryForwardOffsets;
        private ConfigEntry<float> _meleeForwardOffset;
        private ConfigEntry<float> _bowForwardOffset;
        private ConfigEntry<float> _magicForwardOffset;
        private ConfigEntry<bool> _diagnostics;

        private bool _hasPendingEnabled;
        private bool _pendingEnabled;
        private bool _hasPendingForwardOffset;
        private float _pendingForwardOffset;
        private bool _hasPendingHorizontalOffset;
        private float _pendingHorizontalOffset;
        private bool _hasPendingVerticalOffset;
        private float _pendingVerticalOffset;
        private bool _hasPendingMeleeForwardOffset;
        private float _pendingMeleeForwardOffset;
        private bool _hasPendingBowForwardOffset;
        private float _pendingBowForwardOffset;
        private bool _hasPendingMagicForwardOffset;
        private float _pendingMagicForwardOffset;
        private bool _hasPendingDiagnostics;
        private bool _pendingDiagnostics;

        private Transform _offsetRoot;
        private Camera _offsetCamera;
        private Vector3 _originalWorldPosition;
        private bool _renderOffsetApplied;
        private Transform _lastReportedRoot;

        private Harmony _harmony;
        private FieldInfo _inputBonesArrayField;
        private FieldInfo _readTransformField;
        private FieldInfo _bonesInFlightField;
        private FieldInfo _crouchTweenActiveField;
        private FieldInfo _linkedTransformsArrayField;
        private HeroBodyData _cachedKandraBodyData;
        private KandraRig[] _cachedKandraRigs = new KandraRig[0];
        private KandraRenderer[] _cachedKandraRenderers =
            new KandraRenderer[0];
        private float _nextKandraRigRefreshTime;
        private HeroBodyData _lastReportedKandraBodyData;
        private RigManager _pendingKandraRigManager;
        private int _kandraCollectionFrame = -1;
        private int _lastSynchronizedKandraFrame = -1;
        private CharacterHandBase _cachedMainHandWeapon;
        private CharacterHandBase _cachedOffHandWeapon;
        private LinkedEntitiesAccess[] _cachedWeaponEntityAccess =
            new LinkedEntitiesAccess[0];
        private readonly Dictionary<Entity, DrakeOffsetState>
            _originalDrakeOffsets =
                new Dictionary<Entity, DrakeOffsetState>();
        private float _nextWeaponEntityRefreshTime;
        private int _suspendOffsetsUntilFrame = -1;
        private float _fireplaceOffsetBlend = 1.0f;
        private float _fireplaceBlendStart = 1.0f;
        private float _fireplaceBlendTarget = 1.0f;
        private float _fireplaceBlendStartedAt;
        private float _fireplaceStandFallbackUntil;
        private bool _fireplaceInteractionActive;
        private bool _waitingForFireplaceStand;
        private bool _crouchTweenReadWarningLogged;
        private CharacterHandBase _lastReportedMainHandWeapon;
        private CharacterHandBase _lastReportedOffHandWeapon;
        private FieldInfo _bowArrowInMainHandField;
        private FieldInfo _bowArrowInControllerField;
        private FieldInfo _bowMainHandEnabledField;
        private string _bowDiagnosticState;
        private float _bowDiagnosticsUntilTime;
        private float _nextBowDiagnosticTime;
        private string _viewmodelDiagnosticSignature;
        private float _nextViewmodelDiagnosticTime;

        internal static FirstPersonArmsAdjusterPlugin Instance
        {
            get;
            private set;
        }

        private void Awake()
        {
            try
            {
                Instance = this;
                ResetConfigIfSchemaChanged();
                BindConfig();
                PatchRenderSystems();
                Camera.onPreCull += OnCameraPreCull;
                Camera.onPostRender += OnCameraPostRender;
                RenderPipelineManager.beginCameraRendering +=
                    OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering +=
                    OnEndCameraRendering;
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                SceneManager.sceneUnloaded += OnSceneUnloaded;

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded. ForwardOffset="
                    + _forwardOffset.Value.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; restored 0.2.0 Kandra bone, culling, and linked Drake equipment offsets are active; the world camera FOV is unchanged.");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowLoadTimeError(
                        PluginGuid,
                        PluginName,
                        exception);
                enabled = false;
            }
        }

        private void Update()
        {
            // A failed or interrupted camera render must never leave the hero
            // body displaced during gameplay or physics updates.
            RestoreRenderOffset();
            UpdateFireplaceOffsetBlend();
        }

        private void OnDisable()
        {
            RestoreRenderOffset();
            RestoreDrakeOffsets();
            ClearSceneCaches();
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            RenderPipelineManager.beginCameraRendering -=
                OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -=
                OnEndCameraRendering;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            RestoreDrakeOffsets();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            RestoreRenderOffset();
            ClearSceneCaches();

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            BeginSceneTransition(
                "active scene changed from "
                + SceneName(previous)
                + " to "
                + SceneName(next));
        }

        private void OnSceneUnloaded(Scene scene)
        {
            BeginSceneTransition("scene unloaded: " + SceneName(scene));
        }

        private void BeginSceneTransition(string reason)
        {
            RestoreRenderOffset();
            _suspendOffsetsUntilFrame = Math.Max(
                _suspendOffsetsUntilFrame,
                Time.frameCount + SceneTransitionSuspensionFrames);
            ClearSceneCaches();
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(
                    "Suspending first-person native offsets through frame "
                    + _suspendOffsetsUntilFrame.ToString(
                        CultureInfo.InvariantCulture)
                    + " after "
                    + reason
                    + ".");
            }
        }

        private void ClearSceneCaches()
        {
            _lastReportedRoot = null;
            _pendingKandraRigManager = null;
            _kandraCollectionFrame = -1;
            _lastSynchronizedKandraFrame = -1;
            _cachedKandraBodyData = null;
            _cachedKandraRigs = new KandraRig[0];
            _cachedKandraRenderers = new KandraRenderer[0];
            _nextKandraRigRefreshTime = 0.0f;
            _lastReportedKandraBodyData = null;
            _cachedMainHandWeapon = null;
            _cachedOffHandWeapon = null;
            _cachedWeaponEntityAccess = new LinkedEntitiesAccess[0];
            _nextWeaponEntityRefreshTime = 0.0f;
            _lastReportedMainHandWeapon = null;
            _lastReportedOffHandWeapon = null;
            _originalDrakeOffsets.Clear();
        }

        private bool OffsetsSuspended()
        {
            return Time.frameCount <= _suspendOffsetsUntilFrame;
        }

        private void UpdateFireplaceOffsetBlend()
        {
            FireplaceUI fireplace =
                Awaken.TG.MVC.World.Any<FireplaceUI>();
            bool interactionActive = fireplace != null
                && !fireplace.HasBeenDiscarded;
            float now = Time.unscaledTime;
            if (interactionActive != _fireplaceInteractionActive)
            {
                _fireplaceInteractionActive = interactionActive;
                BeginFireplaceBlend(0.0f, now);
                if (interactionActive)
                {
                    _waitingForFireplaceStand = false;
                    if (_diagnostics != null && _diagnostics.Value)
                    {
                        Logger.LogInfo(
                            "Blending the first-person offset to vanilla for a fireplace interaction.");
                    }
                }
                else
                {
                    _waitingForFireplaceStand = true;
                    _fireplaceStandFallbackUntil =
                        now + FireplaceStandFallbackSeconds;
                    if (_diagnostics != null && _diagnostics.Value)
                    {
                        Logger.LogInfo(
                            "Holding the vanilla first-person position until the fireplace stand-up transition completes.");
                    }
                }
            }

            if (_waitingForFireplaceStand)
            {
                bool signalAvailable;
                bool crouchTweenActive = IsHeroCrouchTweenActive(
                    out signalAvailable);
                if (!crouchTweenActive
                    && (signalAvailable
                        || now >= _fireplaceStandFallbackUntil))
                {
                    _waitingForFireplaceStand = false;
                    BeginFireplaceBlend(1.0f, now);
                    if (_diagnostics != null && _diagnostics.Value)
                    {
                        Logger.LogInfo(
                            signalAvailable
                                ? "Restoring the configured first-person offset after the controller completed its stand-up transition."
                                : "Restoring the configured first-person offset after the stand-up fallback interval.");
                    }
                }
            }

            AdvanceFireplaceBlend(now);
        }

        private void BeginFireplaceBlend(float target, float now)
        {
            _fireplaceBlendStart = _fireplaceOffsetBlend;
            _fireplaceBlendTarget = target;
            _fireplaceBlendStartedAt = now;
        }

        private void AdvanceFireplaceBlend(float now)
        {
            float duration = _fireplaceBlendTarget <= 0.0f
                ? FireplaceBlendOutSeconds
                : FireplaceBlendInSeconds;
            float elapsed = now - _fireplaceBlendStartedAt;
            float progress = duration <= 0.0f
                ? 1.0f
                : Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress
                * progress
                * (3.0f - (2.0f * progress));
            _fireplaceOffsetBlend = Mathf.LerpUnclamped(
                _fireplaceBlendStart,
                _fireplaceBlendTarget,
                easedProgress);
        }

        private bool IsHeroCrouchTweenActive(out bool signalAvailable)
        {
            signalAvailable = false;
            if (_crouchTweenActiveField == null)
            {
                return false;
            }

            Hero hero = Hero.Current;
            VHeroController controller = hero == null
                ? null
                : hero.VHeroController;
            if (controller == null)
            {
                return false;
            }

            try
            {
                object value = _crouchTweenActiveField.GetValue(controller);
                if (value is bool)
                {
                    signalAvailable = true;
                    return (bool)value;
                }
            }
            catch (Exception exception)
            {
                if (!_crouchTweenReadWarningLogged)
                {
                    _crouchTweenReadWarningLogged = true;
                    Logger.LogWarning(
                        "Could not read the hero crouch transition state; using the fireplace stand-up fallback interval: "
                        + exception.Message);
                }
            }

            return false;
        }

        private static string SceneName(Scene scene)
        {
            return scene.IsValid() && !String.IsNullOrEmpty(scene.name)
                ? scene.name
                : "<unnamed>";
        }

        private void PatchRenderSystems()
        {
            MethodInfo collectBoneMatrices = AccessTools.Method(
                typeof(RigManager),
                nameof(RigManager.CollectBoneMatrices));
            MethodInfo collectPostfix = AccessTools.Method(
                typeof(RigManagerCollectBoneMatricesPatch),
                nameof(RigManagerCollectBoneMatricesPatch.Postfix));
            MethodInfo linkedTransformUpdate = AccessTools.Method(
                typeof(LinkedTransformSystem),
                "OnUpdate");
            MethodInfo linkedTransformPrefix = AccessTools.Method(
                typeof(LinkedTransformSystemPatch),
                nameof(LinkedTransformSystemPatch.Prefix));
            _inputBonesArrayField = AccessTools.Field(
                typeof(RigManager),
                "_inputBonesArray");
            _readTransformField = AccessTools.Field(
                typeof(RigManager),
                "_readTransform");
            _bonesInFlightField = AccessTools.Field(
                typeof(RigManager),
                "_bonesInFlight");
            _crouchTweenActiveField = AccessTools.Field(
                typeof(VHeroController),
                "_crouchTweenActive");
            _linkedTransformsArrayField = AccessTools.Field(
                typeof(LinkedTransformSystem),
                "_transformsArray");
            if (collectBoneMatrices == null
                || collectPostfix == null
                || linkedTransformUpdate == null
                || linkedTransformPrefix == null
                || _inputBonesArrayField == null
                || _readTransformField == null
                || _bonesInFlightField == null)
            {
                throw new MissingMemberException(
                    "Could not resolve the restored Kandra and linked-transform members.");
            }
            if (_crouchTweenActiveField == null)
            {
                Logger.LogWarning(
                    "Could not resolve the hero crouch transition state; fireplace exits will use the conservative fallback interval.");
            }
            if (_linkedTransformsArrayField == null)
            {
                Logger.LogWarning(
                    "Could not resolve per-entity linked transforms; Drake equipment will use its parent transform as a compatibility fallback.");
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                collectBoneMatrices,
                postfix: new HarmonyMethod(collectPostfix));
            _harmony.Patch(
                linkedTransformUpdate,
                prefix: new HarmonyMethod(linkedTransformPrefix));
        }

        internal void CaptureKandraBoneCollection(RigManager rigManager)
        {
            if (rigManager == null
                || OffsetsSuspended()
                || _enabled == null
                || !_enabled.Value)
            {
                return;
            }

            _pendingKandraRigManager = rigManager;
            _kandraCollectionFrame = Time.frameCount;
        }

        private void ApplyLateKandraOffset(
            HeroBodyData bodyData,
            float3 translation)
        {
            if (OffsetsSuspended()
                || _lastSynchronizedKandraFrame == Time.frameCount
                || _pendingKandraRigManager == null
                || _kandraCollectionFrame != Time.frameCount
                || (uint)_bonesInFlightField.GetValue(
                    _pendingKandraRigManager) == 0)
            {
                return;
            }

            _lastSynchronizedKandraFrame = Time.frameCount;
            RefreshKandraRigs(bodyData);
            if (_cachedKandraRigs.Length == 0)
            {
                return;
            }

            NativeArray<Bone> bones =
                (NativeArray<Bone>)_inputBonesArrayField.GetValue(
                    _pendingKandraRigManager);
            if (!bones.IsCreated)
            {
                return;
            }

            JobHandle dependency =
                (JobHandle)_readTransformField.GetValue(
                    _pendingKandraRigManager);
            int rigCount = 0;
            int boneCount = 0;
            MemoryBookkeeper.MemoryRegion region =
                default(MemoryBookkeeper.MemoryRegion);
            for (int index = 0;
                index < _cachedKandraRigs.Length;
                index++)
            {
                KandraRig rig = _cachedKandraRigs[index];
                if (rig == null
                    || !_pendingKandraRigManager.TryGetMemoryRegionFor(
                        rig,
                        out region)
                    || !region.IsValid)
                {
                    continue;
                }

                int start = (int)region.start;
                int length = (int)region.length;
                if (start < 0
                    || length <= 0
                    || start > bones.Length
                    || length > bones.Length - start)
                {
                    continue;
                }

                dependency = new OffsetKandraBonesJob
                {
                    Bones = bones,
                    StartIndex = start,
                    Translation = translation
                }.Schedule(length, 32, dependency);
                rigCount++;
                boneCount += length;
            }

            if (rigCount == 0)
            {
                return;
            }

            _readTransformField.SetValue(
                _pendingKandraRigManager,
                dependency);
            int cullingRendererCount =
                ApplyKandraCullingOffset(translation);
            if (_diagnostics.Value
                && _lastReportedKandraBodyData != bodyData)
            {
                _lastReportedKandraBodyData = bodyData;
                Logger.LogInfo(
                    "Late-offsetting "
                    + boneCount.ToString(CultureInfo.InvariantCulture)
                    + " first-person Kandra bones across "
                    + rigCount.ToString(CultureInfo.InvariantCulture)
                    + " rig(s), with corrected culling for "
                    + cullingRendererCount.ToString(
                        CultureInfo.InvariantCulture)
                    + " renderer(s), under "
                    + GetTransformPath(bodyData.transform)
                    + ".");
            }
        }

        private int ApplyKandraCullingOffset(float3 translation)
        {
            if (OffsetsSuspended())
            {
                return 0;
            }

            KandraRendererManager rendererManager =
                KandraRendererManager.Instance;
            VisibilityCullingManager cullingManager =
                rendererManager == null
                    ? null
                    : rendererManager.VisibilityCullingManager;
            if (cullingManager == null
                || _cachedKandraRenderers.Length == 0
                || !cullingManager.rootBones.IsCreated
                || !cullingManager.xs.IsCreated
                || !cullingManager.ys.IsCreated
                || !cullingManager.zs.IsCreated)
            {
                return 0;
            }

            List<uint> rendererSlots = new List<uint>();
            for (int index = 0;
                index < _cachedKandraRenderers.Length;
                index++)
            {
                KandraRenderer renderer =
                    _cachedKandraRenderers[index];
                if (renderer == null
                    || KandraRendererManager.IsInvalidId(
                        renderer.RenderingId)
                    || KandraRendererManager.IsWaitingId(
                        renderer.RenderingId))
                {
                    continue;
                }

                uint slot = KandraRendererManager.USlot(
                    renderer.RenderingId);
                if (slot < cullingManager.rootBones.Length
                    && slot < cullingManager.xs.Length
                    && slot < cullingManager.ys.Length
                    && slot < cullingManager.zs.Length)
                {
                    rendererSlots.Add(slot);
                }
            }

            if (rendererSlots.Count == 0)
            {
                return 0;
            }

            NativeArray<uint> slots = new NativeArray<uint>(
                rendererSlots.Count,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            for (int index = 0; index < rendererSlots.Count; index++)
            {
                slots[index] = rendererSlots[index];
            }

            JobHandle cullingJob = new OffsetKandraCullingJob
            {
                Slots = slots,
                RootBones = cullingManager.rootBones,
                Xs = cullingManager.xs,
                Ys = cullingManager.ys,
                Zs = cullingManager.zs,
                Translation = translation
            }.Schedule(
                rendererSlots.Count,
                16,
                cullingManager.collectCullingDataJobHandle);
            cullingManager.collectCullingDataJobHandle =
                slots.Dispose(cullingJob);
            return rendererSlots.Count;
        }

        private void RefreshKandraRigs(HeroBodyData bodyData)
        {
            float now = Time.unscaledTime;
            if (_cachedKandraBodyData == bodyData
                && now < _nextKandraRigRefreshTime)
            {
                return;
            }

            _cachedKandraBodyData = bodyData;
            _nextKandraRigRefreshTime = now + 0.5f;
            HashSet<KandraRig> rigs = new HashSet<KandraRig>();
            KandraRig[] bodyRigs =
                bodyData.GetComponentsInChildren<KandraRig>(true);
            for (int index = 0; index < bodyRigs.Length; index++)
            {
                if (bodyRigs[index] != null)
                {
                    rigs.Add(bodyRigs[index]);
                }
            }

            KandraRenderer[] renderers =
                bodyData.GetComponentsInChildren<KandraRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                KandraRenderer renderer = renderers[index];
                if (renderer != null && renderer.rendererData.rig != null)
                {
                    rigs.Add(renderer.rendererData.rig);
                }
            }

            _cachedKandraRigs = new KandraRig[rigs.Count];
            rigs.CopyTo(_cachedKandraRigs);
            _cachedKandraRenderers = renderers;
        }

        internal void ApplyDrakeWeaponOffset(
            LinkedTransformSystem linkedTransformSystem)
        {
            if (linkedTransformSystem == null)
            {
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null
                || !world.IsCreated
                || linkedTransformSystem.World != world)
            {
                return;
            }

            EntityManager entityManager =
                linkedTransformSystem.EntityManager;
            if (OffsetsSuspended())
            {
                _originalDrakeOffsets.Clear();
                return;
            }

            Hero hero = Hero.Current;
            if (_enabled == null
                || !_enabled.Value
                || hero == null
                || Hero.TppActive)
            {
                RestoreDrakeOffsets(entityManager, null);
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            Camera camera = controller == null
                ? null
                : controller.MainCamera;
            if (bodyData == null || camera == null)
            {
                RestoreDrakeOffsets(entityManager, null);
                return;
            }

            Vector3 localOffset = GetEffectiveLocalOffset(hero);
            if (localOffset.sqrMagnitude <= 0.00000001f)
            {
                RestoreDrakeOffsets(entityManager, null);
                return;
            }

            Vector3 worldOffset =
                camera.transform.TransformVector(localOffset);
            float3 translation = new float3(
                worldOffset.x,
                worldOffset.y,
                worldOffset.z);
            ApplyLateKandraOffset(bodyData, translation);
            RefreshWeaponEntityAccess(hero);
            if (_cachedWeaponEntityAccess.Length == 0)
            {
                RestoreDrakeOffsets(entityManager, null);
                return;
            }

            entityManager.CompleteDependencyBeforeRW<
                LinkedTransformLocalToWorldOffsetComponent>();
            TransformAccessArray linkedTransforms =
                default(TransformAccessArray);
            bool hasLinkedTransforms = _linkedTransformsArrayField != null;
            if (hasLinkedTransforms)
            {
                linkedTransforms = (TransformAccessArray)
                    _linkedTransformsArrayField.GetValue(
                        linkedTransformSystem);
                hasLinkedTransforms = linkedTransforms.isCreated;
            }
            HashSet<Entity> retainedEntities = new HashSet<Entity>();
            for (int index = 0;
                index < _cachedWeaponEntityAccess.Length;
                index++)
            {
                LinkedEntitiesAccess access =
                    _cachedWeaponEntityAccess[index];
                if (access == null)
                {
                    continue;
                }

                UnsafeArray<Entity> linkedEntities =
                    access.LinkedEntities;
                if (!linkedEntities.IsCreated)
                {
                    continue;
                }

                for (uint entityIndex = 0;
                    entityIndex < linkedEntities.Length;
                    entityIndex++)
                {
                    Entity entity = linkedEntities[entityIndex];
                    if (retainedEntities.Contains(entity)
                        || !entityManager.Exists(entity)
                        || !entityManager.HasComponent<LocalToWorld>(entity)
                        || !entityManager.HasComponent<
                            LinkedTransformComponent>(entity))
                    {
                        continue;
                    }

                    retainedEntities.Add(entity);
                    Vector3 localTranslation =
                        access.transform.InverseTransformVector(worldOffset);
                    if (hasLinkedTransforms
                        && entityManager.HasComponent<
                            LinkedTransformIndexComponent>(entity))
                    {
                        LinkedTransformIndexComponent linkedIndex =
                            entityManager.GetComponentData<
                                LinkedTransformIndexComponent>(entity);
                        if (linkedIndex.index >= 0
                            && linkedIndex.index < linkedTransforms.length)
                        {
                            Transform linkedTransform =
                                linkedTransforms[linkedIndex.index];
                            if (linkedTransform != null)
                            {
                                localTranslation =
                                    linkedTransform.InverseTransformVector(
                                        worldOffset);
                            }
                        }
                    }
                    float4x4 entityOffset = float4x4.identity;
                    entityOffset.c3 = new float4(
                        localTranslation.x,
                        localTranslation.y,
                        localTranslation.z,
                        1.0f);

                    DrakeOffsetState originalState;
                    if (!_originalDrakeOffsets.TryGetValue(
                        entity,
                        out originalState))
                    {
                        originalState = new DrakeOffsetState
                        {
                            HadOriginalOffset =
                                entityManager.HasComponent<
                                    LinkedTransformLocalToWorldOffsetComponent>(
                                        entity),
                            OriginalOffset = float4x4.identity
                        };
                        if (originalState.HadOriginalOffset)
                        {
                            originalState.OriginalOffset =
                                entityManager.GetComponentData<
                                    LinkedTransformLocalToWorldOffsetComponent>(
                                        entity).offsetMatrix;
                        }
                        _originalDrakeOffsets.Add(entity, originalState);
                    }

                    LinkedTransformLocalToWorldOffsetComponent offset =
                        new LinkedTransformLocalToWorldOffsetComponent(
                            math.mul(
                                entityOffset,
                                originalState.OriginalOffset));
                    if (entityManager.HasComponent<
                        LinkedTransformLocalToWorldOffsetComponent>(entity))
                    {
                        entityManager.SetComponentData(entity, offset);
                    }
                    else
                    {
                        entityManager.AddComponentData(entity, offset);
                    }
                }
            }

            RestoreDrakeOffsets(entityManager, retainedEntities);
            if (_diagnostics.Value
                && retainedEntities.Count > 0
                && (_lastReportedMainHandWeapon != hero.MainHandWeapon
                    || _lastReportedOffHandWeapon
                        != hero.OffHandWeapon))
            {
                _lastReportedMainHandWeapon = hero.MainHandWeapon;
                _lastReportedOffHandWeapon = hero.OffHandWeapon;
                Logger.LogInfo(
                    "Applying the first-person offset to "
                    + retainedEntities.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + " linked Drake weapon render entity or entities before the game's transform sync.");
            }
        }

        private void RestoreDrakeOffsets()
        {
            if (_originalDrakeOffsets.Count == 0)
            {
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _originalDrakeOffsets.Clear();
                return;
            }

            RestoreDrakeOffsets(world.EntityManager, null);
        }

        private void RestoreDrakeOffsets(
            EntityManager entityManager,
            HashSet<Entity> retainedEntities)
        {
            if (_originalDrakeOffsets.Count == 0)
            {
                return;
            }

            entityManager.CompleteDependencyBeforeRW<
                LinkedTransformLocalToWorldOffsetComponent>();
            List<Entity> restoredEntities = new List<Entity>();
            foreach (KeyValuePair<Entity, DrakeOffsetState> pair
                in _originalDrakeOffsets)
            {
                Entity entity = pair.Key;
                if (retainedEntities != null
                    && retainedEntities.Contains(entity))
                {
                    continue;
                }

                if (entityManager.Exists(entity))
                {
                    if (pair.Value.HadOriginalOffset)
                    {
                        LinkedTransformLocalToWorldOffsetComponent original =
                            new LinkedTransformLocalToWorldOffsetComponent(
                                pair.Value.OriginalOffset);
                        if (entityManager.HasComponent<
                            LinkedTransformLocalToWorldOffsetComponent>(
                                entity))
                        {
                            entityManager.SetComponentData(entity, original);
                        }
                        else
                        {
                            entityManager.AddComponentData(entity, original);
                        }
                    }
                    else if (entityManager.HasComponent<
                        LinkedTransformLocalToWorldOffsetComponent>(entity))
                    {
                        entityManager.RemoveComponent<
                            LinkedTransformLocalToWorldOffsetComponent>(entity);
                    }
                }
                restoredEntities.Add(entity);
            }

            for (int index = 0;
                index < restoredEntities.Count;
                index++)
            {
                _originalDrakeOffsets.Remove(restoredEntities[index]);
            }
        }

        private void RefreshWeaponEntityAccess(Hero hero)
        {
            CharacterHandBase mainWeapon = hero.MainHandWeapon;
            CharacterHandBase offWeapon = hero.OffHandWeapon;
            float now = Time.unscaledTime;
            if (_cachedMainHandWeapon == mainWeapon
                && _cachedOffHandWeapon == offWeapon
                && now < _nextWeaponEntityRefreshTime)
            {
                return;
            }

            _cachedMainHandWeapon = mainWeapon;
            _cachedOffHandWeapon = offWeapon;
            _nextWeaponEntityRefreshTime = now + 0.1f;
            HashSet<LinkedEntitiesAccess> accesses =
                new HashSet<LinkedEntitiesAccess>();
            CollectWeaponEntityAccess(mainWeapon, accesses);
            CollectWeaponEntityAccess(offWeapon, accesses);
            CollectWeaponEntityAccess(hero.MainHand, accesses);
            CollectWeaponEntityAccess(hero.OffHand, accesses);
            _cachedWeaponEntityAccess =
                new LinkedEntitiesAccess[accesses.Count];
            accesses.CopyTo(_cachedWeaponEntityAccess);
        }

        private static void CollectWeaponEntityAccess(
            CharacterHandBase weapon,
            HashSet<LinkedEntitiesAccess> accesses)
        {
            if (weapon == null)
            {
                return;
            }

            LinkedEntitiesAccess[] weaponAccesses =
                weapon.GetComponentsInChildren<LinkedEntitiesAccess>(true);
            for (int index = 0;
                index < weaponAccesses.Length;
                index++)
            {
                if (weaponAccesses[index] != null)
                {
                    accesses.Add(weaponAccesses[index]);
                }
            }
        }

        private static void CollectWeaponEntityAccess(
            Transform handSocket,
            HashSet<LinkedEntitiesAccess> accesses)
        {
            if (handSocket == null)
            {
                return;
            }

            LinkedEntitiesAccess[] socketAccesses =
                handSocket.GetComponentsInChildren<LinkedEntitiesAccess>(true);
            for (int index = 0;
                index < socketAccesses.Length;
                index++)
            {
                if (socketAccesses[index] != null)
                {
                    accesses.Add(socketAccesses[index]);
                }
            }
        }

        private void OnCameraPreCull(Camera camera)
        {
            TryApplyRenderOffset(camera);
        }

        private void OnCameraPostRender(Camera camera)
        {
            RestoreRenderOffset(camera);
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            TryApplyRenderOffset(camera);
        }

        private void OnEndCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            RestoreRenderOffset(camera);
        }

        private void TryApplyRenderOffset(Camera camera)
        {
            if (_renderOffsetApplied
                || camera == null
                || OffsetsSuspended()
                || _enabled == null
                || !_enabled.Value)
            {
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null || Hero.TppActive)
            {
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            if (bodyData == null
                || bodyData.transform == null
                || controller.MainCamera != camera)
            {
                return;
            }

            Vector3 localOffset = GetEffectiveLocalOffset(hero);
            if (localOffset.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            _offsetRoot = bodyData.transform;
            _offsetCamera = camera;
            _originalWorldPosition = _offsetRoot.position;
            _offsetRoot.position =
                _originalWorldPosition
                + camera.transform.TransformVector(localOffset);
            _renderOffsetApplied = true;

            if (_diagnostics.Value && _lastReportedRoot != _offsetRoot)
            {
                _lastReportedRoot = _offsetRoot;
                Logger.LogInfo(
                    "Applying first-person render offset to "
                    + GetTransformPath(_offsetRoot)
                    + ": horizontal="
                    + localOffset.x.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + ", vertical="
                    + localOffset.y.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + ", forward="
                    + localOffset.z.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " meters.");
            }
        }

        private void RestoreRenderOffset(Camera camera)
        {
            if (_renderOffsetApplied && _offsetCamera == camera)
            {
                RestoreRenderOffset();
            }
        }

        private void RestoreRenderOffset()
        {
            if (!_renderOffsetApplied)
            {
                return;
            }

            if (_offsetRoot != null)
            {
                _offsetRoot.position = _originalWorldPosition;
            }

            _renderOffsetApplied = false;
            _offsetRoot = null;
            _offsetCamera = null;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private float GetEffectiveForwardOffset(Hero hero)
        {
            if (_useCategoryForwardOffsets == null
                || !_useCategoryForwardOffsets.Value
                || hero == null)
            {
                return _forwardOffset.Value;
            }

            if (hero.MainHandWeapon is CharacterBow
                || hero.OffHandWeapon is CharacterBow)
            {
                return _bowForwardOffset.Value;
            }

            if (hero.MainHandWeapon is CharacterMagic
                || hero.OffHandWeapon is CharacterMagic)
            {
                return _magicForwardOffset.Value;
            }

            if (hero.MainHandWeapon != null
                || hero.OffHandWeapon != null)
            {
                return _meleeForwardOffset.Value;
            }

            return _forwardOffset.Value;
        }

        private Vector3 GetEffectiveLocalOffset(Hero hero)
        {
            UpdateFireplaceOffsetBlend();
            return new Vector3(
                _horizontalOffset.Value,
                _verticalOffset.Value,
                GetEffectiveForwardOffset(hero))
                * _fireplaceOffsetBlend;
        }

        internal void BeginBowDiagnostics(
            CharacterBow bow,
            string state)
        {
            Hero hero = Hero.Current;
            if (OffsetsSuspended()
                || hero == null
                || (hero.MainHandWeapon != bow
                    && hero.OffHandWeapon != bow))
            {
                return;
            }

            _bowDiagnosticState = state;
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }

            if (String.Equals(
                    state,
                    nameof(CharacterBow.OnHoldBow),
                    StringComparison.Ordinal))
            {
                _bowDiagnosticsUntilTime = Time.unscaledTime + 20.0f;
            }
            else if (String.Equals(
                    state,
                    nameof(CharacterBow.OnPullBow),
                    StringComparison.Ordinal))
            {
                _bowDiagnosticsUntilTime = Time.unscaledTime + 5.0f;
            }
            else
            {
                _bowDiagnosticsUntilTime = Time.unscaledTime + 2.0f;
            }

            _nextBowDiagnosticTime = Time.unscaledTime + 0.25f;
            LogBowDiagnosticSnapshot(bow, state + " transition");
        }

        internal void CaptureBowArrowToggle(
            CharacterBow bow,
            bool requestedMainHandEnabled)
        {
            Hero hero = Hero.Current;
            if (OffsetsSuspended()
                || hero == null
                || (hero.MainHandWeapon != bow
                    && hero.OffHandWeapon != bow)
                || _diagnostics == null
                || !_diagnostics.Value)
            {
                return;
            }

            _bowDiagnosticsUntilTime = Math.Max(
                _bowDiagnosticsUntilTime,
                Time.unscaledTime + 1.0f);
            LogBowDiagnosticSnapshot(
                bow,
                "ToggleArrows requestedMainHandEnabled="
                    + requestedMainHandEnabled.ToString());
        }

        private void UpdateBowDiagnostics()
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || OffsetsSuspended()
                || Time.unscaledTime > _bowDiagnosticsUntilTime
                || Time.unscaledTime < _nextBowDiagnosticTime)
            {
                return;
            }

            _nextBowDiagnosticTime = Time.unscaledTime + 0.25f;
            Hero hero = Hero.Current;
            CharacterBow bow = hero == null
                ? null
                : hero.MainHandWeapon as CharacterBow;
            if (bow == null && hero != null)
            {
                bow = hero.OffHandWeapon as CharacterBow;
            }

            LogBowDiagnosticSnapshot(
                bow,
                (_bowDiagnosticState ?? "bow") + " sample");
        }

        private void UpdateViewmodelLayerDiagnostics()
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || OffsetsSuspended()
                || Time.unscaledTime < _nextViewmodelDiagnosticTime)
            {
                return;
            }

            _nextViewmodelDiagnosticTime = Time.unscaledTime + 1.0f;
            Hero hero = Hero.Current;
            if (hero == null || Hero.TppActive)
            {
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            Camera camera = controller == null
                ? null
                : controller.MainCamera;
            CharacterBow bow = hero.MainHandWeapon as CharacterBow;
            if (bow == null)
            {
                bow = hero.OffHandWeapon as CharacterBow;
            }

            GameObject mainArrow = GetPooledArrowInstance(
                bow,
                _bowArrowInMainHandField);
            GameObject controllerArrow = GetPooledArrowInstance(
                bow,
                _bowArrowInControllerField);
            string signature = BuildHierarchyStateToken(
                    bodyData == null ? null : bodyData.transform)
                + "|"
                + BuildHierarchyStateToken(hero.MainHand)
                + "|"
                + BuildHierarchyStateToken(hero.OffHand)
                + "|"
                + BuildHierarchyStateToken(
                    hero.MainHandWeapon == null
                        ? null
                        : hero.MainHandWeapon.transform)
                + "|"
                + BuildHierarchyStateToken(
                    hero.OffHandWeapon == null
                        ? null
                        : hero.OffHandWeapon.transform)
                + "|"
                + BuildHierarchyStateToken(
                    mainArrow == null ? null : mainArrow.transform)
                + "|"
                + BuildHierarchyStateToken(
                    controllerArrow == null
                        ? null
                        : controllerArrow.transform);
            if (String.Equals(
                    signature,
                    _viewmodelDiagnosticSignature,
                    StringComparison.Ordinal))
            {
                return;
            }

            _viewmodelDiagnosticSignature = signature;
            try
            {
                Logger.LogInfo(
                    "Viewmodel layer diagnostic frame="
                    + Time.frameCount.ToString(CultureInfo.InvariantCulture)
                    + "; camera="
                    + (camera == null
                        ? "missing"
                        : GetTransformPath(camera.transform)
                            + "{cullingMask=0x"
                            + camera.cullingMask.ToString("X8")
                            + ",nearClip="
                            + camera.nearClipPlane.ToString(
                                "0.####",
                                CultureInfo.InvariantCulture)
                            + "}")
                    + "; body="
                    + DescribeRenderableHierarchy(
                        bodyData == null ? null : bodyData.transform)
                    + "; mainSocket="
                    + DescribeRenderableHierarchy(hero.MainHand)
                    + "; offSocket="
                    + DescribeRenderableHierarchy(hero.OffHand)
                    + "; mainWeapon="
                    + DescribeRenderableHierarchy(
                        hero.MainHandWeapon == null
                            ? null
                            : hero.MainHandWeapon.transform)
                    + "; offWeapon="
                    + DescribeRenderableHierarchy(
                        hero.OffHandWeapon == null
                            ? null
                            : hero.OffHandWeapon.transform)
                    + "; arrowMain="
                    + DescribeRenderableHierarchy(
                        mainArrow == null ? null : mainArrow.transform)
                    + "; arrowController="
                    + DescribeRenderableHierarchy(
                        controllerArrow == null
                            ? null
                            : controllerArrow.transform)
                    + ".");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Viewmodel layer diagnostics skipped after an error: "
                    + exception.Message);
            }
        }

        private static GameObject GetPooledArrowInstance(
            CharacterBow bow,
            FieldInfo field)
        {
            if (bow == null || field == null)
            {
                return null;
            }

            IPooledInstance pooled = field.GetValue(bow) as IPooledInstance;
            return pooled != null && pooled.InstanceLoaded
                ? pooled.Instance
                : null;
        }

        private static string BuildHierarchyStateToken(Transform root)
        {
            if (root == null)
            {
                return "missing";
            }

            return root.GetInstanceID().ToString(
                    CultureInfo.InvariantCulture)
                + ":"
                + root.gameObject.activeSelf.ToString()
                + ":"
                + root.gameObject.activeInHierarchy.ToString()
                + ":"
                + root.gameObject.layer.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static string DescribeRenderableHierarchy(Transform root)
        {
            if (root == null)
            {
                return "missing";
            }

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            KandraRenderer[] kandraRenderers =
                root.GetComponentsInChildren<KandraRenderer>(true);
            Renderer[] unityRenderers =
                root.GetComponentsInChildren<Renderer>(true);
            int[] layerCounts = new int[32];
            int activeObjects = 0;
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];
                if (current == null)
                {
                    continue;
                }

                int layer = current.gameObject.layer;
                if (layer >= 0 && layer < layerCounts.Length)
                {
                    layerCounts[layer]++;
                }
                if (current.gameObject.activeInHierarchy)
                {
                    activeObjects++;
                }
            }

            StringBuilder description = new StringBuilder();
            description.Append(GetTransformPath(root));
            description.Append("{rootLayer=");
            description.Append(DescribeLayer(root.gameObject.layer));
            description.Append(",active=");
            description.Append(root.gameObject.activeInHierarchy);
            description.Append(",objects=");
            description.Append(transforms.Length);
            description.Append(",activeObjects=");
            description.Append(activeObjects);
            description.Append(",layers=");
            AppendLayerCounts(description, layerCounts);
            description.Append(",kandra=");
            description.Append(kandraRenderers.Length);
            AppendRendererSamples(description, kandraRenderers);
            description.Append(",unityRenderers=");
            description.Append(unityRenderers.Length);
            AppendRendererSamples(description, unityRenderers);
            description.Append("}");
            return description.ToString();
        }

        private static void AppendLayerCounts(
            StringBuilder description,
            int[] layerCounts)
        {
            bool appended = false;
            for (int layer = 0; layer < layerCounts.Length; layer++)
            {
                if (layerCounts[layer] == 0)
                {
                    continue;
                }

                if (appended)
                {
                    description.Append("|");
                }
                description.Append(DescribeLayer(layer));
                description.Append(":");
                description.Append(layerCounts[layer]);
                appended = true;
            }
            if (!appended)
            {
                description.Append("none");
            }
        }

        private static void AppendRendererSamples<T>(
            StringBuilder description,
            T[] renderers)
            where T : Component
        {
            int sampleCount = Math.Min(renderers.Length, 8);
            if (sampleCount == 0)
            {
                return;
            }

            description.Append("[");
            for (int index = 0; index < sampleCount; index++)
            {
                if (index > 0)
                {
                    description.Append("|");
                }

                Component renderer = renderers[index];
                if (renderer == null)
                {
                    description.Append("destroyed");
                    continue;
                }

                description.Append(GetTransformPath(renderer.transform));
                description.Append("@");
                description.Append(
                    DescribeLayer(renderer.gameObject.layer));
                description.Append(":");
                description.Append(
                    renderer.gameObject.activeInHierarchy
                        ? "active"
                        : "inactive");
            }
            if (renderers.Length > sampleCount)
            {
                description.Append("|+");
                description.Append(renderers.Length - sampleCount);
            }
            description.Append("]");
        }

        private static string DescribeLayer(int layer)
        {
            string name = LayerMask.LayerToName(layer);
            return layer.ToString(CultureInfo.InvariantCulture)
                + (String.IsNullOrEmpty(name) ? String.Empty : ":" + name);
        }

        private void LogBowDiagnosticSnapshot(
            CharacterBow bow,
            string reason)
        {
            try
            {
                Hero hero = Hero.Current;
                VHeroController controller = hero == null
                    ? null
                    : hero.VHeroController;
                Camera camera = controller == null
                    ? null
                    : controller.MainCamera;
                HeroBodyData bodyData = controller == null
                    ? null
                    : controller.BodyData;
                if (bow == null && hero != null)
                {
                    bow = hero.MainHandWeapon as CharacterBow;
                    if (bow == null)
                    {
                        bow = hero.OffHandWeapon as CharacterBow;
                    }
                }

                string mainHandEnabled = "unknown";
                if (bow != null && _bowMainHandEnabledField != null)
                {
                    mainHandEnabled = ((bool)_bowMainHandEnabledField
                        .GetValue(bow)).ToString();
                }

                string kandraState = DescribeKandraVisibility(bodyData);
                Vector3 visualWorldOffset =
                    camera == null || hero == null
                        ? Vector3.zero
                        : camera.transform.TransformVector(
                            new Vector3(
                                _horizontalOffset.Value,
                                _verticalOffset.Value,
                                GetEffectiveForwardOffset(hero)));
                Logger.LogInfo(
                    "Bow diagnostic ["
                    + reason
                    + "] frame="
                    + Time.frameCount.ToString(CultureInfo.InvariantCulture)
                    + "; nearClip="
                    + (camera == null
                        ? "missing"
                        : camera.nearClipPlane.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture))
                    + "; effectiveForward="
                    + (hero == null
                        ? "missing"
                        : GetEffectiveForwardOffset(hero).ToString(
                            "0.###",
                            CultureInfo.InvariantCulture))
                    + "; mainHandEnabled="
                    + mainHandEnabled
                    + "; mainHand="
                    + DescribeTransform(
                        hero == null ? null : hero.MainHand,
                        camera,
                        visualWorldOffset)
                    + "; offHand="
                    + DescribeTransform(
                        hero == null ? null : hero.OffHand,
                        camera,
                        visualWorldOffset)
                    + "; arrowMain="
                    + DescribePooledArrow(
                        bow,
                        _bowArrowInMainHandField,
                        camera,
                        visualWorldOffset)
                    + "; arrowController="
                    + DescribePooledArrow(
                        bow,
                        _bowArrowInControllerField,
                        camera,
                        visualWorldOffset)
                    + "; kandra="
                    + kandraState
                    + ".");
            }
            catch (Exception exception)
            {
                _bowDiagnosticsUntilTime = 0.0f;
                Logger.LogWarning(
                    "Bow diagnostic sampling stopped after an error: "
                    + exception.Message);
            }
        }

        private string DescribeKandraVisibility(HeroBodyData bodyData)
        {
            if (bodyData == null)
            {
                return "body-missing";
            }

            KandraRenderer[] renderers =
                bodyData.GetComponentsInChildren<KandraRenderer>(true);
            KandraRendererManager manager = KandraRendererManager.Instance;
            int active = 0;
            int registered = 0;
            int cameraVisible = 0;
            for (int index = 0;
                index < renderers.Length;
                index++)
            {
                KandraRenderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.gameObject.activeInHierarchy)
                {
                    active++;
                }

                if (manager == null
                    || KandraRendererManager.IsInvalidId(
                        renderer.RenderingId)
                    || KandraRendererManager.IsWaitingId(
                        renderer.RenderingId))
                {
                    continue;
                }

                registered++;
                if (manager.IsCameraVisible(renderer.RenderingId))
                {
                    cameraVisible++;
                }
            }

            return "total="
                + renderers.Length.ToString(
                    CultureInfo.InvariantCulture)
                + ",active="
                + active.ToString(CultureInfo.InvariantCulture)
                + ",registered="
                + registered.ToString(CultureInfo.InvariantCulture)
                + ",cameraVisible="
                + cameraVisible.ToString(CultureInfo.InvariantCulture);
        }

        private static string DescribePooledArrow(
            CharacterBow bow,
            FieldInfo field,
            Camera camera,
            Vector3 visualWorldOffset)
        {
            if (bow == null)
            {
                return "bow-missing";
            }
            if (field == null)
            {
                return "field-missing";
            }

            IPooledInstance pooled = field.GetValue(bow) as IPooledInstance;
            if (pooled == null)
            {
                return "pool-null";
            }
            if (!pooled.InstanceLoaded)
            {
                return "not-loaded";
            }

            GameObject arrow = pooled.Instance;
            return arrow == null
                ? "instance-null"
                : DescribeTransform(
                    arrow.transform,
                    camera,
                    visualWorldOffset);
        }

        private static string DescribeTransform(
            Transform transform,
            Camera camera,
            Vector3 visualWorldOffset)
        {
            if (transform == null)
            {
                return "missing";
            }

            Vector3 viewport = camera == null
                ? Vector3.zero
                : camera.WorldToViewportPoint(transform.position);
            Vector3 renderedViewport = camera == null
                ? Vector3.zero
                : camera.WorldToViewportPoint(
                    transform.position + visualWorldOffset);
            return GetTransformPath(transform)
                + "{layer="
                + DescribeLayer(transform.gameObject.layer)
                + ",activeSelf="
                + transform.gameObject.activeSelf.ToString()
                + ",activeHierarchy="
                + transform.gameObject.activeInHierarchy.ToString()
                + ",parent="
                + (transform.parent == null
                    ? "none"
                    : transform.parent.name)
                + ",local="
                + FormatVector3(transform.localPosition)
                + ",viewport="
                + (camera == null
                    ? "camera-missing"
                    : FormatVector3(viewport))
                + ",renderViewport="
                + (camera == null
                    ? "camera-missing"
                    : FormatVector3(renderedViewport))
                + "}";
        }

        private static string FormatVector3(Vector3 value)
        {
            return "("
                + value.x.ToString("0.###", CultureInfo.InvariantCulture)
                + ","
                + value.y.ToString("0.###", CultureInfo.InvariantCulture)
                + ","
                + value.z.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
        }

        private void BindConfig()
        {
            Config.Bind(
                "1. Core",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "1. Core",
                "Enabled",
                true,
                "Master switch for the first-person render offset.");
            _useCategoryForwardOffsets = Config.Bind(
                "1. Core",
                "UseCategoryForwardOffsets",
                true,
                "Use separate forward offsets for melee weapons, bows, and magic. Unarmed and unknown equipment continue to use ForwardOffset.");
            _forwardOffset = Config.Bind(
                "2. Viewmodel Position",
                "ForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Meters to move the first-person body and equipped items away from the camera. Positive values make them appear farther away without changing FOV.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _horizontalOffset = Config.Bind(
                "2. Viewmodel Position",
                "HorizontalOffset",
                0.0f,
                new ConfigDescription(
                    "Meters to move the first-person body and equipped items right or left in camera space.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _verticalOffset = Config.Bind(
                "2. Viewmodel Position",
                "VerticalOffset",
                0.0f,
                new ConfigDescription(
                    "Meters to move the first-person body and equipped items up or down in camera space.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _meleeForwardOffset = Config.Bind(
                "2. Viewmodel Position",
                "MeleeForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Forward offset used for melee weapons when UseCategoryForwardOffsets is enabled.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _bowForwardOffset = Config.Bind(
                "2. Viewmodel Position",
                "BowForwardOffset",
                0.10f,
                new ConfigDescription(
                    "Forward offset used for bows when UseCategoryForwardOffsets is enabled.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _magicForwardOffset = Config.Bind(
                "2. Viewmodel Position",
                "MagicForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Forward offset used for equipped magic when UseCategoryForwardOffsets is enabled.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f)));
            _diagnostics = Config.Bind(
                "3. Diagnostics",
                "Diagnostics",
                false,
                "Log the resolved first-person hierarchy and active Kandra bone, culling, and linked Drake equipment offset paths.");

            RestorePreservedSettings();
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
            if (String.IsNullOrWhiteSpace(configPath)
                || !File.Exists(configPath))
            {
                return;
            }

            int storedSchemaVersion = 0;
            string currentSection = String.Empty;
            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length > 1
                    && line[0] == '['
                    && line[line.Length - 1] == ']')
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                const string schemaPrefix = "ConfigSchemaVersion =";
                if (String.Equals(
                        currentSection,
                        "1. Core",
                        StringComparison.Ordinal)
                    && line.StartsWith(
                        schemaPrefix,
                        StringComparison.Ordinal))
                {
                    Int32.TryParse(
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

            CapturePreservedSettings(configPath, storedSchemaVersion);
            string backupPath = configPath
                + ".pre-schema-"
                + storedSchemaVersion.ToString(
                    CultureInfo.InvariantCulture)
                + "-"
                + DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture)
                + ".bak";

            try
            {
                File.Copy(configPath, backupPath, false);
                File.WriteAllText(configPath, String.Empty);
                Config.Clear();
                Config.Reload();
                Logger.LogInfo(
                    "Configuration schema changed from "
                    + storedSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + " to "
                    + ConfigSchemaVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ". Generated fresh defaults and backed up the old config to "
                    + backupPath
                    + ".");
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier
                    .TryShowConfigReset(
                        PluginGuid,
                        PluginName,
                        storedSchemaVersion,
                        ConfigSchemaVersion);
            }
            catch (Exception exception)
            {
                ClearPendingPreservedSettings();
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, configPath, true);
                        Config.Clear();
                        Config.Reload();
                    }
                }
                catch (Exception restoreException)
                {
                    Logger.LogError(
                        "Could not restore the previous "
                        + PluginName
                        + " config after a failed schema reset: "
                        + restoreException.Message);
                }

                throw new InvalidOperationException(
                    "Failed to reset "
                    + PluginName
                    + " config schema. Original config was left in place when possible.",
                    exception);
            }
        }

        private void CapturePreservedSettings(
            string configPath,
            int storedSchemaVersion)
        {
            ClearPendingPreservedSettings();
            Grailwright.Shared.ConfigRecoveryCustomizationProfile profile =
                Grailwright.Shared.ConfigPreviousSettingsRecovery
                    .ReadCustomizationProfile(
                        configPath,
                        storedSchemaVersion,
                        ConfigSchemaVersion,
                        ConfigRecoveryKeepCurrentDefaultRules,
                        ConfigRecoveryPermanentExclusions);

            _hasPendingEnabled = profile.TryGetCustomizedValue(
                "1. Core",
                "Enabled",
                out _pendingEnabled);
            _hasPendingForwardOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "ForwardOffset",
                out _pendingForwardOffset);
            _hasPendingHorizontalOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "HorizontalOffset",
                out _pendingHorizontalOffset);
            _hasPendingVerticalOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "VerticalOffset",
                out _pendingVerticalOffset);
            _hasPendingMeleeForwardOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "MeleeForwardOffset",
                out _pendingMeleeForwardOffset);
            _hasPendingBowForwardOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "BowForwardOffset",
                out _pendingBowForwardOffset);
            _hasPendingMagicForwardOffset = profile.TryGetCustomizedValue(
                "2. Viewmodel Position",
                "MagicForwardOffset",
                out _pendingMagicForwardOffset);
            _hasPendingDiagnostics = profile.TryGetCustomizedValue(
                "3. Diagnostics",
                "Diagnostics",
                out _pendingDiagnostics);
        }

        private void RestorePreservedSettings()
        {
            int restoredCount = 0;
            int clampedCount = 0;
            bool clamped;
            if (_hasPendingEnabled
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enabled,
                    _pendingEnabled,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }

            RestorePreservedFloat(
                _hasPendingForwardOffset,
                _forwardOffset,
                _pendingForwardOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingHorizontalOffset,
                _horizontalOffset,
                _pendingHorizontalOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingVerticalOffset,
                _verticalOffset,
                _pendingVerticalOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingMeleeForwardOffset,
                _meleeForwardOffset,
                _pendingMeleeForwardOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingBowForwardOffset,
                _bowForwardOffset,
                _pendingBowForwardOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingMagicForwardOffset,
                _magicForwardOffset,
                _pendingMagicForwardOffset,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingDiagnostics
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _diagnostics,
                    _pendingDiagnostics,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (restoredCount > 0)
            {
                Logger.LogInfo(
                    "Preserved "
                    + restoredCount.ToString(
                        CultureInfo.InvariantCulture)
                    + " viewmodel setting(s) across the config schema reset; clamped="
                    + clampedCount.ToString(
                        CultureInfo.InvariantCulture)
                    + ".");
            }
            ClearPendingPreservedSettings();
        }

        private static void RestorePreservedFloat(
            bool hasPendingValue,
            ConfigEntry<float> entry,
            float pendingValue,
            ref int restoredCount,
            ref int clampedCount)
        {
            if (!hasPendingValue)
            {
                return;
            }

            bool clamped;
            if (!Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                entry,
                pendingValue,
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

        private void ClearPendingPreservedSettings()
        {
            _hasPendingEnabled = false;
            _hasPendingForwardOffset = false;
            _hasPendingHorizontalOffset = false;
            _hasPendingVerticalOffset = false;
            _hasPendingMeleeForwardOffset = false;
            _hasPendingBowForwardOffset = false;
            _hasPendingMagicForwardOffset = false;
            _hasPendingDiagnostics = false;
        }
    }

    internal static class RigManagerCollectBoneMatricesPatch
    {
        internal static void Postfix(RigManager __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.CaptureKandraBoneCollection(__instance);
            }
        }
    }

    internal static class LinkedTransformSystemPatch
    {
        internal static void Prefix(LinkedTransformSystem __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.ApplyDrakeWeaponOffset(__instance);
            }
        }
    }

    internal static class CharacterBowStatePatch
    {
        internal static void Postfix(
            CharacterBow __instance,
            MethodBase __originalMethod)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.BeginBowDiagnostics(
                    __instance,
                    __originalMethod.Name);
            }
        }
    }

    internal static class CharacterBowToggleArrowsPatch
    {
        internal static void Postfix(
            CharacterBow __instance,
            bool mainHandEnabled)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.CaptureBowArrowToggle(
                    __instance,
                    mainHandEnabled);
            }
        }
    }
}
