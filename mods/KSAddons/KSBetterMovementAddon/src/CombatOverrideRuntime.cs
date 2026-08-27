using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Fights;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.MovementSystems;
using Awaken.TG.Main.Heroes.Stats;
using BepInEx.Configuration;
using BepInEx.Logging;
using BetterSprint;
using HarmonyLib;
using UnityEngine;

namespace Keenan.TGFoA.BetterMovementAddon
{
    public enum CombatOverrideMode
    {
        Off,
        HalfSpeedBonuses,
        SpeedBonuses,
        MobilityAdvantages,
        FullVanilla
    }

    internal sealed class CombatOverrideRuntime : IDisposable
    {
        private const float SpeedBlendSeconds = 1f;
        private const float NativeSprintFovMultiplier = 1.055f;

        private static readonly FieldInfo IsDrawingBowField =
            AccessTools.Field(typeof(HumanoidMovementBase), "_isDrawingBow");
        private static readonly FieldInfo SprintActiveField =
            AccessTools.Field(typeof(HeroFoV), "_sprintActive");
        private static readonly FieldInfo ConfigBoolValueField =
            AccessTools.Field(typeof(ConfigEntry<bool>), "_typedValue");
        private static readonly HashSet<string> MobilityPatchTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "BetterSprint.Patches.BowPatches+DashBowCondition_Patch",
                "BetterSprint.Patches.DashPatches+DashAllowedByStamina_Patch",
                "BetterSprint.Patches.DashPatches+CanDash_Patch",
                "BetterSprint.Patches.EncumberedPatches+ApplyStatusState_Patch",
                "BetterSprint.Patches.SprintPatches+SprintingRequirementsMet_Patch",
                "BetterSprint.Patches.SprintPatches+CanSprint_Patch",
                "BetterSprint.Patches.SprintPatches+GetInputVector_Patch",
                "BetterSprint.Patches.SprintPatches+GetBaseTargetSpeed_Patch"
            };

        private readonly BetterMovementAddonPlugin _plugin;
        private readonly ConfigEntry<CombatOverrideMode> _mode;
        private readonly ManualLogSource _logger;
        private readonly ConfigEntry<bool> _parentEnabled;
        private readonly ConfigEntry<float> _parentSprintFovMultiplier;
        private readonly HashSet<MethodBase> _fullTargets = new HashSet<MethodBase>();
        private readonly HashSet<MethodBase> _mobilityTargets = new HashSet<MethodBase>();

        private HumanoidMovementBase _lastMovement;
        private float _speedBlend;
        private float _speedBlendStart;
        private float _speedBlendTarget;
        private float _speedBlendElapsed;
        private bool _mobilityActive;
        private bool _fullVanillaActive;
        private int _suppressionDepth;
        private bool _hasSuppressedState;
        private bool _suppressedParentEnabled;
        private bool _disposed;

        internal static CombatOverrideRuntime Instance { get; private set; }

        internal CombatOverrideRuntime(
            BetterMovementAddonPlugin plugin,
            ConfigEntry<CombatOverrideMode> mode,
            ManualLogSource logger)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            FieldInfo parentConfigField = AccessTools.Field(
                typeof(BetterSprint.Plugin),
                "PluginConfig");
            PluginConfig parentConfig = parentConfigField == null
                ? null
                : parentConfigField.GetValue(null) as PluginConfig;
            if (parentConfig == null || BetterSprint.Plugin.Instance == null)
            {
                throw new MissingMemberException(
                    "Better Movement's runtime configuration is unavailable.");
            }

            _parentEnabled = parentConfig.Enabled;
            _parentSprintFovMultiplier = parentConfig.SprintFovMultiplier;
            if (_parentEnabled == null
                || _parentSprintFovMultiplier == null
                || IsDrawingBowField == null
                || SprintActiveField == null
                || ConfigBoolValueField == null)
            {
                throw new MissingMemberException(
                    "Better Movement or the current game build does not expose the expected combat-override state.");
            }

            Instance = this;
        }

        internal void Patch(Harmony harmony)
        {
            if (harmony == null)
            {
                throw new ArgumentNullException(nameof(harmony));
            }

            InventoryParentTargets();
            if (_fullTargets.Count == 0 || _mobilityTargets.Count == 0)
            {
                throw new MissingMethodException(
                    "Could not inventory Better Movement's Harmony targets.");
            }

            HarmonyMethod suppressionPrefix = new HarmonyMethod(
                typeof(CombatOverrideRuntime),
                nameof(SuppressionPrefix));
            suppressionPrefix.priority = Priority.First;
            HarmonyMethod suppressionFinalizer = new HarmonyMethod(
                typeof(CombatOverrideRuntime),
                nameof(SuppressionFinalizer));
            suppressionFinalizer.priority = Priority.Last;

            foreach (MethodBase target in _fullTargets.Union(_mobilityTargets))
            {
                harmony.Patch(
                    target,
                    prefix: suppressionPrefix,
                    finalizer: suppressionFinalizer);
            }

            PatchFloatGetter(
                harmony,
                "EffectiveWalkSpeedMultiplier",
                nameof(SpeedMultiplierPostfix));
            PatchFloatGetter(
                harmony,
                "EffectiveJogSpeedMultiplier",
                nameof(SpeedMultiplierPostfix));
            PatchFloatGetter(
                harmony,
                "EffectiveSprintSpeedMultiplier",
                nameof(SpeedMultiplierPostfix));
            PatchFloatGetter(
                harmony,
                "EffectiveSwimSpeedMultiplier",
                nameof(SpeedMultiplierPostfix));

            MethodInfo dashLimitGetter = AccessTools.PropertyGetter(
                typeof(PluginConfig),
                "EffectiveDashMaxOptimalCounters");
            if (dashLimitGetter == null)
            {
                throw new MissingMethodException(
                    "Could not find Better Movement's effective dash-limit getter.");
            }

            harmony.Patch(
                dashLimitGetter,
                postfix: new HarmonyMethod(
                    typeof(CombatOverrideRuntime),
                    nameof(DashLimitPostfix)));

            Type sprintPatchType = AccessTools.TypeByName(
                "BetterSprint.Patches.SprintPatches+ResolveSprintingState_Patch");
            MethodInfo staminaWrapper = sprintPatchType == null
                ? null
                : AccessTools.Method(
                    sprintPatchType,
                    "TryDecreaseContinuouslyWrapper");
            MethodInfo combatWrapper = sprintPatchType == null
                ? null
                : AccessTools.Method(
                    sprintPatchType,
                    "ShouldDrainSprintStaminaWrapper");
            if (staminaWrapper == null || combatWrapper == null)
            {
                throw new MissingMethodException(
                    "Could not find Better Movement's sprint-stamina wrappers.");
            }

            harmony.Patch(
                staminaWrapper,
                prefix: new HarmonyMethod(
                    typeof(CombatOverrideRuntime),
                    nameof(SprintStaminaPrefix)));
            harmony.Patch(
                combatWrapper,
                prefix: new HarmonyMethod(
                    typeof(CombatOverrideRuntime),
                    nameof(SprintCombatCheckPrefix)));

            MethodBase fovTarget = _fullTargets.FirstOrDefault(
                method => method.DeclaringType == typeof(HeroFoV)
                    && method.Name == "GetMovementFoVMultiplier");
            if (fovTarget == null)
            {
                throw new MissingMethodException(
                    "Could not find the sprint FOV target patched by Better Movement.");
            }

            HarmonyMethod fovPostfix = new HarmonyMethod(
                typeof(CombatOverrideRuntime),
                nameof(SprintFovPostfix));
            fovPostfix.priority = Priority.Last;
            harmony.Patch(fovTarget, postfix: fovPostfix);
        }

        internal void Update()
        {
            if (_disposed)
            {
                return;
            }

            CombatOverrideMode configuredMode = _mode.Value;
            bool inCombat = Hero.Current != null && Hero.Current.IsInCombat();
            bool wantsMobilityOverride = inCombat
                && configuredMode >= CombatOverrideMode.MobilityAdvantages;
            bool wantsFullVanilla = inCombat
                && configuredMode == CombatOverrideMode.FullVanilla;

            if (_mobilityActive != wantsMobilityOverride)
            {
                _mobilityActive = wantsMobilityOverride;
                RefreshEncumbrance();
            }

            bool holdVanillaSpeed = _fullVanillaActive && !wantsFullVanilla;
            float speedTarget = 0f;
            if (holdVanillaSpeed
                || (inCombat
                    && configuredMode >= CombatOverrideMode.SpeedBonuses))
            {
                speedTarget = 1f;
            }
            else if (inCombat
                && configuredMode == CombatOverrideMode.HalfSpeedBonuses)
            {
                speedTarget = 0.5f;
            }

            UpdateSpeedBlend(speedTarget);

            if (wantsFullVanilla
                && !_fullVanillaActive
                && _speedBlend >= 0.999f
                && IsActionBoundarySafe())
            {
                _fullVanillaActive = true;
                RefreshEncumbrance();
                _plugin.LogDiagnostic(
                    "Full Vanilla combat override activated at a safe movement boundary.");
            }
            else if (!wantsFullVanilla
                && _fullVanillaActive
                && IsActionBoundarySafe())
            {
                _fullVanillaActive = false;
                RefreshEncumbrance();
                _plugin.LogDiagnostic(
                    "Full Vanilla combat override released at a safe movement boundary.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_hasSuppressedState)
            {
                RestoreParentEnabled();
            }
            _fullVanillaActive = false;
            _mobilityActive = false;
            _speedBlend = 0f;
            _speedBlendStart = 0f;
            _speedBlendTarget = 0f;
            _speedBlendElapsed = 0f;
            RefreshEncumbrance();
            _lastMovement = null;
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private void InventoryParentTargets()
        {
            Assembly parentAssembly = typeof(BetterSprint.Plugin).Assembly;
            foreach (MethodBase target in Harmony.GetAllPatchedMethods())
            {
                Patches patches = Harmony.GetPatchInfo(target);
                if (patches == null)
                {
                    continue;
                }

                IEnumerable<Patch> parentPatches = patches.Prefixes
                    .Concat(patches.Postfixes)
                    .Concat(patches.Transpilers)
                    .Where(patch => patch.PatchMethod != null
                        && patch.PatchMethod.DeclaringType != null
                        && patch.PatchMethod.DeclaringType.Assembly == parentAssembly);
                bool hasParentPatch = false;
                bool hasMobilityPatch = false;
                bool cleanupOnly = target.DeclaringType == typeof(Hero)
                    && target.Name == "OnDiscard";
                foreach (Patch patch in parentPatches)
                {
                    hasParentPatch = true;
                    if (MobilityPatchTypes.Contains(
                            patch.PatchMethod.DeclaringType.FullName))
                    {
                        hasMobilityPatch = true;
                    }
                }

                if (!hasParentPatch || cleanupOnly)
                {
                    continue;
                }

                _fullTargets.Add(target);
                if (hasMobilityPatch
                    && !(target.DeclaringType == typeof(HumanoidMovementBase)
                        && target.Name == "Update"))
                {
                    _mobilityTargets.Add(target);
                }
            }
        }

        private void UpdateSpeedBlend(float target)
        {
            if (!Mathf.Approximately(target, _speedBlendTarget))
            {
                _speedBlendStart = _speedBlend;
                _speedBlendTarget = target;
                _speedBlendElapsed = 0f;
            }

            if (_speedBlendElapsed >= SpeedBlendSeconds)
            {
                _speedBlend = _speedBlendTarget;
                return;
            }

            _speedBlendElapsed = Mathf.Min(
                SpeedBlendSeconds,
                _speedBlendElapsed + Time.deltaTime);
            _speedBlend = Mathf.Lerp(
                _speedBlendStart,
                _speedBlendTarget,
                _speedBlendElapsed / SpeedBlendSeconds);
        }

        private static void PatchFloatGetter(
            Harmony harmony,
            string propertyName,
            string postfixName)
        {
            MethodInfo getter = AccessTools.PropertyGetter(
                typeof(PluginConfig),
                propertyName);
            if (getter == null)
            {
                throw new MissingMethodException(
                    "Could not find Better Movement's " + propertyName + " getter.");
            }

            harmony.Patch(
                getter,
                postfix: new HarmonyMethod(
                    typeof(CombatOverrideRuntime),
                    postfixName));
        }

        private bool ShouldSuppress(MethodBase target)
        {
            if (_fullVanillaActive && _fullTargets.Contains(target))
            {
                return true;
            }

            if (!_mobilityActive || !_mobilityTargets.Contains(target))
            {
                return false;
            }

            if (target.DeclaringType == typeof(HumanoidMovementBase)
                && target.Name == "GetBaseTargetSpeed"
                && _speedBlend < 0.999f)
            {
                return false;
            }

            return true;
        }

        private bool BeginParentSuppression()
        {
            _suppressionDepth++;
            if (_suppressionDepth > 1)
            {
                return true;
            }

            _suppressedParentEnabled = (bool)ConfigBoolValueField.GetValue(
                _parentEnabled);
            _hasSuppressedState = true;
            if (_suppressedParentEnabled)
            {
                ConfigBoolValueField.SetValue(_parentEnabled, false);
            }

            return true;
        }

        private void EndParentSuppression()
        {
            if (_suppressionDepth <= 0)
            {
                return;
            }

            _suppressionDepth--;
            if (_suppressionDepth == 0)
            {
                RestoreParentEnabled();
            }
        }

        private void RestoreParentEnabled()
        {
            if (!_hasSuppressedState)
            {
                return;
            }

            if (_suppressionDepth > 0)
            {
                _suppressionDepth = 0;
            }

            bool currentEnabled = (bool)ConfigBoolValueField.GetValue(
                _parentEnabled);
            if (currentEnabled != _suppressedParentEnabled)
            {
                ConfigBoolValueField.SetValue(
                    _parentEnabled,
                    _suppressedParentEnabled);
            }

            _hasSuppressedState = false;
        }

        private bool IsActionBoundarySafe()
        {
            HumanoidMovementBase movement = _lastMovement;
            if (movement == null)
            {
                return true;
            }

            try
            {
                if (movement.IsPerformingAction)
                {
                    return false;
                }

                object drawingBow = IsDrawingBowField.GetValue(movement);
                if (drawingBow is bool && (bool)drawingBow)
                {
                    return false;
                }

                VHeroController controller;
                return !BetterMovementAddonPlugin.TryGetController(
                        movement,
                        out controller)
                    || controller == null
                    || controller.Grounded;
            }
            catch
            {
                return true;
            }
        }

        private void RefreshEncumbrance()
        {
            try
            {
                BetterSprint.Patches.EncumberedPatches.ApplyUpdate();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Could not refresh encumbrance for the combat override: "
                    + exception.Message);
            }
        }

        private static void SuppressionPrefix(
            MethodBase __originalMethod,
            object __instance,
            out bool __state)
        {
            __state = false;
            CombatOverrideRuntime runtime = Instance;
            if (runtime == null || runtime._disposed)
            {
                return;
            }

            HumanoidMovementBase movement = __instance as HumanoidMovementBase;
            if (movement is HeroMovementSystem)
            {
                runtime._lastMovement = movement;
            }

            if (runtime.ShouldSuppress(__originalMethod))
            {
                __state = runtime.BeginParentSuppression();
            }
        }

        private static Exception SuppressionFinalizer(
            Exception __exception,
            bool __state)
        {
            CombatOverrideRuntime runtime = Instance;
            if (__state && runtime != null)
            {
                runtime.EndParentSuppression();
            }

            return __exception;
        }

        private static void SpeedMultiplierPostfix(ref float __result)
        {
            CombatOverrideRuntime runtime = Instance;
            if (runtime == null
                || runtime._disposed
                || runtime._speedBlend <= 0f
                || __result <= 1f)
            {
                return;
            }

            __result = Mathf.Lerp(__result, 1f, runtime._speedBlend);
        }

        private static void DashLimitPostfix(ref int __result)
        {
            CombatOverrideRuntime runtime = Instance;
            if (runtime != null
                && !runtime._disposed
                && (runtime._mobilityActive || runtime._fullVanillaActive)
                && __result > 0)
            {
                __result = 0;
            }
        }

        private static bool SprintStaminaPrefix(
            HeroStaminaUsedUpEffect effect,
            float amount,
            float deltaTime,
            ref bool __result)
        {
            CombatOverrideRuntime runtime = Instance;
            if (runtime == null || !runtime._fullVanillaActive)
            {
                return true;
            }

            __result = effect.TryDecreaseContinuously(amount, deltaTime);
            return false;
        }

        private static bool SprintCombatCheckPrefix(
            Hero hero,
            ref bool __result)
        {
            CombatOverrideRuntime runtime = Instance;
            if (runtime == null || !runtime._fullVanillaActive)
            {
                return true;
            }

            __result = hero != null && hero.IsInCombat();
            return false;
        }

        private static void SprintFovPostfix(
            HeroFoV __instance,
            ref float __result)
        {
            CombatOverrideRuntime runtime = Instance;
            if (runtime == null
                || !runtime._fullVanillaActive
                || __instance == null)
            {
                return;
            }

            object sprintActive = SprintActiveField.GetValue(__instance);
            float configured = runtime._parentSprintFovMultiplier.Value;
            if (sprintActive is bool
                && (bool)sprintActive
                && configured > 0f)
            {
                __result *= NativeSprintFovMultiplier / configured;
            }
        }
    }
}
