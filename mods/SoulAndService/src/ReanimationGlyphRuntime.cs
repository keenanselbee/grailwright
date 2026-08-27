using System;
using System.Collections.Generic;
using Awaken.TG.Assets;
using Awaken.TG.Graphics.VFX.Binders;
using Awaken.TG.Main.Fights.NPCs;
using Cysharp.Threading.Tasks;
using FMODUnity;
using UnityEngine;
using UnityEngine.VFX;

namespace SoulAndService
{
    internal static class ReanimationGlyphRuntime
    {
        private const string ShockAuraVfxKey =
            "0c7757225700cda4db246fd6bc3bc59f";
        private const string QualityControllerTypeName =
            "Awaken.TG.Main.Settings.Controllers.VfxPropertyByQualityController";
        private const float VisualRefreshSeconds = 0.50f;
        private const float RetrySeconds = 5.0f;
        private const float AuraParticleBudgetEquivalentServants = 4.0f;
        private const float MinimumAuraParticleBudgetScale = 0.25f;
        private const float MaximumAuraIntensity = 20.0f;
        private static readonly Color DefaultNecroticGreen =
            new Color(0.15686275f, 1.0f, 0.36862746f);
        private static readonly Color DefaultAuraGlowColor =
            new Color(0.78431374f, 1.0f, 0.83529413f);
        private static readonly Color DefaultAuraHazeColor =
            new Color(0.13725491f, 0.47843137f, 0.33333334f);

        private sealed class GlyphState
        {
            internal NpcElement Npc;
            internal Transform VisualMarker;
            internal readonly CombinedEffectState Aura =
                new CombinedEffectState();
        }

        private sealed class CombinedEffectState
        {
            internal IPooledInstance Pooled;
            internal VisualEffect Effect;
            internal VisualEffectSnapshot Snapshot;
            internal List<BehaviourState> DisabledBehaviours;
            internal string AssetKey;
            internal bool Loading;
            internal int Generation;
            internal float RetryAt;
            internal int ConfigSignature = Int32.MinValue;
        }

        private struct ReanimationEffectSettings
        {
            internal bool Enabled;
            internal Color AuraArcColor;
            internal Color AuraGlowColor;
            internal Color AuraHazeColor;
            internal int AuraParticleAmount;
            internal float AuraIntensity;
            internal float ElectricityOpacity;
            internal float SmokeOpacity;
            internal float AuraScale;
            internal int Signature;
        }

        private struct BehaviourState
        {
            internal Behaviour Behaviour;
            internal bool Enabled;
        }

        private sealed class VisualEffectSnapshot
        {
            private readonly Dictionary<string, float> _floats =
                new Dictionary<string, float>();
            private readonly Dictionary<string, int> _ints =
                new Dictionary<string, int>();
            private readonly Dictionary<string, Vector2> _vector2s =
                new Dictionary<string, Vector2>();
            private readonly Dictionary<string, Gradient> _gradients =
                new Dictionary<string, Gradient>();

            internal void SetFloat(VisualEffect effect, string name, float value)
            {
                if (!effect.HasFloat(name))
                {
                    return;
                }
                if (!_floats.ContainsKey(name))
                {
                    _floats[name] = effect.GetFloat(name);
                }
                effect.SetFloat(name, value);
            }

            internal void SetInt(VisualEffect effect, string name, int value)
            {
                if (!effect.HasInt(name))
                {
                    return;
                }
                if (!_ints.ContainsKey(name))
                {
                    _ints[name] = effect.GetInt(name);
                }
                effect.SetInt(name, value);
            }

            internal void SetVector2(
                VisualEffect effect,
                string name,
                Vector2 value)
            {
                if (!effect.HasVector2(name))
                {
                    return;
                }
                if (!_vector2s.ContainsKey(name))
                {
                    _vector2s[name] = effect.GetVector2(name);
                }
                effect.SetVector2(name, value);
            }

            internal void SetGradient(
                VisualEffect effect,
                string name,
                Gradient value)
            {
                if (!effect.HasGradient(name))
                {
                    return;
                }
                if (!_gradients.ContainsKey(name))
                {
                    _gradients[name] = effect.GetGradient(name);
                }
                effect.SetGradient(name, value);
            }

            internal void Restore(VisualEffect effect)
            {
                if (effect == null)
                {
                    return;
                }
                foreach (KeyValuePair<string, float> pair in _floats)
                {
                    effect.SetFloat(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, int> pair in _ints)
                {
                    effect.SetInt(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Vector2> pair in _vector2s)
                {
                    effect.SetVector2(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Gradient> pair in _gradients)
                {
                    effect.SetGradient(pair.Key, pair.Value);
                }
            }
        }

        private static readonly Dictionary<string, GlyphState> States =
            new Dictionary<string, GlyphState>();
        private static readonly List<string> StateIdBuffer =
            new List<string>();
        private static float _nextVisualRefreshAt;

        internal static void Attach(string summonId, NpcElement npc)
        {
            if (string.IsNullOrEmpty(summonId) || npc == null)
            {
                return;
            }
            Remove(summonId);
            GlyphState state = new GlyphState { Npc = npc };
            States[summonId] = state;
            Refresh(
                state,
                summonId,
                GetEffectSettings(
                    CountActiveVisualStates(),
                    SummonRuntime.GetEmpowermentCombatMultiplier(summonId)));
        }

        internal static void Update()
        {
            if (States.Count == 0 || Time.unscaledTime < _nextVisualRefreshAt)
            {
                return;
            }
            _nextVisualRefreshAt = Time.unscaledTime + VisualRefreshSeconds;
            StateIdBuffer.Clear();
            foreach (string summonId in States.Keys)
            {
                StateIdBuffer.Add(summonId);
            }
            foreach (string summonId in StateIdBuffer)
            {
                GlyphState state;
                if (!States.TryGetValue(summonId, out state))
                {
                    continue;
                }
                if (state.Npc == null
                    || state.Npc.HasBeenDiscarded
                    || !state.Npc.IsAlive)
                {
                    Remove(summonId);
                }
            }
            int activeVisualCount = CountActiveVisualStates();
            foreach (string summonId in StateIdBuffer)
            {
                GlyphState state;
                if (States.TryGetValue(summonId, out state))
                {
                    Refresh(
                        state,
                        summonId,
                        GetEffectSettings(
                            activeVisualCount,
                            SummonRuntime.GetEmpowermentCombatMultiplier(
                                summonId)));
                }
            }
        }

        internal static void Remove(string summonId)
        {
            GlyphState state;
            if (string.IsNullOrEmpty(summonId)
                || !States.TryGetValue(summonId, out state))
            {
                return;
            }
            States.Remove(summonId);
            ReleaseEffect(state.Aura);
        }

        internal static void Shutdown()
        {
            foreach (string id in new List<string>(States.Keys))
            {
                Remove(id);
            }
            _nextVisualRefreshAt = 0.0f;
            StateIdBuffer.Clear();
        }

        private static void Refresh(
            GlyphState state,
            string summonId,
            ReanimationEffectSettings settings)
        {
            Transform marker = state.Npc == null
                || state.Npc.Controller == null
                || state.Npc.Controller.AlivePrefab == null
                    ? null
                    : state.Npc.Controller.AlivePrefab.transform;
            if (!ReferenceEquals(marker, state.VisualMarker))
            {
                ReleaseEffect(state.Aura);
                state.VisualMarker = marker;
            }
            if (marker == null)
            {
                return;
            }
            RefreshAura(summonId, state, marker, settings);
        }

        private static void RefreshAura(
            string summonId,
            GlyphState state,
            Transform marker,
            ReanimationEffectSettings settings)
        {
            CombinedEffectState effectState = state.Aura;
            if (!settings.Enabled)
            {
                ReleaseEffect(effectState);
                return;
            }
            if (effectState.Pooled != null)
            {
                string desiredAssetKey = ShockAuraVfxKey;
                if (!string.Equals(
                        effectState.AssetKey,
                        desiredAssetKey,
                        StringComparison.Ordinal))
                {
                    ReleaseEffect(effectState);
                }
            }
            if (effectState.Pooled != null)
            {
                if (effectState.ConfigSignature != settings.Signature)
                {
                    ConfigureAuraEffect(
                        effectState.Effect,
                        effectState.Snapshot,
                        settings);
                    effectState.ConfigSignature = settings.Signature;
                }
                return;
            }
            if (effectState.Loading
                || Time.unscaledTime < effectState.RetryAt)
            {
                return;
            }
            effectState.Loading = true;
            LoadAndConfigureAura(
                summonId,
                state,
                marker,
                settings,
                effectState.Generation).Forget();
        }

        private static async UniTaskVoid LoadAndConfigureAura(
            string summonId,
            GlyphState state,
            Transform marker,
            ReanimationEffectSettings settings,
            int generation)
        {
            CombinedEffectState effectState = state.Aura;
            IPooledInstance pooled = null;
            VisualEffect effect = null;
            VisualEffectSnapshot snapshot = null;
            List<BehaviourState> disabledBehaviours = null;
            try
            {
                string assetKey = ShockAuraVfxKey;
                pooled = await PrefabPool.Instantiate(
                    new ShareableARAssetReference(assetKey),
                    Vector3.zero,
                    Quaternion.identity,
                    marker,
                    Vector3.one);
                GlyphState current;
                if (pooled == null
                    || pooled.Instance == null
                    || !States.TryGetValue(summonId, out current)
                    || !ReferenceEquals(current, state)
                    || !ReferenceEquals(current.Aura, effectState)
                    || effectState.Generation != generation
                    || !ReferenceEquals(state.VisualMarker, marker))
                {
                    pooled?.Return();
                    return;
                }

                effect = pooled.Instance.GetComponentInChildren<VisualEffect>(true);
                if (effect == null)
                {
                    throw new InvalidOperationException(
                        "the native body-aura prefab has no VisualEffect");
                }
                foreach (VFXBodyMarkerBinder binder
                    in pooled.Instance.GetComponentsInChildren<VFXBodyMarkerBinder>(true))
                {
                    binder.SetBody(state.Npc.VFXBodyMarker);
                }
                disabledBehaviours = DisableQualityControllers(
                    pooled.Instance,
                    true);
                snapshot = new VisualEffectSnapshot();
                ConfigureAuraEffect(effect, snapshot, settings);
                effectState.Pooled = pooled;
                effectState.Effect = effect;
                effectState.Snapshot = snapshot;
                effectState.DisabledBehaviours = disabledBehaviours;
                effectState.AssetKey = assetKey;
                effectState.ConfigSignature = settings.Signature;
                pooled = null;

                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogDiagnostic(
                        "Attached body-bound Reanimation VFX to " + summonId
                        + "; electricParticles="
                        + (settings.ElectricityOpacity > 0.0f
                            ? GetAuraParticleCount(150, settings.AuraParticleAmount)
                            : 0)
                        + "; hazeParticles="
                        + (settings.SmokeOpacity > 0.0f
                            ? GetAuraParticleCount(100, settings.AuraParticleAmount)
                            : 0)
                        + ".");
                }
            }
            catch (Exception exception)
            {
                RestoreAndReturn(
                    pooled,
                    effect,
                    snapshot,
                    disabledBehaviours);
                effectState.RetryAt = Time.unscaledTime + RetrySeconds;
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Could not attach body-bound Reanimation VFX: "
                        + exception.GetBaseException().Message);
                }
            }
            finally
            {
                effectState.Loading = false;
            }
        }

        private static void ConfigureAuraEffect(
            VisualEffect effect,
            VisualEffectSnapshot snapshot,
            ReanimationEffectSettings settings)
        {
            int electricParticleCount = settings.ElectricityOpacity > 0.0f
                ? GetAuraParticleCount(150, settings.AuraParticleAmount)
                : 0;
            int hazeParticleCount = settings.SmokeOpacity > 0.0f
                ? GetAuraParticleCount(100, settings.AuraParticleAmount)
                : 0;
            Gradient arcGradient = CreateAuraGradient(
                settings.AuraArcColor,
                settings.AuraGlowColor,
                settings.AuraIntensity);
            Gradient hazeGradient = CreateAuraHazeGradient(
                settings.AuraHazeColor,
                settings.AuraIntensity);
            effect.Stop();
            effect.Reinit();
            snapshot.SetInt(effect, "Count", electricParticleCount);
            snapshot.SetInt(effect, "Smoke-Count", hazeParticleCount);
            snapshot.SetFloat(effect, "Spawn Rate", 0.0f);
            snapshot.SetFloat(
                effect,
                "Fire Alpha",
                0.10f * settings.ElectricityOpacity);
            snapshot.SetFloat(effect, "Smoke-Alpha", settings.SmokeOpacity);
            snapshot.SetVector2(
                effect,
                "Size Min/Max",
                new Vector2(0.02f, 0.05f) * settings.AuraScale);
            snapshot.SetVector2(
                effect,
                "Fire Size Min Max",
                new Vector2(0.18f, 0.40f) * settings.AuraScale);
            snapshot.SetFloat(
                effect,
                "Smoke Size",
                0.30f * settings.AuraScale);
            snapshot.SetGradient(effect, "Color-Fire", arcGradient);
            snapshot.SetGradient(effect, "Coilor-Smoke", hazeGradient);
            effect.Play();
        }

        private static ReanimationEffectSettings GetEffectSettings(
            int activeVisualCount,
            float empowermentMultiplier)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            ReanimationEffectSettings settings = new ReanimationEffectSettings();
            if (plugin == null
                || plugin.ReanimationVfxEnabled == null
                || !plugin.ReanimationVfxEnabled.Value)
            {
                return settings;
            }

            float auraBudgetScale = 1.0f;
            if (plugin.ReanimationDynamicParticleBudget != null
                && plugin.ReanimationDynamicParticleBudget.Value
                && activeVisualCount > 0)
            {
                auraBudgetScale = Mathf.Clamp(
                    AuraParticleBudgetEquivalentServants / activeVisualCount,
                    MinimumAuraParticleBudgetScale,
                    1.0f);
            }

            settings.AuraArcColor = ResolveConfiguredColor(
                plugin.ReanimationAuraArcColor == null
                    ? null
                    : plugin.ReanimationAuraArcColor.Value,
                DefaultNecroticGreen);
            settings.AuraGlowColor = ResolveConfiguredColor(
                plugin.ReanimationAuraGlowColor == null
                    ? null
                    : plugin.ReanimationAuraGlowColor.Value,
                DefaultAuraGlowColor);
            settings.AuraHazeColor = ResolveConfiguredColor(
                plugin.ReanimationAuraHazeColor == null
                    ? null
                    : plugin.ReanimationAuraHazeColor.Value,
                DefaultAuraHazeColor);
            settings.AuraParticleAmount = ScaleParticleAmount(
                plugin.ReanimationAuraParticleAmount == null
                    ? 75
                    : plugin.ReanimationAuraParticleAmount.Value,
                auraBudgetScale);
            float configuredIntensity = plugin.ReanimationAuraIntensity == null
                ? 10.0f
                : plugin.ReanimationAuraIntensity.Value;
            settings.AuraIntensity = Mathf.Min(
                MaximumAuraIntensity,
                configuredIntensity * Mathf.Clamp(
                    empowermentMultiplier,
                    1.0f,
                    1.50f));
            settings.ElectricityOpacity =
                plugin.ReanimationElectricityOpacity == null
                    ? 1.0f
                    : plugin.ReanimationElectricityOpacity.Value;
            settings.SmokeOpacity = plugin.ReanimationSmokeOpacity == null
                ? 0.5f
                : plugin.ReanimationSmokeOpacity.Value;
            settings.AuraScale = plugin.ReanimationAuraScale == null
                ? 1.0f
                : plugin.ReanimationAuraScale.Value;
            settings.Enabled = settings.AuraParticleAmount > 0
                && settings.AuraIntensity > 0.0f
                && (settings.ElectricityOpacity > 0.0f
                    || settings.SmokeOpacity > 0.0f);

            unchecked
            {
                settings.Signature = settings.Enabled.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraArcColor.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraGlowColor.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraHazeColor.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraParticleAmount;
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraIntensity.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.ElectricityOpacity.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.SmokeOpacity.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.AuraScale.GetHashCode();
            }
            return settings;
        }

        private static int ScaleParticleAmount(int particleAmount, float scale)
        {
            return particleAmount <= 0
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(particleAmount * scale));
        }

        private static int GetAuraParticleCount(int baseline, int particleAmount)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(baseline * particleAmount / 100.0f));
        }

        private static int CountActiveVisualStates()
        {
            int count = 0;
            foreach (GlyphState state in States.Values)
            {
                if (state != null
                    && state.Npc != null
                    && !state.Npc.HasBeenDiscarded
                    && state.Npc.IsAlive
                    && state.Npc.Controller != null
                    && state.Npc.Controller.AlivePrefab != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static List<BehaviourState> DisableQualityControllers(
            GameObject root,
            bool disableAudioAndLights)
        {
            List<BehaviourState> output = new List<BehaviourState>();
            foreach (Behaviour behaviour
                in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }
                string typeName = behaviour.GetType().FullName;
                bool disable = string.Equals(
                    typeName,
                    QualityControllerTypeName,
                    StringComparison.Ordinal);
                if (disableAudioAndLights)
                {
                    disable = disable
                        || behaviour is Light
                        || behaviour is StudioEventEmitter;
                }
                if (!disable)
                {
                    continue;
                }
                output.Add(new BehaviourState
                {
                    Behaviour = behaviour,
                    Enabled = behaviour.enabled
                });
                if (behaviour is StudioEventEmitter audioEmitter)
                {
                    audioEmitter.Stop(true);
                }
                behaviour.enabled = false;
            }
            return output;
        }

        private static void ReleaseEffect(CombinedEffectState state)
        {
            if (state == null)
            {
                return;
            }
            state.Generation++;
            if (state.Pooled != null)
            {
                RestoreAndReturn(
                    state.Pooled,
                    state.Effect,
                    state.Snapshot,
                    state.DisabledBehaviours);
            }
            state.Pooled = null;
            state.Effect = null;
            state.Snapshot = null;
            state.DisabledBehaviours = null;
            state.AssetKey = null;
            state.ConfigSignature = Int32.MinValue;
            state.RetryAt = 0.0f;
        }

        private static void RestoreAndReturn(
            IPooledInstance pooled,
            VisualEffect effect,
            VisualEffectSnapshot snapshot,
            List<BehaviourState> controllers)
        {
            if (effect != null)
            {
                effect.Stop();
                snapshot?.Restore(effect);
                effect.Reinit();
            }
            pooled?.Return();
            if (controllers != null)
            {
                foreach (BehaviourState state in controllers)
                {
                    if (state.Behaviour != null)
                    {
                        state.Behaviour.enabled = state.Enabled;
                    }
                }
            }
        }

        private static Color ResolveConfiguredColor(
            string configured,
            Color fallback)
        {
            Color color;
            if (!string.IsNullOrWhiteSpace(configured)
                && ColorUtility.TryParseHtmlString(configured.Trim(), out color))
            {
                color.a = 1.0f;
                return color;
            }

            return fallback;
        }

        private static Gradient CreateAuraGradient(
            Color arcColor,
            Color glowColor,
            float intensity)
        {
            Color arc = arcColor * (2.0f * intensity);
            Color glow = glowColor * (2.0f * intensity);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(arc * 0.35f, 0.0f),
                    new GradientColorKey(arc, 0.35f),
                    new GradientColorKey(glow, 0.58f),
                    new GradientColorKey(arc * 0.20f, 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.10f),
                    new GradientAlphaKey(0.85f, 0.72f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            return gradient;
        }

        private static Gradient CreateAuraHazeGradient(
            Color glowColor,
            float intensity)
        {
            Color glow = glowColor * intensity;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        Color.Lerp(Color.black, glow, 0.30f),
                        0.0f),
                    new GradientColorKey(glow, 0.45f),
                    new GradientColorKey(
                        Color.Lerp(Color.black, glow, 0.24f),
                        1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.18f),
                    new GradientAlphaKey(0.72f, 0.72f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            return gradient;
        }

    }
}
