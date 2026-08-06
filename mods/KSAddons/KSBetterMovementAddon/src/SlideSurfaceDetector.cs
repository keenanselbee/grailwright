using System;
using System.Reflection;
using Awaken.TG.Main.Heroes.Combat;
using Awaken.TG.Main.Heroes.FootSteps;
using Awaken.TG.Main.Heroes.MovementSystems;
using Awaken.TG.Main.Utility.Animations;
using Awaken.TG.Main.Utility.Terrain;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace Keenan.TGFoA.BetterMovementAddon
{
    internal sealed class SlideSurfaceDetector : IDisposable
    {
        private const int GroundMask = 26697;
        private const float RayStartHeight = 0.2f;
        private const float RayDistance = 0.5f;

        private static readonly FieldInfo SplatmapsSampleShaderField =
            AccessTools.Field(typeof(VHeroFootsteps), "splatmapsSampleShader");
        private static readonly FieldInfo WalkOnWaterField =
            AccessTools.Field(typeof(VHeroFootsteps), "_walkOnWater");

        private readonly BetterMovementAddonPlugin _plugin;
        private readonly ManualLogSource _logger;
        private readonly FMODParameter[] _surfaceParameters;

        private VHeroFootsteps _heroFootsteps;
        private FootstepCategoryProvider _provider;
        private bool _requestPending;
        private bool _providerFailureLogged;
        private int _activeGeneration;
        private int _requestSerial;
        private bool _disposed;

        internal SlideSurfaceDetector(
            BetterMovementAddonPlugin plugin,
            ManualLogSource logger)
        {
            _plugin = plugin;
            _logger = logger;
            SurfaceType[] terrainTypes = SurfaceType.TerrainTypes;
            _surfaceParameters = new FMODParameter[terrainTypes.Length];
            for (int index = 0; index < terrainTypes.Length; index++)
            {
                _surfaceParameters[index] = new FMODParameter(
                    terrainTypes[index].FModParameterName,
                    0f);
            }
        }

        internal void BeginSlide(int generation)
        {
            _activeGeneration = generation;
        }

        internal void EndSlide(int generation)
        {
            _activeGeneration = generation;
        }

        internal void RequestSurface(
            HumanoidMovementBase movement,
            int generation,
            Action<int, string> completed)
        {
            if (_disposed
                || _requestPending
                || generation != _activeGeneration
                || completed == null)
            {
                return;
            }

            VHeroController controller;
            if (!BetterMovementAddonPlugin.TryGetController(movement, out controller)
                || controller == null)
            {
                return;
            }

            EnsureProvider(controller);
            if (IsWalkingOnWater())
            {
                completed(generation, SurfaceType.TerrainPuddle.FModParameterName);
                return;
            }

            RaycastHit hit;
            Vector3 rayStart = controller.Transform.position + Vector3.up * RayStartHeight;
            if (!Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out hit,
                    RayDistance,
                    GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            IFootstepSource terrainSource =
                hit.collider.GetComponentInParent(typeof(IFootstepSource)) as IFootstepSource;
            if (terrainSource != null)
            {
                if (_provider == null)
                {
                    completed(generation, SurfaceType.TerrainGround.FModParameterName);
                    return;
                }

                Texture2D[] splatmaps;
                int[] fmodIndices;
                Vector2 uv;
                terrainSource.GetSampleData(hit, out splatmaps, out fmodIndices, out uv);
                int serial = ++_requestSerial;
                _requestPending = true;
                ResolveTerrainAsync(
                    _provider,
                    splatmaps,
                    fmodIndices,
                    uv,
                    generation,
                    serial,
                    completed).Forget();
                return;
            }

            string surface = SurfaceType.TerrainGround.FModParameterName;
            MeshSurfaceType meshSurface = hit.collider.GetComponentInParent<MeshSurfaceType>();
            if (meshSurface != null && meshSurface.SurfaceType != null)
            {
                surface = meshSurface.SurfaceType.FModParameterName;
            }

            completed(generation, surface);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _requestSerial++;
            if (_provider != null)
            {
                try
                {
                    _provider.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Slide terrain sampler cleanup failed: "
                        + exception.GetBaseException().Message);
                }

                _provider = null;
            }

            _heroFootsteps = null;
        }

        private async UniTaskVoid ResolveTerrainAsync(
            FootstepCategoryProvider provider,
            Texture2D[] splatmaps,
            int[] fmodIndices,
            Vector2 uv,
            int generation,
            int serial,
            Action<int, string> completed)
        {
            try
            {
                FootStepsUtils.ClearParameters(_surfaceParameters);
                await provider.FillFootsteps(
                    splatmaps,
                    fmodIndices,
                    uv,
                    _surfaceParameters,
                    SurfaceType.TerrainGround.FModParameterName);

                if (_disposed
                    || serial != _requestSerial
                    || generation != _activeGeneration)
                {
                    return;
                }

                completed(generation, SelectStrongestSurface());
            }
            catch (Exception exception)
            {
                if (!_disposed && serial == _requestSerial)
                {
                    _logger.LogWarning(
                        "Slide terrain sampling failed; using ground audio for this sample: "
                        + exception.GetBaseException().Message);
                    completed(generation, SurfaceType.TerrainGround.FModParameterName);
                }
            }
            finally
            {
                if (serial == _requestSerial)
                {
                    _requestPending = false;
                }
            }
        }

        private void EnsureProvider(VHeroController controller)
        {
            if (_provider != null && _heroFootsteps != null)
            {
                return;
            }

            if (_requestPending)
            {
                return;
            }

            VHeroFootsteps footsteps = controller.GetComponentInChildren<VHeroFootsteps>(true);
            if (footsteps == null)
            {
                footsteps = controller.GetComponentInParent<VHeroFootsteps>();
            }
            if (footsteps == null)
            {
                footsteps = UnityEngine.Object.FindAnyObjectByType<VHeroFootsteps>();
            }
            if (footsteps == null)
            {
                LogProviderFailureOnce("Could not find the hero footstep component.");
                return;
            }

            if (SplatmapsSampleShaderField == null)
            {
                LogProviderFailureOnce("Could not find VHeroFootsteps.splatmapsSampleShader.");
                return;
            }

            ComputeShader shader =
                SplatmapsSampleShaderField.GetValue(footsteps) as ComputeShader;
            if (shader == null)
            {
                LogProviderFailureOnce("The hero footstep splatmap shader is not ready.");
                return;
            }

            if (_provider != null)
            {
                _provider.Dispose();
            }

            _heroFootsteps = footsteps;
            _provider = new FootstepCategoryProvider(shader);
            _providerFailureLogged = false;
            if (_plugin.DiagnosticsEnabled)
            {
                _logger.LogInfo("Initialized the dedicated slide terrain splatmap sampler.");
            }
        }

        private bool IsWalkingOnWater()
        {
            if (_heroFootsteps == null || WalkOnWaterField == null)
            {
                return false;
            }

            try
            {
                object value = WalkOnWaterField.GetValue(_heroFootsteps);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private string SelectStrongestSurface()
        {
            string strongest = SurfaceType.TerrainGround.FModParameterName;
            float strongestValue = 0f;
            for (int index = 0; index < _surfaceParameters.Length; index++)
            {
                FMODParameter parameter = _surfaceParameters[index];
                if (parameter.value > strongestValue)
                {
                    strongest = parameter.name;
                    strongestValue = parameter.value;
                }
            }

            return strongest;
        }

        private void LogProviderFailureOnce(string message)
        {
            if (_providerFailureLogged)
            {
                return;
            }

            _providerFailureLogged = true;
            _logger.LogWarning(
                message
                + " Terrain meshes still use their explicit surface type; blended terrain temporarily falls back to ground audio.");
        }
    }
}
