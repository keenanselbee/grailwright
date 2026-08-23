using System;
using System.Reflection;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Domains;
using Awaken.TG.Main.Animations.FSM.Heroes.Machines;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.Items;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SoulAndService
{
    internal static class SoulRendInnerLightRuntime
    {
        private const float MinimumVisibleIntensity = 0.001f;
        private const float HdrpIntensityMultiplier = 50000.0f;
        private const float CastBoostMultiplier = 3.0f;
        private const float CastBoostDelaySeconds = 0.3f;
        private const float CastBoostRampUpSeconds = 0.01f;
        private const float CastBoostRampDownSeconds = 0.25f;
        private const string FirstPersonArmsAdjusterPluginGuid =
            "ks.tgfoa.first-person-arms-adjuster";
        private const string FirstPersonArmsAdjusterApiTypeName =
            "FirstPersonArmsAdjuster.FirstPersonArmsAdjusterApi";
        private static readonly Color SoulRendColor = new Color(0.18f, 1.0f, 0.35f);

        private delegate bool TryGetVisualWorldOffset(out Vector3 offset);

        private sealed class HandLightState
        {
            internal readonly EquipmentSlotType Slot;
            internal readonly CastingHand CastingHand;
            internal readonly string ObjectName;
            internal GameObject LightObject;
            internal Light Light;
            internal Transform Anchor;
            internal Hero AnchorHero;
            internal float NextAnchorProbeAt;
            internal float CastStartedAt;
            internal float CastBoostFactor = 1.0f;
            internal bool WasCasting;

            internal HandLightState(
                EquipmentSlotType slot,
                CastingHand castingHand,
                string objectName)
            {
                Slot = slot;
                CastingHand = castingHand;
                ObjectName = objectName;
            }
        }

        private static readonly HandLightState MainHand = new HandLightState(
            EquipmentSlotType.MainHand,
            CastingHand.MainHand,
            "SoulAndServiceMainHandLight");
        private static readonly HandLightState OffHand = new HandLightState(
            EquipmentSlotType.OffHand,
            CastingHand.OffHand,
            "SoulAndServiceOffHandLight");
        private static TryGetVisualWorldOffset _tryGetVisualWorldOffset;
        private static bool _firstPersonArmsAdjusterBridgeResolved;
        private static bool _firstPersonArmsAdjusterFailureLogged;
        private static Type _hdAdditionalLightDataType;
        private static bool _hdAdditionalLightDataResolved;
        private static bool _hdAdditionalLightDataFailureLogged;

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            Hero hero = Hero.Current;
            if (plugin == null || hero == null)
            {
                UpdateHand(MainHand, null, null, false);
                UpdateHand(OffHand, null, null, false);
                return;
            }

            MagicFSM mainFsm = null;
            MagicFSM offFsm = null;
            foreach (MagicFSM fsm in hero.Elements<MagicFSM>())
            {
                if (fsm == null)
                {
                    continue;
                }
                if (fsm.CastingHand == CastingHand.MainHand)
                {
                    mainFsm = fsm;
                }
                else if (fsm.CastingHand == CastingHand.OffHand)
                {
                    offFsm = fsm;
                }
            }

            UpdateHand(
                MainHand,
                hero,
                mainFsm,
                ShouldShow(plugin, hero, MainHand, mainFsm));
            UpdateHand(
                OffHand,
                hero,
                offFsm,
                ShouldShow(plugin, hero, OffHand, offFsm));
        }

        internal static void LateUpdate()
        {
            if (!ShouldUpdatePosition(MainHand)
                && !ShouldUpdatePosition(OffHand))
            {
                return;
            }

            Vector3 visualWorldOffset;
            if (!TryGetFirstPersonArmsVisualWorldOffset(
                    out visualWorldOffset))
            {
                visualWorldOffset = Vector3.zero;
            }
            UpdatePosition(MainHand, Hero.Current, visualWorldOffset);
            UpdatePosition(OffHand, Hero.Current, visualWorldOffset);
        }

        internal static void Shutdown()
        {
            DestroyHand(MainHand);
            DestroyHand(OffHand);
            _tryGetVisualWorldOffset = null;
            _firstPersonArmsAdjusterBridgeResolved = false;
            _firstPersonArmsAdjusterFailureLogged = false;
            _hdAdditionalLightDataType = null;
            _hdAdditionalLightDataResolved = false;
            _hdAdditionalLightDataFailureLogged = false;
        }

        private static bool ShouldShow(
            SoulAndServicePlugin plugin,
            Hero hero,
            HandLightState state,
            MagicFSM fsm)
        {
            if (!plugin.IsEnabled
                || plugin.SoulSalvageOverhaul == null
                || !plugin.SoulSalvageOverhaul.Value
                || plugin.SoulRendInnerLightEnabled == null
                || !plugin.SoulRendInnerLightEnabled.Value
                || plugin.SoulRendInnerLightIntensity == null
                || plugin.SoulRendInnerLightIntensity.Value <= MinimumVisibleIntensity
                || hero.HeroItems == null
                || SoulSalvageRuntime.IsVersatileWeaponsHandSuppressed(state.Slot))
            {
                return false;
            }

            Item item = hero.HeroItems.EquippedItem(state.Slot);
            if (!SoulSalvageRuntime.IsSoulSalvageItem(item) || fsm == null)
            {
                return false;
            }

            string currentState = fsm.CurrentStateType.ToString();
            string nextState = fsm.CurrentStateToEnterType.ToString();
            return !IsHiddenState(currentState) && !IsHiddenState(nextState);
        }

        private static bool IsHiddenState(string state)
        {
            return string.IsNullOrEmpty(state)
                || string.Equals(state, "Empty", StringComparison.OrdinalIgnoreCase)
                || state.IndexOf("UnEquip", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void UpdateHand(
            HandLightState state,
            Hero hero,
            MagicFSM fsm,
            bool shouldShow)
        {
            bool casting = shouldShow && fsm != null && fsm.IsCasting;
            if (casting && !state.WasCasting)
            {
                state.CastStartedAt = Time.unscaledTime;
            }
            state.WasCasting = casting;

            float boostTarget = casting
                && Time.unscaledTime >= state.CastStartedAt + CastBoostDelaySeconds
                    ? CastBoostMultiplier
                    : 1.0f;
            float rampSeconds = boostTarget > state.CastBoostFactor
                ? CastBoostRampUpSeconds
                : CastBoostRampDownSeconds;
            float boostDelta = Time.unscaledDeltaTime
                * (CastBoostMultiplier - 1.0f) / Math.Max(0.01f, rampSeconds);
            state.CastBoostFactor = Mathf.MoveTowards(
                state.CastBoostFactor,
                boostTarget,
                boostDelta);

            float targetIntensity = shouldShow
                ? GetBrightness() * state.CastBoostFactor * HdrpIntensityMultiplier
                : 0.0f;
            if (state.Light == null && targetIntensity <= MinimumVisibleIntensity)
            {
                return;
            }
            if (!EnsureHand(state, hero))
            {
                return;
            }

            state.Light.range = GetRange();

            float fadeSeconds = GetValue(
                SoulAndServicePlugin.Instance.SoulRendInnerLightFadeSeconds,
                0.12f);
            float nextIntensity = targetIntensity;
            if (fadeSeconds > 0.0f)
            {
                float reference = Math.Max(
                    1.0f,
                    Math.Max(state.Light.intensity, targetIntensity));
                nextIntensity = Mathf.MoveTowards(
                    state.Light.intensity,
                    targetIntensity,
                    Time.unscaledDeltaTime * reference / fadeSeconds);
            }
            state.Light.intensity = nextIntensity;
            ConfigureHdrpData(state, nextIntensity);
            bool visible = nextIntensity > MinimumVisibleIntensity;
            state.Light.enabled = visible;
            state.LightObject.SetActive(visible);
        }

        private static bool EnsureHand(HandLightState state, Hero hero)
        {
            if (state.LightObject == null)
            {
                Transform anchor = ResolveAnchor(state, hero);
                if (anchor == null)
                {
                    return false;
                }
                state.LightObject = new GameObject(state.ObjectName);
                state.LightObject.transform.position = anchor.position;
                state.Light = state.LightObject.AddComponent<Light>();
            }
            else if (state.Light == null)
            {
                state.Light = state.LightObject.GetComponent<Light>()
                    ?? state.LightObject.AddComponent<Light>();
            }

            state.Light.type = LightType.Point;
            state.Light.color = SoulRendColor;
            state.Light.shadows = LightShadows.None;
            state.Light.bounceIntensity = 0.0f;
            state.Light.cullingMask = ~0;
            state.Light.renderMode = LightRenderMode.Auto;
            return true;
        }

        private static Transform ResolveAnchor(HandLightState state, Hero hero)
        {
            if (!ReferenceEquals(state.AnchorHero, hero))
            {
                state.AnchorHero = hero;
                state.Anchor = null;
                state.NextAnchorProbeAt = 0.0f;
            }
            if (state.Anchor != null)
            {
                return state.Anchor;
            }
            if (hero == null || Time.unscaledTime < state.NextAnchorProbeAt)
            {
                return null;
            }
            state.NextAnchorProbeAt = Time.unscaledTime + 0.5f;
            string propertyName = state.Slot == EquipmentSlotType.MainHand
                ? "MainHand"
                : "OffHand";
            state.Anchor = GetTransformProperty(hero, propertyName);
            if (state.Anchor == null && hero.VHeroController != null)
            {
                state.Anchor = GetTransformProperty(
                    hero.VHeroController,
                    state.Slot == EquipmentSlotType.MainHand
                        ? "MainHandWrist"
                        : "OffHandWrist");
            }
            return state.Anchor;
        }

        private static Transform GetTransformProperty(object owner, string propertyName)
        {
            if (owner == null)
            {
                return null;
            }
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(owner, null) as Transform;
        }

        private static float GetBrightness()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            float brightness = GetValue(plugin.SoulRendInnerLightIntensity, 0.5f);
            brightness *= GetValue(
                plugin.SoulRendInnerLightIntensityMultiplier,
                0.8f);
            if (IsInterior())
            {
                brightness *= GetValue(
                    plugin.SoulRendInnerLightInteriorIntensityMultiplier,
                    1.0f);
            }
            return brightness * GetPowerValue(
                GetValue(plugin.SoulRendInnerLightMinimumPowerBrightnessMultiplier, 0.2f),
                GetValue(plugin.SoulRendInnerLightMasteryBrightnessMultiplier, 2.0f),
                GetValue(plugin.SoulRendInnerLightMaximumPowerBrightnessMultiplier, 3.0f));
        }

        private static float GetRange()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            return GetPowerValue(
                GetValue(plugin.SoulRendInnerLightMinimumPowerRange, 1.5f),
                GetValue(plugin.SoulRendInnerLightMasteryRange, 3.0f),
                GetValue(plugin.SoulRendInnerLightMaximumPowerRange, 4.5f));
        }

        private static float GetPowerValue(float minimum, float mastery, float maximum)
        {
            float power = Mathf.Clamp(SoulProgressionRuntime.GetNecromanticPower(), 0.0f, 200.0f);
            if (power <= 100.0f)
            {
                float progress = Mathf.Clamp01(power / 100.0f);
                progress = progress * progress * (3.0f - (2.0f * progress));
                return Mathf.Lerp(minimum, mastery, progress);
            }
            return Mathf.Lerp(
                mastery,
                maximum,
                Mathf.Clamp01((power - 100.0f) / 100.0f));
        }

        private static bool ShouldUpdatePosition(HandLightState state)
        {
            return state.LightObject != null
                && state.LightObject.activeSelf;
        }

        private static void UpdatePosition(
            HandLightState state,
            Hero hero,
            Vector3 visualWorldOffset)
        {
            if (!ShouldUpdatePosition(state))
            {
                return;
            }
            Transform anchor = ResolveAnchor(state, hero);
            if (anchor == null)
            {
                return;
            }
            if (state.LightObject.transform.parent != null)
            {
                state.LightObject.transform.SetParent(null, true);
            }
            state.LightObject.transform.position = anchor.position
                + visualWorldOffset;
        }

        private static bool TryGetFirstPersonArmsVisualWorldOffset(
            out Vector3 visualWorldOffset)
        {
            visualWorldOffset = Vector3.zero;
            if (!TryResolveFirstPersonArmsAdjusterBridge())
            {
                return false;
            }
            try
            {
                return _tryGetVisualWorldOffset(out visualWorldOffset);
            }
            catch (Exception exception)
            {
                _tryGetVisualWorldOffset = null;
                LogFirstPersonArmsAdjusterFailureOnce(
                    "First Person Arms Adjuster visual-offset API failed: "
                    + exception.GetBaseException().Message + ".");
                visualWorldOffset = Vector3.zero;
                return false;
            }
        }

        private static bool TryResolveFirstPersonArmsAdjusterBridge()
        {
            if (_firstPersonArmsAdjusterBridgeResolved)
            {
                return _tryGetVisualWorldOffset != null;
            }
            _firstPersonArmsAdjusterBridgeResolved = true;

            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    FirstPersonArmsAdjusterPluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }
            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                FirstPersonArmsAdjusterApiTypeName,
                false);
            MethodInfo method = apiType == null
                ? null
                : apiType.GetMethod(
                    "TryGetCurrentVisualWorldOffset",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Vector3).MakeByRefType() },
                    null);
            if (method == null || method.ReturnType != typeof(bool))
            {
                LogFirstPersonArmsAdjusterFailureOnce(
                    "First Person Arms Adjuster is loaded, but its visual-offset API could not be found.");
                return false;
            }
            try
            {
                _tryGetVisualWorldOffset =
                    (TryGetVisualWorldOffset)Delegate.CreateDelegate(
                        typeof(TryGetVisualWorldOffset),
                        method);
                SoulAndServicePlugin.Instance?.LogDiagnostic(
                    "Soul Rend hand lights connected to the First Person Arms Adjuster visual-offset API.");
                return true;
            }
            catch (Exception exception)
            {
                LogFirstPersonArmsAdjusterFailureOnce(
                    "First Person Arms Adjuster visual-offset API binding failed: "
                    + exception.GetBaseException().Message + ".");
                return false;
            }
        }

        private static void LogFirstPersonArmsAdjusterFailureOnce(
            string message)
        {
            if (_firstPersonArmsAdjusterFailureLogged)
            {
                return;
            }
            _firstPersonArmsAdjusterFailureLogged = true;
            SoulAndServicePlugin.Instance?.LogWarning(message);
        }

        private static void ConfigureHdrpData(
            HandLightState state,
            float renderIntensity)
        {
            if (state.Light == null || state.LightObject == null)
            {
                return;
            }
            Type hdType = ResolveHdAdditionalLightDataType();
            if (hdType == null)
            {
                return;
            }
            try
            {
                Component hdData = state.Light.GetComponent(hdType)
                    ?? state.LightObject.AddComponent(hdType);
                TrySetMember(
                    hdData,
                    new[] { "intensity", "m_Intensity" },
                    renderIntensity);
                TrySetMember(
                    hdData,
                    new[] { "lightDimmer", "m_LightDimmer" },
                    1.0f);
                TrySetMember(
                    hdData,
                    new[] { "volumetricDimmer", "m_VolumetricDimmer" },
                    0.0f);
                TrySetMember(
                    hdData,
                    new[] { "affectDiffuse", "m_AffectDiffuse" },
                    true);
                TrySetMember(
                    hdData,
                    new[] { "affectSpecular", "m_AffectSpecular" },
                    true);
                MethodInfo enableShadows = hdType.GetMethod(
                    "EnableShadows",
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
                enableShadows?.Invoke(hdData, new object[] { false });
                TrySetMember(
                    hdData,
                    new[]
                    {
                        "shadowDimmer",
                        "m_ShadowDimmer",
                        "shadowIntensity"
                    },
                    0.0f);
            }
            catch (Exception exception)
            {
                if (!_hdAdditionalLightDataFailureLogged)
                {
                    _hdAdditionalLightDataFailureLogged = true;
                    SoulAndServicePlugin.Instance?.LogWarning(
                        "Soul Rend HDRP hand-light setup failed: "
                        + exception.GetBaseException().Message + ".");
                }
            }
        }

        private static Type ResolveHdAdditionalLightDataType()
        {
            if (_hdAdditionalLightDataResolved)
            {
                return _hdAdditionalLightDataType;
            }
            _hdAdditionalLightDataResolved = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(
                    "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData",
                    false);
                if (type != null)
                {
                    _hdAdditionalLightDataType = type;
                    break;
                }
            }
            return _hdAdditionalLightDataType;
        }

        private static bool TrySetMember(
            object instance,
            string[] memberNames,
            object value)
        {
            if (instance == null)
            {
                return false;
            }
            const BindingFlags Flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type type = instance.GetType();
            foreach (string memberName in memberNames)
            {
                PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
                FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return true;
                }
            }
            return false;
        }

        private static bool IsInterior()
        {
            try
            {
                SceneService sceneService = World.Services.TryGet<SceneService>();
                return sceneService != null && !sceneService.IsOpenWorld;
            }
            catch
            {
                return false;
            }
        }

        private static float GetValue(
            BepInEx.Configuration.ConfigEntry<float> entry,
            float fallback)
        {
            return entry == null ? fallback : Math.Max(0.0f, entry.Value);
        }

        private static void DestroyHand(HandLightState state)
        {
            if (state.LightObject != null)
            {
                UnityEngine.Object.Destroy(state.LightObject);
            }
            state.LightObject = null;
            state.Light = null;
            state.Anchor = null;
            state.AnchorHero = null;
            state.NextAnchorProbeAt = 0.0f;
            state.CastStartedAt = 0.0f;
            state.CastBoostFactor = 1.0f;
            state.WasCasting = false;
        }
    }
}
