using System;
using System.Collections.Generic;
using Awaken.TG.Assets;
using Awaken.TG.Main.Fights.NPCs;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.VFX;

namespace SoulAndService
{
    internal static class ReanimationGlyphRuntime
    {
        private const string BodySurfaceVfxKey =
            "22fdfa954ef8f9c4ea62779e08eedfbf";
        private const string QualityControllerTypeName =
            "Awaken.TG.Main.Settings.Controllers.VfxPropertyByQualityController";
        private const float VisualRefreshSeconds = 0.50f;
        private const float RetrySeconds = 5.0f;
        private const float RuneParticleBudgetEquivalentServants = 4.0f;
        private const float SmokeParticleBudgetEquivalentServants = 3.0f;
        private const float MinimumRuneParticleBudgetScale = 0.25f;
        private const float MinimumSmokeParticleBudgetScale = 0.15f;
        private const int RuneAtlasSize = 256;
        private const int RuneCellSize = RuneAtlasSize / 2;

        private sealed class GlyphState
        {
            internal NpcElement Npc;
            internal Transform VisualMarker;
            internal readonly CombinedEffectState Effect =
                new CombinedEffectState();
        }

        private sealed class CombinedEffectState
        {
            internal IPooledInstance Pooled;
            internal VisualEffect Effect;
            internal VisualEffectSnapshot Snapshot;
            internal List<BehaviourState> QualityControllers;
            internal bool Loading;
            internal int Generation;
            internal float RetryAt;
            internal int ConfigSignature = Int32.MinValue;
        }

        private struct CombinedEffectSettings
        {
            internal bool Enabled;
            internal bool RunesEnabled;
            internal float RuneIntensity;
            internal int RuneParticleAmount;
            internal bool SmokeEnabled;
            internal float SmokeIntensity;
            internal int SmokeParticleAmount;
            internal int Signature;
        }

        private struct BehaviourState
        {
            internal MonoBehaviour Behaviour;
            internal bool Enabled;
        }

        private sealed class VisualEffectSnapshot
        {
            private readonly Dictionary<string, float> _floats =
                new Dictionary<string, float>();
            private readonly Dictionary<string, int> _ints =
                new Dictionary<string, int>();
            private readonly Dictionary<string, bool> _bools =
                new Dictionary<string, bool>();
            private readonly Dictionary<string, Vector2> _vector2s =
                new Dictionary<string, Vector2>();
            private readonly Dictionary<string, Vector3> _vector3s =
                new Dictionary<string, Vector3>();
            private readonly Dictionary<string, Gradient> _gradients =
                new Dictionary<string, Gradient>();
            private readonly Dictionary<string, Texture> _textures =
                new Dictionary<string, Texture>();

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

            internal void SetBool(VisualEffect effect, string name, bool value)
            {
                if (!effect.HasBool(name))
                {
                    return;
                }
                if (!_bools.ContainsKey(name))
                {
                    _bools[name] = effect.GetBool(name);
                }
                effect.SetBool(name, value);
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

            internal void SetVector3(
                VisualEffect effect,
                string name,
                Vector3 value)
            {
                if (!effect.HasVector3(name))
                {
                    return;
                }
                if (!_vector3s.ContainsKey(name))
                {
                    _vector3s[name] = effect.GetVector3(name);
                }
                effect.SetVector3(name, value);
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

            internal void SetTexture(
                VisualEffect effect,
                string name,
                Texture value)
            {
                if (!effect.HasTexture(name))
                {
                    return;
                }
                if (!_textures.ContainsKey(name))
                {
                    _textures[name] = effect.GetTexture(name);
                }
                effect.SetTexture(name, value);
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
                foreach (KeyValuePair<string, bool> pair in _bools)
                {
                    effect.SetBool(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Vector2> pair in _vector2s)
                {
                    effect.SetVector2(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Vector3> pair in _vector3s)
                {
                    effect.SetVector3(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Gradient> pair in _gradients)
                {
                    effect.SetGradient(pair.Key, pair.Value);
                }
                foreach (KeyValuePair<string, Texture> pair in _textures)
                {
                    effect.SetTexture(pair.Key, pair.Value);
                }
            }
        }

        private static readonly Dictionary<string, GlyphState> States =
            new Dictionary<string, GlyphState>();
        private static readonly List<string> StateIdBuffer =
            new List<string>();
        private static Texture2D _runeAtlas;
        private static Gradient _runeGradient;
        private static float _runeGradientIntensity = float.NaN;
        private static Gradient _smokeGradient;
        private static Gradient _sparkleGradient;
        private static float _nextVisualRefreshAt;

        internal static void Attach(string summonId, NpcElement npc)
        {
            if (string.IsNullOrEmpty(summonId) || npc == null)
            {
                return;
            }
            Remove(summonId);
            GlyphState state = new GlyphState
            {
                Npc = npc
            };
            States[summonId] = state;
            Refresh(
                state,
                summonId,
                GetEffectSettings(CountActiveVisualStates()));
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
            CombinedEffectSettings settings =
                GetEffectSettings(CountActiveVisualStates());
            foreach (string summonId in StateIdBuffer)
            {
                GlyphState state;
                if (States.TryGetValue(summonId, out state))
                {
                    Refresh(state, summonId, settings);
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
            ReleaseEffect(state.Effect);
        }

        internal static void Shutdown()
        {
            foreach (string id in new List<string>(States.Keys))
            {
                Remove(id);
            }
            if (_runeAtlas != null)
            {
                UnityEngine.Object.Destroy(_runeAtlas);
                _runeAtlas = null;
            }
            _runeGradient = null;
            _runeGradientIntensity = float.NaN;
            _smokeGradient = null;
            _sparkleGradient = null;
            _nextVisualRefreshAt = 0.0f;
            StateIdBuffer.Clear();
        }

        private static void Refresh(
            GlyphState state,
            string summonId,
            CombinedEffectSettings settings)
        {
            Transform marker = state.Npc == null
                || state.Npc.Controller == null
                || state.Npc.Controller.AlivePrefab == null
                    ? null
                    : state.Npc.Controller.AlivePrefab.transform;
            if (!ReferenceEquals(marker, state.VisualMarker))
            {
                ReleaseEffect(state.Effect);
                state.VisualMarker = marker;
            }
            if (marker == null)
            {
                return;
            }
            RefreshEffect(
                summonId,
                state,
                marker,
                settings);
        }

        private static void RefreshEffect(
            string summonId,
            GlyphState state,
            Transform marker,
            CombinedEffectSettings settings)
        {
            CombinedEffectState effectState = state.Effect;
            if (!settings.Enabled)
            {
                if (effectState.Pooled != null || effectState.Loading)
                {
                    ReleaseEffect(effectState);
                }
                return;
            }
            if (effectState.Pooled != null)
            {
                if (effectState.ConfigSignature != settings.Signature)
                {
                    ConfigureEffect(
                        effectState.Effect,
                        effectState.Snapshot,
                        state.Npc,
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
            int generation = effectState.Generation;
            LoadAndConfigure(
                summonId,
                state,
                marker,
                settings,
                generation).Forget();
        }

        private static async UniTaskVoid LoadAndConfigure(
            string summonId,
            GlyphState state,
            Transform marker,
            CombinedEffectSettings settings,
            int generation)
        {
            CombinedEffectState effectState = state.Effect;
            IPooledInstance pooled = null;
            VisualEffect effect = null;
            VisualEffectSnapshot snapshot = null;
            List<BehaviourState> controllers = null;
            try
            {
                pooled = await PrefabPool.Instantiate(
                    new ShareableARAssetReference(BodySurfaceVfxKey),
                    Vector3.zero,
                    Quaternion.identity,
                    marker,
                    Vector3.one);
                GlyphState current;
                if (pooled == null
                    || pooled.Instance == null
                    || !States.TryGetValue(summonId, out current)
                    || !ReferenceEquals(current, state)
                    || !ReferenceEquals(current.Effect, effectState)
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
                        "the native body-surface prefab has no VisualEffect");
                }
                controllers = DisableQualityControllers(pooled.Instance);
                snapshot = new VisualEffectSnapshot();
                ConfigureEffect(
                    effect,
                    snapshot,
                    state.Npc,
                    settings);
                effectState.Pooled = pooled;
                effectState.Effect = effect;
                effectState.Snapshot = snapshot;
                effectState.QualityControllers = controllers;
                effectState.ConfigSignature = settings.Signature;
                pooled = null;

                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    float boundsSize = GetBodyBoundsSize(state.Npc);
                    plugin.LogDiagnostic(
                        "Attached combined reanimation VFX to " + summonId
                        + "; bodyBounds=" + boundsSize.ToString("0.###")
                        + "; runeParticles="
                        + GetRuneParticleCount(
                            boundsSize,
                            settings.RuneParticleAmount)
                        + "; smokeParticles="
                        + GetSmokeParticleCount(settings.SmokeParticleAmount)
                        + ".");
                }
            }
            catch (Exception exception)
            {
                RestoreAndReturn(pooled, effect, snapshot, controllers);
                effectState.RetryAt = Time.unscaledTime + RetrySeconds;
                SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Could not attach combined reanimation VFX: "
                        + exception.GetBaseException().Message);
                }
            }
            finally
            {
                effectState.Loading = false;
            }
        }

        private static void ConfigureEffect(
            VisualEffect effect,
            VisualEffectSnapshot snapshot,
            NpcElement npc,
            CombinedEffectSettings settings)
        {
            float boundsSize = GetBodyBoundsSize(npc);
            float sizeScale = Mathf.Clamp(Mathf.Sqrt(boundsSize), 0.85f, 1.55f);
            int runeParticleCount = settings.RunesEnabled
                ? GetRuneParticleCount(
                    boundsSize,
                    settings.RuneParticleAmount)
                : 0;
            int smokeParticleCount = settings.SmokeEnabled
                ? GetSmokeParticleCount(settings.SmokeParticleAmount)
                : 0;
            effect.Stop();
            effect.Reinit();

            snapshot.SetFloat(
                effect,
                "Smoke-Alpha",
                settings.SmokeEnabled
                    ? 0.45f * settings.SmokeIntensity
                    : 0.0f);
            snapshot.SetInt(effect, "Smoke-Count", smokeParticleCount);
            snapshot.SetInt(effect, "Count", runeParticleCount);
            snapshot.SetFloat(effect, "Spawn Rate", runeParticleCount / 2.0f);
            snapshot.SetFloat(effect, "Flipbook FPS", 0.20f);
            snapshot.SetFloat(
                effect,
                "Fire Alpha",
                settings.RunesEnabled ? 0.45f : 0.0f);
            snapshot.SetVector2(effect, "Fire Lifetime", new Vector2(1.5f, 2.5f));
            snapshot.SetVector2(
                effect,
                "Size Min/Max",
                settings.SmokeEnabled
                    ? new Vector2(0.08f, 0.16f) * sizeScale
                    : Vector2.zero);
            snapshot.SetVector2(
                effect,
                "Fire Size Min Max",
                settings.RunesEnabled
                    ? new Vector2(0.030f, 0.048f) * sizeScale
                    : Vector2.zero);
            snapshot.SetVector2(effect, "Fire Flipbook Size", new Vector2(2.0f, 2.0f));
            snapshot.SetVector3(effect, "Initial Velocity_vector", Vector3.zero);
            snapshot.SetVector3(
                effect,
                "AddVelocitySmoke",
                settings.SmokeEnabled
                    ? new Vector3(0.0f, 0.03f, 0.0f)
                    : Vector3.zero);
            snapshot.SetVector3(effect, "Fire Gravity", Vector3.zero);
            snapshot.SetGradient(
                effect,
                "Color-Fire",
                GetOrCreateGlyphGradient(settings.RuneIntensity));
            snapshot.SetGradient(
                effect,
                "Sparkle Fire Gradient",
                GetOrCreateSparkleGradient());
            snapshot.SetGradient(
                effect,
                "Coilor-Smoke",
                GetOrCreateSmokeGradient());
            snapshot.SetTexture(effect, "Fire Texture", GetOrCreateRuneAtlas());
            snapshot.SetBool(effect, "Fire||Sparks", true);
            effect.Play();
        }

        private static CombinedEffectSettings GetEffectSettings(
            int activeVisualCount)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            CombinedEffectSettings settings = new CombinedEffectSettings();
            if (plugin == null
                || plugin.ReanimationVfxEnabled == null
                || !plugin.ReanimationVfxEnabled.Value)
            {
                return settings;
            }

            float runeBudgetScale = 1.0f;
            float smokeBudgetScale = 1.0f;
            if (plugin.ReanimationDynamicParticleBudget != null
                && plugin.ReanimationDynamicParticleBudget.Value
                && activeVisualCount > 0)
            {
                runeBudgetScale = Mathf.Clamp(
                    RuneParticleBudgetEquivalentServants / activeVisualCount,
                    MinimumRuneParticleBudgetScale,
                    1.0f);
                smokeBudgetScale = Mathf.Clamp(
                    SmokeParticleBudgetEquivalentServants / activeVisualCount,
                    MinimumSmokeParticleBudgetScale,
                    1.0f);
            }

            settings.RuneIntensity = plugin.ReanimationRuneIntensity.Value;
            settings.RuneParticleAmount = ScaleParticleAmount(
                plugin.ReanimationRuneParticleAmount.Value,
                runeBudgetScale);
            settings.RunesEnabled = plugin.ReanimationRunesEnabled.Value
                && settings.RuneIntensity > 0.0f
                && settings.RuneParticleAmount > 0;
            settings.SmokeIntensity = plugin.ReanimationSmokeIntensity.Value;
            settings.SmokeParticleAmount = ScaleParticleAmount(
                plugin.ReanimationSmokeParticleAmount.Value,
                smokeBudgetScale);
            settings.SmokeEnabled = plugin.ReanimationSmokeEnabled.Value
                && settings.SmokeIntensity > 0.0f
                && settings.SmokeParticleAmount > 0;
            settings.Enabled = settings.RunesEnabled || settings.SmokeEnabled;

            unchecked
            {
                settings.Signature = settings.RunesEnabled.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.RuneIntensity.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.RuneParticleAmount;
                settings.Signature = (settings.Signature * 397)
                    ^ settings.SmokeEnabled.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.SmokeIntensity.GetHashCode();
                settings.Signature = (settings.Signature * 397)
                    ^ settings.SmokeParticleAmount;
            }
            return settings;
        }

        private static int ScaleParticleAmount(int particleAmount, float scale)
        {
            return particleAmount <= 0
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(particleAmount * scale));
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

        private static float GetBodyBoundsSize(NpcElement npc)
        {
            if (npc == null || npc.VFXBodyMarker == null)
            {
                return 1.0f;
            }
            return Mathf.Clamp(npc.VFXBodyMarker.BoundsSize, 0.50f, 4.0f);
        }

        private static int GetGlyphCount(float boundsSize)
        {
            return Mathf.Clamp(Mathf.RoundToInt(22.0f + boundsSize * 10.0f), 28, 56);
        }

        private static int GetRuneParticleCount(
            float boundsSize,
            int particleAmount)
        {
            return particleAmount <= 0
                ? 0
                : Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        GetGlyphCount(boundsSize) * particleAmount / 100.0f));
        }

        private static int GetSmokeParticleCount(int particleAmount)
        {
            return particleAmount <= 0
                ? 0
                : Mathf.Max(
                    1,
                    Mathf.RoundToInt(12.0f * particleAmount / 100.0f));
        }

        private static List<BehaviourState> DisableQualityControllers(
            GameObject root)
        {
            List<BehaviourState> output = new List<BehaviourState>();
            foreach (MonoBehaviour behaviour
                in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null
                    || !string.Equals(
                        behaviour.GetType().FullName,
                        QualityControllerTypeName,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                output.Add(new BehaviourState
                {
                    Behaviour = behaviour,
                    Enabled = behaviour.enabled
                });
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
                    state.QualityControllers);
            }
            state.Pooled = null;
            state.Effect = null;
            state.Snapshot = null;
            state.QualityControllers = null;
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
            pooled?.Return();
        }

        private static Gradient GetOrCreateGlyphGradient(float intensity)
        {
            if (_runeGradient != null
                && Mathf.Approximately(_runeGradientIntensity, intensity))
            {
                return _runeGradient;
            }
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(0.405f, 4.05f, 1.53f) * intensity,
                        0.0f),
                    new GradientColorKey(
                        new Color(1.08f, 15.75f, 5.2875f) * intensity,
                        0.45f),
                    new GradientColorKey(
                        new Color(0.27f, 3.0375f, 1.08f) * intensity,
                        1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.72f, 0.18f),
                    new GradientAlphaKey(0.72f, 0.78f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            _runeGradient = gradient;
            _runeGradientIntensity = intensity;
            return _runeGradient;
        }

        private static Gradient GetOrCreateSmokeGradient()
        {
            if (_smokeGradient != null)
            {
                return _smokeGradient;
            }
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.01f, 0.045f, 0.018f), 0.0f),
                    new GradientColorKey(new Color(0.025f, 0.15f, 0.055f), 0.45f),
                    new GradientColorKey(new Color(0.008f, 0.035f, 0.014f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.18f),
                    new GradientAlphaKey(0.72f, 0.72f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            _smokeGradient = gradient;
            return _smokeGradient;
        }

        private static Gradient GetOrCreateSparkleGradient()
        {
            if (_sparkleGradient != null)
            {
                return _sparkleGradient;
            }
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.black, 0.0f),
                    new GradientColorKey(Color.black, 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            _sparkleGradient = gradient;
            return _sparkleGradient;
        }

        private static Texture2D GetOrCreateRuneAtlas()
        {
            if (_runeAtlas != null)
            {
                return _runeAtlas;
            }
            Color32[] pixels = new Color32[RuneAtlasSize * RuneAtlasSize];
            DrawBranchedRune(pixels, 0, 0);
            DrawDiamondRune(pixels, RuneCellSize, 0);
            DrawHookedRune(pixels, 0, RuneCellSize);
            DrawBoundRune(pixels, RuneCellSize, RuneCellSize);
            _runeAtlas = new Texture2D(
                RuneAtlasSize,
                RuneAtlasSize,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = "SoulAndService_ReanimationRunes",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            _runeAtlas.SetPixels32(pixels);
            _runeAtlas.Apply(true, true);
            return _runeAtlas;
        }

        private static void DrawBranchedRune(Color32[] pixels, int x, int y)
        {
            DrawLine(pixels, x, y, 0.50f, 0.16f, 0.50f, 0.84f);
            DrawLine(pixels, x, y, 0.50f, 0.38f, 0.27f, 0.20f);
            DrawLine(pixels, x, y, 0.50f, 0.38f, 0.73f, 0.20f);
            DrawLine(pixels, x, y, 0.50f, 0.62f, 0.30f, 0.76f);
        }

        private static void DrawDiamondRune(Color32[] pixels, int x, int y)
        {
            DrawLine(pixels, x, y, 0.50f, 0.14f, 0.74f, 0.48f);
            DrawLine(pixels, x, y, 0.74f, 0.48f, 0.50f, 0.82f);
            DrawLine(pixels, x, y, 0.50f, 0.82f, 0.26f, 0.48f);
            DrawLine(pixels, x, y, 0.26f, 0.48f, 0.50f, 0.14f);
            DrawLine(pixels, x, y, 0.50f, 0.14f, 0.50f, 0.82f);
        }

        private static void DrawHookedRune(Color32[] pixels, int x, int y)
        {
            DrawLine(pixels, x, y, 0.34f, 0.18f, 0.67f, 0.82f);
            DrawLine(pixels, x, y, 0.34f, 0.18f, 0.72f, 0.30f);
            DrawLine(pixels, x, y, 0.48f, 0.47f, 0.26f, 0.58f);
            DrawLine(pixels, x, y, 0.59f, 0.68f, 0.76f, 0.59f);
        }

        private static void DrawBoundRune(Color32[] pixels, int x, int y)
        {
            DrawLine(pixels, x, y, 0.50f, 0.14f, 0.50f, 0.86f);
            DrawLine(pixels, x, y, 0.50f, 0.22f, 0.28f, 0.42f);
            DrawLine(pixels, x, y, 0.28f, 0.42f, 0.50f, 0.58f);
            DrawLine(pixels, x, y, 0.50f, 0.58f, 0.72f, 0.42f);
            DrawLine(pixels, x, y, 0.72f, 0.42f, 0.50f, 0.22f);
            DrawLine(pixels, x, y, 0.30f, 0.75f, 0.70f, 0.75f);
        }

        private static void DrawLine(
            Color32[] pixels,
            int cellX,
            int cellY,
            float normalizedX1,
            float normalizedY1,
            float normalizedX2,
            float normalizedY2)
        {
            float x1 = cellX + normalizedX1 * RuneCellSize;
            float y1 = cellY + normalizedY1 * RuneCellSize;
            float x2 = cellX + normalizedX2 * RuneCellSize;
            float y2 = cellY + normalizedY2 * RuneCellSize;
            const float coreRadius = 1.6f;
            const float glowRadius = 6.0f;
            int minimumX = Mathf.Max(cellX, Mathf.FloorToInt(Mathf.Min(x1, x2) - glowRadius));
            int maximumX = Mathf.Min(
                cellX + RuneCellSize - 1,
                Mathf.CeilToInt(Mathf.Max(x1, x2) + glowRadius));
            int minimumY = Mathf.Max(cellY, Mathf.FloorToInt(Mathf.Min(y1, y2) - glowRadius));
            int maximumY = Mathf.Min(
                cellY + RuneCellSize - 1,
                Mathf.CeilToInt(Mathf.Max(y1, y2) + glowRadius));
            float deltaX = x2 - x1;
            float deltaY = y2 - y1;
            float lengthSquared = deltaX * deltaX + deltaY * deltaY;
            for (int py = minimumY; py <= maximumY; py++)
            {
                for (int px = minimumX; px <= maximumX; px++)
                {
                    float projection = lengthSquared <= 0.0001f
                        ? 0.0f
                        : Mathf.Clamp01(
                            ((px - x1) * deltaX + (py - y1) * deltaY)
                            / lengthSquared);
                    float nearestX = x1 + projection * deltaX;
                    float nearestY = y1 + projection * deltaY;
                    float distance = Mathf.Sqrt(
                        (px - nearestX) * (px - nearestX)
                        + (py - nearestY) * (py - nearestY));
                    if (distance > glowRadius)
                    {
                        continue;
                    }
                    float alpha = distance <= coreRadius
                        ? 1.0f
                        : 0.45f * (1.0f
                            - (distance - coreRadius)
                            / (glowRadius - coreRadius));
                    int index = py * RuneAtlasSize + px;
                    byte alphaByte = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(alpha * 255.0f),
                        0,
                        255);
                    if (alphaByte > pixels[index].a)
                    {
                        pixels[index] = new Color32(255, 255, 255, alphaByte);
                    }
                }
            }
        }
    }
}
