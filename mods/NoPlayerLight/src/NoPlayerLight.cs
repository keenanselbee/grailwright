using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: AssemblyTitle("No Player Light")]
[assembly: AssemblyDescription("Disables the player HeroLight object in Tainted Grail: The Fall of Avalon")]
[assembly: AssemblyCompany("Keenan")]
[assembly: AssemblyProduct("No Player Light")]
[assembly: AssemblyVersion("1.3.5.0")]
[assembly: AssemblyFileVersion("1.3.5.0")]

namespace NoPlayerLight
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ks.tgfoa.grail-floating-text", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ks.tgfoa.no-player-light";
        public const string PluginName = "No Player Light";
        public const string PluginVersion = "1.3.5";

        private const string HeroLightObjectName = "HeroLight";

        private static readonly float[] SceneLoadRetryDelays = { 0f, 0.25f, 1f, 2f, 5f };

        private Coroutine _sceneLoadRetries;

        private void Awake()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                DisableHeroLight();

                Logger.LogInfo(
                    PluginName
                    + " "
                    + PluginVersion
                    + " loaded; HeroLight scans run only during startup and scene-load retries.");
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginName + " failed to initialize: " + exception);
                Grailwright.Shared.GrailFloatingTextLoadErrorNotifier.TryShowLoadTimeError(PluginGuid, PluginName, exception);
                SceneManager.sceneLoaded -= OnSceneLoaded;
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_sceneLoadRetries != null)
            {
                StopCoroutine(_sceneLoadRetries);
                _sceneLoadRetries = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_sceneLoadRetries != null)
            {
                StopCoroutine(_sceneLoadRetries);
            }

            _sceneLoadRetries = StartCoroutine(SceneLoadRetryScans());
        }

        private IEnumerator SceneLoadRetryScans()
        {
            for (int i = 0; i < SceneLoadRetryDelays.Length; i++)
            {
                float delay = SceneLoadRetryDelays[i];
                if (delay > 0f)
                {
                    yield return new WaitForSecondsRealtime(delay);
                }

                DisableHeroLight();
            }

            _sceneLoadRetries = null;
        }

        private void DisableHeroLight()
        {
            GameObject heroLight = GameObject.Find(HeroLightObjectName);
            if (heroLight != null)
            {
                heroLight.SetActive(false);
            }
        }
    }
}
