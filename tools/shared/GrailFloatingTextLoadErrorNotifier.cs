using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Grailwright.Shared
{
    internal static class GrailFloatingTextLoadErrorNotifier
    {
        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const float LoadErrorDurationSeconds = 10.0f;
        private const float LoadErrorFadeSeconds = 0.25f;
        private const float LoadErrorOpacity = 1.0f;

        private static bool _resolved;
        private static MethodInfo _tryShowWithIconMethod;
        private static MethodInfo _tryShowMethod;

        internal static bool TryShowLoadTimeError(string sourceId, string sourceName, Exception exception)
        {
            return TryShowLoadTimeError(sourceId, sourceName, "load-time error. Check BepInEx log.");
        }

        internal static bool TryShowLoadTimeError(string sourceId, string sourceName, string message)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "grailwright";
            }

            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = "Grailwright mod";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "load-time error. Check BepInEx log.";
            }

            return TryShow(sourceId, sourceName.Trim() + " " + message.Trim());
        }

        private static bool TryShow(string sourceId, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !TryResolve())
            {
                return false;
            }

            try
            {
                object result;
                if (_tryShowWithIconMethod != null)
                {
                    result = _tryShowWithIconMethod.Invoke(
                        null,
                        new object[]
                        {
                            sourceId,
                            text,
                            "Error",
                            "System",
                            "Critical",
                            "load-time-error",
                            "debug",
                            LoadErrorDurationSeconds,
                            LoadErrorFadeSeconds,
                            LoadErrorOpacity
                        });
                }
                else
                {
                    result = _tryShowMethod.Invoke(
                        null,
                        new object[]
                        {
                            sourceId,
                            text,
                            "Error",
                            "System",
                            "Critical",
                            "load-time-error",
                            LoadErrorDurationSeconds,
                            LoadErrorFadeSeconds,
                            LoadErrorOpacity
                        });
                }

                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolve()
        {
            if (_resolved)
            {
                return _tryShowWithIconMethod != null || _tryShowMethod != null;
            }

            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(GrailFloatingTextPluginGuid, out pluginInfo) ||
                pluginInfo == null ||
                pluginInfo.Instance == null)
            {
                return false;
            }

            Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(GrailFloatingTextApiTypeName, false);
            if (apiType == null)
            {
                return false;
            }

            _tryShowWithIconMethod = apiType.GetMethod(
                "TryShow",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                null);

            _tryShowMethod = apiType.GetMethod(
                "TryShow",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                },
                null);

            _resolved = _tryShowWithIconMethod != null || _tryShowMethod != null;
            return _resolved;
        }
    }
}
