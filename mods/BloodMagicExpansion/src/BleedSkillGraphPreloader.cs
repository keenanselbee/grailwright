using System;
using System.Collections;
using System.Globalization;
using Awaken.TG.Main.Skills;
using Awaken.TG.MVC;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BloodMagicExpansion
{
    internal sealed class BleedSkillGraphPreloader : IDisposable
    {
        private static readonly Guid BleedDependencySkillGraphGuid =
            new Guid("cd907bac-60da-de34-2865-b51650a1fc50");
        private static readonly Guid BleedSkillGraphGuid =
            new Guid("8bdba74d-7f56-4c44-bbfc-eea18f3653b7");

        private readonly MonoBehaviour _host;
        private readonly ManualLogSource _log;
        private readonly ConfigEntry<bool> _enabled;
        private readonly Func<bool> _isPluginActive;

        private Coroutine _routine;
        private StreamedSkillGraphs _observedService;
        private bool _dependencyPrimed;
        private bool _dependencyRetained;
        private bool _parentRetained;

        public BleedSkillGraphPreloader(
            MonoBehaviour host,
            ManualLogSource log,
            ConfigEntry<bool> enabled,
            Func<bool> isPluginActive)
        {
            _host = host;
            _log = log;
            _enabled = enabled;
            _isPluginActive = isPluginActive;
        }

        public void Start()
        {
            if (_routine == null)
            {
                _routine = _host.StartCoroutine(PreloadLoop());
            }
        }

        public void Dispose()
        {
            if (_routine != null)
            {
                _host.StopCoroutine(_routine);
                _routine = null;
            }

            ReleaseRetainedGraphs();
        }

        private IEnumerator PreloadLoop()
        {
            while (true)
            {
                if (!_isPluginActive()
                    || _enabled == null
                    || !_enabled.Value)
                {
                    ReleaseRetainedGraphs();
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }

                StreamedSkillGraphs current = null;
                try
                {
                    current =
                        World.Services.TryGet<StreamedSkillGraphs>();
                }
                catch
                {
                }

                if (current == null)
                {
                    ForgetRetainedGraphs();
                    yield return null;
                    continue;
                }

                if (!ReferenceEquals(_observedService, current))
                {
                    _observedService = current;
                    _dependencyPrimed = false;
                    _dependencyRetained = false;
                    _parentRetained = false;

                    // Let the newly registered gameplay service finish its
                    // current frame before starting synchronous graph work.
                    yield return null;
                    continue;
                }

                if (!_dependencyPrimed)
                {
                    if (!TryRetain(
                            current,
                            BleedDependencySkillGraphGuid,
                            "dependency"))
                    {
                        yield return new WaitForSecondsRealtime(1.0f);
                        continue;
                    }

                    _dependencyPrimed = true;
                    _dependencyRetained = true;
                    yield return null;
                    continue;
                }

                if (!_parentRetained)
                {
                    if (!TryRetain(
                            current,
                            BleedSkillGraphGuid,
                            "parent"))
                    {
                        yield return new WaitForSecondsRealtime(1.0f);
                        continue;
                    }

                    _parentRetained = true;

                    // The retained parent owns its dependency reference, so
                    // the temporary direct dependency reference can go.
                    try
                    {
                        current.Release(
                            BleedDependencySkillGraphGuid);
                        _dependencyRetained = false;
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(
                            "Could not release the temporary Bleed dependency graph reference: "
                            + ex.GetBaseException().Message);
                    }

                    yield return null;
                    continue;
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private bool TryRetain(
            StreamedSkillGraphs service,
            Guid guid,
            string label)
        {
            long startedAt =
                System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                object graph = service.Get(guid);
                if (graph == null)
                {
                    try
                    {
                        service.Release(guid);
                    }
                    catch
                    {
                    }

                    throw new InvalidOperationException(
                        "The graph service returned no graph.");
                }

                double elapsedMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp()
                        - startedAt)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency;
                _log.LogInfo(
                    "Preloaded and retained Bleed "
                    + label
                    + " skill graph "
                    + guid.ToString("D")
                    + " in "
                    + elapsedMilliseconds.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture)
                    + " ms.");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    "Could not preload Bleed "
                    + label
                    + " skill graph "
                    + guid.ToString("D")
                    + ": "
                    + ex.GetBaseException().Message);
                return false;
            }
        }

        private void ReleaseRetainedGraphs()
        {
            StreamedSkillGraphs service =
                _observedService;
            if (service != null)
            {
                if (_parentRetained)
                {
                    try
                    {
                        service.Release(
                            BleedSkillGraphGuid);
                    }
                    catch
                    {
                    }
                }

                if (_dependencyRetained)
                {
                    try
                    {
                        service.Release(
                            BleedDependencySkillGraphGuid);
                    }
                    catch
                    {
                    }
                }
            }

            ForgetRetainedGraphs();
        }

        private void ForgetRetainedGraphs()
        {
            _observedService = null;
            _dependencyPrimed = false;
            _dependencyRetained = false;
            _parentRetained = false;
        }
    }
}
