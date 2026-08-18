using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace AmbushIntegrity
{
    internal sealed class GrailFloatingTextBridge
    {
        private const string ProviderPluginGuid = "ks.tgfoa.grail-floating-text";
        private const string ApiTypeName = "GrailFloatingText.NotificationApi";

        private readonly ManualLogSource _log;
        private readonly string _sourceId;
        private bool _resolutionAttempted;
        private bool _failureLogged;
        private MethodInfo _tryShowImmediateEvent;
        private MethodInfo _tryShowEvent;

        public GrailFloatingTextBridge(ManualLogSource log, string sourceId)
        {
            _log = log;
            _sourceId = string.IsNullOrWhiteSpace(sourceId)
                ? "ks.tgfoa.ambush-integrity"
                : sourceId;
        }

        public bool TryShowAwarenessState(string text)
        {
            return TryShow(
                "ambush-integrity-awareness",
                text,
                "Status",
                "Status",
                "Normal",
                "ambush-integrity-awareness",
                "status",
                "Medium");
        }

        public bool TryShowCommittedAmbush(string text)
        {
            return TryShow(
                "ambush-integrity-clean-ambush",
                text,
                "Combat",
                "Combat",
                "High",
                string.Empty,
                "one_handed_dagger",
                "Medium");
        }

        public bool TryShowAmbushResisted(string text)
        {
            return TryShow(
                "ambush-integrity-ambush-resisted",
                text,
                "Warning",
                "Combat",
                "Normal",
                string.Empty,
                "warning",
                "Medium");
        }

        public bool TryShowDiagnostic(string text)
        {
            return TryShow(
                "ambush-integrity-diagnostic",
                text,
                "System",
                "System",
                "Low",
                "ambush-integrity-diagnostics",
                "debug",
                "Short");
        }

        public bool IsAvailable()
        {
            return TryResolve();
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
            string collapseKey,
            string iconId,
            string durationBucket)
        {
            if (string.IsNullOrWhiteSpace(text) || !TryResolve())
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
                            _sourceId,
                            eventId,
                            text,
                            style,
                            category,
                            priority,
                            collapseKey,
                            iconId,
                            durationBucket,
                            "Immediate",
                            -1.0f,
                            1.0f
                        });
                }
                else
                {
                    result = _tryShowEvent.Invoke(
                        null,
                        new object[]
                        {
                            _sourceId,
                            eventId,
                            text,
                            style,
                            category,
                            priority,
                            collapseKey,
                            iconId,
                            durationBucket,
                            -1.0f,
                            1.0f
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
                return _tryShowImmediateEvent != null || _tryShowEvent != null;
            }

            _resolutionAttempted = true;
            BepInEx.PluginInfo pluginInfo;
            if (!Chainloader.PluginInfos.TryGetValue(ProviderPluginGuid, out pluginInfo)
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
                if (_tryShowImmediateEvent == null && _tryShowEvent == null)
                {
                    throw new MissingMethodException(ApiTypeName, "TryShowEvent");
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
            if (_log != null)
            {
                _log.LogWarning(
                    "Grail Floating Text integration is unavailable; Ambush Integrity gameplay remains active: "
                    + exception.GetBaseException().Message);
            }
        }
    }
}
