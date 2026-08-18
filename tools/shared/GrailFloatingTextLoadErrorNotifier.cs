using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace Grailwright.Shared
{
    internal static class GrailFloatingTextLoadErrorNotifier
    {
        private const string GrailFloatingTextPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string GrailFloatingTextApiTypeName = "GrailFloatingText.NotificationApi";
        private const float LoadErrorFadeSeconds = 0.25f;
        private const float LoadErrorOpacity = 1.0f;

        private static bool _resolved;
        private static MethodInfo _tryShowEventWithIconMethod;

        internal static bool TryShowConfigReset(
            string sourceId,
            string sourceName,
            int previousSchemaVersion,
            int currentSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "grailwright";
            }

            return TryShowConfigReset(
                sourceId,
                BuildConfigResetMessage(sourceName, previousSchemaVersion, currentSchemaVersion));
        }

        internal static string BuildConfigResetMessage(
            string sourceName,
            int previousSchemaVersion,
            int currentSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                sourceName = "Grailwright mod";
            }

            return sourceName.Trim()
                + " config reset: schema "
                + previousSchemaVersion
                + " to "
                + currentSchemaVersion
                + ".";
        }

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

        internal static bool TryShowSystemNotification(
            string sourceId,
            string eventId,
            string text,
            string priority = "Normal",
            string collapseKey = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "grailwright";
            }
            if (string.IsNullOrWhiteSpace(eventId)
                || string.IsNullOrWhiteSpace(text)
                || !TryResolve())
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(priority))
            {
                priority = "Normal";
            }
            if (string.IsNullOrWhiteSpace(collapseKey))
            {
                collapseKey = eventId;
            }

            try
            {
                object result = _tryShowEventWithIconMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceId,
                        eventId,
                        text.Trim(),
                        "System",
                        "System",
                        priority,
                        collapseKey,
                        "system",
                        "System",
                        -1.0f,
                        1.0f
                    });

                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryShowEventNotification(
            string sourceId,
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseKey,
            string iconId,
            string durationBucket,
            float fadeSeconds = -1.0f,
            float opacity = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "grailwright";
            }
            if (string.IsNullOrWhiteSpace(eventId)
                || string.IsNullOrWhiteSpace(text)
                || !TryResolve())
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(collapseKey))
            {
                collapseKey = eventId;
            }

            try
            {
                object result = _tryShowEventWithIconMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceId,
                        eventId,
                        text.Trim(),
                        style,
                        category,
                        priority,
                        collapseKey,
                        iconId,
                        durationBucket,
                        fadeSeconds,
                        opacity
                    });

                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryShowDiagnosticNotification(
            string sourceId,
            string eventId,
            string text,
            string collapseKey = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "grailwright";
            }
            if (string.IsNullOrWhiteSpace(eventId)
                || string.IsNullOrWhiteSpace(text)
                || !TryResolve())
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(collapseKey))
            {
                collapseKey = eventId;
            }

            try
            {
                object result = _tryShowEventWithIconMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceId,
                        eventId,
                        text.Trim(),
                        "System",
                        "System",
                        "Low",
                        collapseKey,
                        "system",
                        "Short",
                        -1.0f,
                        1.0f
                    });

                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryShowConfigReset(string sourceId, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !TryResolve())
            {
                return false;
            }

            try
            {
                object result = _tryShowEventWithIconMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceId,
                        "config-reset",
                        text,
                        "System",
                        "System",
                        "High",
                        "config-reset",
                        "system",
                        "System",
                        -1.0f,
                        1.0f
                    });

                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryShow(string sourceId, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !TryResolve())
            {
                return false;
            }

            try
            {
                object result = _tryShowEventWithIconMethod.Invoke(
                    null,
                    new object[]
                    {
                        sourceId,
                        "load-time-error",
                        text,
                        "Error",
                        "System",
                        "Critical",
                        "load-time-error",
                        "debug",
                        "System",
                        LoadErrorFadeSeconds,
                        LoadErrorOpacity
                    });

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
                return _tryShowEventWithIconMethod != null;
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

            _tryShowEventWithIconMethod = apiType.GetMethod(
                "TryShowEvent",
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
                    typeof(string),
                    typeof(string),
                    typeof(float),
                    typeof(float)
                },
                null);

            _resolved = _tryShowEventWithIconMethod != null;
            return _resolved;
        }
    }
}
