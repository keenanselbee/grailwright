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
using Awaken.TG.Main.Animations.FSM.Heroes.Base;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Main.Animations.FSM.Heroes.States.CameraShakes.Dash;
using Awaken.TG.Main.Crafting.Fireplace;
using Awaken.TG.Assets;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Settings.Accessibility;
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
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

[assembly: AssemblyTitle("First Person Arms Adjuster")]
[assembly: AssemblyDescription("Moves the rendered first-person arms and weapons without changing world FOV.")]
[assembly: AssemblyCompany("KS")]
[assembly: AssemblyProduct("First Person Arms Adjuster")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("0.8.8.0")]
[assembly: AssemblyFileVersion("0.8.8.0")]

namespace FirstPersonArmsAdjuster
{
    internal enum HeadBobPreset
    {
        Subtle,
        Balanced,
        Strong
    }

    public static class FirstPersonArmsAdjusterApi
    {
        public const int ApiVersion = 1;

        public static bool TryGetCurrentVisualWorldOffset(
            out Vector3 worldOffset)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance == null)
            {
                worldOffset = Vector3.zero;
                return false;
            }

            return instance.TryGetCurrentVisualWorldOffset(out worldOffset);
        }
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "ks.tgfoa.grail-floating-text",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        TrueThirdPersonPluginGuid,
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        KillingBlowMasteryPluginGuid,
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FirstPersonArmsAdjusterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "ks.tgfoa.first-person-arms-adjuster";
        public const string PluginName = "First Person Arms Adjuster";
        public const string PluginVersion = "0.8.8";
        public const string TrueThirdPersonPluginGuid =
            "kane.tgfoa.true-third-person";
        public const string KillingBlowMasteryPluginGuid =
            "ks.tgfoa.killing-blow-mastery";
        private const string KillingBlowMasteryExecutionVisualApiTypeName =
            "KillingBlowMastery.ExecutionVisualApi";

        private const int ConfigSchemaVersion = 20;
        private const int ConfigRecoveryBaselineSchema = 1;
        private const int SceneTransitionSuspensionFrames = 45;
        private const float FireplaceBlendOutSeconds = 0.25f;
        private const float FireplaceStandFallbackSeconds = 1.15f;
        private const float FireplaceBlendInSeconds = 0.40f;
        private const float HeldMeleeBlendOutSeconds = 0.16f;
        private const float HeldMeleeBlendInSeconds = 0.22f;
        private const float SprintAttackBlendOutSeconds = 0.12f;
        private const float SprintAttackBlendInSeconds = 0.22f;
        private const float BowDrawGuardBlendInSeconds = 0.18f;
        private const float BowDrawGuardBlendOutSeconds = 0.40f;
        private const float BowPullGuardFullNormalizedTime = 0.65f;
        private const float BowReleaseProjectileNormalizedTime = 0.05f;
        private const float DodgeRetractionMaximumMeters = 0.25f;
        private const float DodgeRetractionBlendInSeconds = 0.06f;
        private const float DodgeRetractionBlendOutSeconds = 0.20f;
        private const float DodgeRetractionHoldSeconds = 0.12f;
        private const float DodgeActivitySignalGraceSeconds = 0.05f;
        private const float ExecutionGuardBlendInSeconds = 0.15f;
        private const float ExecutionGuardBlendOutSeconds = 0.25f;
        private const float ExecutionMoveTowardVanillaStrength = 0.50f;
        private const float ExecutionShoulderRetractionMeters = 0.12f;
        private const float ExecutionNativeStateGraceSeconds = 0.25f;
        private const int ExecutionPhaseActive = 2;
        private const float HeadBobSpeedThreshold = 0.05f;
        private const float HeadBobMaximumDeltaTime = 0.05f;
        private const float HeadBobBlendInSeconds = 0.18f;
        private const float HeadBobBlendOutSeconds = 0.28f;
        private const float HeadBobMinimumResponseTime = 0.02f;
        private const float HeadBobMaximumResponseTime = 0.18f;
        private const float HeadBobSprintBlendSeconds = 0.20f;
        private const float HeadBobMaximumSprintAmplitudeBonus = 0.75f;
        private const float HeadBobMaximumSprintCadenceBonus = 0.25f;
        private const float HeadBobMaximumWalkVerticalCadenceHz = 3.2f;
        private const float HeadBobMaximumSprintVerticalCadenceHz = 4.2f;
        private const float HeadBobCadenceSoftKneeRatio = 0.8f;
        private const float DefaultShoulderSpineRetractionWeight = 0.35f;
        private const float DefaultShoulderSpine1RetractionWeight = 0.75f;
        private const float DefaultShoulderSpine2RetractionWeight = 1.0f;
        private const float DefaultShoulderJointRetractionWeight = 1.0f;
        private const float DefaultShoulderUpperArmRetractionWeight = 0.6f;
        private const float DefaultShoulderForearmRetractionWeight = 0.2f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float SheathingBlendStartNormalizedTime = 0.45f;
        private const float SheathingBlendEndNormalizedTime = 0.90f;
        private const float SheathingBlendRestoreSeconds = 0.20f;
        private static readonly Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule[]
            ConfigRecoveryKeepCurrentDefaultRules =
                new[]
                {
                    new Grailwright.Shared.ConfigRecoveryKeepCurrentDefaultRule(
                        16,
                        "Position",
                        "ShoulderRetraction",
                        "Schema 16 extends retraction behind the vanilla position and strengthens the torso bone taper.")
                };
        private static readonly ConfigDefinition[] ConfigRecoveryPermanentExclusions =
            new ConfigDefinition[0];
        private static readonly FieldInfo NativeHeadBobIntensityField =
            AccessTools.Field(typeof(HeadBobbingSetting), "_intensity");
        private static readonly PropertyInfo NativeHeadBobEnabledProperty =
            AccessTools.Property(typeof(HeadBobbingSetting), "Enabled");

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
            public float3 RendererRetraction;
            public float3 ShoulderRetraction;
            public bool ApplyShoulderRetraction;
            public float SpineRetractionWeight;
            public float Spine1RetractionWeight;
            public float Spine2RetractionWeight;
            public float LeftShoulderRetractionWeight;
            public float RightShoulderRetractionWeight;
            public float UpperArmRetractionWeight;
            public float ForearmRetractionWeight;
            public float LowerTorsoRetractionWeight;
            public float ChestHelperRetractionWeight;
            public float ShoulderFixRetractionWeight;
            public float NativeClothRetractionWeight;
            public float TestBoneRetractionWeight;
            public int TestBoneIndex;
            public int HipsIndex;
            public int LeftBreastBaseIndex;
            public int RightBreastBaseIndex;
            public int LeftBreastIndex;
            public int RightBreastIndex;
            public int LeftShoulderFixIndex;
            public int RightShoulderFixIndex;
            public int NativeClothStartIndex;
            public int NativeClothEndIndex;
            public bool NativeClothIndicesContiguous;
            public int SpineIndex;
            public int Spine1Index;
            public int Spine2Index;
            public int LeftShoulderIndex;
            public int RightShoulderIndex;
            public int LeftUpperArmIndex;
            public int RightUpperArmIndex;
            public int LeftForearmIndex;
            public int RightForearmIndex;

            public void Execute(int index)
            {
                int boneIndex = StartIndex + index;
                Bone bone = Bones[boneIndex];
                float shoulderWeight = 0.0f;
                if (ApplyShoulderRetraction)
                {
                    if (index == SpineIndex)
                    {
                        shoulderWeight = SpineRetractionWeight;
                    }
                    else if (index == Spine1Index)
                    {
                        shoulderWeight = Spine1RetractionWeight;
                    }
                    else if (index == Spine2Index)
                    {
                        shoulderWeight = Spine2RetractionWeight;
                    }
                    else if (index == LeftShoulderIndex)
                    {
                        shoulderWeight = LeftShoulderRetractionWeight;
                    }
                    else if (index == RightShoulderIndex)
                    {
                        shoulderWeight = RightShoulderRetractionWeight;
                    }
                    else if (index == LeftUpperArmIndex
                        || index == RightUpperArmIndex)
                    {
                        shoulderWeight = UpperArmRetractionWeight;
                    }
                    else if (index == LeftForearmIndex
                        || index == RightForearmIndex)
                    {
                        shoulderWeight = ForearmRetractionWeight;
                    }
                }
                if (index == TestBoneIndex)
                {
                    shoulderWeight += TestBoneRetractionWeight;
                }
                if (index == HipsIndex)
                {
                    shoulderWeight += LowerTorsoRetractionWeight;
                }
                if (index == LeftBreastBaseIndex
                    || index == RightBreastBaseIndex
                    || index == LeftBreastIndex
                    || index == RightBreastIndex)
                {
                    shoulderWeight += ChestHelperRetractionWeight;
                }
                if (index == LeftShoulderFixIndex
                    || index == RightShoulderFixIndex)
                {
                    shoulderWeight += ShoulderFixRetractionWeight;
                }
                if (NativeClothIndicesContiguous
                    && index >= NativeClothStartIndex
                    && index <= NativeClothEndIndex)
                {
                    shoulderWeight += NativeClothRetractionWeight;
                }
                bone.boneTransform.c3 =
                    bone.boneTransform.c3
                        + Translation
                        + RendererRetraction
                        + (ShoulderRetraction * shoulderWeight);
                Bones[boneIndex] = bone;
            }
        }

        private struct OffsetKandraCullingJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<uint> Slots;
            [ReadOnly]
            public NativeArray<float3> Translations;
            public UnsafeArray<float4x4> RootBones;
            public UnsafeArray<float> Xs;
            public UnsafeArray<float> Ys;
            public UnsafeArray<float> Zs;
            public void Execute(int index)
            {
                uint slot = Slots[index];
                float3 translation = Translations[index];
                float4x4 rootBone = RootBones[slot];
                rootBone.c3 = new float4(
                    rootBone.c3.xyz + translation,
                    rootBone.c3.w);
                RootBones[slot] = rootBone;
                Xs[slot] += translation.x;
                Ys[slot] += translation.y;
                Zs[slot] += translation.z;
            }
        }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _forwardOffset;
        private ConfigEntry<float> _horizontalOffset;
        private ConfigEntry<float> _verticalOffset;
        private ConfigEntry<float> _shoulderRetraction;
        private ConfigEntry<float> _spineRetractionPercent;
        private ConfigEntry<float> _spine1RetractionPercent;
        private ConfigEntry<float> _spine2RetractionPercent;
        private ConfigEntry<float> _leftShoulderRetractionPercent;
        private ConfigEntry<float> _rightShoulderRetractionPercent;
        private ConfigEntry<float> _upperArmRetractionPercent;
        private ConfigEntry<float> _forearmRetractionPercent;
        private ConfigEntry<float> _lowerTorsoRetractionPercent;
        private ConfigEntry<float> _chestHelperRetractionPercent;
        private ConfigEntry<float> _shoulderFixRetractionPercent;
        private ConfigEntry<float> _nativeClothRetractionPercent;
        private ConfigEntry<float> _torsoRendererRetractionPercent;
        private ConfigEntry<string> _testRetractionBoneName;
        private ConfigEntry<float> _testBoneRetractionPercent;
        private ConfigEntry<bool> _useCategoryForwardOffsets;
        private ConfigEntry<bool> _adjustAttachedEffects;
        private ConfigEntry<bool> _enableAnimationGuards;
        private ConfigEntry<bool> _mitigateHeldMeleeBodyIntrusion;
        private ConfigEntry<bool> _enableDodgeGuard;
        private ConfigEntry<bool> _enableSheathingGuard;
        private ConfigEntry<bool> _enableBowDrawGuard;
        private ConfigEntry<float> _bowDrawMaximumOffsetPercent;
        private ConfigEntry<bool> _useSharedGuardTarget;
        private ConfigEntry<float> _sharedMoveTowardVanillaPercent;
        private ConfigEntry<float> _meleeForwardOffset;
        private ConfigEntry<float> _bowForwardOffset;
        private ConfigEntry<float> _magicForwardOffset;
        private ConfigEntry<float> _heldMeleeOffsetScale;
        private ConfigEntry<float> _heldMeleeExtraForwardOffset;
        private ConfigEntry<float> _heldMeleeExtraVerticalOffset;
        private ConfigEntry<bool> _enableHeadBob;
        private ConfigEntry<HeadBobPreset> _headBobPreset;
        private ConfigEntry<float> _headBobSmoothness;
        private ConfigEntry<float> _sprintEmphasis;
        private ConfigEntry<float> _headBobSpeedPercent;
        private ConfigEntry<bool> _stabilizeViewmodelDuringHeadBob;
        private ConfigEntry<float> _viewmodelHeadBobFollowPercent;
        private ConfigEntry<bool> _suppressMotionBlurDuringHeadBob;
        private ConfigEntry<bool> _temporalSafeHeadBobTiming;
        private ConfigEntry<bool> _diagnostics;

        private bool _hasPendingEnabled;
        private bool _pendingEnabled;
        private bool _hasPendingEnableAnimationGuards;
        private bool _pendingEnableAnimationGuards;
        private bool _hasPendingMitigateHeldMeleeBodyIntrusion;
        private bool _pendingMitigateHeldMeleeBodyIntrusion;
        private bool _hasPendingEnableDodgeGuard;
        private bool _pendingEnableDodgeGuard;
        private bool _hasPendingEnableSheathingGuard;
        private bool _pendingEnableSheathingGuard;
        private bool _hasPendingEnableBowDrawGuard;
        private bool _pendingEnableBowDrawGuard;
        private bool _hasPendingBowDrawMaximumOffsetPercent;
        private float _pendingBowDrawMaximumOffsetPercent;
        private bool _hasPendingUseSharedGuardTarget;
        private bool _pendingUseSharedGuardTarget;
        private bool _hasPendingSharedMoveTowardVanillaPercent;
        private float _pendingSharedMoveTowardVanillaPercent;
        private bool _hasPendingForwardOffset;
        private float _pendingForwardOffset;
        private bool _hasPendingHorizontalOffset;
        private float _pendingHorizontalOffset;
        private bool _hasPendingVerticalOffset;
        private float _pendingVerticalOffset;
        private bool _hasPendingShoulderRetraction;
        private float _pendingShoulderRetraction;
        private bool _hasPendingSpineRetractionPercent;
        private float _pendingSpineRetractionPercent;
        private bool _hasPendingSpine1RetractionPercent;
        private float _pendingSpine1RetractionPercent;
        private bool _hasPendingSpine2RetractionPercent;
        private float _pendingSpine2RetractionPercent;
        private bool _hasPendingLeftShoulderRetractionPercent;
        private float _pendingLeftShoulderRetractionPercent;
        private bool _hasPendingRightShoulderRetractionPercent;
        private float _pendingRightShoulderRetractionPercent;
        private bool _hasPendingUpperArmRetractionPercent;
        private float _pendingUpperArmRetractionPercent;
        private bool _hasPendingForearmRetractionPercent;
        private float _pendingForearmRetractionPercent;
        private bool _hasPendingLowerTorsoRetractionPercent;
        private float _pendingLowerTorsoRetractionPercent;
        private bool _hasPendingChestHelperRetractionPercent;
        private float _pendingChestHelperRetractionPercent;
        private bool _hasPendingShoulderFixRetractionPercent;
        private float _pendingShoulderFixRetractionPercent;
        private bool _hasPendingNativeClothRetractionPercent;
        private float _pendingNativeClothRetractionPercent;
        private bool _hasPendingTorsoRendererRetractionPercent;
        private float _pendingTorsoRendererRetractionPercent;
        private bool _hasPendingTestRetractionBoneName;
        private string _pendingTestRetractionBoneName;
        private bool _hasPendingTestBoneRetractionPercent;
        private float _pendingTestBoneRetractionPercent;
        private bool _hasPendingMeleeForwardOffset;
        private float _pendingMeleeForwardOffset;
        private bool _hasPendingBowForwardOffset;
        private float _pendingBowForwardOffset;
        private bool _hasPendingMagicForwardOffset;
        private float _pendingMagicForwardOffset;
        private bool _hasPendingHeldMeleeOffsetScale;
        private float _pendingHeldMeleeOffsetScale;
        private bool _hasPendingHeldMeleeExtraForwardOffset;
        private float _pendingHeldMeleeExtraForwardOffset;
        private bool _hasPendingHeldMeleeExtraVerticalOffset;
        private float _pendingHeldMeleeExtraVerticalOffset;
        private bool _hasPendingEnableHeadBob;
        private bool _pendingEnableHeadBob;
        private bool _hasPendingHeadBobPreset;
        private HeadBobPreset _pendingHeadBobPreset;
        private bool _hasPendingHeadBobSmoothness;
        private float _pendingHeadBobSmoothness;
        private bool _hasPendingSprintEmphasis;
        private float _pendingSprintEmphasis;
        private bool _hasPendingHeadBobSpeedPercent;
        private float _pendingHeadBobSpeedPercent;
        private bool _hasPendingStabilizeViewmodelDuringHeadBob;
        private bool _pendingStabilizeViewmodelDuringHeadBob;
        private bool _hasPendingViewmodelHeadBobFollowPercent;
        private float _pendingViewmodelHeadBobFollowPercent;
        private bool _hasPendingSuppressMotionBlurDuringHeadBob;
        private bool _pendingSuppressMotionBlurDuringHeadBob;
        private bool _hasPendingTemporalSafeHeadBobTiming;
        private bool _pendingTemporalSafeHeadBobTiming;
        private bool _hasPendingDiagnostics;
        private bool _pendingDiagnostics;

        private Transform _offsetRoot;
        private Camera _offsetCamera;
        private Vector3 _originalWorldPosition;
        private bool _renderOffsetApplied;
        private Transform _lastReportedRoot;
        private Vector3 _currentVisualWorldOffset;
        private float _currentShoulderRetractionMeters;
        private Vector3 _currentShoulderRetractionWorldOffset;
        private int _currentVisualWorldOffsetFrame = -1;
        private bool _hasCurrentVisualWorldOffset;
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
        private readonly List<uint> _kandraCullingRendererSlots =
            new List<uint>();
        private readonly List<float3> _kandraCullingRendererTranslations =
            new List<float3>();
        private readonly HashSet<KandraRig> _kandraRigRefreshRigs =
            new HashSet<KandraRig>();
        private readonly List<KandraRig> _kandraRigRefreshBodyRigs =
            new List<KandraRig>();
        private readonly List<KandraRenderer> _kandraRigRefreshRenderers =
            new List<KandraRenderer>();
        private readonly List<KandraRig> _staleShoulderBoneProfiles =
            new List<KandraRig>();
        private KandraRenderer _torsoRetractionRenderer;
        private KandraRig _torsoRetractionDedicatedRig;
        private KandraRenderer _pendingTorsoRetractionRenderer;
        private KandraRig _pendingTorsoRetractionOriginalRig;
        private HeroBodyData _pendingTorsoRetractionBodyData;
        private bool _pendingTorsoRetractionRendererWasEnabled;
        private bool _torsoRetractionRequestedLastFrame;
        private string _torsoRendererDiagnosticSignature;
        private readonly Dictionary<KandraRig, ShoulderBoneProfile>
            _shoulderBoneProfiles =
                new Dictionary<KandraRig, ShoulderBoneProfile>();
        private float _nextKandraRigRefreshTime;
        private HeroBodyData _lastReportedKandraBodyData;
        private int _lastSynchronizedKandraFrame = -1;
        private CharacterHandBase _cachedMainHandWeapon;
        private CharacterHandBase _cachedOffHandWeapon;
        private readonly List<LinkedEntitiesAccess> _cachedWeaponEntityAccess =
            new List<LinkedEntitiesAccess>();
        private readonly Dictionary<Entity, DrakeOffsetState>
            _originalDrakeOffsets =
                new Dictionary<Entity, DrakeOffsetState>();
        private readonly HashSet<Entity> _retainedDrakeEntities =
            new HashSet<Entity>();
        private readonly List<Entity> _restoredDrakeEntities =
            new List<Entity>();
        private readonly HashSet<LinkedEntitiesAccess> _weaponEntityAccesses =
            new HashSet<LinkedEntitiesAccess>();
        private readonly List<LinkedEntitiesAccess> _weaponEntityAccessScanBuffer =
            new List<LinkedEntitiesAccess>();
        private float _nextWeaponEntityRefreshTime;
        private CharacterHandBase _cachedEffectMainHandWeapon;
        private CharacterHandBase _cachedEffectOffHandWeapon;
        private readonly List<PresentationEffectOffsetState>
            _attachedEffectOffsets =
                new List<PresentationEffectOffsetState>();
        private readonly HashSet<Transform> _attachedEffectExcludedRoots =
            new HashSet<Transform>();
        private readonly HashSet<Transform> _attachedEffectCandidates =
            new HashSet<Transform>();
        private readonly List<VisualEffect> _visualEffectScanBuffer =
            new List<VisualEffect>();
        private readonly List<ParticleSystem> _particleSystemScanBuffer =
            new List<ParticleSystem>();
        private float _nextAttachedEffectRefreshTime;
        private int _lastReportedAttachedEffectCount = -1;
        private int _suspendOffsetsUntilFrame = -1;
        private float _fireplaceOffsetBlend = 1.0f;
        private float _fireplaceBlendStart = 1.0f;
        private float _fireplaceBlendTarget = 1.0f;
        private float _fireplaceBlendStartedAt;
        private float _fireplaceStandFallbackUntil;
        private bool _fireplaceInteractionActive;
        private bool _waitingForFireplaceStand;
        private bool _crouchTweenReadWarningLogged;
        private float _heldMeleeMitigationBlend;
        private float _heldMeleeBlendStart;
        private float _heldMeleeBlendTarget;
        private float _heldMeleeBlendStartedAt;
        private bool _heldMeleeAttackActive;
        private float _sprintAttackOffsetBlend;
        private float _sprintAttackBlendStart;
        private float _sprintAttackBlendTarget;
        private float _sprintAttackBlendStartedAt;
        private bool _sprintAttackActive;
        private float _dodgeShoulderRetractionBlend;
        private float _dodgeShoulderRetractionHoldUntil;
        private float _lastDodgeActivitySignalTime = float.NegativeInfinity;
        private bool _dodgeActive;
        private int _dodgeRetractionUpdateFrame = -1;
        private MethodInfo _killingBlowMasteryTryGetExecutionVisualStateMethod;
        private float _nextKillingBlowMasteryApiResolveTime;
        private bool _killingBlowMasteryApiUnavailableForSession;
        private bool _killingBlowMasteryApiFailureLogged;
        private float _executionGuardBlend;
        private float _executionGuardBlendStart;
        private float _executionGuardBlendTarget;
        private float _executionGuardBlendStartedAt;
        private bool _executionGuardActive;
        private int _executionGuardSequence;
        private bool _executionNativeFinisherObserved;
        private float _executionNativeStateMissingSince = float.NegativeInfinity;
        private int _executionGuardUpdateFrame = -1;
        private float _sheathingOffsetBlend = 1.0f;
        private int _sheathingBlendUpdateFrame = -1;
        private bool _sheathingActive;
        private float _bowDrawGuardBlend;
        private float _bowDrawGuardBlendTarget;
        private bool _bowDrawGuardActive;
        private string _meleeFsmDiagnosticSignature;
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
        private float _nextPausedRenderOffsetDiagnosticTime;
        private float _nextShoulderRetractionDiagnosticTime;
        private string _shoulderProfileDiagnosticSignature;
        private bool _nativeHeadBobRecoveryFailureReported;
        private float _headBobStridePhase;
        private float _headBobRawVerticalCadence;
        private float _headBobEffectiveVerticalCadence;
        private float _headBobTargetVerticalCadence;
        private float _headBobSmoothedVerticalCadence;
        private float _headBobCadenceVelocity;
        private float _headBobSmoothedVerticalAmplitude;
        private float _headBobVerticalAmplitudeVelocity;
        private float _headBobSmoothedLateralAmplitude;
        private float _headBobLateralAmplitudeVelocity;
        private float _headBobWeight;
        private float _headBobWeightVelocity;
        private float _headBobSprintWeight;
        private float _headBobSprintWeightVelocity;
        private Vector3 _headBobLocalOffset;
        private Vector3 _headBobCameraWorldOffset;
        private Vector3 _viewmodelHeadBobAppliedWorldOffset;
        private Camera _headBobCamera;
        private Vector3 _headBobOriginalCameraPosition;
        private bool _headBobApplied;
        private Camera _headBobMotionBlurCamera;
        private MotionBlur _headBobMotionBlur;
        private bool _headBobMotionBlurOriginalCameraValue;
        private bool _headBobMotionBlurSuppressed;
        private bool _headBobMotionBlurUnavailableReported;
        private bool _temporalSafeHeadBobPatchInstalled;
        private string _headBobApplicationPhase;
        private float _nextHeadBobDiagnosticTime;
        private float _nextTemporalHeadBobDiagnosticTime;

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
                    + " m; ShoulderRetraction="
                    + _shoulderRetraction.Value.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " m; HeadBob="
                    + _enableHeadBob.Value.ToString(
                        CultureInfo.InvariantCulture)
                    + "@"
                    + _headBobPreset.Value.ToString()
                    + "; SprintEmphasis="
                    + Mathf.Clamp01(
                        _sprintEmphasis.Value).ToString(
                             "0.##",
                             CultureInfo.InvariantCulture)
                    + "; HeadBobSpeedPercent="
                    + Mathf.Clamp(
                        _headBobSpeedPercent.Value,
                        50.0f,
                        150.0f).ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                    + "; StabilizeViewmodelDuringHeadBob="
                    + _stabilizeViewmodelDuringHeadBob.Value.ToString(
                        CultureInfo.InvariantCulture)
                    + "; ViewmodelHeadBobFollowPercent="
                    + Mathf.Clamp(
                        _viewmodelHeadBobFollowPercent.Value,
                        0.0f,
                        100.0f).ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)
                    + "; TemporalSafeHeadBobTiming="
                    + _temporalSafeHeadBobTiming.Value.ToString(
                        CultureInfo.InvariantCulture)
                    + "; native Kandra, culling, and linked Drake presentation offsets are active; the world camera FOV is unchanged.");
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
            RestoreHeadBob();
            RestoreRenderOffset();
            UpdateTorsoRendererRetractionRig();
            UpdateHeadBob();
            UpdateFireplaceOffsetBlend();
            UpdateBowDrawGuardBlend();
            UpdateHeldMeleeOffsetBlend();
            UpdateSprintAttackOffsetBlend();
            UpdateDodgeShoulderRetractionBlend();
            UpdateExecutionGuardBlend();
            UpdateSheathingOffsetBlend();
        }

        private void LateUpdate()
        {
            ApplyAttachedEffectOffsets();
        }

        private void OnDisable()
        {
            RestoreHeadBob();
            ResetHeadBob();
            RestoreRenderOffset();
            RestoreDrakeOffsets();
            RestoreAttachedEffectOffsets();
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
            RestoreHeadBob();
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
            RestoreHeadBob();
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
            CancelPendingTorsoRendererRigSwap();
            RestoreAttachedEffectOffsets();
            _lastReportedRoot = null;
            _lastSynchronizedKandraFrame = -1;
            _cachedKandraBodyData = null;
            _cachedKandraRigs = new KandraRig[0];
            _cachedKandraRenderers = new KandraRenderer[0];
            _kandraCullingRendererSlots.Clear();
            _kandraCullingRendererTranslations.Clear();
            _kandraRigRefreshRigs.Clear();
            _kandraRigRefreshBodyRigs.Clear();
            _kandraRigRefreshRenderers.Clear();
            _staleShoulderBoneProfiles.Clear();
            _shoulderBoneProfiles.Clear();
            _nextKandraRigRefreshTime = 0.0f;
            _lastReportedKandraBodyData = null;
            _cachedMainHandWeapon = null;
            _cachedOffHandWeapon = null;
            _cachedWeaponEntityAccess.Clear();
            _retainedDrakeEntities.Clear();
            _restoredDrakeEntities.Clear();
            _weaponEntityAccesses.Clear();
            _weaponEntityAccessScanBuffer.Clear();
            _nextWeaponEntityRefreshTime = 0.0f;
            _cachedEffectMainHandWeapon = null;
            _cachedEffectOffHandWeapon = null;
            _attachedEffectOffsets.Clear();
            _attachedEffectExcludedRoots.Clear();
            _attachedEffectCandidates.Clear();
            _visualEffectScanBuffer.Clear();
            _particleSystemScanBuffer.Clear();
            _nextAttachedEffectRefreshTime = 0.0f;
            _lastReportedAttachedEffectCount = -1;
            _lastReportedMainHandWeapon = null;
            _lastReportedOffHandWeapon = null;
            _originalDrakeOffsets.Clear();
            _heldMeleeMitigationBlend = 0.0f;
            _heldMeleeBlendStart = 0.0f;
            _heldMeleeBlendTarget = 0.0f;
            _heldMeleeAttackActive = false;
            _sprintAttackOffsetBlend = 0.0f;
            _sprintAttackBlendStart = 0.0f;
            _sprintAttackBlendTarget = 0.0f;
            _sprintAttackActive = false;
            _dodgeShoulderRetractionBlend = 0.0f;
            _dodgeShoulderRetractionHoldUntil = 0.0f;
            _lastDodgeActivitySignalTime = float.NegativeInfinity;
            _dodgeActive = false;
            _dodgeRetractionUpdateFrame = -1;
            _executionGuardBlend = 0.0f;
            _executionGuardBlendStart = 0.0f;
            _executionGuardBlendTarget = 0.0f;
            _executionGuardBlendStartedAt = 0.0f;
            _executionGuardActive = false;
            _executionGuardSequence = 0;
            _executionNativeFinisherObserved = false;
            _executionNativeStateMissingSince = float.NegativeInfinity;
            _executionGuardUpdateFrame = -1;
            _sheathingOffsetBlend = 1.0f;
            _sheathingBlendUpdateFrame = -1;
            _sheathingActive = false;
            _bowDrawGuardBlend = 0.0f;
            _bowDrawGuardBlendTarget = 0.0f;
            _bowDrawGuardActive = false;
            _meleeFsmDiagnosticSignature = null;
            _currentVisualWorldOffset = Vector3.zero;
            _currentShoulderRetractionMeters = 0.0f;
            _currentShoulderRetractionWorldOffset = Vector3.zero;
            _currentVisualWorldOffsetFrame = -1;
            _hasCurrentVisualWorldOffset = false;
            _nextPausedRenderOffsetDiagnosticTime = 0.0f;
            _nextShoulderRetractionDiagnosticTime = 0.0f;
            _shoulderProfileDiagnosticSignature = null;
            _torsoRendererDiagnosticSignature = null;
            ResetHeadBob();
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

        private bool IsAnimationGuardEnabled(
            ConfigEntry<bool> guardToggle)
        {
            return _enableAnimationGuards != null
                && _enableAnimationGuards.Value
                && guardToggle != null
                && guardToggle.Value;
        }

        private bool TryGetSharedAnimationGuardStrength(
            out float strength)
        {
            strength = 0.0f;
            if (_useSharedGuardTarget == null
                || !_useSharedGuardTarget.Value)
            {
                return false;
            }

            float percent = _sharedMoveTowardVanillaPercent == null
                ? 50.0f
                : _sharedMoveTowardVanillaPercent.Value;
            strength = Mathf.Clamp01(percent / 100.0f);
            return true;
        }

        private void UpdateHeldMeleeOffsetBlend()
        {
            float now = Time.unscaledTime;
            Hero hero = Hero.Current;
            bool mitigationEnabled = IsAnimationGuardEnabled(
                _mitigateHeldMeleeBodyIntrusion);
            float sharedGuardStrength;
            bool useSharedGuardTarget =
                TryGetSharedAnimationGuardStrength(
                    out sharedGuardStrength);
            bool attackActive = mitigationEnabled
                && !_bowDrawGuardActive
                && (useSharedGuardTarget
                    ? IsExpandedMeleeAttackActive(hero)
                    : IsHeldMeleeAttackActive(hero));
            ReportMeleeFsmDiagnostics(
                hero,
                attackActive,
                mitigationEnabled
                    && !useSharedGuardTarget
                    && IsSprintAttackActive(hero));
            float target = attackActive ? 1.0f : 0.0f;
            if (!Mathf.Approximately(target, _heldMeleeBlendTarget))
            {
                _heldMeleeBlendStart = _heldMeleeMitigationBlend;
                _heldMeleeBlendTarget = target;
                _heldMeleeBlendStartedAt = now;
            }

            if (attackActive != _heldMeleeAttackActive)
            {
                _heldMeleeAttackActive = attackActive;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        attackActive
                            ? (useSharedGuardTarget
                                ? "Blending the first-person offset toward the shared guard target for a melee attack."
                                : "Blending the first-person offset toward the held-melee scale to prevent body intrusion.")
                            : "Restoring the configured first-person offset after the held melee attack.");
                }
            }

            float duration = _heldMeleeBlendTarget
                    > _heldMeleeBlendStart
                ? HeldMeleeBlendOutSeconds
                : HeldMeleeBlendInSeconds;
            float elapsed = now - _heldMeleeBlendStartedAt;
            float progress = duration <= 0.0f
                ? 1.0f
                : Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress
                * progress
                * (3.0f - (2.0f * progress));
            _heldMeleeMitigationBlend = Mathf.LerpUnclamped(
                _heldMeleeBlendStart,
                _heldMeleeBlendTarget,
                easedProgress);
        }

        private static bool IsHeldMeleeAttackActive(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            foreach (MeleeFSM melee in hero.Elements<MeleeFSM>())
            {
                if (melee == null
                    || !melee.IsLayerActive
                    || melee.GeneralStateType
                        != HeroGeneralStateType.HeavyAttack)
                {
                    continue;
                }

                HeroStateType state = melee.CurrentStateType;
                if (state == HeroStateType.HeavyAttackStart
                    || state == HeroStateType.HeavyAttackStartAlternate
                    || state == HeroStateType.HeavyAttackWait
                    || state == HeroStateType.HeavyAttackWaitAlternate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExpandedMeleeAttackActive(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            foreach (MeleeFSM melee in hero.Elements<MeleeFSM>())
            {
                if (melee != null
                    && melee.IsLayerActive
                    && (IsExpandedMeleeAttackState(
                            melee.CurrentStateType)
                        || IsExpandedMeleeAttackState(
                            melee.CurrentStateToEnterType)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExpandedMeleeAttackState(
            HeroStateType state)
        {
            return state == HeroStateType.LightAttackTired
                || state == HeroStateType.LightAttackForward
                || state == HeroStateType.LightAttackInitial
                || state == HeroStateType.LightAttackFirst
                || state == HeroStateType.LightAttackSecond
                || state == HeroStateType.HeavyAttackStart
                || state == HeroStateType.HeavyAttackStartAlternate
                || state == HeroStateType.HeavyAttackWait
                || state == HeroStateType.HeavyAttackWaitAlternate
                || state == HeroStateType.HeavyAttackEnd
                || state == HeroStateType.HeavyAttackEndAlternate;
        }

        private void UpdateSprintAttackOffsetBlend()
        {
            float now = Time.unscaledTime;
            Hero hero = Hero.Current;
            bool mitigationEnabled = IsAnimationGuardEnabled(
                _mitigateHeldMeleeBodyIntrusion);
            float sharedGuardStrength;
            bool useSharedGuardTarget =
                TryGetSharedAnimationGuardStrength(
                    out sharedGuardStrength);
            bool attackActive = mitigationEnabled
                && !useSharedGuardTarget
                && IsSprintAttackActive(hero);
            ReportMeleeFsmDiagnostics(
                hero,
                mitigationEnabled
                    && (useSharedGuardTarget
                        ? IsExpandedMeleeAttackActive(hero)
                        : IsHeldMeleeAttackActive(hero)),
                attackActive);
            float target = attackActive ? 1.0f : 0.0f;
            if (!Mathf.Approximately(target, _sprintAttackBlendTarget))
            {
                _sprintAttackBlendStart = _sprintAttackOffsetBlend;
                _sprintAttackBlendTarget = target;
                _sprintAttackBlendStartedAt = now;
            }

            if (attackActive != _sprintAttackActive)
            {
                _sprintAttackActive = attackActive;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        attackActive
                            ? "Blending the first-person offset to vanilla for a sprint attack."
                            : "Restoring the configured first-person offset after the sprint attack.");
                }
            }

            float duration = _sprintAttackBlendTarget
                    > _sprintAttackBlendStart
                ? SprintAttackBlendOutSeconds
                : SprintAttackBlendInSeconds;
            float elapsed = now - _sprintAttackBlendStartedAt;
            float progress = duration <= 0.0f
                ? 1.0f
                : Mathf.Clamp01(elapsed / duration);
            float easedProgress = progress
                * progress
                * (3.0f - (2.0f * progress));
            _sprintAttackOffsetBlend = Mathf.LerpUnclamped(
                _sprintAttackBlendStart,
                _sprintAttackBlendTarget,
                easedProgress);
        }

        private static bool IsSprintAttackActive(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            foreach (MeleeFSM melee in hero.Elements<MeleeFSM>())
            {
                if (melee != null
                    && melee.CurrentStateType
                        == HeroStateType.LightAttackForward)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateDodgeShoulderRetractionBlend()
        {
            if (_dodgeRetractionUpdateFrame == Time.frameCount)
            {
                return;
            }

            _dodgeRetractionUpdateFrame = Time.frameCount;
            Hero hero = Hero.Current;
            bool dodgeStateActive = false;
            if (hero != null)
            {
                foreach (LegsFSM legs in hero.Elements<LegsFSM>())
                {
                    if (legs != null
                        && (IsDodgeState(legs.CurrentStateType)
                            || IsDodgeState(
                                legs.CurrentStateToEnterType)))
                    {
                        dodgeStateActive = true;
                        break;
                    }
                }
            }

            float now = Time.unscaledTime;
            bool dodgeActivitySignaled = now
                    - _lastDodgeActivitySignalTime
                <= DodgeActivitySignalGraceSeconds;
            bool dodgeActive = IsAnimationGuardEnabled(
                    _enableDodgeGuard)
                && (dodgeStateActive || dodgeActivitySignaled);
            if (dodgeActive)
            {
                _dodgeShoulderRetractionHoldUntil = now
                    + DodgeRetractionHoldSeconds;
            }

            bool holdMaximum = dodgeActive
                || (IsAnimationGuardEnabled(_enableDodgeGuard)
                    && now < _dodgeShoulderRetractionHoldUntil);
            float target = holdMaximum ? 1.0f : 0.0f;
            float duration = target
                    > _dodgeShoulderRetractionBlend
                ? DodgeRetractionBlendInSeconds
                : DodgeRetractionBlendOutSeconds;
            float maxDelta = duration <= 0.0f
                ? 1.0f
                : Time.unscaledDeltaTime / duration;
            _dodgeShoulderRetractionBlend = Mathf.MoveTowards(
                _dodgeShoulderRetractionBlend,
                target,
                maxDelta);

            if (dodgeActive != _dodgeActive)
            {
                _dodgeActive = dodgeActive;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        dodgeActive
                            ? "Fading shoulder retraction toward 0.25 metres for the dodge guard."
                            : "Holding then restoring configured shoulder retraction after the dodge guard.");
                }
            }
        }

        internal void NotifyDodgeActivity()
        {
            _lastDodgeActivitySignalTime = Time.unscaledTime;
            _dodgeRetractionUpdateFrame = -1;
        }

        private static bool IsDodgeState(HeroStateType state)
        {
            return state >= HeroStateType.DashFront
                && state <= HeroStateType.DashBackRight;
        }

        private void UpdateExecutionGuardBlend()
        {
            if (_executionGuardUpdateFrame == Time.frameCount)
            {
                return;
            }

            _executionGuardUpdateFrame = Time.frameCount;
            int sequence = 0;
            bool targetDeathConfirmed = false;
            bool apiActive = _enableAnimationGuards != null
                && _enableAnimationGuards.Value
                && TryReadExecutionVisualState(
                    out sequence,
                    out targetDeathConfirmed);
            if (apiActive && sequence != _executionGuardSequence)
            {
                _executionGuardSequence = sequence;
                _executionNativeFinisherObserved = false;
                _executionNativeStateMissingSince =
                    float.NegativeInfinity;
            }

            bool nativeFinisherActive = apiActive
                && IsNativeFinisherStateActive(Hero.Current);
            if (nativeFinisherActive)
            {
                _executionNativeFinisherObserved = true;
                _executionNativeStateMissingSince =
                    float.NegativeInfinity;
            }
            else if (apiActive
                && targetDeathConfirmed
                && _executionNativeFinisherObserved
                && float.IsNegativeInfinity(
                    _executionNativeStateMissingSince))
            {
                _executionNativeStateMissingSince = Time.unscaledTime;
            }

            bool nativeStateTimedOut = apiActive
                && targetDeathConfirmed
                && _executionNativeFinisherObserved
                && !float.IsNegativeInfinity(
                    _executionNativeStateMissingSince)
                && Time.unscaledTime
                    - _executionNativeStateMissingSince
                    >= ExecutionNativeStateGraceSeconds;
            bool active = apiActive && !nativeStateTimedOut;
            float target = active ? 1.0f : 0.0f;
            if (!Mathf.Approximately(
                    target,
                    _executionGuardBlendTarget))
            {
                _executionGuardBlendStart = _executionGuardBlend;
                _executionGuardBlendTarget = target;
                _executionGuardBlendStartedAt = Time.unscaledTime;
            }

            float duration = _executionGuardBlendTarget
                    > _executionGuardBlendStart
                ? ExecutionGuardBlendInSeconds
                : ExecutionGuardBlendOutSeconds;
            float progress = duration <= 0.0f
                ? 1.0f
                : Mathf.Clamp01(
                    (Time.unscaledTime
                        - _executionGuardBlendStartedAt)
                    / duration);
            float easedProgress = progress
                * progress
                * (3.0f - (2.0f * progress));
            _executionGuardBlend = Mathf.LerpUnclamped(
                _executionGuardBlendStart,
                _executionGuardBlendTarget,
                easedProgress);

            if (active != _executionGuardActive)
            {
                _executionGuardActive = active;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        active
                            ? "Blending the execution viewmodel halfway toward vanilla with additional shoulder retraction."
                            : "Restoring the configured viewmodel after the execution guard.");
                }
            }
        }

        private static bool IsNativeFinisherStateActive(Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            foreach (HeroAnimatorSubstateMachine fsm
                in hero.Elements<HeroAnimatorSubstateMachine>())
            {
                if (fsm != null
                    && (fsm.CurrentStateType == HeroStateType.Finisher
                        || fsm.CurrentStateToEnterType
                            == HeroStateType.Finisher))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryReadExecutionVisualState(
            out int sequence,
            out bool targetDeathConfirmed)
        {
            sequence = 0;
            targetDeathConfirmed = false;
            if (!ResolveKillingBlowMasteryExecutionVisualApi())
            {
                return false;
            }

            object[] arguments = { 0, 0, null, 0.0f, false };
            try
            {
                object result =
                    _killingBlowMasteryTryGetExecutionVisualStateMethod
                        .Invoke(null, arguments);
                if (!(result is bool) || !(bool)result)
                {
                    return false;
                }

                sequence = Convert.ToInt32(
                    arguments[0],
                    CultureInfo.InvariantCulture);
                int phase = Convert.ToInt32(
                    arguments[1],
                    CultureInfo.InvariantCulture);
                targetDeathConfirmed = arguments[4] is bool
                    && (bool)arguments[4];
                return phase == ExecutionPhaseActive
                    && arguments[2] != null;
            }
            catch (Exception exception)
            {
                _killingBlowMasteryTryGetExecutionVisualStateMethod = null;
                _killingBlowMasteryApiUnavailableForSession = true;
                LogKillingBlowMasteryApiFailure(
                    "Could not read Killing Blow Mastery's execution state: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private bool ResolveKillingBlowMasteryExecutionVisualApi()
        {
            if (_killingBlowMasteryTryGetExecutionVisualStateMethod != null)
            {
                return true;
            }
            if (_killingBlowMasteryApiUnavailableForSession
                || Time.unscaledTime
                    < _nextKillingBlowMasteryApiResolveTime)
            {
                return false;
            }

            _nextKillingBlowMasteryApiResolveTime =
                Time.unscaledTime + 0.5f;
            BepInEx.PluginInfo pluginInfo;
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                    KillingBlowMasteryPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                _killingBlowMasteryApiUnavailableForSession = true;
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                KillingBlowMasteryExecutionVisualApiTypeName,
                false);
            FieldInfo apiVersionField = apiType == null
                ? null
                : apiType.GetField(
                    "ApiVersion",
                    BindingFlags.Public | BindingFlags.Static);
            MethodInfo tryGetStateMethod = apiType == null
                ? null
                : apiType.GetMethod(
                    "TryGetState",
                    BindingFlags.Public | BindingFlags.Static);
            if (apiVersionField == null
                || !object.Equals(
                    apiVersionField.GetRawConstantValue(),
                    1)
                || tryGetStateMethod == null)
            {
                _killingBlowMasteryApiUnavailableForSession = true;
                LogKillingBlowMasteryApiFailure(
                    "Killing Blow Mastery is loaded without execution-visual API v1; the execution guard is unavailable.");
                return false;
            }

            _killingBlowMasteryTryGetExecutionVisualStateMethod =
                tryGetStateMethod;
            if (_diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(
                    "Killing Blow Mastery execution-guard integration is active.");
            }
            return true;
        }

        private void LogKillingBlowMasteryApiFailure(string message)
        {
            if (_killingBlowMasteryApiFailureLogged)
            {
                return;
            }

            _killingBlowMasteryApiFailureLogged = true;
            Logger.LogWarning(message);
        }

        private bool IsHeadBobAccessibilityEnabled()
        {
            HeadBobbingSetting setting =
                Awaken.TG.MVC.World.Any<HeadBobbingSetting>();
            float nativeIntensity;
            return setting != null
                && TryGetNativeHeadBobIntensity(
                    setting,
                    out nativeIntensity)
                && nativeIntensity > 0.0f;
        }

        private void UpdateSheathingOffsetBlend()
        {
            if (_sheathingBlendUpdateFrame == Time.frameCount)
            {
                return;
            }

            _sheathingBlendUpdateFrame = Time.frameCount;
            float sheathingBlend = 1.0f;
            bool sheathing = IsAnimationGuardEnabled(
                    _enableSheathingGuard)
                && TryGetSheathingOffsetBlend(
                    Hero.Current,
                    out sheathingBlend);
            if (sheathing)
            {
                _sheathingOffsetBlend = sheathingBlend;
            }
            else
            {
                float maxDelta = SheathingBlendRestoreSeconds <= 0.0f
                    ? 1.0f
                    : Time.unscaledDeltaTime
                        / SheathingBlendRestoreSeconds;
                _sheathingOffsetBlend = Mathf.MoveTowards(
                    _sheathingOffsetBlend,
                    1.0f,
                    maxDelta);
            }

            if (sheathing != _sheathingActive)
            {
                _sheathingActive = sheathing;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        sheathing
                            ? "Blending the first-person offset to vanilla during the sheathing animation."
                            : "Restoring the configured first-person offset after sheathing.");
                }
            }
        }

        private static bool TryGetSheathingOffsetBlend(
            Hero hero,
            out float blend)
        {
            blend = 1.0f;
            if (hero == null)
            {
                return false;
            }

            bool sheathing = false;
            foreach (HeroAnimatorSubstateMachine fsm
                in hero.Elements<HeroAnimatorSubstateMachine>())
            {
                if (fsm == null)
                {
                    continue;
                }

                HeroStateType currentState = fsm.CurrentStateType;
                HeroStateType targetState = fsm.CurrentStateToEnterType;
                if (!IsSheathingState(currentState)
                    && !IsSheathingState(targetState))
                {
                    continue;
                }

                sheathing = true;
                float normalizedTime = 0.0f;
                HeroAnimatorState animatorState = fsm.CurrentAnimatorState;
                if (IsSheathingState(currentState)
                    && animatorState != null)
                {
                    normalizedTime = Mathf.Max(
                        0.0f,
                        animatorState.TimeElapsedNormalized);
                }

                float progress = Mathf.InverseLerp(
                    SheathingBlendStartNormalizedTime,
                    SheathingBlendEndNormalizedTime,
                    normalizedTime);
                float easedProgress = progress
                    * progress
                    * (3.0f - (2.0f * progress));
                blend = Mathf.Min(blend, 1.0f - easedProgress);
            }

            return sheathing;
        }

        private static bool IsSheathingState(HeroStateType state)
        {
            return state == HeroStateType.UnEquipWeapon
                || state == HeroStateType.UnEquipWeaponAlternate;
        }

        private void UpdateBowDrawGuardBlend()
        {
            float target = IsAnimationGuardEnabled(
                    _enableBowDrawGuard)
                ? GetBowDrawGuardTarget(Hero.Current)
                : 0.0f;
            _bowDrawGuardBlendTarget = target;

            bool active = target > 0.0f
                || _bowDrawGuardBlend > 0.001f;
            if (active != _bowDrawGuardActive)
            {
                _bowDrawGuardActive = active;
                if (_diagnostics != null && _diagnostics.Value)
                {
                    Logger.LogInfo(
                        active
                            ? "Applying the bow-draw depth ceiling."
                            : "Restoring the normal configured depth after bow draw.");
                }
            }

            float duration = _bowDrawGuardBlendTarget
                    > _bowDrawGuardBlend
                ? BowDrawGuardBlendInSeconds
                : BowDrawGuardBlendOutSeconds;
            float maxDelta = duration <= 0.0f
                ? 1.0f
                : Time.unscaledDeltaTime / duration;
            _bowDrawGuardBlend = Mathf.MoveTowards(
                _bowDrawGuardBlend,
                _bowDrawGuardBlendTarget,
                maxDelta);
        }

        private static float GetBowDrawGuardTarget(Hero hero)
        {
            if (hero == null)
            {
                return 0.0f;
            }

            float strongestTarget = 0.0f;
            foreach (BowFSM bow in hero.Elements<BowFSM>())
            {
                if (bow == null)
                {
                    continue;
                }

                HeroStateType state = bow.CurrentStateType;
                if (state == HeroStateType.BowPull)
                {
                    HeroAnimatorState animatorState =
                        bow.CurrentAnimatorState;
                    float normalizedTime = animatorState == null
                        ? 0.0f
                        : Mathf.Max(
                            0.0f,
                            animatorState.TimeElapsedNormalized);
                    float pullProgress = Mathf.Clamp01(
                        normalizedTime
                        / BowPullGuardFullNormalizedTime);
                    float easedPullProgress = pullProgress
                        * pullProgress
                        * (3.0f - (2.0f * pullProgress));
                    strongestTarget = Mathf.Max(
                        strongestTarget,
                        easedPullProgress);
                }
                else if (state == HeroStateType.BowHold)
                {
                    strongestTarget = 1.0f;
                }
                else if (state == HeroStateType.BowRelease)
                {
                    HeroAnimatorState animatorState =
                        bow.CurrentAnimatorState;
                    float normalizedTime = animatorState == null
                        ? 0.0f
                        : Mathf.Max(
                            0.0f,
                            animatorState.TimeElapsedNormalized);
                    if (normalizedTime
                        < BowReleaseProjectileNormalizedTime)
                    {
                        strongestTarget = 1.0f;
                    }
                }
                else if (bow.CurrentStateToEnterType
                    == HeroStateType.BowHold
                    || bow.CurrentStateToEnterType
                        == HeroStateType.BowRelease)
                {
                    strongestTarget = Mathf.Max(
                        strongestTarget,
                        1.0f);
                }
            }

            return strongestTarget;
        }

        private void ReportMeleeFsmDiagnostics(
            Hero hero,
            bool heldAttackActive,
            bool sprintAttackActive)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                _meleeFsmDiagnosticSignature = null;
                return;
            }

            StringBuilder description = new StringBuilder();
            description.Append("heldMitigation=")
                .Append(heldAttackActive ? "active" : "inactive")
                .Append("; sprintMitigation=")
                .Append(sprintAttackActive ? "active" : "inactive");
            if (hero == null)
            {
                description.Append("; hero=none");
            }
            else
            {
                int fsmCount = 0;
                foreach (MeleeFSM melee in hero.Elements<MeleeFSM>())
                {
                    if (melee == null)
                    {
                        continue;
                    }

                    description.Append(fsmCount == 0 ? "; " : " | ")
                        .Append(melee.GetType().Name)
                        .Append(": layerActive=")
                        .Append(melee.IsLayerActive)
                        .Append(", general=")
                        .Append(melee.GeneralStateType)
                        .Append(", current=")
                        .Append(melee.CurrentStateType)
                        .Append(", target=")
                        .Append(melee.CurrentStateToEnterType);
                    fsmCount++;
                }

                if (fsmCount == 0)
                {
                    description.Append("; meleeFSMs=none");
                }
            }

            string signature = description.ToString();
            if (String.Equals(
                    signature,
                    _meleeFsmDiagnosticSignature,
                    StringComparison.Ordinal))
            {
                return;
            }

            _meleeFsmDiagnosticSignature = signature;
            Logger.LogInfo("Melee FSM states: " + signature + ".");
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
            MethodInfo kandraPreLateUpdateEnd = AccessTools.Method(
                typeof(KandraRendererManager),
                "OnPreLateUpdateEnd");
            MethodInfo kandraPostfix = AccessTools.Method(
                typeof(KandraRendererManagerPreLateUpdateEndPatch),
                nameof(KandraRendererManagerPreLateUpdateEndPatch.Postfix));
            MethodInfo linkedTransformUpdate = AccessTools.Method(
                typeof(LinkedTransformSystem),
                "OnUpdate");
            MethodInfo linkedTransformPrefix = AccessTools.Method(
                typeof(LinkedTransformSystemPatch),
                nameof(LinkedTransformSystemPatch.Prefix));
            MethodInfo heroControllerProcessUpdate = AccessTools.Method(
                typeof(VHeroController),
                "ProcessUpdate",
                new[] { typeof(float) });
            MethodInfo heroControllerProcessUpdatePostfix =
                AccessTools.Method(
                    typeof(VHeroControllerProcessUpdatePatch),
                    nameof(VHeroControllerProcessUpdatePatch.Postfix));
            MethodInfo headBobbingIntensityGetter =
                AccessTools.PropertyGetter(
                    typeof(HeadBobbingSetting),
                    "Intensity");
            MethodInfo headBobbingPostfix = AccessTools.Method(
                typeof(HeadBobbingIntensityPatch),
                nameof(HeadBobbingIntensityPatch.Postfix));
            MethodInfo hdCameraUpdate = AccessTools.Method(
                typeof(HDCamera),
                "Update");
            MethodInfo hdCameraUpdatePrefix = AccessTools.Method(
                typeof(HDCameraUpdateHeadBobPatch),
                nameof(HDCameraUpdateHeadBobPatch.Prefix));
            MethodInfo hdCameraUpdatePostfix = AccessTools.Method(
                typeof(HDCameraUpdateHeadBobPatch),
                nameof(HDCameraUpdateHeadBobPatch.Postfix));
            MethodInfo legsOnHeroDashed = AccessTools.Method(
                typeof(LegsFSM),
                "OnHeroDashed",
                new[] { typeof(Vector2) });
            MethodInfo legsOnHeroDashedForward = AccessTools.Method(
                typeof(LegsFSM),
                "OnHeroDashedForward",
                new[] { typeof(bool) });
            MethodInfo dashBaseOnUpdate = AccessTools.Method(
                typeof(DashBaseState),
                "OnUpdate",
                new[] { typeof(float) });
            MethodInfo dodgeActivityPostfix = AccessTools.Method(
                typeof(DodgeActivityPatch),
                nameof(DodgeActivityPatch.Postfix));
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
            if (kandraPreLateUpdateEnd == null
                || kandraPostfix == null
                || linkedTransformUpdate == null
                || linkedTransformPrefix == null
                || heroControllerProcessUpdate == null
                || heroControllerProcessUpdatePostfix == null
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
                kandraPreLateUpdateEnd,
                postfix: new HarmonyMethod(kandraPostfix));
            _harmony.Patch(
                linkedTransformUpdate,
                prefix: new HarmonyMethod(linkedTransformPrefix));
            _harmony.Patch(
                heroControllerProcessUpdate,
                postfix: new HarmonyMethod(
                    heroControllerProcessUpdatePostfix));
            if (headBobbingIntensityGetter != null
                && headBobbingPostfix != null
                && NativeHeadBobIntensityField != null
                && NativeHeadBobEnabledProperty != null)
            {
                _harmony.Patch(
                    headBobbingIntensityGetter,
                    postfix: new HarmonyMethod(headBobbingPostfix));
            }
            else
            {
                Logger.LogWarning(
                    "Could not resolve the native head-bob members. Viewmodel positioning remains active, but first-person head-bob control is unavailable.");
            }
            if (hdCameraUpdate != null
                && hdCameraUpdatePrefix != null
                && hdCameraUpdatePostfix != null)
            {
                _harmony.Patch(
                    hdCameraUpdate,
                    prefix: new HarmonyMethod(hdCameraUpdatePrefix),
                    postfix: new HarmonyMethod(hdCameraUpdatePostfix));
                _temporalSafeHeadBobPatchInstalled = true;
            }
            else
            {
                Logger.LogWarning(
                    "Could not resolve HDRP's camera-update hook. Temporal Safe Head Bob Timing will retain the normal render-callback path.");
            }
            bool dodgeActivityPatchInstalled = false;
            if (dodgeActivityPostfix != null)
            {
                if (legsOnHeroDashed != null)
                {
                    _harmony.Patch(
                        legsOnHeroDashed,
                        postfix: new HarmonyMethod(
                            dodgeActivityPostfix));
                    dodgeActivityPatchInstalled = true;
                }
                if (legsOnHeroDashedForward != null)
                {
                    _harmony.Patch(
                        legsOnHeroDashedForward,
                        postfix: new HarmonyMethod(
                            dodgeActivityPostfix));
                    dodgeActivityPatchInstalled = true;
                }
                if (dashBaseOnUpdate != null)
                {
                    _harmony.Patch(
                        dashBaseOnUpdate,
                        postfix: new HarmonyMethod(
                            dodgeActivityPostfix));
                    dodgeActivityPatchInstalled = true;
                }
            }
            if (!dodgeActivityPatchInstalled)
            {
                Logger.LogWarning(
                    "Could not resolve the native dodge callbacks. Dynamic shoulder retraction will retain FSM polling as a compatibility fallback.");
            }
        }

        internal void SuppressNativeFirstPersonHeadBob(
            ref float intensity)
        {
            Hero hero = Hero.Current;
            if (_enabled == null
                || !_enabled.Value
                || hero == null
                || Hero.TppActive)
            {
                return;
            }

            intensity = 0.0f;
        }

        private bool TryGetNativeHeadBobIntensity(
            HeadBobbingSetting setting,
            out float intensity)
        {
            intensity = 0.0f;
            try
            {
                object enabledValue =
                    NativeHeadBobEnabledProperty.GetValue(setting, null);
                if (!(enabledValue is bool))
                {
                    throw new InvalidCastException(
                        "The accessibility getter returned an unexpected value.");
                }
                if (!(bool)enabledValue)
                {
                    return true;
                }

                object intensityValue =
                    NativeHeadBobIntensityField.GetValue(setting);
                if (!(intensityValue is float))
                {
                    throw new InvalidCastException(
                        "The native intensity field returned an unexpected value.");
                }

                intensity = (float)intensityValue;
                return true;
            }
            catch (Exception exception)
            {
                if (!_nativeHeadBobRecoveryFailureReported)
                {
                    _nativeHeadBobRecoveryFailureReported = true;
                    Logger.LogWarning(
                        "Could not recover vanilla first-person head-bob intensity; leaving the incoming value unchanged: "
                        + exception.Message);
                }
                return false;
            }
        }

        internal void ApplyKandraRenderOffset(
            KandraRendererManager rendererManager)
        {
            if (rendererManager == null
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
            if (bodyData == null)
            {
                return;
            }

            Vector3 worldOffset;
            if (!TryGetCurrentVisualWorldOffset(out worldOffset)
                || worldOffset.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            ApplyLateKandraOffset(
                bodyData,
                rendererManager.RigManager,
                new float3(
                    worldOffset.x,
                    worldOffset.y,
                    worldOffset.z),
                new float3(
                    _currentShoulderRetractionWorldOffset.x,
                    _currentShoulderRetractionWorldOffset.y,
                    _currentShoulderRetractionWorldOffset.z));
        }

        private void ApplyLateKandraOffset(
            HeroBodyData bodyData,
            RigManager rigManager,
            float3 translation,
            float3 shoulderRetraction)
        {
            if (OffsetsSuspended()
                || _lastSynchronizedKandraFrame == Time.frameCount
                || rigManager == null
                || (uint)_bonesInFlightField.GetValue(
                    rigManager) == 0)
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
                    rigManager);
            if (!bones.IsCreated)
            {
                return;
            }

            JobHandle dependency =
                (JobHandle)_readTransformField.GetValue(
                    rigManager);
            int rigCount = 0;
            int boneCount = 0;
            int shoulderRigCount = 0;
            int shoulderBoneCount = 0;
            int torsoRendererBoneCount = 0;
            float spineRetractionWeight = GetRetractionWeight(
                _spineRetractionPercent,
                DefaultShoulderSpineRetractionWeight);
            float spine1RetractionWeight = GetRetractionWeight(
                _spine1RetractionPercent,
                DefaultShoulderSpine1RetractionWeight);
            float spine2RetractionWeight = GetRetractionWeight(
                _spine2RetractionPercent,
                DefaultShoulderSpine2RetractionWeight);
            float leftShoulderRetractionWeight = GetRetractionWeight(
                _leftShoulderRetractionPercent,
                DefaultShoulderJointRetractionWeight);
            float rightShoulderRetractionWeight = GetRetractionWeight(
                _rightShoulderRetractionPercent,
                DefaultShoulderJointRetractionWeight);
            float upperArmRetractionWeight = GetRetractionWeight(
                _upperArmRetractionPercent,
                DefaultShoulderUpperArmRetractionWeight);
            float forearmRetractionWeight = GetRetractionWeight(
                _forearmRetractionPercent,
                DefaultShoulderForearmRetractionWeight);
            float lowerTorsoRetractionWeight = GetRetractionWeight(
                _lowerTorsoRetractionPercent,
                0.0f,
                400.0f);
            float chestHelperRetractionWeight = GetRetractionWeight(
                _chestHelperRetractionPercent,
                0.0f,
                400.0f);
            float shoulderFixRetractionWeight = GetRetractionWeight(
                _shoulderFixRetractionPercent,
                0.0f,
                400.0f);
            float nativeClothRetractionWeight = GetRetractionWeight(
                _nativeClothRetractionPercent,
                0.0f,
                400.0f);
            float torsoRendererRetractionWeight = GetRetractionWeight(
                _torsoRendererRetractionPercent,
                1.0f,
                400.0f);
            float testBoneRetractionWeight = GetRetractionWeight(
                _testBoneRetractionPercent,
                0.0f,
                400.0f);
            MemoryBookkeeper.MemoryRegion region =
                default(MemoryBookkeeper.MemoryRegion);
            for (int index = 0;
                index < _cachedKandraRigs.Length;
                index++)
            {
                KandraRig rig = _cachedKandraRigs[index];
                if (rig == null
                    || !rigManager.TryGetMemoryRegionFor(
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

                ShoulderBoneProfile shoulderProfile =
                    GetShoulderBoneProfile(rig);
                float3 rendererRetraction =
                    rig == _torsoRetractionDedicatedRig
                        ? shoulderRetraction
                            * torsoRendererRetractionWeight
                        : float3.zero;
                bool applyShoulderRetraction =
                    math.lengthsq(shoulderRetraction) > 0.00000001f
                    && (shoulderProfile.AffectedBoneCount > 0
                        || (lowerTorsoRetractionWeight > 0.0f
                            && shoulderProfile.HipsIndex >= 0)
                        || (chestHelperRetractionWeight > 0.0f
                            && shoulderProfile.ChestHelperBoneCount > 0)
                        || (shoulderFixRetractionWeight > 0.0f
                            && shoulderProfile.ShoulderFixBoneCount > 0)
                        || (nativeClothRetractionWeight > 0.0f
                            && shoulderProfile.NativeClothBoneCount > 0));
                dependency = new OffsetKandraBonesJob
                {
                    Bones = bones,
                    StartIndex = start,
                    Translation = translation,
                    RendererRetraction = rendererRetraction,
                    ShoulderRetraction = shoulderRetraction,
                    ApplyShoulderRetraction =
                        applyShoulderRetraction,
                    SpineRetractionWeight = spineRetractionWeight,
                    Spine1RetractionWeight = spine1RetractionWeight,
                    Spine2RetractionWeight = spine2RetractionWeight,
                    LeftShoulderRetractionWeight =
                        leftShoulderRetractionWeight,
                    RightShoulderRetractionWeight =
                        rightShoulderRetractionWeight,
                    UpperArmRetractionWeight =
                        upperArmRetractionWeight,
                    ForearmRetractionWeight = forearmRetractionWeight,
                    LowerTorsoRetractionWeight =
                        lowerTorsoRetractionWeight,
                    ChestHelperRetractionWeight =
                        chestHelperRetractionWeight,
                    ShoulderFixRetractionWeight =
                        shoulderFixRetractionWeight,
                    NativeClothRetractionWeight =
                        nativeClothRetractionWeight,
                    TestBoneRetractionWeight =
                        testBoneRetractionWeight,
                    TestBoneIndex = shoulderProfile.TestBoneIndex,
                    HipsIndex = shoulderProfile.HipsIndex,
                    LeftBreastBaseIndex =
                        shoulderProfile.LeftBreastBaseIndex,
                    RightBreastBaseIndex =
                        shoulderProfile.RightBreastBaseIndex,
                    LeftBreastIndex = shoulderProfile.LeftBreastIndex,
                    RightBreastIndex = shoulderProfile.RightBreastIndex,
                    LeftShoulderFixIndex =
                        shoulderProfile.LeftShoulderFixIndex,
                    RightShoulderFixIndex =
                        shoulderProfile.RightShoulderFixIndex,
                    NativeClothStartIndex =
                        shoulderProfile.NativeClothStartIndex,
                    NativeClothEndIndex =
                        shoulderProfile.NativeClothEndIndex,
                    NativeClothIndicesContiguous =
                        shoulderProfile.NativeClothIndicesContiguous,
                    SpineIndex = shoulderProfile.SpineIndex,
                    Spine1Index = shoulderProfile.Spine1Index,
                    Spine2Index = shoulderProfile.Spine2Index,
                    LeftShoulderIndex =
                        shoulderProfile.LeftShoulderIndex,
                    RightShoulderIndex =
                        shoulderProfile.RightShoulderIndex,
                    LeftUpperArmIndex =
                        shoulderProfile.LeftUpperArmIndex,
                    RightUpperArmIndex =
                        shoulderProfile.RightUpperArmIndex,
                    LeftForearmIndex =
                        shoulderProfile.LeftForearmIndex,
                    RightForearmIndex =
                        shoulderProfile.RightForearmIndex
                }.Schedule(length, 32, dependency);
                rigCount++;
                boneCount += length;
                if (math.lengthsq(rendererRetraction) > 0.00000001f)
                {
                    torsoRendererBoneCount += length;
                }
                if (applyShoulderRetraction)
                {
                    shoulderRigCount++;
                    shoulderBoneCount +=
                        shoulderProfile.AffectedBoneCount;
                    if (lowerTorsoRetractionWeight > 0.0f
                        && shoulderProfile.HipsIndex >= 0)
                    {
                        shoulderBoneCount++;
                    }
                    if (chestHelperRetractionWeight > 0.0f)
                    {
                        shoulderBoneCount +=
                            shoulderProfile.ChestHelperBoneCount;
                    }
                    if (shoulderFixRetractionWeight > 0.0f)
                    {
                        shoulderBoneCount +=
                            shoulderProfile.ShoulderFixBoneCount;
                    }
                    if (nativeClothRetractionWeight > 0.0f)
                    {
                        shoulderBoneCount +=
                            shoulderProfile.NativeClothBoneCount;
                    }
                }
            }

            if (rigCount == 0)
            {
                return;
            }

            _readTransformField.SetValue(
                rigManager,
                dependency);
            float3 torsoRendererRetraction =
                _torsoRetractionDedicatedRig == null
                    ? float3.zero
                    : shoulderRetraction
                        * torsoRendererRetractionWeight;
            int cullingRendererCount =
                ApplyKandraCullingOffset(
                    translation,
                    torsoRendererRetraction);
            ReportShoulderRetractionDiagnostics(
                shoulderRigCount,
                shoulderBoneCount,
                torsoRendererBoneCount,
                rigCount);
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

        private int ApplyKandraCullingOffset(
            float3 translation,
            float3 torsoRendererRetraction)
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

            List<uint> rendererSlots = _kandraCullingRendererSlots;
            List<float3> rendererTranslations =
                _kandraCullingRendererTranslations;
            rendererSlots.Clear();
            rendererTranslations.Clear();
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
                    rendererTranslations.Add(
                        translation
                            + (renderer == _torsoRetractionRenderer
                                ? torsoRendererRetraction
                                : float3.zero));
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
            NativeArray<float3> translations = new NativeArray<float3>(
                rendererTranslations.Count,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            for (int index = 0; index < rendererSlots.Count; index++)
            {
                slots[index] = rendererSlots[index];
                translations[index] = rendererTranslations[index];
            }

            JobHandle cullingJob = new OffsetKandraCullingJob
            {
                Slots = slots,
                Translations = translations,
                RootBones = cullingManager.rootBones,
                Xs = cullingManager.xs,
                Ys = cullingManager.ys,
                Zs = cullingManager.zs
            }.Schedule(
                rendererSlots.Count,
                16,
                cullingManager.collectCullingDataJobHandle);
            JobHandle slotsDispose = slots.Dispose(cullingJob);
            JobHandle translationsDispose =
                translations.Dispose(cullingJob);
            cullingManager.collectCullingDataJobHandle =
                JobHandle.CombineDependencies(
                    slotsDispose,
                    translationsDispose);
            return rendererSlots.Count;
        }

        private void UpdateTorsoRendererRetractionRig()
        {
            bool requested = _enabled != null
                && _enabled.Value
                && _shoulderRetraction != null
                && _shoulderRetraction.Value > 0.0f
                && GetRetractionWeight(
                    _torsoRendererRetractionPercent,
                    1.0f,
                    400.0f) > 0.0f;
            Hero hero = Hero.Current;
            requested = requested
                && hero != null
                && !Hero.TppActive;
            if (requested != _torsoRetractionRequestedLastFrame)
            {
                _torsoRetractionRequestedLastFrame = requested;
                _nextKandraRigRefreshTime = 0.0f;
            }

            if (_pendingTorsoRetractionRenderer != null)
            {
                if (!requested)
                {
                    CancelPendingTorsoRendererRigSwap();
                }
                else if (KandraRendererManager.IsInvalidId(
                    _pendingTorsoRetractionRenderer.RenderingId))
                {
                    CompleteTorsoRendererRigSwap();
                }
                return;
            }
            if (!requested)
            {
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            if (bodyData == null)
            {
                return;
            }

            RefreshKandraRigs(bodyData);
            TryBeginTorsoRendererRigSwap(
                bodyData,
                _cachedKandraRenderers);
        }

        private void TryBeginTorsoRendererRigSwap(
            HeroBodyData bodyData,
            KandraRenderer[] renderers)
        {
            KandraRenderer target = FindTorsoRenderer(renderers);
            if (target == null
                || (target == _torsoRetractionRenderer
                    && target.rendererData.rig
                        == _torsoRetractionDedicatedRig))
            {
                ReportTorsoRendererOwnership(target, false);
                return;
            }

            KandraRig currentRig = target.rendererData.rig;
            if (currentRig != null
                && currentRig.name.StartsWith(
                    "FPAA_TorsoRendererRig",
                    StringComparison.Ordinal))
            {
                _torsoRetractionRenderer = target;
                _torsoRetractionDedicatedRig = currentRig;
                ReportTorsoRendererOwnership(target, true);
                return;
            }
            if (currentRig == null)
            {
                ReportTorsoRendererOwnership(target, false);
                return;
            }

            _pendingTorsoRetractionRenderer = target;
            _pendingTorsoRetractionOriginalRig = currentRig;
            _pendingTorsoRetractionBodyData = bodyData;
            _pendingTorsoRetractionRendererWasEnabled = target.enabled;
            if (target.enabled)
            {
                target.enabled = false;
            }
        }

        private static KandraRenderer FindTorsoRenderer(
            KandraRenderer[] renderers)
        {
            KandraRenderer cloth2Fallback = null;
            int clothOrdinal = 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                KandraRenderer renderer = renderers[index];
                if (renderer == null
                    || !String.Equals(
                        renderer.name,
                        "Cloth",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                clothOrdinal++;
                if (clothOrdinal == 2)
                {
                    cloth2Fallback = renderer;
                }
                KandraMesh mesh = renderer.rendererData.mesh;
                if (mesh != null
                    && mesh.name.IndexOf(
                        "Torso",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return renderer;
                }
                Material[] materials = renderer.rendererData.materials;
                if (materials == null)
                {
                    continue;
                }
                for (int materialIndex = 0;
                    materialIndex < materials.Length;
                    materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null
                        && material.name.IndexOf(
                            "Torso",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return renderer;
                    }
                }
            }
            return cloth2Fallback;
        }

        private void CompleteTorsoRendererRigSwap()
        {
            KandraRenderer renderer = _pendingTorsoRetractionRenderer;
            KandraRig originalRig = _pendingTorsoRetractionOriginalRig;
            HeroBodyData bodyData = _pendingTorsoRetractionBodyData;
            bool rendererWasEnabled =
                _pendingTorsoRetractionRendererWasEnabled;
            ClearPendingTorsoRendererRigSwap();
            if (renderer == null
                || originalRig == null
                || bodyData == null
                || originalRig.bones == null
                || originalRig.boneParents == null
                || originalRig.boneNames == null)
            {
                if (renderer != null
                    && rendererWasEnabled
                    && !renderer.enabled)
                {
                    renderer.enabled = true;
                }
                return;
            }

            GameObject rigObject = null;
            try
            {
                rigObject = new GameObject(
                    "FPAA_TorsoRendererRig");
                rigObject.SetActive(false);
                rigObject.transform.SetParent(bodyData.transform, false);
                KandraRig dedicatedRig =
                    rigObject.AddComponent<KandraRig>();
                dedicatedRig.animator = originalRig.animator;
                dedicatedRig.bones =
                    (Transform[])originalRig.bones.Clone();
                dedicatedRig.boneParents =
                    (ushort[])originalRig.boneParents.Clone();
                dedicatedRig.boneNames =
                    (FixedString64Bytes[])originalRig.boneNames.Clone();
                rigObject.SetActive(true);
                dedicatedRig.MarkAsBase();
                renderer.rendererData.rig = dedicatedRig;
                if (rendererWasEnabled)
                {
                    renderer.enabled = true;
                }
                _torsoRetractionRenderer = renderer;
                _torsoRetractionDedicatedRig = dedicatedRig;
                _nextKandraRigRefreshTime = 0.0f;
                _shoulderProfileDiagnosticSignature = null;
                ReportTorsoRendererOwnership(renderer, true);
            }
            catch (Exception exception)
            {
                renderer.rendererData.rig = originalRig;
                if (rigObject != null)
                {
                    Destroy(rigObject);
                }
                if (rendererWasEnabled && !renderer.enabled)
                {
                    renderer.enabled = true;
                }
                Logger.LogWarning(
                    "Could not create a dedicated torso-renderer rig; leaving the native renderer unchanged: "
                    + exception.Message);
            }
        }

        private void CancelPendingTorsoRendererRigSwap()
        {
            KandraRenderer renderer = _pendingTorsoRetractionRenderer;
            bool rendererWasEnabled =
                _pendingTorsoRetractionRendererWasEnabled;
            ClearPendingTorsoRendererRigSwap();
            if (renderer != null
                && rendererWasEnabled
                && !renderer.enabled)
            {
                renderer.enabled = true;
            }
        }

        private void ClearPendingTorsoRendererRigSwap()
        {
            _pendingTorsoRetractionRenderer = null;
            _pendingTorsoRetractionOriginalRig = null;
            _pendingTorsoRetractionBodyData = null;
            _pendingTorsoRetractionRendererWasEnabled = false;
        }

        private void ReportTorsoRendererOwnership(
            KandraRenderer renderer,
            bool dedicated)
        {
            if (_diagnostics == null || !_diagnostics.Value)
            {
                return;
            }
            string signature = (renderer == null
                    ? "missing"
                    : GetTransformPath(renderer.transform))
                + "|"
                + dedicated.ToString();
            if (String.Equals(
                signature,
                _torsoRendererDiagnosticSignature,
                StringComparison.Ordinal))
            {
                return;
            }
            _torsoRendererDiagnosticSignature = signature;
            Logger.LogInfo(
                "Torso renderer retraction: target="
                + (renderer == null
                    ? "missing"
                    : GetTransformPath(renderer.transform))
                + "; mesh="
                + (renderer == null
                    || renderer.rendererData.mesh == null
                        ? "missing"
                        : renderer.rendererData.mesh.name)
                + "; dedicatedRig="
                + dedicated.ToString(
                    CultureInfo.InvariantCulture)
                + ".");
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
            HashSet<KandraRig> rigs = _kandraRigRefreshRigs;
            List<KandraRig> bodyRigs = _kandraRigRefreshBodyRigs;
            List<KandraRenderer> renderers = _kandraRigRefreshRenderers;
            rigs.Clear();
            bodyRigs.Clear();
            renderers.Clear();
            bodyData.GetComponentsInChildren(true, bodyRigs);
            for (int index = 0; index < bodyRigs.Count; index++)
            {
                if (bodyRigs[index] != null)
                {
                    rigs.Add(bodyRigs[index]);
                }
            }

            bodyData.GetComponentsInChildren(true, renderers);
            for (int index = 0; index < renderers.Count; index++)
            {
                KandraRenderer renderer = renderers[index];
                if (renderer != null && renderer.rendererData.rig != null)
                {
                    rigs.Add(renderer.rendererData.rig);
                }
            }

            _cachedKandraRigs = new KandraRig[rigs.Count];
            rigs.CopyTo(_cachedKandraRigs);
            _cachedKandraRenderers = renderers.ToArray();

            List<KandraRig> staleProfiles = _staleShoulderBoneProfiles;
            staleProfiles.Clear();
            foreach (KandraRig cachedRig in _shoulderBoneProfiles.Keys)
            {
                if (cachedRig == null || !rigs.Contains(cachedRig))
                {
                    staleProfiles.Add(cachedRig);
                }
            }
            for (int index = 0; index < staleProfiles.Count; index++)
            {
                _shoulderBoneProfiles.Remove(staleProfiles[index]);
            }
        }

        private ShoulderBoneProfile GetShoulderBoneProfile(KandraRig rig)
        {
            int boneCount = rig == null || rig.bones == null
                ? 0
                : rig.bones.Length;
            string testBoneName = _testRetractionBoneName == null
                ? string.Empty
                : (_testRetractionBoneName.Value ?? string.Empty).Trim();
            ShoulderBoneProfile profile;
            if (_shoulderBoneProfiles.TryGetValue(rig, out profile)
                && profile.BoneCount == boneCount
                && string.Equals(
                    profile.TestBoneName,
                    testBoneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }

            profile = new ShoulderBoneProfile(
                boneCount,
                testBoneName);
            if (rig != null && rig.boneNames != null)
            {
                int nameCount = Math.Min(
                    boneCount,
                    rig.boneNames.Length);
                for (int index = 0; index < nameCount; index++)
                {
                    string boneName = rig.boneNames[index].ToString();
                    if (ShoulderBoneNameMatches(boneName, "Spine"))
                    {
                        profile.SetIndex(
                            ref profile.SpineIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(boneName, "Spine1"))
                    {
                        profile.SetIndex(
                            ref profile.Spine1Index,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(boneName, "Spine2"))
                    {
                        profile.SetIndex(
                            ref profile.Spine2Index,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftShoulder"))
                    {
                        profile.SetIndex(
                            ref profile.LeftShoulderIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightShoulder"))
                    {
                        profile.SetIndex(
                            ref profile.RightShoulderIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftArm"))
                    {
                        profile.SetIndex(
                            ref profile.LeftUpperArmIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightArm"))
                    {
                        profile.SetIndex(
                            ref profile.RightUpperArmIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftForeArm"))
                    {
                        profile.SetIndex(
                            ref profile.LeftForearmIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightForeArm"))
                    {
                        profile.SetIndex(
                            ref profile.RightForearmIndex,
                            index);
                    }
                    if (testBoneName.Length > 0
                        && ShoulderBoneNameMatches(
                            boneName,
                            testBoneName))
                    {
                        profile.SetTestBoneIndex(index);
                    }
                    if (ShoulderBoneNameMatches(boneName, "Hips"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.HipsIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftBreast_Base"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.LeftBreastBaseIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightBreast_Base"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.RightBreastBaseIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftBreast"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.LeftBreastIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightBreast"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.RightBreastIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "LeftShoulderFix"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.LeftShoulderFixIndex,
                            index);
                    }
                    else if (ShoulderBoneNameMatches(
                        boneName,
                        "RightShoulderFix"))
                    {
                        profile.SetAuxiliaryIndex(
                            ref profile.RightShoulderFixIndex,
                            index);
                    }
                    string shortBoneName = GetShortBoneName(boneName);
                    if (shortBoneName.StartsWith(
                        "Cloth_Skirt_",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        profile.RecordNativeClothIndex(index);
                    }
                }
            }

            _shoulderBoneProfiles[rig] = profile;
            return profile;
        }

        private static bool ShoulderBoneNameMatches(
            string boneName,
            string expectedName)
        {
            return String.Equals(
                    boneName,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase)
                || (boneName != null
                    && boneName.EndsWith(
                        ":" + expectedName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string GetShortBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
            {
                return string.Empty;
            }

            int separatorIndex = boneName.LastIndexOf(':');
            return separatorIndex < 0
                ? boneName
                : boneName.Substring(separatorIndex + 1);
        }

        private void ReportShoulderRetractionDiagnostics(
            int affectedRigCount,
            int affectedBoneCount,
            int torsoRendererBoneCount,
            int totalRigCount)
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || _shoulderRetraction == null
                || _shoulderRetraction.Value <= 0.0f
                || Time.unscaledTime
                    < _nextShoulderRetractionDiagnosticTime)
            {
                return;
            }

            _nextShoulderRetractionDiagnosticTime =
                Time.unscaledTime + 1.0f;
            Logger.LogInfo(
                "Shoulder retraction: configuredMeters="
                + _shoulderRetraction.Value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; appliedMeters="
                + _currentShoulderRetractionMeters.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; dodgeBlend="
                + _dodgeShoulderRetractionBlend.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; worldOffset="
                + FormatVector3(
                    _currentShoulderRetractionWorldOffset)
                + "; affectedBones="
                + affectedBoneCount.ToString(
                    CultureInfo.InvariantCulture)
                + "; affectedRigs="
                + affectedRigCount.ToString(
                    CultureInfo.InvariantCulture)
                + "/"
                + totalRigCount.ToString(
                    CultureInfo.InvariantCulture)
                + "; torsoRendererBones="
                + torsoRendererBoneCount.ToString(
                    CultureInfo.InvariantCulture)
                + "; torsoRendererMatched="
                + (_torsoRetractionRenderer != null).ToString(
                    CultureInfo.InvariantCulture)
                + "; effectiveRegionMeters="
                + "Spine:"
                + FormatRetractionMeters(
                    _spineRetractionPercent,
                    DefaultShoulderSpineRetractionWeight)
                + ",Spine1:"
                + FormatRetractionMeters(
                    _spine1RetractionPercent,
                    DefaultShoulderSpine1RetractionWeight)
                + ",Spine2:"
                + FormatRetractionMeters(
                    _spine2RetractionPercent,
                    DefaultShoulderSpine2RetractionWeight)
                + ",LeftShoulder:"
                + FormatRetractionMeters(
                    _leftShoulderRetractionPercent,
                    DefaultShoulderJointRetractionWeight)
                + ",RightShoulder:"
                + FormatRetractionMeters(
                    _rightShoulderRetractionPercent,
                    DefaultShoulderJointRetractionWeight)
                + ",UpperArms:"
                + FormatRetractionMeters(
                    _upperArmRetractionPercent,
                    DefaultShoulderUpperArmRetractionWeight)
                + ",Forearms:"
                + FormatRetractionMeters(
                    _forearmRetractionPercent,
                    DefaultShoulderForearmRetractionWeight)
                + ",LowerTorso:"
                + FormatRetractionMeters(
                    _lowerTorsoRetractionPercent,
                    0.0f,
                    400.0f)
                + ",ChestHelpers:"
                + FormatRetractionMeters(
                    _chestHelperRetractionPercent,
                    0.0f,
                    400.0f)
                + ",ShoulderFix:"
                + FormatRetractionMeters(
                    _shoulderFixRetractionPercent,
                    0.0f,
                    400.0f)
                + ",NativeCloth:"
                + FormatRetractionMeters(
                    _nativeClothRetractionPercent,
                    0.0f,
                    400.0f)
                + ",TorsoRenderer:"
                + FormatRetractionMeters(
                    _torsoRendererRetractionPercent,
                    1.0f,
                    400.0f)
                + ",TestBone["
                + (_testRetractionBoneName == null
                    ? string.Empty
                    : _testRetractionBoneName.Value)
                + "]:"
                + FormatRetractionMeters(
                    _testBoneRetractionPercent,
                    0.0f,
                    400.0f)
                + ".");

            string profileSignature = DescribeShoulderBoneProfiles();
            if (!string.Equals(
                    profileSignature,
                    _shoulderProfileDiagnosticSignature,
                    StringComparison.Ordinal))
            {
                _shoulderProfileDiagnosticSignature = profileSignature;
                Logger.LogInfo(
                    "Shoulder retraction bone profiles: "
                    + profileSignature
                    + ".");
            }
        }

        private static float GetRetractionWeight(
            ConfigEntry<float> entry,
            float fallbackWeight)
        {
            return GetRetractionWeight(entry, fallbackWeight, 200.0f);
        }

        private static float GetRetractionWeight(
            ConfigEntry<float> entry,
            float fallbackWeight,
            float maximumPercent)
        {
            return entry == null
                ? fallbackWeight
                : Mathf.Clamp(entry.Value, 0.0f, maximumPercent) * 0.01f;
        }

        private string FormatRetractionMeters(
            ConfigEntry<float> entry,
            float fallbackWeight)
        {
            return FormatRetractionMeters(
                entry,
                fallbackWeight,
                200.0f);
        }

        private string FormatRetractionMeters(
            ConfigEntry<float> entry,
            float fallbackWeight,
            float maximumPercent)
        {
            return (_currentShoulderRetractionMeters
                    * GetRetractionWeight(
                        entry,
                        fallbackWeight,
                        maximumPercent))
                .ToString("0.###", CultureInfo.InvariantCulture);
        }

        private string DescribeShoulderBoneProfiles()
        {
            StringBuilder description = new StringBuilder();
            for (int index = 0; index < _cachedKandraRigs.Length; index++)
            {
                KandraRig rig = _cachedKandraRigs[index];
                if (rig == null)
                {
                    continue;
                }

                ShoulderBoneProfile profile =
                    GetShoulderBoneProfile(rig);
                if (description.Length > 0)
                {
                    description.Append(" | ");
                }
                description.Append(rig.name);
                description.Append("{Spine=");
                description.Append(profile.SpineIndex);
                description.Append(",Spine1=");
                description.Append(profile.Spine1Index);
                description.Append(",Spine2=");
                description.Append(profile.Spine2Index);
                description.Append(",LeftShoulder=");
                description.Append(profile.LeftShoulderIndex);
                description.Append(",RightShoulder=");
                description.Append(profile.RightShoulderIndex);
                description.Append(",LeftArm=");
                description.Append(profile.LeftUpperArmIndex);
                description.Append(",RightArm=");
                description.Append(profile.RightUpperArmIndex);
                description.Append(",LeftForeArm=");
                description.Append(profile.LeftForearmIndex);
                description.Append(",RightForeArm=");
                description.Append(profile.RightForearmIndex);
                description.Append(",Hips=");
                description.Append(profile.HipsIndex);
                description.Append(",ChestHelpers=[");
                description.Append(profile.LeftBreastBaseIndex);
                description.Append(",");
                description.Append(profile.RightBreastBaseIndex);
                description.Append(",");
                description.Append(profile.LeftBreastIndex);
                description.Append(",");
                description.Append(profile.RightBreastIndex);
                description.Append("]");
                description.Append(",ShoulderFix=[");
                description.Append(profile.LeftShoulderFixIndex);
                description.Append(",");
                description.Append(profile.RightShoulderFixIndex);
                description.Append("]");
                description.Append(",NativeCloth=");
                description.Append(profile.NativeClothStartIndex);
                description.Append("-");
                description.Append(profile.NativeClothEndIndex);
                description.Append("/");
                description.Append(profile.NativeClothBoneCount);
                description.Append("/contiguous=");
                description.Append(
                    profile.NativeClothIndicesContiguous);
                description.Append(",TestBone=");
                description.Append(
                    profile.TestBoneName.Length == 0
                        ? "<empty>"
                        : profile.TestBoneName);
                description.Append("@");
                description.Append(profile.TestBoneIndex);
                description.Append(",AllBones=[");
                int nameCount = rig.boneNames == null
                    ? 0
                    : Math.Min(
                        profile.BoneCount,
                        rig.boneNames.Length);
                for (int boneIndex = 0;
                    boneIndex < nameCount;
                    boneIndex++)
                {
                    if (boneIndex > 0)
                    {
                        description.Append(",");
                    }
                    description.Append(boneIndex);
                    description.Append(":");
                    description.Append(rig.boneNames[boneIndex]);
                }
                description.Append("]");
                description.Append("}");
            }

            description.Append("; Renderers=[");
            for (int index = 0;
                index < _cachedKandraRenderers.Length;
                index++)
            {
                KandraRenderer renderer = _cachedKandraRenderers[index];
                if (renderer == null)
                {
                    continue;
                }
                if (description[description.Length - 1] != '[')
                {
                    description.Append(" | ");
                }
                description.Append(GetTransformPath(renderer.transform));
                description.Append("{rig=");
                KandraRig rendererRig = renderer.rendererData.rig;
                description.Append(
                    rendererRig == null
                        ? "missing"
                        : rendererRig.name);
                description.Append("}");
            }
            description.Append("]");

            return description.Length == 0
                ? "none"
                : description.ToString();
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
                RestoreDrakeOffsets(entityManager, null, false);
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            if (bodyData == null)
            {
                RestoreDrakeOffsets(entityManager, null, false);
                return;
            }

            Vector3 worldOffset;
            if (!TryGetCurrentVisualWorldOffset(out worldOffset)
                || worldOffset.sqrMagnitude <= 0.00000001f)
            {
                RestoreDrakeOffsets(entityManager, null, false);
                return;
            }

            RefreshWeaponEntityAccess(hero);
            if (_cachedWeaponEntityAccess.Count == 0)
            {
                RestoreDrakeOffsets(entityManager, null, false);
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
            HashSet<Entity> retainedEntities = _retainedDrakeEntities;
            retainedEntities.Clear();
            for (int index = 0;
                index < _cachedWeaponEntityAccess.Count;
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
                Vector3 accessLocalTranslation =
                    access.transform.InverseTransformVector(worldOffset);

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
                    Vector3 localTranslation = accessLocalTranslation;
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

            RestoreDrakeOffsets(entityManager, retainedEntities, true);
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

            RestoreDrakeOffsets(world.EntityManager, null, false);
        }

        private void RestoreDrakeOffsets(
            EntityManager entityManager,
            HashSet<Entity> retainedEntities,
            bool dependencyCompleted)
        {
            if (_originalDrakeOffsets.Count == 0)
            {
                return;
            }

            if (!dependencyCompleted)
            {
                entityManager.CompleteDependencyBeforeRW<
                    LinkedTransformLocalToWorldOffsetComponent>();
            }
            List<Entity> restoredEntities = _restoredDrakeEntities;
            restoredEntities.Clear();
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
            HashSet<LinkedEntitiesAccess> accesses = _weaponEntityAccesses;
            accesses.Clear();
            CollectWeaponEntityAccess(mainWeapon, accesses);
            CollectWeaponEntityAccess(offWeapon, accesses);
            CollectWeaponEntityAccess(hero.MainHand, accesses);
            CollectWeaponEntityAccess(hero.OffHand, accesses);
            _cachedWeaponEntityAccess.Clear();
            foreach (LinkedEntitiesAccess access in accesses)
            {
                _cachedWeaponEntityAccess.Add(access);
            }
        }

        private void CollectWeaponEntityAccess(
            CharacterHandBase weapon,
            HashSet<LinkedEntitiesAccess> accesses)
        {
            if (weapon == null)
            {
                return;
            }

            List<LinkedEntitiesAccess> weaponAccesses =
                _weaponEntityAccessScanBuffer;
            weaponAccesses.Clear();
            weapon.GetComponentsInChildren(true, weaponAccesses);
            for (int index = 0; index < weaponAccesses.Count; index++)
            {
                if (weaponAccesses[index] != null)
                {
                    accesses.Add(weaponAccesses[index]);
                }
            }
        }

        private void CollectWeaponEntityAccess(
            Transform handSocket,
            HashSet<LinkedEntitiesAccess> accesses)
        {
            if (handSocket == null)
            {
                return;
            }

            List<LinkedEntitiesAccess> socketAccesses =
                _weaponEntityAccessScanBuffer;
            socketAccesses.Clear();
            handSocket.GetComponentsInChildren(true, socketAccesses);
            for (int index = 0; index < socketAccesses.Count; index++)
            {
                if (socketAccesses[index] != null)
                {
                    accesses.Add(socketAccesses[index]);
                }
            }
        }

        private void ApplyAttachedEffectOffsets()
        {
            if (!isActiveAndEnabled
                || OffsetsSuspended()
                || _enabled == null
                || !_enabled.Value
                || _adjustAttachedEffects == null
                || !_adjustAttachedEffects.Value)
            {
                RestoreAttachedEffectOffsets();
                return;
            }

            Hero hero = Hero.Current;
            if (hero == null || Hero.TppActive)
            {
                RestoreAttachedEffectOffsets();
                return;
            }

            VHeroController controller = hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            if (bodyData == null
                || bodyData.transform == null)
            {
                RestoreAttachedEffectOffsets();
                return;
            }

            Vector3 worldOffset;
            if (!TryGetCurrentVisualWorldOffset(out worldOffset))
            {
                RestoreAttachedEffectOffsets();
                return;
            }

            RefreshAttachedEffectOffsets(hero, bodyData.transform);
            for (int index = 0;
                index < _attachedEffectOffsets.Count;
                index++)
            {
                _attachedEffectOffsets[index].Apply(worldOffset);
            }
        }

        private void RefreshAttachedEffectOffsets(
            Hero hero,
            Transform bodyRoot)
        {
            CharacterHandBase mainWeapon = hero.MainHandWeapon;
            CharacterHandBase offWeapon = hero.OffHandWeapon;
            float now = Time.unscaledTime;
            if (_cachedEffectMainHandWeapon == mainWeapon
                && _cachedEffectOffHandWeapon == offWeapon
                && now < _nextAttachedEffectRefreshTime)
            {
                return;
            }

            bool equipmentChanged =
                _cachedEffectMainHandWeapon != mainWeapon
                || _cachedEffectOffHandWeapon != offWeapon;
            RestoreAttachedEffectOffsets();
            _cachedEffectMainHandWeapon = mainWeapon;
            _cachedEffectOffHandWeapon = offWeapon;
            _nextAttachedEffectRefreshTime = now + 0.5f;

            HashSet<Transform> excludedRoots = _attachedEffectExcludedRoots;
            excludedRoots.Clear();
            AddEffectRoot(excludedRoots, mainWeapon == null
                ? null
                : mainWeapon.transform);
            AddEffectRoot(excludedRoots, offWeapon == null
                ? null
                : offWeapon.transform);
            AddEffectRoot(excludedRoots, hero.MainHand);
            AddEffectRoot(excludedRoots, hero.OffHand);

            HashSet<Transform> candidates = _attachedEffectCandidates;
            candidates.Clear();
            CollectPresentationEffectTransforms(
                mainWeapon == null ? null : mainWeapon.transform,
                bodyRoot,
                excludedRoots,
                candidates);
            CollectPresentationEffectTransforms(
                offWeapon == null ? null : offWeapon.transform,
                bodyRoot,
                excludedRoots,
                candidates);
            CollectPresentationEffectTransforms(
                hero.MainHand,
                bodyRoot,
                excludedRoots,
                candidates);
            CollectPresentationEffectTransforms(
                hero.OffHand,
                bodyRoot,
                excludedRoots,
                candidates);

            List<PresentationEffectOffsetState> states =
                _attachedEffectOffsets;
            states.Clear();
            int bodyRootCompensatedCount = 0;
            foreach (Transform candidate in candidates)
            {
                if (HasCandidateAncestor(candidate, candidates))
                {
                    continue;
                }

                bool compensateBodyRootRender = bodyRoot != null
                    && candidate.IsChildOf(bodyRoot);
                states.Add(
                    new PresentationEffectOffsetState(
                        candidate,
                        compensateBodyRootRender));
                if (compensateBodyRootRender)
                {
                    bodyRootCompensatedCount++;
                }
            }
            if (_diagnostics != null
                && _diagnostics.Value
                && (equipmentChanged
                    || _lastReportedAttachedEffectCount
                        != _attachedEffectOffsets.Count))
            {
                _lastReportedAttachedEffectCount =
                    _attachedEffectOffsets.Count;
                Logger.LogInfo(
                    "Cached "
                    + _attachedEffectOffsets.Count.ToString(
                        CultureInfo.InvariantCulture)
                    + " equipped presentation-effect transform(s) for first-person offsetting; body-root render compensation="
                    + bodyRootCompensatedCount.ToString(
                        CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static void AddEffectRoot(
            HashSet<Transform> roots,
            Transform root)
        {
            if (root != null)
            {
                roots.Add(root);
            }
        }

        private void CollectPresentationEffectTransforms(
            Transform root,
            Transform bodyRoot,
            HashSet<Transform> excludedRoots,
            HashSet<Transform> candidates)
        {
            if (root == null)
            {
                return;
            }

            List<VisualEffect> visualEffects = _visualEffectScanBuffer;
            visualEffects.Clear();
            root.GetComponentsInChildren(true, visualEffects);
            for (int index = 0; index < visualEffects.Count; index++)
            {
                VisualEffect visualEffect = visualEffects[index];
                if (visualEffect != null)
                {
                    TryAddPresentationEffectTransform(
                        visualEffect.transform,
                        excludedRoots,
                        candidates);
                }
            }

            List<ParticleSystem> particleSystems = _particleSystemScanBuffer;
            particleSystems.Clear();
            root.GetComponentsInChildren(true, particleSystems);
            for (int index = 0; index < particleSystems.Count; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem != null)
                {
                    TryAddPresentationEffectTransform(
                        particleSystem.transform,
                        excludedRoots,
                        candidates);
                }
            }
        }

        private static void TryAddPresentationEffectTransform(
            Transform effectTransform,
            HashSet<Transform> excludedRoots,
            HashSet<Transform> candidates)
        {
            if (effectTransform == null
                || excludedRoots.Contains(effectTransform)
                || effectTransform.GetComponent<Collider>() != null
                || effectTransform.GetComponent<Rigidbody>() != null
                || effectTransform.GetComponent<CharacterController>() != null
                || effectTransform.GetComponent<Light>() != null)
            {
                return;
            }

            candidates.Add(effectTransform);
        }

        private static bool HasCandidateAncestor(
            Transform candidate,
            HashSet<Transform> candidates)
        {
            Transform current = candidate == null
                ? null
                : candidate.parent;
            while (current != null)
            {
                if (candidates.Contains(current))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private void RestoreAttachedEffectOffsets()
        {
            for (int index = 0;
                index < _attachedEffectOffsets.Count;
                index++)
            {
                _attachedEffectOffsets[index].Restore();
            }
        }

        private void SuspendBodyRootAttachedEffectOffsets()
        {
            for (int index = 0;
                index < _attachedEffectOffsets.Count;
                index++)
            {
                _attachedEffectOffsets[index].SuspendForBodyRootRender();
            }
        }

        private void ResumeBodyRootAttachedEffectOffsets()
        {
            for (int index = 0;
                index < _attachedEffectOffsets.Count;
                index++)
            {
                _attachedEffectOffsets[index].ResumeAfterBodyRootRender();
            }
        }

        private void UpdateHeadBob()
        {
            Hero hero = Hero.Current;
            bool motionAvailable = _enabled != null
                && _enabled.Value
                && _enableHeadBob != null
                && _enableHeadBob.Value
                && IsHeadBobAccessibilityEnabled()
                && hero != null
                && !Hero.TppActive
                && hero.Grounded
                && !hero.IsSwimming
                && hero.VHeroController != null
                && hero.VHeroController.MainCamera != null;
            float speed = motionAvailable
                ? Mathf.Max(0.0f, hero.HorizontalSpeed)
                : 0.0f;
            bool moving = motionAvailable
                && speed >= HeadBobSpeedThreshold;
            float targetWeight = moving
                ? Mathf.Clamp01(speed / 1.5f)
                : 0.0f;
            float deltaTime = Mathf.Min(
                Mathf.Max(0.0f, Time.unscaledDeltaTime),
                HeadBobMaximumDeltaTime);
            float blendTime = targetWeight > _headBobWeight
                ? HeadBobBlendInSeconds
                : HeadBobBlendOutSeconds;
            _headBobWeight = Mathf.SmoothDamp(
                _headBobWeight,
                targetWeight,
                ref _headBobWeightVelocity,
                blendTime,
                Mathf.Infinity,
                deltaTime);

            bool sprinting = moving && hero.IsSprinting;
            _headBobSprintWeight = Mathf.SmoothDamp(
                _headBobSprintWeight,
                sprinting ? 1.0f : 0.0f,
                ref _headBobSprintWeightVelocity,
                HeadBobSprintBlendSeconds,
                Mathf.Infinity,
                deltaTime);

            float targetVerticalCadence = 0.0f;
            float targetVerticalAmplitude = 0.0f;
            float targetLateralAmplitude = 0.0f;
            _headBobRawVerticalCadence = 0.0f;
            _headBobEffectiveVerticalCadence = 0.0f;
            _headBobTargetVerticalCadence = 0.0f;
            if (moving && deltaTime > 0.0f)
            {
                float strideLength;
                float verticalAmplitude;
                float lateralAmplitude;
                GetHeadBobPresetValues(
                    _headBobPreset.Value,
                    out strideLength,
                    out verticalAmplitude,
                    out lateralAmplitude);
                float sprintImpact = Mathf.Clamp01(
                    _sprintEmphasis.Value);
                float sprintAmplitude = Mathf.Lerp(
                    1.0f,
                    1.0f
                        + (HeadBobMaximumSprintAmplitudeBonus
                            * sprintImpact),
                    _headBobSprintWeight);
                float sprintCadence = Mathf.Lerp(
                    1.0f,
                    1.0f
                        + (HeadBobMaximumSprintCadenceBonus
                            * sprintImpact),
                    _headBobSprintWeight);
                float rawVerticalCadence = speed
                    * sprintCadence
                    * 2.0f
                    / strideLength;
                float maximumVerticalCadence = Mathf.Lerp(
                    HeadBobMaximumWalkVerticalCadenceHz,
                    HeadBobMaximumSprintVerticalCadenceHz,
                    sprintImpact * _headBobSprintWeight);
                float effectiveVerticalCadence =
                    SoftLimitHeadBobCadence(
                        rawVerticalCadence,
                        maximumVerticalCadence);
                _headBobRawVerticalCadence = rawVerticalCadence;
                _headBobEffectiveVerticalCadence =
                    effectiveVerticalCadence;
                float cadenceScale = _headBobSpeedPercent == null
                    ? 1.0f
                    : Mathf.Clamp(
                        _headBobSpeedPercent.Value,
                        50.0f,
                        150.0f) / 100.0f;
                targetVerticalCadence =
                    effectiveVerticalCadence * cadenceScale;
                _headBobTargetVerticalCadence =
                    targetVerticalCadence;
                float speedScale = Mathf.Lerp(
                    0.85f,
                    1.15f,
                    Mathf.Clamp01(speed / 5.0f));
                float amplitude = _headBobWeight
                    * speedScale
                    * sprintAmplitude;
                targetVerticalAmplitude =
                    verticalAmplitude * amplitude;
                targetLateralAmplitude =
                    lateralAmplitude * amplitude;
            }

            float smoothness = _headBobSmoothness == null
                ? 0.0f
                : Mathf.Clamp01(
                    _headBobSmoothness.Value);
            if (smoothness <= 0.0f || deltaTime <= 0.0f)
            {
                _headBobSmoothedVerticalCadence =
                    targetVerticalCadence;
                _headBobCadenceVelocity = 0.0f;
                _headBobSmoothedVerticalAmplitude =
                    targetVerticalAmplitude;
                _headBobVerticalAmplitudeVelocity = 0.0f;
                _headBobSmoothedLateralAmplitude =
                    targetLateralAmplitude;
                _headBobLateralAmplitudeVelocity = 0.0f;
            }
            else
            {
                float responseTime = Mathf.Lerp(
                    HeadBobMinimumResponseTime,
                    HeadBobMaximumResponseTime,
                    smoothness);
                _headBobSmoothedVerticalCadence =
                    Mathf.SmoothDamp(
                        _headBobSmoothedVerticalCadence,
                        targetVerticalCadence,
                        ref _headBobCadenceVelocity,
                        responseTime,
                        Mathf.Infinity,
                        deltaTime);
                _headBobSmoothedVerticalAmplitude =
                    Mathf.SmoothDamp(
                        _headBobSmoothedVerticalAmplitude,
                        targetVerticalAmplitude,
                        ref _headBobVerticalAmplitudeVelocity,
                        responseTime,
                        Mathf.Infinity,
                        deltaTime);
                _headBobSmoothedLateralAmplitude =
                    Mathf.SmoothDamp(
                        _headBobSmoothedLateralAmplitude,
                        targetLateralAmplitude,
                        ref _headBobLateralAmplitudeVelocity,
                        responseTime,
                        Mathf.Infinity,
                        deltaTime);
            }

            if (targetVerticalCadence <= 0.0f
                && _headBobSmoothedVerticalCadence <= 0.001f)
            {
                _headBobSmoothedVerticalCadence = 0.0f;
                _headBobCadenceVelocity = 0.0f;
            }
            _headBobStridePhase = Mathf.Repeat(
                _headBobStridePhase
                    + (_headBobSmoothedVerticalCadence
                        * deltaTime
                        * Mathf.PI),
                TwoPi);
            _headBobLocalOffset = new Vector3(
                Mathf.Sin(_headBobStridePhase)
                    * _headBobSmoothedLateralAmplitude,
                -Mathf.Cos(_headBobStridePhase * 2.0f)
                    * _headBobSmoothedVerticalAmplitude,
                0.0f);
            if (targetVerticalAmplitude <= 0.00001f
                && targetLateralAmplitude <= 0.00001f
                && _headBobLocalOffset.sqrMagnitude
                    <= 0.00000001f)
            {
                _headBobSmoothedVerticalAmplitude = 0.0f;
                _headBobVerticalAmplitudeVelocity = 0.0f;
                _headBobSmoothedLateralAmplitude = 0.0f;
                _headBobLateralAmplitudeVelocity = 0.0f;
                _headBobLocalOffset = Vector3.zero;
            }

            ReportHeadBobDiagnostics(
                hero,
                speed,
                motionAvailable);
        }

        private static float SoftLimitHeadBobCadence(
            float rawCadence,
            float maximumCadence)
        {
            rawCadence = Mathf.Max(0.0f, rawCadence);
            maximumCadence = Mathf.Max(0.0f, maximumCadence);
            float knee = maximumCadence
                * HeadBobCadenceSoftKneeRatio;
            if (maximumCadence <= 0.0f)
            {
                return 0.0f;
            }
            if (rawCadence <= knee)
            {
                return rawCadence;
            }

            float range = maximumCadence - knee;
            return maximumCadence
                - (range * Mathf.Exp(
                    -(rawCadence - knee) / range));
        }

        private static void GetHeadBobPresetValues(
            HeadBobPreset preset,
            out float strideLength,
            out float verticalAmplitude,
            out float lateralAmplitude)
        {
            switch (preset)
            {
                case HeadBobPreset.Subtle:
                    strideLength = 2.05f;
                    verticalAmplitude = 0.006f;
                    lateralAmplitude = 0.002f;
                    break;
                case HeadBobPreset.Strong:
                    strideLength = 1.75f;
                    verticalAmplitude = 0.025f;
                    lateralAmplitude = 0.0085f;
                    break;
                default:
                    strideLength = 1.95f;
                    verticalAmplitude = 0.012f;
                    lateralAmplitude = 0.004f;
                    break;
            }
        }

        private void ResetHeadBob()
        {
            _headBobStridePhase = 0.0f;
            _headBobRawVerticalCadence = 0.0f;
            _headBobEffectiveVerticalCadence = 0.0f;
            _headBobTargetVerticalCadence = 0.0f;
            _headBobSmoothedVerticalCadence = 0.0f;
            _headBobCadenceVelocity = 0.0f;
            _headBobSmoothedVerticalAmplitude = 0.0f;
            _headBobVerticalAmplitudeVelocity = 0.0f;
            _headBobSmoothedLateralAmplitude = 0.0f;
            _headBobLateralAmplitudeVelocity = 0.0f;
            _headBobWeight = 0.0f;
            _headBobWeightVelocity = 0.0f;
            _headBobSprintWeight = 0.0f;
            _headBobSprintWeightVelocity = 0.0f;
            _headBobLocalOffset = Vector3.zero;
            _headBobCameraWorldOffset = Vector3.zero;
            _viewmodelHeadBobAppliedWorldOffset = Vector3.zero;
        }

        private void TryApplyHeadBob(
            Camera camera,
            string applicationPhase)
        {
            if (_headBobApplied
                || camera == null
                || _headBobLocalOffset.sqrMagnitude
                    <= 0.00000001f)
            {
                return;
            }

            if (!CanApplyHeadBob(camera))
            {
                return;
            }

            _headBobCamera = camera;
            _headBobOriginalCameraPosition =
                camera.transform.position;
            camera.transform.position =
                _headBobOriginalCameraPosition
                + camera.transform.TransformVector(
                    _headBobLocalOffset);
            _headBobApplied = true;
            _headBobApplicationPhase = applicationPhase;
        }

        private bool UsesTemporalSafeHeadBobTiming()
        {
            return _temporalSafeHeadBobPatchInstalled
                && _temporalSafeHeadBobTiming != null
                && _temporalSafeHeadBobTiming.Value;
        }

        internal void TryApplyHeadBobBeforeHdrpCameraUpdate(
            HDCamera hdCamera)
        {
            if (!UsesTemporalSafeHeadBobTiming() || hdCamera == null)
            {
                return;
            }

            TryApplyHeadBob(
                hdCamera.camera,
                "before-hdrp-camera-update");
        }

        internal void ReportHeadBobAfterHdrpCameraUpdate(
            HDCamera hdCamera)
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || !UsesTemporalSafeHeadBobTiming()
                || !_headBobApplied
                || hdCamera == null
                || hdCamera.camera == null
                || hdCamera.camera != _headBobCamera
                || Time.unscaledTime
                    < _nextTemporalHeadBobDiagnosticTime)
            {
                return;
            }

            _nextTemporalHeadBobDiagnosticTime =
                Time.unscaledTime + 1.0f;
            Vector3 renderedPosition = hdCamera.camera.transform.position;
            Vector3 capturedPosition =
                hdCamera.mainViewConstants.worldSpaceCameraPos;
            Logger.LogInfo(
                "Temporal-safe head bob: phase="
                + (_headBobApplicationPhase ?? "unknown")
                + "; frame="
                + Time.frameCount.ToString(
                    CultureInfo.InvariantCulture)
                + "; renderedPosition="
                + FormatVector3(renderedPosition)
                + "; hdrpCapturedPosition="
                + FormatVector3(capturedPosition)
                + "; matched="
                + ((renderedPosition - capturedPosition).sqrMagnitude
                    <= 0.00000001f).ToString(
                        CultureInfo.InvariantCulture)
                + ".");
        }

        private bool CanApplyHeadBob(Camera camera)
        {
            Hero hero = Hero.Current;
            return camera != null
                && _enabled != null
                && _enabled.Value
                && _enableHeadBob != null
                && _enableHeadBob.Value
                && IsHeadBobAccessibilityEnabled()
                && hero != null
                && !Hero.TppActive
                && hero.VHeroController != null
                && hero.VHeroController.MainCamera == camera;
        }

        private Vector3 GetViewmodelHeadBobFollowWorldOffset(
            Camera camera)
        {
            if (_viewmodelHeadBobFollowPercent == null
                || _stabilizeViewmodelDuringHeadBob == null
                || !CanApplyHeadBob(camera))
            {
                _headBobCameraWorldOffset = Vector3.zero;
                _viewmodelHeadBobAppliedWorldOffset = Vector3.zero;
                return Vector3.zero;
            }

            float followWeight = _stabilizeViewmodelDuringHeadBob.Value
                ? 1.0f
                : Mathf.Clamp01(
                    _viewmodelHeadBobFollowPercent.Value / 100.0f);
            _headBobCameraWorldOffset =
                camera.transform.TransformVector(_headBobLocalOffset);
            _viewmodelHeadBobAppliedWorldOffset =
                _headBobCameraWorldOffset * followWeight;
            return _viewmodelHeadBobAppliedWorldOffset;
        }

        private void TrySuppressHeadBobCameraMotionBlur(Camera camera)
        {
            if (_headBobMotionBlurSuppressed
                || _suppressMotionBlurDuringHeadBob == null
                || !_suppressMotionBlurDuringHeadBob.Value)
            {
                return;
            }

            HDCamera hdCamera = HDCamera.GetOrCreate(camera);
            MotionBlur motionBlur = hdCamera == null
                ? null
                : hdCamera.volumeStack.GetComponent<MotionBlur>();
            if (motionBlur == null
                || motionBlur.cameraMotionBlur == null)
            {
                if (!_headBobMotionBlurUnavailableReported)
                {
                    _headBobMotionBlurUnavailableReported = true;
                    Logger.LogWarning(
                        "Could not suppress camera motion blur during head bob because the main HDRP camera has no motion-blur volume state.");
                }
                return;
            }

            _headBobMotionBlurCamera = camera;
            _headBobMotionBlur = motionBlur;
            _headBobMotionBlurOriginalCameraValue =
                motionBlur.cameraMotionBlur.value;
            motionBlur.cameraMotionBlur.value = false;
            _headBobMotionBlurSuppressed = true;
        }

        private void RestoreHeadBob(Camera camera = null)
        {
            bool restoreCamera = _headBobApplied
                && (camera == null || camera == _headBobCamera);
            bool restoreMotionBlur = _headBobMotionBlurSuppressed
                && (camera == null
                    || camera == _headBobMotionBlurCamera);
            if (!restoreCamera && !restoreMotionBlur)
            {
                return;
            }

            if (restoreCamera)
            {
                if (_headBobCamera != null)
                {
                    _headBobCamera.transform.position =
                        _headBobOriginalCameraPosition;
                }
                _headBobCamera = null;
                _headBobApplied = false;
                _headBobApplicationPhase = null;
            }
            if (restoreMotionBlur)
            {
                RestoreHeadBobMotionBlurSuppression();
            }
        }

        private void RestoreHeadBobMotionBlurSuppression()
        {
            if (_headBobMotionBlur != null
                && _headBobMotionBlur.cameraMotionBlur != null)
            {
                _headBobMotionBlur.cameraMotionBlur.value =
                    _headBobMotionBlurOriginalCameraValue;
            }

            _headBobMotionBlurCamera = null;
            _headBobMotionBlur = null;
            _headBobMotionBlurSuppressed = false;
        }

        private void ReportHeadBobDiagnostics(
            Hero hero,
            float speed,
            bool motionAvailable)
        {
            if (_diagnostics == null
                || !_diagnostics.Value
                || Time.unscaledTime
                    < _nextHeadBobDiagnosticTime)
            {
                return;
            }

            _nextHeadBobDiagnosticTime =
                Time.unscaledTime + 1.0f;
            Logger.LogInfo(
                "Head bob: available="
                + motionAvailable.ToString(
                    CultureInfo.InvariantCulture)
                + "; speed="
                + speed.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; sprinting="
                + (hero != null && hero.IsSprinting).ToString(
                    CultureInfo.InvariantCulture)
                + "; sprintWeight="
                + _headBobSprintWeight.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; preset="
                + _headBobPreset.Value
                + "; smoothness="
                + _headBobSmoothness.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; sprintEmphasis="
                + _sprintEmphasis.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; headBobSpeedPercent="
                + _headBobSpeedPercent.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; stabilizeViewmodel="
                + _stabilizeViewmodelDuringHeadBob.Value.ToString(
                    CultureInfo.InvariantCulture)
                + "; viewmodelFollowPercent="
                + _viewmodelHeadBobFollowPercent.Value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture)
                + "; temporalSafeTiming="
                + UsesTemporalSafeHeadBobTiming().ToString(
                    CultureInfo.InvariantCulture)
                + "; rawVerticalCadenceHz="
                + _headBobRawVerticalCadence.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; effectiveVerticalCadenceHz="
                + _headBobEffectiveVerticalCadence.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; targetVerticalCadenceHz="
                + _headBobTargetVerticalCadence.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; smoothedVerticalCadenceHz="
                + _headBobSmoothedVerticalCadence.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; verticalAmplitude="
                + _headBobSmoothedVerticalAmplitude.ToString(
                    "0.####",
                    CultureInfo.InvariantCulture)
                + "; lateralAmplitude="
                + _headBobSmoothedLateralAmplitude.ToString(
                    "0.####",
                    CultureInfo.InvariantCulture)
                + "; cameraBobWorldOffset="
                + FormatVector3(_headBobCameraWorldOffset)
                + "; appliedViewmodelStabilization="
                + FormatVector3(_viewmodelHeadBobAppliedWorldOffset)
                + "; remainingViewmodelBob="
                + FormatVector3(
                    _headBobCameraWorldOffset
                        - _viewmodelHeadBobAppliedWorldOffset)
                + "; localOffset=("
                + _headBobLocalOffset.x.ToString(
                    "0.####",
                    CultureInfo.InvariantCulture)
                + ","
                + _headBobLocalOffset.y.ToString(
                    "0.####",
                    CultureInfo.InvariantCulture)
                + ",0).");
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (!UsesTemporalSafeHeadBobTiming())
            {
                TryApplyHeadBob(
                    camera,
                    "camera-pre-cull");
            }
            TryApplyRenderOffset(camera);
        }

        private void OnCameraPostRender(Camera camera)
        {
            RestoreRenderOffset(camera);
            RestoreHeadBob(camera);
        }

        private void OnBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (!UsesTemporalSafeHeadBobTiming())
            {
                TryApplyHeadBob(
                    camera,
                    "begin-camera-rendering");
            }
            if (_headBobApplied && _headBobCamera == camera)
            {
                TrySuppressHeadBobCameraMotionBlur(camera);
            }
            TryApplyRenderOffset(camera);
        }

        private void OnEndCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            RestoreRenderOffset(camera);
            RestoreHeadBob(camera);
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

            Vector3 worldOffset;
            if (!TryGetCurrentVisualWorldOffset(out worldOffset)
                || worldOffset.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            _offsetRoot = bodyData.transform;
            _offsetCamera = camera;
            _originalWorldPosition = _offsetRoot.position;
            SuspendBodyRootAttachedEffectOffsets();
            _offsetRoot.position =
                _originalWorldPosition
                + worldOffset;
            _renderOffsetApplied = true;

            if (_diagnostics.Value && _lastReportedRoot != _offsetRoot)
            {
                _lastReportedRoot = _offsetRoot;
                Vector3 localOffset =
                    camera.transform.InverseTransformVector(worldOffset);
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
            ResumeBodyRootAttachedEffectOffsets();

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
            UpdateBowDrawGuardBlend();
            UpdateHeldMeleeOffsetBlend();
            UpdateSprintAttackOffsetBlend();
            UpdateDodgeShoulderRetractionBlend();
            UpdateExecutionGuardBlend();
            UpdateSheathingOffsetBlend();
            float configuredForwardOffset =
                GetEffectiveForwardOffset(hero);
            float bowDrawMaximumOffsetPercent =
                _bowDrawMaximumOffsetPercent == null
                    ? 33.0f
                    : Mathf.Clamp(
                        _bowDrawMaximumOffsetPercent.Value,
                        0.0f,
                        100.0f);
            float bowDrawDepthCeiling = _bowForwardOffset.Value
                * (bowDrawMaximumOffsetPercent / 100.0f);
            configuredForwardOffset = Mathf.LerpUnclamped(
                configuredForwardOffset,
                Mathf.Min(
                    configuredForwardOffset,
                    bowDrawDepthCeiling),
                _bowDrawGuardBlend);
            Vector3 configuredOffset = new Vector3(
                _horizontalOffset.Value,
                _verticalOffset.Value,
                configuredForwardOffset);
            float sharedGuardStrength;
            bool useSharedGuardTarget =
                TryGetSharedAnimationGuardStrength(
                    out sharedGuardStrength);
            float retainedScale;
            Vector3 heldCorrection;
            float sprintAttackRetainedScale;
            float sheathingRetainedScale;
            float effectiveHeldMeleeBlend = _bowDrawGuardActive
                ? 0.0f
                : _heldMeleeMitigationBlend;
            if (useSharedGuardTarget)
            {
                float strongestMoveTowardVanilla = Mathf.Max(
                    Mathf.Max(
                        effectiveHeldMeleeBlend
                            * sharedGuardStrength,
                        _sprintAttackOffsetBlend
                            * sharedGuardStrength),
                    Mathf.Max(
                        (1.0f - _sheathingOffsetBlend)
                            * sharedGuardStrength,
                        _executionGuardBlend
                            * ExecutionMoveTowardVanillaStrength));
                retainedScale = 1.0f - Mathf.Clamp01(
                    strongestMoveTowardVanilla);
                heldCorrection = Vector3.zero;
                sprintAttackRetainedScale = 1.0f;
                sheathingRetainedScale = 1.0f;
            }
            else
            {
                retainedScale = Mathf.LerpUnclamped(
                    1.0f,
                    _heldMeleeOffsetScale.Value,
                    effectiveHeldMeleeBlend);
                heldCorrection = new Vector3(
                    0.0f,
                    _heldMeleeExtraVerticalOffset.Value,
                    _heldMeleeExtraForwardOffset.Value)
                    * effectiveHeldMeleeBlend;
                sprintAttackRetainedScale =
                    1.0f - _sprintAttackOffsetBlend;
                sheathingRetainedScale = _sheathingOffsetBlend;
            }
            return (configuredOffset * retainedScale + heldCorrection)
                * _fireplaceOffsetBlend
                * sprintAttackRetainedScale
                * sheathingRetainedScale
                * (useSharedGuardTarget
                    ? 1.0f
                    : 1.0f
                        - (_executionGuardBlend
                            * ExecutionMoveTowardVanillaStrength));
        }

        internal void CaptureVisualWorldOffsetAfterCameraRotation(
            VHeroController controller)
        {
            Hero hero = Hero.Current;
            if (hero == null || hero.VHeroController != controller)
            {
                return;
            }

            RefreshCurrentVisualWorldOffset(controller);
        }

        internal bool TryGetCurrentVisualWorldOffset(
            out Vector3 worldOffset)
        {
            if (_currentVisualWorldOffsetFrame != Time.frameCount
                && Time.timeScale <= 0.0f)
            {
                RefreshCurrentVisualWorldOffset(null);
                ReportPausedRenderOffsetFallback();
            }

            worldOffset = _currentVisualWorldOffset;
            return _currentVisualWorldOffsetFrame == Time.frameCount
                && _hasCurrentVisualWorldOffset;
        }

        private void ReportPausedRenderOffsetFallback()
        {
            if (!_hasCurrentVisualWorldOffset
                || _diagnostics == null
                || !_diagnostics.Value
                || Time.unscaledTime
                    < _nextPausedRenderOffsetDiagnosticTime)
            {
                return;
            }

            _nextPausedRenderOffsetDiagnosticTime =
                Time.unscaledTime + 1.0f;
            Logger.LogInfo(
                "Refreshed the first-person presentation offset during paused rendering because VHeroController.ProcessUpdate did not provide a current-frame snapshot; frame="
                + Time.frameCount.ToString(
                    CultureInfo.InvariantCulture)
                + "; worldOffset="
                + FormatVector3(_currentVisualWorldOffset)
                + ".");
        }

        private void RefreshCurrentVisualWorldOffset(
            VHeroController updatedController)
        {
            if (_currentVisualWorldOffsetFrame == Time.frameCount)
            {
                return;
            }

            _currentVisualWorldOffsetFrame = Time.frameCount;
            _currentVisualWorldOffset = Vector3.zero;
            _currentShoulderRetractionMeters = 0.0f;
            _currentShoulderRetractionWorldOffset = Vector3.zero;
            _hasCurrentVisualWorldOffset = false;

            if (!isActiveAndEnabled
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

            VHeroController controller = updatedController
                ?? hero.VHeroController;
            HeroBodyData bodyData = controller == null
                ? null
                : controller.BodyData;
            Camera camera = controller == null
                ? null
                : controller.MainCamera;
            if (bodyData == null
                || bodyData.transform == null
                || camera == null)
            {
                return;
            }

            Transform visualBasis = controller.fppParent == null
                ? camera.transform
                : controller.fppParent.transform;
            Vector3 configuredLocalOffset =
                GetEffectiveLocalOffset(hero);
            Vector3 configuredWorldOffset = visualBasis.TransformVector(
                configuredLocalOffset);
            if (_shoulderRetraction != null)
            {
                float configuredShoulderRetraction = Mathf.Clamp(
                    _shoulderRetraction.Value,
                    0.0f,
                    0.25f);
                float dodgeRetractionRemaining = 1.0f
                    - _dodgeShoulderRetractionBlend;
                float dodgeRetractionProgress = 1.0f
                    - (dodgeRetractionRemaining
                        * dodgeRetractionRemaining
                        * dodgeRetractionRemaining);
                float dodgeShoulderRetraction = Mathf.Lerp(
                    configuredShoulderRetraction,
                    DodgeRetractionMaximumMeters,
                    dodgeRetractionProgress);
                float executionShoulderRetraction = Mathf.Lerp(
                    configuredShoulderRetraction,
                    Mathf.Max(
                        configuredShoulderRetraction,
                        ExecutionShoulderRetractionMeters),
                    _executionGuardBlend);
                _currentShoulderRetractionMeters = Mathf.Max(
                    dodgeShoulderRetraction,
                    executionShoulderRetraction);
                _currentShoulderRetractionWorldOffset =
                    visualBasis.TransformVector(
                        new Vector3(
                            0.0f,
                            0.0f,
                            -_currentShoulderRetractionMeters));
            }
            Vector3 viewmodelHeadBobWorldOffset =
                GetViewmodelHeadBobFollowWorldOffset(camera);
            _currentVisualWorldOffset = configuredWorldOffset
                + viewmodelHeadBobWorldOffset;
            _hasCurrentVisualWorldOffset = true;
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
                "General",
                "ConfigSchemaVersion",
                ConfigSchemaVersion,
                new ConfigDescription(
                    "Configuration layout version. Older layouts are backed up and regenerated.",
                    null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                new ConfigDescription(
                    "Turns first-person arm and equipment positioning plus FPAA head bob on or off. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "General",
                        DisplayName = "Enabled",
                        SectionOrder = 0,
                        Order = 0
                    }));
            _useCategoryForwardOffsets = Config.Bind(
                "Equipment Depth",
                "UseCategoryForwardOffsets",
                true,
                new ConfigDescription(
                    "Uses separate depth offsets for melee weapons, bows, and magic. When off, all equipment uses General / Unarmed Depth Offset. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Equipment Depth",
                        DisplayName = "Use Separate Equipment Depths",
                        SectionOrder = 20,
                        Order = 0
                    }));
            _adjustAttachedEffects = Config.Bind(
                "Advanced - Effects",
                "AdjustAttachedEffects",
                true,
                new ConfigDescription(
                    "Moves supported attached visual effects with the rendered first-person item. Gameplay roots, lights, colliders, and sockets are unchanged. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Effects",
                        DisplayName = "Keep Attached Effects Aligned",
                        SectionOrder = 45,
                        Order = 0
                    }));
            _enableAnimationGuards = Config.Bind(
                "Advanced - Animation Guards",
                "EnableAnimationGuards",
                true,
                new ConfigDescription(
                    "Master switch for FPAA's melee attack, dodge, all-equipment sheathing, and bow-draw presentation guards. Turning it off smoothly restores the normal configured offset without disabling positioning, retraction, effects, or head bob. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Enable Animation Guards",
                        SectionOrder = 30,
                        Order = 0
                    }));
            _mitigateHeldMeleeBodyIntrusion = Config.Bind(
                "Advanced - Animation Guards",
                "MitigateHeldMeleeBodyIntrusion",
                true,
                new ConfigDescription(
                    "Enables presentation correction for every normal melee light and heavy attack phase while the shared target is on. When the shared target is off, the established held-heavy and forward-attack coverage and tuning remain in effect. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Enable Attack Guards",
                        SectionOrder = 30,
                        Order = 10
                    }));
            _enableDodgeGuard = Config.Bind(
                "Advanced - Animation Guards",
                "EnableDodgeGuard",
                true,
                new ConfigDescription(
                    "Smoothly increases shoulder retraction toward 0.25 metres during every directional dodge, holds maximum retraction across rapid chained dodges, then restores the configured Shoulder Retraction. The complete viewmodel offset is not moved toward vanilla. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Enable Dodge Guard",
                        SectionOrder = 30,
                        Order = 20
                    }));
            _enableSheathingGuard = Config.Bind(
                "Advanced - Animation Guards",
                "EnableSheathingGuard",
                true,
                new ConfigDescription(
                    "Enables FPAA's animation-timed return toward vanilla for normal and alternate sheathing from melee, dual-wield, bow, and magic equipment. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Enable Sheathing Guard",
                        SectionOrder = 30,
                        Order = 30
                    }));
            _enableBowDrawGuard = Config.Bind(
                "Advanced - Animation Guards",
                "EnableBowDrawGuard",
                true,
                new ConfigDescription(
                    "Limits FPAA's positive depth contribution during bow pull, hold, release, and cancelled draw without changing aim, animation, or projectile origin. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Enable Bow Draw Guard",
                        SectionOrder = 30,
                        Order = 40
                    }));
            _bowDrawMaximumOffsetPercent = Config.Bind(
                "Advanced - Animation Guards",
                "BowDrawMaximumOffsetPercent",
                33.0f,
                new ConfigDescription(
                    "Maximum FPAA depth contribution during bow draw as a percentage of Bow Depth Offset. At the defaults, 33 percent of the 0.30 metre bow offset produces about a 0.10 metre ceiling; 0 reaches vanilla. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 100.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Bow Draw Maximum Offset (%)",
                        SectionOrder = 30,
                        Order = 50
                    }));
            _useSharedGuardTarget = Config.Bind(
                "Advanced - Animation Guards",
                "UseSharedGuardTarget",
                true,
                new ConfigDescription(
                    "Makes enabled melee attack and sheathing guards share one Move Toward Vanilla target. Simultaneous guards use the strongest influence instead of multiplying together. Dodge uses dynamic shoulder retraction independently. Off restores the established independent melee and sheathing behavior and narrower attack coverage. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Use Shared Guard Target",
                        SectionOrder = 30,
                        Order = 60
                    }));
            _sharedMoveTowardVanillaPercent = Config.Bind(
                "Advanced - Animation Guards",
                "SharedMoveTowardVanillaPercent",
                50.0f,
                new ConfigDescription(
                    "Shared percentage of the configured presentation offset removed at each enabled guard's strongest point. 0 keeps the configured position; 100 reaches vanilla. In shared mode this replaces held-melee extra corrections so every guard reaches the same exact target. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 100.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Shared Move Toward Vanilla (%)",
                        SectionOrder = 30,
                        Order = 70
                    }));
            _forwardOffset = Config.Bind(
                "Position",
                "ForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Moves unarmed and unrecognized equipment along the camera depth axis. Positive moves it farther from the camera; negative moves it closer. When separate equipment depths are off, this is used for all equipment. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Position",
                        DisplayName = "General / Unarmed Depth Offset (m)",
                        SectionOrder = 10,
                        Order = 0
                    }));
            _horizontalOffset = Config.Bind(
                "Position",
                "HorizontalOffset",
                0.0f,
                new ConfigDescription(
                    "Moves the first-person body and equipped items horizontally. Positive moves right; negative moves left. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Position",
                        DisplayName = "Horizontal Offset (m)",
                        SectionOrder = 10,
                        Order = 10
                    }));
            _verticalOffset = Config.Bind(
                "Position",
                "VerticalOffset",
                0.0f,
                new ConfigDescription(
                    "Moves the first-person body and equipped items vertically. Positive moves up; negative moves down. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Position",
                        DisplayName = "Vertical Offset (m)",
                        SectionOrder = 10,
                        Order = 20
                    }));
            _shoulderRetraction = Config.Bind(
                "Position",
                "ShoulderRetraction",
                0.05f,
                new ConfigDescription(
                    "Retracts only the rendered torso and shoulder region toward and behind the camera to reduce body visibility during first-person poses. The correction tapers from Spine through the chest, shoulders, upper arms, and forearms to zero at the hands. Dodge guards smoothly raise this value toward 0.25 metres without moving the complete viewmodel offset. 0 disables normal retraction; changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 0.25f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Position",
                        DisplayName = "Shoulder Retraction (m)",
                        SectionOrder = 10,
                        Order = 30
                    }));
            _spineRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "SpineRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to the lower Spine bone. Raise this when lower chest geometry remains visible; large gaps from adjacent spine percentages can stretch blended geometry. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Spine Retraction (%)",
                        SectionOrder = 25,
                        Order = 0
                    }));
            _spine1RetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "Spine1RetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to the middle chest Spine1 bone. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Spine1 Retraction (%)",
                        SectionOrder = 25,
                        Order = 10
                    }));
            _spine2RetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "Spine2RetractionPercent",
                100.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to the upper chest Spine2 bone. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Spine2 Retraction (%)",
                        SectionOrder = 25,
                        Order = 20
                    }));
            _leftShoulderRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "LeftShoulderRetractionPercent",
                100.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to the left shoulder joint for asymmetric pose testing. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Left Shoulder Retraction (%)",
                        SectionOrder = 25,
                        Order = 30
                    }));
            _rightShoulderRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "RightShoulderRetractionPercent",
                100.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to the right shoulder joint for asymmetric pose testing. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Right Shoulder Retraction (%)",
                        SectionOrder = 25,
                        Order = 40
                    }));
            _upperArmRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "UpperArmRetractionPercent",
                30.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to both upper-arm bones. Use this to smooth the transition from shoulders toward fixed hands. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Upper-Arm Retraction (%)",
                        SectionOrder = 25,
                        Order = 50
                    }));
            _forearmRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "ForearmRetractionPercent",
                20.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied to both forearm bones. Hands remain fixed at zero regardless of this value. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 200.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Forearm Retraction (%)",
                        SectionOrder = 25,
                        Order = 60
                    }));
            _lowerTorsoRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "LowerTorsoRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Additional percentage of Shoulder Retraction applied to the native Hips bone for lower-torso geometry that does not follow the spine profile. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Lower Torso Retraction (%)",
                        SectionOrder = 25,
                        Order = 70
                    }));
            _chestHelperRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "ChestHelperRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Additional percentage of Shoulder Retraction applied equally to the native left and right Breast_Base and Breast helper bones. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Chest Helper Retraction (%)",
                        SectionOrder = 25,
                        Order = 80
                    }));
            _shoulderFixRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "ShoulderFixRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Additional percentage of Shoulder Retraction applied equally to the native left and right ShoulderFix helper bones. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Shoulder-Fix Retraction (%)",
                        SectionOrder = 25,
                        Order = 90
                    }));
            _nativeClothRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "NativeClothRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Additional percentage of Shoulder Retraction applied to the contiguous native Cloth_Skirt bone group used by the FppArms cloth renderers. Use only when native garment geometry enters view. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Native Cloth Retraction (%)",
                        SectionOrder = 25,
                        Order = 100
                    }));
            _torsoRendererRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "TorsoRendererRetractionPercent",
                50.0f,
                new ConfigDescription(
                    "Percentage of Shoulder Retraction applied uniformly to the complete native torso-garment renderer identified by its Torso mesh or material name, with the second Cloth renderer as a compatibility fallback. This renderer-specific correction uses a dedicated render rig so first-person arms, hands, weapons, and other cloth renderers keep their normal profile. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Torso Renderer Retraction (%)",
                        SectionOrder = 25,
                        Order = 110
                    }));
            _testRetractionBoneName = Config.Bind(
                "Advanced - Retraction Profile",
                "TestRetractionBoneName",
                string.Empty,
                new ConfigDescription(
                    "Exact bone name to test after Diagnostics reports the full native first-person bone inventory. Empty disables the test target. Use one bone at a time and avoid hand, finger, or weapon-socket bones. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Test Retraction Bone Name",
                        SectionOrder = 25,
                        Order = 120
                    }));
            _testBoneRetractionPercent = Config.Bind(
                "Advanced - Retraction Profile",
                "TestBoneRetractionPercent",
                0.0f,
                new ConfigDescription(
                    "Additional percentage of Shoulder Retraction applied only to the exact test bone. 400 percent equals 1.00 metre when the master distance is 0.25. This is additive if the selected bone already belongs to the normal profile. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 400.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Retraction Profile",
                        DisplayName = "Test Bone Retraction (%)",
                        SectionOrder = 25,
                        Order = 130
                    }));
            _meleeForwardOffset = Config.Bind(
                "Equipment Depth",
                "MeleeForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Depth offset for melee weapons when separate equipment depths are enabled. Positive moves them farther from the camera; negative moves them closer. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Equipment Depth",
                        DisplayName = "Melee Depth Offset (m)",
                        SectionOrder = 20,
                        Order = 10
                    }));
            _bowForwardOffset = Config.Bind(
                "Equipment Depth",
                "BowForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Depth offset for bows when separate equipment depths are enabled. Positive moves them farther from the camera; negative moves them closer. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Equipment Depth",
                        DisplayName = "Bow Depth Offset (m)",
                        SectionOrder = 20,
                        Order = 20
                    }));
            _magicForwardOffset = Config.Bind(
                "Equipment Depth",
                "MagicForwardOffset",
                0.30f,
                new ConfigDescription(
                    "Depth offset for magic when separate equipment depths are enabled. Positive moves it farther from the camera; negative moves it closer. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Equipment Depth",
                        DisplayName = "Magic Depth Offset (m)",
                        SectionOrder = 20,
                        Order = 30
                    }));
            _heldMeleeOffsetScale = Config.Bind(
                "Advanced - Animation Guards",
                "HeldMeleeOffsetScale",
                1.0f,
                new ConfigDescription(
                    "Amount of the normal viewmodel offset retained while a melee heavy attack is raised or held. 0 uses the vanilla position; 1 retains the full normal offset. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 1.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Normal Offset Retained (0-1)",
                        SectionOrder = 30,
                        Order = 80
                    }));
            _heldMeleeExtraForwardOffset = Config.Bind(
                "Advanced - Animation Guards",
                "HeldMeleeExtraForwardOffset",
                -0.05f,
                new ConfigDescription(
                    "Additional depth correction while a melee heavy attack is raised or held, after Normal Offset Retained. Positive moves the viewmodel farther from the camera; negative moves it closer to hide body intrusion. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Extra Depth Correction (m)",
                        SectionOrder = 30,
                        Order = 90
                    }));
            _heldMeleeExtraVerticalOffset = Config.Bind(
                "Advanced - Animation Guards",
                "HeldMeleeExtraVerticalOffset",
                -0.05f,
                new ConfigDescription(
                    "Additional vertical correction while a melee heavy attack is raised or held, after Normal Offset Retained. Positive moves the viewmodel up; negative moves it down. Changes apply immediately.",
                    new AcceptableValueRange<float>(-0.50f, 0.50f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Advanced - Animation Guards",
                        DisplayName = "Extra Vertical Correction (m)",
                        SectionOrder = 30,
                        Order = 100
                    }));
            _enableHeadBob = Config.Bind(
                "Head Bob",
                "EnableHeadBob",
                true,
                new ConfigDescription(
                    "Enables FPAA's camera-only first-person head bob. The game's Accessibility / Head Bob setting remains the global master switch. Native arm-moving bob stays suppressed, optional viewmodel follow remains render-only, and third person is untouched. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Enable Head Bob",
                        SectionOrder = 25,
                        Order = 0
                    }));
            _headBobPreset = Config.Bind(
                "Head Bob",
                "HeadBobPreset",
                HeadBobPreset.Balanced,
                new ConfigDescription(
                    "Selects Subtle, Balanced, or Strong head bob. Strong has the greatest vertical and side-to-side movement. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Head Bob Strength",
                        SectionOrder = 25,
                        Order = 10
                    }));
            _headBobSmoothness = Config.Bind(
                "Head Bob",
                "HeadBobSmoothness",
                0.7f,
                new ConfigDescription(
                    "Smooths changes in head-bob cadence and strength without filtering or weakening the steady jogging waveform. 0 responds immediately; higher values ease more gradually between movement speeds and sprint states. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 1.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Head Bob Smoothness",
                        SectionOrder = 25,
                        Order = 20
                    }));
            _sprintEmphasis = Config.Bind(
                "Head Bob",
                "SprintEmphasis",
                0.75f,
                new ConfigDescription(
                    "Controls how much stronger and faster head bob becomes while sprinting. At the default 0.75, sprinting adds about 56% movement and 19% cadence; 0 removes the sprint bonus. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 1.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Sprint Emphasis",
                        SectionOrder = 25,
                        Order = 30
                    }));
            _headBobSpeedPercent = Config.Bind(
                "Head Bob",
                "HeadBobSpeedPercent",
                75.0f,
                new ConfigDescription(
                    "Scales head-bob cadence after FPAA's movement-driven soft limiter without changing movement strength or player speed. 50 is half speed, 100 is the normal gait, and 150 is one-and-a-half speed. Changes apply immediately.",
                    new AcceptableValueRange<float>(50.0f, 150.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Head Bob Speed (%)",
                        SectionOrder = 25,
                        Order = 40
                    }));
            _stabilizeViewmodelDuringHeadBob = Config.Bind(
                "Head Bob",
                "StabilizeViewmodelDuringHeadBob",
                true,
                new ConfigDescription(
                    "When enabled, first-person arms, weapons, and presentation effects follow the exact camera-space head-bob translation, overriding Viewmodel Head-Bob Follow so FPAA's bob causes no relative viewmodel motion. This cannot remove movement built into native weapon animations. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Stabilize Viewmodel During Head Bob",
                        SectionOrder = 25,
                        Order = 50
                    }));
            _viewmodelHeadBobFollowPercent = Config.Bind(
                "Head Bob",
                "ViewmodelHeadBobFollowPercent",
                100.0f,
                new ConfigDescription(
                    "Percentage of FPAA camera head-bob translation followed exactly in camera space by the first-person arms, weapons, and presentation effects. 0 leaves the full relative arm motion; 75 leaves one quarter of it; 100 removes FPAA's relative viewmodel bob. Stabilize Viewmodel During Head Bob overrides this value. This is render-only and never changes aim, attacks, colliders, or projectiles. Changes apply immediately.",
                    new AcceptableValueRange<float>(0.0f, 100.0f),
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Viewmodel Head-Bob Follow (%)",
                        SectionOrder = 25,
                        Order = 60
                    }));
            _suppressMotionBlurDuringHeadBob = Config.Bind(
                "Head Bob",
                "SuppressMotionBlurDuringHeadBob",
                false,
                new ConfigDescription(
                    "Temporarily excludes the main first-person camera's movement from HDRP motion blur only while FPAA head bob is visible, then restores the exact prior value. Moving objects retain their normal blur. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Suppress Motion Blur During Head Bob",
                        SectionOrder = 25,
                        Order = 70
                    }));
            _temporalSafeHeadBobTiming = Config.Bind(
                "Head Bob",
                "TemporalSafeHeadBobTiming",
                true,
                new ConfigDescription(
                    "Experimental A/B test. When enabled, applies the same camera-only head-bob offset immediately before HDRP records its temporal camera matrices instead of during later render callbacks. This may reduce TAA, DLSS, or FSR smearing without changing bob motion or adding a render pass. Changes apply immediately.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Head Bob",
                        DisplayName = "Temporal-Safe Head Bob Timing (Test)",
                        SectionOrder = 25,
                        Order = 80
                    }));
            _diagnostics = Config.Bind(
                "Diagnostics",
                "Diagnostics",
                false,
                new ConfigDescription(
                    "Writes first-person hierarchy, camera motion, active bone, culling, and equipped-item offset details to the BepInEx log. Enable only while troubleshooting.",
                    null,
                    new Grailwright.Shared.ConfigRecoveryUiMetadata
                    {
                        DisplaySection = "Diagnostics",
                        DisplayName = "Diagnostics",
                        SectionOrder = 50,
                        Order = 0
                    }));

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
                        "General",
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
                "General",
                "Enabled",
                out _pendingEnabled);
            _hasPendingEnableAnimationGuards =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "EnableAnimationGuards",
                    out _pendingEnableAnimationGuards);
            _hasPendingMitigateHeldMeleeBodyIntrusion =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "MitigateHeldMeleeBodyIntrusion",
                    out _pendingMitigateHeldMeleeBodyIntrusion);
            _hasPendingEnableDodgeGuard =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "EnableDodgeGuard",
                    out _pendingEnableDodgeGuard);
            _hasPendingEnableSheathingGuard =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "EnableSheathingGuard",
                    out _pendingEnableSheathingGuard);
            _hasPendingEnableBowDrawGuard =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "EnableBowDrawGuard",
                    out _pendingEnableBowDrawGuard);
            _hasPendingBowDrawMaximumOffsetPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "BowDrawMaximumOffsetPercent",
                    out _pendingBowDrawMaximumOffsetPercent);
            _hasPendingUseSharedGuardTarget =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "UseSharedGuardTarget",
                    out _pendingUseSharedGuardTarget);
            _hasPendingSharedMoveTowardVanillaPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "SharedMoveTowardVanillaPercent",
                    out _pendingSharedMoveTowardVanillaPercent);
            _hasPendingForwardOffset = profile.TryGetCustomizedValue(
                "Position",
                "ForwardOffset",
                out _pendingForwardOffset);
            _hasPendingHorizontalOffset = profile.TryGetCustomizedValue(
                "Position",
                "HorizontalOffset",
                out _pendingHorizontalOffset);
            _hasPendingVerticalOffset = profile.TryGetCustomizedValue(
                "Position",
                "VerticalOffset",
                out _pendingVerticalOffset);
            _hasPendingShoulderRetraction =
                profile.TryGetCustomizedValue(
                    "Position",
                    "ShoulderRetraction",
                    out _pendingShoulderRetraction);
            _hasPendingSpineRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "SpineRetractionPercent",
                    out _pendingSpineRetractionPercent);
            _hasPendingSpine1RetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "Spine1RetractionPercent",
                    out _pendingSpine1RetractionPercent);
            _hasPendingSpine2RetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "Spine2RetractionPercent",
                    out _pendingSpine2RetractionPercent);
            _hasPendingLeftShoulderRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "LeftShoulderRetractionPercent",
                    out _pendingLeftShoulderRetractionPercent);
            _hasPendingRightShoulderRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "RightShoulderRetractionPercent",
                    out _pendingRightShoulderRetractionPercent);
            _hasPendingUpperArmRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "UpperArmRetractionPercent",
                    out _pendingUpperArmRetractionPercent);
            _hasPendingForearmRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "ForearmRetractionPercent",
                    out _pendingForearmRetractionPercent);
            _hasPendingLowerTorsoRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "LowerTorsoRetractionPercent",
                    out _pendingLowerTorsoRetractionPercent);
            _hasPendingChestHelperRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "ChestHelperRetractionPercent",
                    out _pendingChestHelperRetractionPercent);
            _hasPendingShoulderFixRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "ShoulderFixRetractionPercent",
                    out _pendingShoulderFixRetractionPercent);
            _hasPendingNativeClothRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "NativeClothRetractionPercent",
                    out _pendingNativeClothRetractionPercent);
            _hasPendingTorsoRendererRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "TorsoRendererRetractionPercent",
                    out _pendingTorsoRendererRetractionPercent);
            _hasPendingTestRetractionBoneName =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "TestRetractionBoneName",
                    out _pendingTestRetractionBoneName);
            _hasPendingTestBoneRetractionPercent =
                profile.TryGetCustomizedValue(
                    "Advanced - Retraction Profile",
                    "TestBoneRetractionPercent",
                    out _pendingTestBoneRetractionPercent);
            _hasPendingMeleeForwardOffset = profile.TryGetCustomizedValue(
                "Equipment Depth",
                "MeleeForwardOffset",
                out _pendingMeleeForwardOffset);
            _hasPendingBowForwardOffset = profile.TryGetCustomizedValue(
                "Equipment Depth",
                "BowForwardOffset",
                out _pendingBowForwardOffset);
            _hasPendingMagicForwardOffset = profile.TryGetCustomizedValue(
                "Equipment Depth",
                "MagicForwardOffset",
                out _pendingMagicForwardOffset);
            _hasPendingHeldMeleeOffsetScale =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "HeldMeleeOffsetScale",
                    out _pendingHeldMeleeOffsetScale);
            _hasPendingHeldMeleeExtraForwardOffset =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "HeldMeleeExtraForwardOffset",
                    out _pendingHeldMeleeExtraForwardOffset);
            _hasPendingHeldMeleeExtraVerticalOffset =
                profile.TryGetCustomizedValue(
                    "Advanced - Animation Guards",
                    "HeldMeleeExtraVerticalOffset",
                    out _pendingHeldMeleeExtraVerticalOffset);
            _hasPendingEnableHeadBob =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "EnableHeadBob",
                    out _pendingEnableHeadBob);
            _hasPendingHeadBobPreset =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "HeadBobPreset",
                    out _pendingHeadBobPreset);
            _hasPendingHeadBobSmoothness =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "HeadBobSmoothness",
                    out _pendingHeadBobSmoothness);
            _hasPendingSprintEmphasis =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "SprintEmphasis",
                    out _pendingSprintEmphasis);
            _hasPendingHeadBobSpeedPercent =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "HeadBobSpeedPercent",
                    out _pendingHeadBobSpeedPercent);
            _hasPendingStabilizeViewmodelDuringHeadBob =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "StabilizeViewmodelDuringHeadBob",
                    out _pendingStabilizeViewmodelDuringHeadBob);
            _hasPendingViewmodelHeadBobFollowPercent =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "ViewmodelHeadBobFollowPercent",
                    out _pendingViewmodelHeadBobFollowPercent);
            _hasPendingSuppressMotionBlurDuringHeadBob =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "SuppressMotionBlurDuringHeadBob",
                    out _pendingSuppressMotionBlurDuringHeadBob);
            _hasPendingTemporalSafeHeadBobTiming =
                profile.TryGetCustomizedValue(
                    "Head Bob",
                    "TemporalSafeHeadBobTiming",
                    out _pendingTemporalSafeHeadBobTiming);
            _hasPendingDiagnostics = profile.TryGetCustomizedValue(
                "Diagnostics",
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
            if (_hasPendingMitigateHeldMeleeBodyIntrusion
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _mitigateHeldMeleeBodyIntrusion,
                    _pendingMitigateHeldMeleeBodyIntrusion,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingEnableAnimationGuards
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enableAnimationGuards,
                    _pendingEnableAnimationGuards,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingEnableDodgeGuard
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enableDodgeGuard,
                    _pendingEnableDodgeGuard,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingEnableSheathingGuard
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enableSheathingGuard,
                    _pendingEnableSheathingGuard,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingEnableBowDrawGuard
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enableBowDrawGuard,
                    _pendingEnableBowDrawGuard,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            RestorePreservedFloat(
                _hasPendingBowDrawMaximumOffsetPercent,
                _bowDrawMaximumOffsetPercent,
                _pendingBowDrawMaximumOffsetPercent,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingUseSharedGuardTarget
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _useSharedGuardTarget,
                    _pendingUseSharedGuardTarget,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            RestorePreservedFloat(
                _hasPendingSharedMoveTowardVanillaPercent,
                _sharedMoveTowardVanillaPercent,
                _pendingSharedMoveTowardVanillaPercent,
                ref restoredCount,
                ref clampedCount);
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
                _hasPendingShoulderRetraction,
                _shoulderRetraction,
                _pendingShoulderRetraction,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingSpineRetractionPercent,
                _spineRetractionPercent,
                _pendingSpineRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingSpine1RetractionPercent,
                _spine1RetractionPercent,
                _pendingSpine1RetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingSpine2RetractionPercent,
                _spine2RetractionPercent,
                _pendingSpine2RetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingLeftShoulderRetractionPercent,
                _leftShoulderRetractionPercent,
                _pendingLeftShoulderRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingRightShoulderRetractionPercent,
                _rightShoulderRetractionPercent,
                _pendingRightShoulderRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingUpperArmRetractionPercent,
                _upperArmRetractionPercent,
                _pendingUpperArmRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingForearmRetractionPercent,
                _forearmRetractionPercent,
                _pendingForearmRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingLowerTorsoRetractionPercent,
                _lowerTorsoRetractionPercent,
                _pendingLowerTorsoRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingChestHelperRetractionPercent,
                _chestHelperRetractionPercent,
                _pendingChestHelperRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingShoulderFixRetractionPercent,
                _shoulderFixRetractionPercent,
                _pendingShoulderFixRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingNativeClothRetractionPercent,
                _nativeClothRetractionPercent,
                _pendingNativeClothRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingTorsoRendererRetractionPercent,
                _torsoRendererRetractionPercent,
                _pendingTorsoRendererRetractionPercent,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingTestRetractionBoneName
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _testRetractionBoneName,
                    _pendingTestRetractionBoneName,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            RestorePreservedFloat(
                _hasPendingTestBoneRetractionPercent,
                _testBoneRetractionPercent,
                _pendingTestBoneRetractionPercent,
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
            RestorePreservedFloat(
                _hasPendingHeldMeleeOffsetScale,
                _heldMeleeOffsetScale,
                _pendingHeldMeleeOffsetScale,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingHeldMeleeExtraForwardOffset,
                _heldMeleeExtraForwardOffset,
                _pendingHeldMeleeExtraForwardOffset,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingHeldMeleeExtraVerticalOffset,
                _heldMeleeExtraVerticalOffset,
                _pendingHeldMeleeExtraVerticalOffset,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingEnableHeadBob
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _enableHeadBob,
                    _pendingEnableHeadBob,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingHeadBobPreset
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _headBobPreset,
                    _pendingHeadBobPreset,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            RestorePreservedFloat(
                _hasPendingHeadBobSmoothness,
                _headBobSmoothness,
                _pendingHeadBobSmoothness,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingSprintEmphasis,
                _sprintEmphasis,
                _pendingSprintEmphasis,
                ref restoredCount,
                ref clampedCount);
            RestorePreservedFloat(
                _hasPendingHeadBobSpeedPercent,
                _headBobSpeedPercent,
                _pendingHeadBobSpeedPercent,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingStabilizeViewmodelDuringHeadBob
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _stabilizeViewmodelDuringHeadBob,
                    _pendingStabilizeViewmodelDuringHeadBob,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            RestorePreservedFloat(
                _hasPendingViewmodelHeadBobFollowPercent,
                _viewmodelHeadBobFollowPercent,
                _pendingViewmodelHeadBobFollowPercent,
                ref restoredCount,
                ref clampedCount);
            if (_hasPendingSuppressMotionBlurDuringHeadBob
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _suppressMotionBlurDuringHeadBob,
                    _pendingSuppressMotionBlurDuringHeadBob,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
            if (_hasPendingTemporalSafeHeadBobTiming
                && Grailwright.Shared.ConfigPreviousSettingsRecovery.TryRestore(
                    _temporalSafeHeadBobTiming,
                    _pendingTemporalSafeHeadBobTiming,
                    out clamped))
            {
                restoredCount++;
                if (clamped)
                {
                    clampedCount++;
                }
            }
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
            _hasPendingEnableAnimationGuards = false;
            _hasPendingMitigateHeldMeleeBodyIntrusion = false;
            _hasPendingEnableDodgeGuard = false;
            _hasPendingEnableSheathingGuard = false;
            _hasPendingEnableBowDrawGuard = false;
            _hasPendingBowDrawMaximumOffsetPercent = false;
            _hasPendingUseSharedGuardTarget = false;
            _hasPendingSharedMoveTowardVanillaPercent = false;
            _hasPendingForwardOffset = false;
            _hasPendingHorizontalOffset = false;
            _hasPendingVerticalOffset = false;
            _hasPendingShoulderRetraction = false;
            _hasPendingSpineRetractionPercent = false;
            _hasPendingSpine1RetractionPercent = false;
            _hasPendingSpine2RetractionPercent = false;
            _hasPendingLeftShoulderRetractionPercent = false;
            _hasPendingRightShoulderRetractionPercent = false;
            _hasPendingUpperArmRetractionPercent = false;
            _hasPendingForearmRetractionPercent = false;
            _hasPendingLowerTorsoRetractionPercent = false;
            _hasPendingChestHelperRetractionPercent = false;
            _hasPendingShoulderFixRetractionPercent = false;
            _hasPendingNativeClothRetractionPercent = false;
            _hasPendingTorsoRendererRetractionPercent = false;
            _hasPendingTestRetractionBoneName = false;
            _pendingTestRetractionBoneName = null;
            _hasPendingTestBoneRetractionPercent = false;
            _hasPendingMeleeForwardOffset = false;
            _hasPendingBowForwardOffset = false;
            _hasPendingMagicForwardOffset = false;
            _hasPendingHeldMeleeOffsetScale = false;
            _hasPendingHeldMeleeExtraForwardOffset = false;
            _hasPendingHeldMeleeExtraVerticalOffset = false;
            _hasPendingEnableHeadBob = false;
            _hasPendingHeadBobPreset = false;
            _hasPendingHeadBobSmoothness = false;
            _hasPendingSprintEmphasis = false;
            _hasPendingHeadBobSpeedPercent = false;
            _hasPendingStabilizeViewmodelDuringHeadBob = false;
            _hasPendingViewmodelHeadBobFollowPercent = false;
            _hasPendingSuppressMotionBlurDuringHeadBob = false;
            _hasPendingTemporalSafeHeadBobTiming = false;
            _hasPendingDiagnostics = false;
        }

        private sealed class ShoulderBoneProfile
        {
            internal readonly int BoneCount;
            internal readonly string TestBoneName;
            internal int SpineIndex = -1;
            internal int Spine1Index = -1;
            internal int Spine2Index = -1;
            internal int LeftShoulderIndex = -1;
            internal int RightShoulderIndex = -1;
            internal int LeftUpperArmIndex = -1;
            internal int RightUpperArmIndex = -1;
            internal int LeftForearmIndex = -1;
            internal int RightForearmIndex = -1;
            internal int HipsIndex = -1;
            internal int LeftBreastBaseIndex = -1;
            internal int RightBreastBaseIndex = -1;
            internal int LeftBreastIndex = -1;
            internal int RightBreastIndex = -1;
            internal int LeftShoulderFixIndex = -1;
            internal int RightShoulderFixIndex = -1;
            internal int NativeClothStartIndex = -1;
            internal int NativeClothEndIndex = -1;
            internal int NativeClothBoneCount;
            internal bool NativeClothIndicesContiguous = true;
            internal int TestBoneIndex = -1;
            internal int AffectedBoneCount;

            internal int ChestHelperBoneCount
            {
                get
                {
                    int count = 0;
                    count += LeftBreastBaseIndex >= 0 ? 1 : 0;
                    count += RightBreastBaseIndex >= 0 ? 1 : 0;
                    count += LeftBreastIndex >= 0 ? 1 : 0;
                    count += RightBreastIndex >= 0 ? 1 : 0;
                    return count;
                }
            }

            internal int ShoulderFixBoneCount
            {
                get
                {
                    return (LeftShoulderFixIndex >= 0 ? 1 : 0)
                        + (RightShoulderFixIndex >= 0 ? 1 : 0);
                }
            }

            internal ShoulderBoneProfile(
                int boneCount,
                string testBoneName)
            {
                BoneCount = boneCount;
                TestBoneName = testBoneName ?? string.Empty;
            }

            internal void SetIndex(ref int target, int index)
            {
                if (target >= 0)
                {
                    return;
                }

                target = index;
                AffectedBoneCount++;
            }

            internal void SetTestBoneIndex(int index)
            {
                if (TestBoneIndex >= 0)
                {
                    return;
                }

                TestBoneIndex = index;
                if (index != SpineIndex
                    && index != Spine1Index
                    && index != Spine2Index
                    && index != LeftShoulderIndex
                    && index != RightShoulderIndex
                    && index != LeftUpperArmIndex
                    && index != RightUpperArmIndex
                    && index != LeftForearmIndex
                    && index != RightForearmIndex)
                {
                    AffectedBoneCount++;
                }
            }

            internal void SetAuxiliaryIndex(ref int target, int index)
            {
                if (target < 0)
                {
                    target = index;
                }
            }

            internal void RecordNativeClothIndex(int index)
            {
                if (NativeClothBoneCount == 0)
                {
                    NativeClothStartIndex = index;
                    NativeClothEndIndex = index;
                }
                else
                {
                    if (index != NativeClothEndIndex + 1)
                    {
                        NativeClothIndicesContiguous = false;
                    }
                    NativeClothEndIndex = index;
                }
                NativeClothBoneCount++;
            }
        }

        private sealed class PresentationEffectOffsetState
        {
            private readonly Transform _transform;
            private readonly bool _compensateBodyRootRender;
            private Vector3 _appliedLocalOffset;
            private bool _suspendedForBodyRootRender;

            internal PresentationEffectOffsetState(
                Transform transform,
                bool compensateBodyRootRender)
            {
                _transform = transform;
                _compensateBodyRootRender = compensateBodyRootRender;
            }

            internal void Apply(Vector3 worldOffset)
            {
                if (_transform == null)
                {
                    return;
                }

                Vector3 nativeLocalPosition =
                    _transform.localPosition - _appliedLocalOffset;
                Transform parent = _transform.parent;
                _appliedLocalOffset = parent == null
                    ? worldOffset
                    : parent.InverseTransformVector(worldOffset);
                _transform.localPosition =
                    nativeLocalPosition + _appliedLocalOffset;
            }

            internal void SuspendForBodyRootRender()
            {
                if (!_compensateBodyRootRender
                    || _suspendedForBodyRootRender
                    || _transform == null
                    || _appliedLocalOffset.sqrMagnitude <= 0.00000001f)
                {
                    return;
                }

                _transform.localPosition -= _appliedLocalOffset;
                _suspendedForBodyRootRender = true;
            }

            internal void ResumeAfterBodyRootRender()
            {
                if (!_suspendedForBodyRootRender)
                {
                    return;
                }

                if (_transform != null)
                {
                    _transform.localPosition += _appliedLocalOffset;
                }
                _suspendedForBodyRootRender = false;
            }

            internal void Restore()
            {
                if (_transform != null
                    && !_suspendedForBodyRootRender
                    && _appliedLocalOffset.sqrMagnitude > 0.00000001f)
                {
                    _transform.localPosition -= _appliedLocalOffset;
                }
                _suspendedForBodyRootRender = false;
                _appliedLocalOffset = Vector3.zero;
            }
        }
    }

    internal static class KandraRendererManagerPreLateUpdateEndPatch
    {
        internal static void Postfix(KandraRendererManager __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.ApplyKandraRenderOffset(__instance);
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

    internal static class VHeroControllerProcessUpdatePatch
    {
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter(
            FirstPersonArmsAdjusterPlugin.TrueThirdPersonPluginGuid)]
        internal static void Postfix(VHeroController __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.CaptureVisualWorldOffsetAfterCameraRotation(
                    __instance);
            }
        }
    }

    internal static class HeadBobbingIntensityPatch
    {
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter(
            FirstPersonArmsAdjusterPlugin.TrueThirdPersonPluginGuid)]
        internal static void Postfix(
            ref float __result)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.SuppressNativeFirstPersonHeadBob(
                    ref __result);
            }
        }
    }

    internal static class HDCameraUpdateHeadBobPatch
    {
        internal static void Prefix(HDCamera __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.TryApplyHeadBobBeforeHdrpCameraUpdate(
                    __instance);
            }
        }

        internal static void Postfix(HDCamera __instance)
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.ReportHeadBobAfterHdrpCameraUpdate(
                    __instance);
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

    internal static class DodgeActivityPatch
    {
        internal static void Postfix()
        {
            FirstPersonArmsAdjusterPlugin instance =
                FirstPersonArmsAdjusterPlugin.Instance;
            if (instance != null)
            {
                instance.NotifyDodgeActivity();
            }
        }
    }
}
