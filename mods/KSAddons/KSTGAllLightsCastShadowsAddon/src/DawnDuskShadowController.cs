using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Awaken.TG.Main.Timing;
using Awaken.TG.MVC;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameDayNightSystem = Awaken.TG.Graphics.DayNightSystem.DayNightSystem;

namespace TGAllLightsCastShadowsAddon
{
    public sealed partial class Plugin
    {
        private const string DawnDuskBlendMinutesFieldName =
            "shadowIntensityBlendMinutes";
        private static readonly FieldInfo DawnDuskBlendMinutesField =
            typeof(GameDayNightSystem).GetField(
                DawnDuskBlendMinutesFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        private ConfigEntry<bool> _improveDawnDuskShadows;
        private ConfigEntry<int> _dawnDuskShadowBlendMinutes;
        private ConfigEntry<bool> _normalizeDawnDuskForEyesInTheDark;
        private ConfigEntry<float> _eyesDawnDuskSecondsPerSide;
        private readonly Dictionary<int, DawnDuskShadowState>
            _dawnDuskShadowStates =
                new Dictionary<int, DawnDuskShadowState>();
        private readonly HashSet<int> _foundDawnDuskSystems =
            new HashSet<int>();
        private readonly List<int> _staleDawnDuskSystemIds =
            new List<int>();
        private bool _eyesInTheDarkInstalled;
        private bool _dawnDuskSettingsDirty;
        private bool _hasDawnDuskSceneSnapshot;
        private bool _dawnDuskReflectionWarningLogged;
        private bool _dawnDuskOwnershipWarningLogged;
        private int _dawnDuskSceneSignature;
        private int _lastReportedDawnDuskBlendMinutes = -1;
        private float _nextDawnDuskUpdate;

        private void BindDawnDuskShadowConfig()
        {
            _improveDawnDuskShadows = Config.Bind(
                "Directional Shadows",
                "ImproveDawnDuskShadows",
                false,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Shortens the weak directional-shadow period around the existing sun/moon handoff without creating another directional light or shadow map. Disabled by default so the visual change remains opt-in.",
                    "Directional Shadows", "Improve Dawn and Dusk Shadows", 15, 0));
            _dawnDuskShadowBlendMinutes = Config.Bind(
                "Directional Shadows",
                "ShadowBlendMinutes",
                10,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "In-game minutes on each side of the handoff when Eyes in the Dark normalization is unavailable or disabled. The game normally uses 60 minutes.",
                    "Directional Shadows", "Shadow Blend Minutes", 15, 10,
                    new AcceptableValueRange<int>(1, 120)));
            _normalizeDawnDuskForEyesInTheDark = Config.Bind(
                "Directional Shadows",
                "NormalizeForEyesInTheDark",
                true,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "When Eyes in the Dark is installed, converts a real-seconds target through its live world-clock rate so threat-scaled nights and daylight keep a stable visual transition.",
                    "Directional Shadows", "Normalize for Eyes in the Dark", 15, 20));
            _eyesDawnDuskSecondsPerSide = Config.Bind(
                "Directional Shadows",
                "EyesBlendSecondsPerSide",
                30f,
                Grailwright.Shared.ConfigUiDescription.Create(
                    "Target real seconds on each side of dawn and dusk while Eyes in the Dark normalization is active. Thirty seconds aligns with half of Eyes' default 60-second dusk presentation fade.",
                    "Directional Shadows", "Eyes Blend Seconds Per Side", 15, 30,
                    new AcceptableValueRange<float>(1f, 300f)));
        }

        private void InitializeDawnDuskShadowController()
        {
            _eyesInTheDarkInstalled = Chainloader.PluginInfos.ContainsKey(
                EyesInTheDarkPluginGuid);
            _dawnDuskSettingsDirty = true;
            _nextDawnDuskUpdate = 0f;
            if (_eyesInTheDarkInstalled)
            {
                Logger.LogInfo(
                    "Eyes in the Dark detected; optional dawn/dusk shadows can use live real-time normalization.");
            }
        }

        private void SubscribeDawnDuskShadowConfigEvents()
        {
            _improveDawnDuskShadows.SettingChanged +=
                OnDawnDuskShadowSettingChanged;
            _dawnDuskShadowBlendMinutes.SettingChanged +=
                OnDawnDuskShadowSettingChanged;
            _normalizeDawnDuskForEyesInTheDark.SettingChanged +=
                OnDawnDuskShadowSettingChanged;
            _eyesDawnDuskSecondsPerSide.SettingChanged +=
                OnDawnDuskShadowSettingChanged;
        }

        private void UnsubscribeDawnDuskShadowConfigEvents()
        {
            if (_improveDawnDuskShadows == null)
            {
                return;
            }
            _improveDawnDuskShadows.SettingChanged -=
                OnDawnDuskShadowSettingChanged;
            _dawnDuskShadowBlendMinutes.SettingChanged -=
                OnDawnDuskShadowSettingChanged;
            _normalizeDawnDuskForEyesInTheDark.SettingChanged -=
                OnDawnDuskShadowSettingChanged;
            _eyesDawnDuskSecondsPerSide.SettingChanged -=
                OnDawnDuskShadowSettingChanged;
        }

        private void OnDawnDuskShadowSettingChanged(
            object sender,
            EventArgs args)
        {
            _dawnDuskSettingsDirty = true;
            _nextDawnDuskUpdate = 0f;
            if (!_improveDawnDuskShadows.Value)
            {
                RestoreAllDawnDuskShadowSystems("feature disabled");
            }
        }

        private void UpdateDawnDuskShadows()
        {
            if (_improveDawnDuskShadows == null
                || Time.unscaledTime < _nextDawnDuskUpdate)
            {
                return;
            }
            _nextDawnDuskUpdate = Time.unscaledTime + 0.25f;
            if (!_improveDawnDuskShadows.Value)
            {
                if (_dawnDuskShadowStates.Count > 0)
                {
                    RestoreAllDawnDuskShadowSystems("feature disabled");
                }
                return;
            }
            if (DawnDuskBlendMinutesField == null)
            {
                ReportDawnDuskReflectionWarning(
                    "Could not find DayNightSystem."
                    + DawnDuskBlendMinutesFieldName
                    + "; dawn/dusk shadows retain the game's original transition.");
                return;
            }

            int sceneSignature = CalculateLoadedDawnDuskSceneSignature();
            int blendMinutes = ResolveDawnDuskBlendMinutes();
            if (_dawnDuskSettingsDirty
                || !_hasDawnDuskSceneSnapshot
                || sceneSignature != _dawnDuskSceneSignature)
            {
                DiscoverDawnDuskShadowSystems(blendMinutes);
                _dawnDuskSceneSignature = sceneSignature;
                _hasDawnDuskSceneSnapshot = true;
                _dawnDuskSettingsDirty = false;
            }
            else
            {
                ApplyTrackedDawnDuskShadowSystems(blendMinutes);
            }

            if (blendMinutes != _lastReportedDawnDuskBlendMinutes)
            {
                _lastReportedDawnDuskBlendMinutes = blendMinutes;
                Logger.LogInfo(
                    "Dawn/dusk directional-shadow blend applied: "
                    + blendMinutes.ToString(CultureInfo.InvariantCulture)
                    + " in-game minute(s) per side"
                    + (UsesEyesDawnDuskNormalization()
                        ? ", normalized to approximately "
                            + _eyesDawnDuskSecondsPerSide.Value.ToString(
                                "0.#",
                                CultureInfo.InvariantCulture)
                            + " real second(s) through Eyes in the Dark."
                        : "."));
            }
        }

        private int ResolveDawnDuskBlendMinutes()
        {
            int configuredMinutes = _dawnDuskShadowBlendMinutes.Value;
            if (!UsesEyesDawnDuskNormalization())
            {
                return SafeShadowSelectionRules.ResolveDawnDuskBlendMinutes(
                    configuredMinutes,
                    false,
                    _eyesDawnDuskSecondsPerSide.Value,
                    0f);
            }

            float weatherSecondsPerRealSecond = 0f;
            try
            {
                GameRealTime clock = World.Any<GameRealTime>();
                if (clock != null && !clock.HasBeenDiscarded)
                {
                    weatherSecondsPerRealSecond =
                        clock.WeatherSecondsPerRealSecond;
                }
            }
            catch
            {
            }
            return SafeShadowSelectionRules.ResolveDawnDuskBlendMinutes(
                configuredMinutes,
                true,
                _eyesDawnDuskSecondsPerSide.Value,
                weatherSecondsPerRealSecond);
        }

        private bool UsesEyesDawnDuskNormalization()
        {
            return _eyesInTheDarkInstalled
                && _normalizeDawnDuskForEyesInTheDark.Value;
        }

        private void DiscoverDawnDuskShadowSystems(int blendMinutes)
        {
            _foundDawnDuskSystems.Clear();
            GameDayNightSystem[] systems =
                UnityEngine.Object.FindObjectsByType<GameDayNightSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                GameDayNightSystem system = systems[i];
                if (!IsLoadedDawnDuskSystem(system))
                {
                    continue;
                }
                int id = system.GetInstanceID();
                _foundDawnDuskSystems.Add(id);
                DawnDuskShadowState state;
                if (!_dawnDuskShadowStates.TryGetValue(id, out state))
                {
                    try
                    {
                        state = new DawnDuskShadowState(
                            system,
                            (int)DawnDuskBlendMinutesField.GetValue(system));
                        _dawnDuskShadowStates.Add(id, state);
                    }
                    catch (Exception exception)
                    {
                        ReportDawnDuskReflectionWarning(
                            "Could not capture the dawn/dusk shadow transition: "
                            + exception.GetBaseException().Message);
                        continue;
                    }
                }
                ApplyDawnDuskBlend(state, blendMinutes);
            }

            _staleDawnDuskSystemIds.Clear();
            foreach (int id in _dawnDuskShadowStates.Keys)
            {
                if (!_foundDawnDuskSystems.Contains(id))
                {
                    _staleDawnDuskSystemIds.Add(id);
                }
            }
            for (int i = 0; i < _staleDawnDuskSystemIds.Count; i++)
            {
                int id = _staleDawnDuskSystemIds[i];
                DawnDuskShadowState state;
                if (_dawnDuskShadowStates.TryGetValue(id, out state))
                {
                    RestoreDawnDuskShadowState(state);
                }
                _dawnDuskShadowStates.Remove(id);
            }
        }

        private void ApplyTrackedDawnDuskShadowSystems(int blendMinutes)
        {
            _staleDawnDuskSystemIds.Clear();
            foreach (KeyValuePair<int, DawnDuskShadowState> pair
                in _dawnDuskShadowStates)
            {
                if (!IsDawnDuskSystemAlive(pair.Value.System))
                {
                    _staleDawnDuskSystemIds.Add(pair.Key);
                    continue;
                }
                ApplyDawnDuskBlend(pair.Value, blendMinutes);
            }
            for (int i = 0; i < _staleDawnDuskSystemIds.Count; i++)
            {
                _dawnDuskShadowStates.Remove(
                    _staleDawnDuskSystemIds[i]);
            }
            if (_staleDawnDuskSystemIds.Count > 0)
            {
                _hasDawnDuskSceneSnapshot = false;
            }
        }

        private void ApplyDawnDuskBlend(
            DawnDuskShadowState state,
            int blendMinutes)
        {
            try
            {
                int current = (int)DawnDuskBlendMinutesField.GetValue(
                    state.System);
                if (state.OwnsValue && current != state.LastAppliedBlendMinutes)
                {
                    state.OwnsValue = false;
                    ReportDawnDuskOwnershipWarning();
                    return;
                }
                if (!state.OwnsValue && state.HasApplied)
                {
                    return;
                }
                if (current != blendMinutes)
                {
                    DawnDuskBlendMinutesField.SetValue(
                        state.System,
                        blendMinutes);
                }
                state.LastAppliedBlendMinutes = blendMinutes;
                state.HasApplied = true;
                state.OwnsValue = true;
            }
            catch (Exception exception)
            {
                ReportDawnDuskReflectionWarning(
                    "Could not apply the dawn/dusk shadow transition: "
                    + exception.GetBaseException().Message);
            }
        }

        private void BeforeDawnDuskSceneTransition()
        {
            RestoreAllDawnDuskShadowSystems("scene transition");
            _dawnDuskSettingsDirty = true;
            _nextDawnDuskUpdate = 0f;
        }

        private void RestoreAllDawnDuskShadowSystems(string reason)
        {
            if (_dawnDuskShadowStates.Count == 0)
            {
                return;
            }
            int restored = 0;
            foreach (DawnDuskShadowState state in
                _dawnDuskShadowStates.Values)
            {
                if (RestoreDawnDuskShadowState(state))
                {
                    restored++;
                }
            }
            _dawnDuskShadowStates.Clear();
            _foundDawnDuskSystems.Clear();
            _staleDawnDuskSystemIds.Clear();
            _hasDawnDuskSceneSnapshot = false;
            _dawnDuskSceneSignature = 0;
            _lastReportedDawnDuskBlendMinutes = -1;
            if (restored > 0 && _diagnostics != null && _diagnostics.Value)
            {
                Logger.LogInfo(
                    "Restored "
                    + restored.ToString(CultureInfo.InvariantCulture)
                    + " dawn/dusk shadow setting(s) ("
                    + reason
                    + ").");
            }
        }

        private bool RestoreDawnDuskShadowState(DawnDuskShadowState state)
        {
            if (!state.OwnsValue
                || !IsDawnDuskSystemAlive(state.System)
                || DawnDuskBlendMinutesField == null)
            {
                return false;
            }
            try
            {
                int current = (int)DawnDuskBlendMinutesField.GetValue(
                    state.System);
                if (current != state.LastAppliedBlendMinutes)
                {
                    state.OwnsValue = false;
                    ReportDawnDuskOwnershipWarning();
                    return false;
                }
                DawnDuskBlendMinutesField.SetValue(
                    state.System,
                    state.OriginalBlendMinutes);
                state.OwnsValue = false;
                return true;
            }
            catch (Exception exception)
            {
                ReportDawnDuskReflectionWarning(
                    "Could not restore the dawn/dusk shadow transition: "
                    + exception.GetBaseException().Message);
                return false;
            }
        }

        private void ReportDawnDuskReflectionWarning(string message)
        {
            if (_dawnDuskReflectionWarningLogged)
            {
                return;
            }
            _dawnDuskReflectionWarningLogged = true;
            Logger.LogWarning(message);
        }

        private void ReportDawnDuskOwnershipWarning()
        {
            if (_dawnDuskOwnershipWarningLogged)
            {
                return;
            }
            _dawnDuskOwnershipWarningLogged = true;
            Logger.LogWarning(
                "Dawn/dusk shadow ownership was released because another system changed the blend after this addon; the external value will not be overwritten or restored.");
        }

        private static int CalculateLoadedDawnDuskSceneSignature()
        {
            unchecked
            {
                int signature = 17;
                int sceneCount = SceneManager.sceneCount;
                signature = signature * 31 + sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    signature = signature * 31 + scene.handle;
                    signature = signature * 31 + (scene.isLoaded ? 1 : 0);
                }
                return signature;
            }
        }

        private static bool IsLoadedDawnDuskSystem(GameDayNightSystem system)
        {
            if (!IsDawnDuskSystemAlive(system))
            {
                return false;
            }
            Scene scene = system.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool IsDawnDuskSystemAlive(GameDayNightSystem system)
        {
            try
            {
                return system != null && system.gameObject != null;
            }
            catch
            {
                return false;
            }
        }

        private sealed class DawnDuskShadowState
        {
            internal readonly GameDayNightSystem System;
            internal readonly int OriginalBlendMinutes;
            internal int LastAppliedBlendMinutes;
            internal bool HasApplied;
            internal bool OwnsValue;

            internal DawnDuskShadowState(
                GameDayNightSystem system,
                int originalBlendMinutes)
            {
                System = system;
                OriginalBlendMinutes = originalBlendMinutes;
            }
        }
    }
}
