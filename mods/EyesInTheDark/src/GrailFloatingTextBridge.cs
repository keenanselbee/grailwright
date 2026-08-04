using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace EyesInTheDark
{
    internal sealed class GrailFloatingTextBridge
    {
        private const string PluginGuid =
            "ks.tgfoa.grail-floating-text";
        private const string ApiTypeName =
            "GrailFloatingText.NotificationApi";

        private readonly ManualLogSource _log;
        private bool _resolutionAttempted;
        private bool _failureLogged;
        private MethodInfo _tryShowImmediateEvent;
        private MethodInfo _tryShowEvent;

        public GrailFloatingTextBridge(ManualLogSource log)
        {
            _log = log;
        }

        public bool TryShowAtmosphere(
            string eventId,
            string text,
            string collapseLane,
            bool warning)
        {
            return TryShow(
                eventId,
                text,
                warning ? "Warning" : "Wyrd",
                "Status",
                warning ? "High" : "Normal",
                collapseLane,
                "wyrd",
                "Medium");
        }

        public bool TryShowDiagnostic(
            string eventId,
            string text)
        {
            return TryShow(
                eventId,
                text,
                "System",
                "System",
                "Low",
                "eyes-in-the-dark-diagnostics",
                string.Empty,
                "Short");
        }

        public void Release()
        {
            _tryShowImmediateEvent = null;
            _tryShowEvent = null;
        }

        private bool TryShow(
            string eventId,
            string text,
            string style,
            string category,
            string priority,
            string collapseLane,
            string iconId,
            string durationBucket)
        {
            if (string.IsNullOrWhiteSpace(text)
                || !TryResolve())
            {
                return false;
            }

            try
            {
                object result;
                if (_tryShowImmediateEvent != null)
                {
                    result = _tryShowImmediateEvent.Invoke(
                        null,
                        new object[]
                        {
                            EyesInTheDarkPlugin.PluginGuid,
                            eventId,
                            text,
                            style,
                            category,
                            priority,
                            collapseLane,
                            iconId,
                            durationBucket,
                            "Immediate",
                            -1f,
                            1f
                        });
                }
                else
                {
                    result = _tryShowEvent.Invoke(
                        null,
                        new object[]
                        {
                            EyesInTheDarkPlugin.PluginGuid,
                            eventId,
                            text,
                            style,
                            category,
                            priority,
                            collapseLane,
                            iconId,
                            durationBucket,
                            -1f,
                            1f
                        });
                }

                return result is bool && (bool)result;
            }
            catch (Exception exception)
            {
                Disable(exception);
                return false;
            }
        }

        private bool TryResolve()
        {
            if (_resolutionAttempted)
            {
                return _tryShowImmediateEvent != null
                    || _tryShowEvent != null;
            }

            _resolutionAttempted = true;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(
                    PluginGuid,
                    out pluginInfo)
                || pluginInfo == null
                || pluginInfo.Instance == null)
            {
                return false;
            }

            try
            {
                Type apiType = pluginInfo.Instance.GetType().Assembly.GetType(
                    ApiTypeName,
                    false);
                if (apiType == null)
                {
                    throw new MissingMemberException(ApiTypeName);
                }

                _tryShowImmediateEvent = AccessTools.Method(
                    apiType,
                    "TryShowEvent",
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
                        typeof(string),
                        typeof(float),
                        typeof(float)
                    });
                _tryShowEvent = AccessTools.Method(
                    apiType,
                    "TryShowEvent",
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
                    });
                if (_tryShowImmediateEvent == null
                    && _tryShowEvent == null)
                {
                    throw new MissingMethodException(
                        ApiTypeName,
                        "TryShowEvent");
                }

                return true;
            }
            catch (Exception exception)
            {
                Disable(exception);
                return false;
            }
        }

        private void Disable(Exception exception)
        {
            _tryShowImmediateEvent = null;
            _tryShowEvent = null;
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            _log.LogWarning(
                "Grail Floating Text integration is unavailable; Eyes gameplay and presentation remain active: "
                + exception.GetBaseException().Message);
        }
    }
}
