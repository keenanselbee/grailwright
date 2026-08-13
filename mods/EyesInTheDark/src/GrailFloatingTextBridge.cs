using System;
using System.Collections.Generic;
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
        private MethodInfo _trySetBuiltInEventClaim;
        private readonly HashSet<string> _activeBuiltInEventClaims =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public GrailFloatingTextBridge(ManualLogSource log)
        {
            _log = log;
        }

        public bool TryShowAtmosphere(
            string eventId,
            string text,
            string collapseLane,
            bool warning,
            WyrdnessPalette palette)
        {
            return TryShow(
                eventId,
                text,
                palette == WyrdnessPalette.NativeOrange
                    ? "Orange"
                    : "Purple",
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

        public bool TrySetBuiltInEventClaim(
            string eventId,
            bool active)
        {
            if (string.IsNullOrWhiteSpace(eventId)
                || !TryResolve()
                || _trySetBuiltInEventClaim == null)
            {
                return false;
            }

            try
            {
                object result = _trySetBuiltInEventClaim.Invoke(
                    null,
                    new object[]
                    {
                        EyesInTheDarkPlugin.PluginGuid,
                        eventId,
                        active
                    });
                bool accepted = result is bool && (bool)result;
                if (accepted)
                {
                    if (active)
                    {
                        _activeBuiltInEventClaims.Add(eventId);
                    }
                    else
                    {
                        _activeBuiltInEventClaims.Remove(eventId);
                    }
                }

                return accepted;
            }
            catch (Exception exception)
            {
                Disable(exception);
                return false;
            }
        }

        public void Release()
        {
            ReleaseBuiltInEventClaims();
            _tryShowImmediateEvent = null;
            _tryShowEvent = null;
            _trySetBuiltInEventClaim = null;
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
                    || _tryShowEvent != null
                    || _trySetBuiltInEventClaim != null;
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
                _trySetBuiltInEventClaim = AccessTools.Method(
                    apiType,
                    "TrySetBuiltInEventClaim",
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(bool)
                    });
                if (_tryShowImmediateEvent == null
                    && _tryShowEvent == null
                    && _trySetBuiltInEventClaim == null)
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
            ReleaseBuiltInEventClaims();
            _tryShowImmediateEvent = null;
            _tryShowEvent = null;
            _trySetBuiltInEventClaim = null;
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            _log.LogWarning(
                "Grail Floating Text integration is unavailable; Eyes gameplay and presentation remain active: "
                + exception.GetBaseException().Message);
        }

        private void ReleaseBuiltInEventClaims()
        {
            if (_trySetBuiltInEventClaim != null)
            {
                string[] eventIds = new string[
                    _activeBuiltInEventClaims.Count];
                _activeBuiltInEventClaims.CopyTo(eventIds);
                for (int i = 0; i < eventIds.Length; i++)
                {
                    try
                    {
                        _trySetBuiltInEventClaim.Invoke(
                            null,
                            new object[]
                            {
                                EyesInTheDarkPlugin.PluginGuid,
                                eventIds[i],
                                false
                            });
                    }
                    catch
                    {
                    }
                }
            }

            _activeBuiltInEventClaims.Clear();
        }
    }
}
