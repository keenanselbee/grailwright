using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Awaken.TG.MVC;
using Awaken.TG.MVC.Elements;
using Awaken.TG.MVC.Events;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Fights.DamageInfo;
using Awaken.TG.Main.Fights.Factions;
using Awaken.TG.Main.Fights.NPCs;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Locations;
using Awaken.TG.Main.Locations.Attachments.Elements;
using Awaken.TG.Main.Memories;
using Awaken.TG.Main.Stories.Quests;
using Awaken.TG.Main.Templates;
using Awaken.TG.Main.UI.TitleScreen.Loading;
using BepInEx.Logging;
using UnityEngine;

namespace AvalonUntold
{
    internal static class Plugin
    {
        internal static ManualLogSourceShim Log;
        internal static string AutoScanBlocked;
    }

    internal sealed class GlowController : IListenerOwner
    {
        private struct Pending
        {
            public string Id;
            public NpcElement Npc;
        }

        private static readonly Color GoldenColour = new Color(1f, 0.776f, 0.294f, 1f);
        private readonly Queue<Pending> _queue = new Queue<Pending>();
        private readonly HashSet<string> _queued = new HashSet<string>(StringComparer.Ordinal);
        private readonly Stopwatch _bakeClock = new Stopwatch();

        private bool _installed;
        private bool _active;
        private bool _broken;
        private bool _displayReady;
        private bool _indexDirty;
        private bool _sweepNeeded;
        private GlowMode _mode = GlowMode.Balanced;
        private float _maxDistance = 20f;
        private float _bakeBudgetMilliseconds = 1.5f;
        private float _availabilityRefreshSeconds = 15f;
        private int _scanBudgetMilliseconds = 5;
        private int _bakeCursor;
        private Camera _cachedCamera;
        private float _nextCameraProbeAt;
        private float _nextOutlineInstallAt;
        private GameplayMemory _observedMemory;
        private QuestScanner _scanner;
        private ScanReport _scanReport;
        private IEnumerator _scanJob;
        private int _scanAttempts;
        private float _nextScanAttemptAt;
        private IEnumerator _rebuildJob;
        private float _lastAvailabilityRefreshAt;
        private float _nextAvailabilityRefreshAt;
        private float _nextRebuildAttemptAt;
        private float _nextIdleScanPollAt;

        public bool CanReceiveEvents => true;

        internal static GlowMode SelectionMode { get; private set; } = GlowMode.Balanced;

        internal static GlowMode CurrentMode()
        {
            return SelectionMode;
        }

        internal GlowController(ManualLogSource logger)
        {
            Plugin.Log = new ManualLogSourceShim(logger);
            QuestGiverIndex.SuppressLockedQuests = true;
        }

        internal void Tick(
            bool wyrdSightActive,
            GlowMode mode,
            float maxDistance,
            int scanBudgetMilliseconds,
            float bakeBudgetMilliseconds,
            int outlineRefreshRate,
            float availabilityRefreshSeconds)
        {
            if (_broken)
            {
                return;
            }

            try
            {
                EnsureInstalled();
                ApplySettings(
                    mode,
                    maxDistance,
                    scanBudgetMilliseconds,
                    bakeBudgetMilliseconds,
                    outlineRefreshRate,
                    availabilityRefreshSeconds);
                if (_scanJob != null
                    || wyrdSightActive
                    || Time.unscaledTime >= _nextIdleScanPollAt)
                {
                    PumpScan();
                    if (!wyrdSightActive && _scanJob == null)
                    {
                        _nextIdleScanPollAt = Time.unscaledTime + 1f;
                    }
                }

                if (!wyrdSightActive)
                {
                    DeactivateOutline();
                    return;
                }

                ActivateOutline();
                QueueContinuousAvailabilityRefresh();
                PumpRebuild();
                if (!_displayReady)
                {
                    return;
                }

                if (_sweepNeeded)
                {
                    _sweepNeeded = false;
                    EnqueueAll();
                }

                DrainQueue();
                PumpOutline();
            }
            catch (Exception exception)
            {
                _broken = true;
                Plugin.Log.Error("quest-giver highlighting disabled for this session: " + exception);
                Disable();
            }
        }

        internal void Dispose()
        {
            Disable();
            Plugin.Log = null;
        }

        private void EnsureInstalled()
        {
            if (_installed || World.EventSystem == null)
            {
                return;
            }

            World.EventSystem.ListenTo<IModel, Model>(
                "*", World.Events.ModelFullyInitialized<NpcElement>(), this, OnNpcInitialized);
            World.EventSystem.ListenTo<IModel, Model>(
                "*", World.Events.ModelDiscarded<NpcElement>(), this, OnNpcDiscarded);
            World.EventSystem.ListenTo<LoadingScreenUI, LoadingScreenUI>(
                "*", LoadingScreenUI.Events.SceneInitializationEnded, this, OnSceneReady);
            World.EventSystem.ListenTo<Quest, QuestUtils.QuestStateChange>(
                "*", QuestUtils.Events.QuestAdded, this, OnQuestChanged);
            World.EventSystem.ListenTo<Quest, QuestUtils.QuestStateChange>(
                "*", QuestUtils.Events.QuestStateChanged, this, OnQuestChanged);
            World.EventSystem.ListenTo<Quest, QuestUtils.QuestStateChange>(
                "*", QuestUtils.Events.QuestCompleted, this, OnQuestChanged);
            World.EventSystem.ListenTo<Quest, QuestUtils.QuestStateChange>(
                "*", QuestUtils.Events.QuestFailed, this, OnQuestChanged);
            World.EventSystem.ListenTo<NpcElement, UnconsciousElement>(
                "*", UnconsciousElement.Events.LoseConscious, this, OnConsciousnessChanged);
            World.EventSystem.ListenTo<NpcElement, UnconsciousElement>(
                "*", UnconsciousElement.Events.RegainConscious, this, OnConsciousnessChanged);
            World.EventSystem.ListenTo<ICharacter, ICharacter>(
                "*", FactionService.Events.AntagonismChanged, this, OnAntagonismChanged);
            World.EventSystem.ListenTo<IAlive, DamageOutcome>(
                "*", IAlive.Events.AfterDeath, this, OnNpcDeath);
            _installed = true;
        }

        private void ApplySettings(
            GlowMode mode,
            float maxDistance,
            int scanBudgetMilliseconds,
            float bakeBudgetMilliseconds,
            int outlineRefreshRate,
            float availabilityRefreshSeconds)
        {
            _maxDistance = Mathf.Clamp(maxDistance, 5f, 100f);
            _scanBudgetMilliseconds = Mathf.Clamp(scanBudgetMilliseconds, 1, 10);
            _bakeBudgetMilliseconds = Mathf.Clamp(bakeBudgetMilliseconds, 0.25f, 4f);
            QuestGlow.OutlineRefreshIntervalSeconds = 1f / Mathf.Clamp(outlineRefreshRate, 10, 60);
            float clampedRefreshSeconds = Mathf.Clamp(availabilityRefreshSeconds, 5f, 60f);
            if (!Mathf.Approximately(_availabilityRefreshSeconds, clampedRefreshSeconds))
            {
                _availabilityRefreshSeconds = clampedRefreshSeconds;
                if (_lastAvailabilityRefreshAt > 0f)
                {
                    _nextAvailabilityRefreshAt = _lastAvailabilityRefreshAt + _availabilityRefreshSeconds;
                }
            }
            OutlinePass.Colour = GoldenColour;
            OutlinePass.Intensity = 4f;
            OutlinePass.WidthPixels = 4f;
            OutlinePass.MaxDistance = _maxDistance;

            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            SelectionMode = mode;
            QuestGiverIndex.SuppressLockedQuests = mode != GlowMode.Thorough;
            QuestGiverIndex current = QuestGiverIndex.Current;
            if (current != null && current.IsReady && current.BoundToCurrentMemory())
            {
                current.RefreshSuppression();
                _displayReady = !_indexDirty;
                _sweepNeeded = true;
            }
        }

        private void PumpScan()
        {
            TemplatesProvider templates = null;
            GameplayMemory memory = null;
            if (World.Services == null
                || !World.Services.TryGet<TemplatesProvider>(out templates)
                || !templates.AllLoaded
                || !World.Services.TryGet<GameplayMemory>(out memory)
                || Hero.Current == null)
            {
                return;
            }

            if (!ReferenceEquals(memory, _observedMemory))
            {
                if (_scanner != null && _scanner.ArchiveLoadPending)
                {
                    return;
                }

                _observedMemory = memory;
                _scanner = null;
                _scanReport = null;
                _scanJob = null;
                _rebuildJob = null;
                _scanAttempts = 0;
                _nextScanAttemptAt = 0f;
                _nextIdleScanPollAt = 0f;
                _lastAvailabilityRefreshAt = 0f;
                _nextAvailabilityRefreshAt = 0f;
                _displayReady = false;
                _indexDirty = false;
                _sweepNeeded = false;
                _queue.Clear();
                _queued.Clear();
                QuestGlow.RemoveAll();
            }

            QuestGiverIndex current = QuestGiverIndex.Current;
            if (_scanJob == null
                && current != null
                && current.IsReady
                && current.BoundToCurrentMemory())
            {
                _scanner = null;
                bool readyForDisplay = !_indexDirty && _rebuildJob == null;
                bool becameReady = !_displayReady && readyForDisplay;
                _displayReady = readyForDisplay;
                if (becameReady)
                {
                    _sweepNeeded = true;
                }
                Plugin.AutoScanBlocked = null;
                return;
            }

            if (_scanJob != null)
            {
                bool continuing;
                try
                {
                    continuing = _scanJob.MoveNext();
                }
                catch (Exception exception)
                {
                    continuing = false;
                    Plugin.Log.Error("background quest scan failed: " + exception);
                }

                if (continuing)
                {
                    return;
                }

                _scanJob = null;
                current = QuestGiverIndex.Current;
                if (current != null && current.IsReady && current.BoundToCurrentMemory())
                {
                    _indexDirty = false;
                    _displayReady = true;
                    _sweepNeeded = true;
                    MarkAvailabilityRefreshed();
                    Plugin.AutoScanBlocked = null;
                    Plugin.Log.Info(
                        "background quest scan completed across "
                        + _scanReport.Frames
                        + " frames in "
                        + _scanReport.ElapsedMs
                        + " ms.");
                    return;
                }

                ScheduleScanRetry();
                return;
            }

            if (_scanAttempts >= 4 || Time.realtimeSinceStartup < _nextScanAttemptAt)
            {
                return;
            }

            _scanAttempts++;
            _scanReport = new ScanReport();
            _scanner = new QuestScanner(_scanReport, templates, memory, Plugin.Log);
            _scanJob = _scanner.Run(8, _scanBudgetMilliseconds, 180, false, 256, false);
            Plugin.Log.Info("background quest scan started (attempt " + _scanAttempts + "/4).");
        }

        private void ScheduleScanRetry()
        {
            float delay = Mathf.Min(15f * Mathf.Pow(2f, _scanAttempts - 1), 120f);
            _nextScanAttemptAt = Time.realtimeSinceStartup + delay;
            string reason = _scanner == null ? null : _scanner.NotPublishedReason;
            Plugin.AutoScanBlocked = "quest scan attempt " + _scanAttempts + "/4 did not publish an index"
                + (string.IsNullOrEmpty(reason) ? string.Empty : " (" + reason + ")");
            Plugin.Log.Warn(Plugin.AutoScanBlocked + "; retrying in " + (int)delay + " seconds.");
        }

        private void ActivateOutline()
        {
            if (!_active)
            {
                _active = true;
                _sweepNeeded = true;
                QuestGlow.RequireFreshPosesForAll();
                QuestGiverIndex current = QuestGiverIndex.Current;
                if (current != null
                    && current.IsReady
                    && current.BoundToCurrentMemory()
                    && Time.unscaledTime - _lastAvailabilityRefreshAt > 0.5f)
                {
                    _indexDirty = true;
                    _displayReady = false;
                }
            }

            if (!OutlinePass.Installed
                && Time.unscaledTime >= _nextOutlineInstallAt
                && !OutlinePass.Install())
            {
                _displayReady = false;
                _nextOutlineInstallAt = Time.unscaledTime + 20f;
            }
        }

        private void DeactivateOutline()
        {
            if (!_active && !OutlinePass.Installed)
            {
                return;
            }

            _active = false;
            for (int i = 0; i < QuestGlow.Live.Count; i++)
            {
                QuestGlow glow = QuestGlow.Live[i];
                if (glow != null)
                {
                    glow.SetOutlineVisible(false);
                }
            }

            OutlinePass.Suspend();
        }

        private void QueueContinuousAvailabilityRefresh()
        {
            if (_lastAvailabilityRefreshAt > 0f
                && _rebuildJob == null
                && !_indexDirty
                && Time.unscaledTime >= _nextAvailabilityRefreshAt)
            {
                _indexDirty = true;
                _displayReady = false;
            }
        }

        private void PumpRebuild()
        {
            QuestGiverIndex current = QuestGiverIndex.Current;
            if (current == null || !current.IsReady || !current.BoundToCurrentMemory())
            {
                _displayReady = false;
                return;
            }

            if (_rebuildJob == null
                && _indexDirty
                && Time.unscaledTime >= _nextRebuildAttemptAt)
            {
                _indexDirty = false;
                _displayReady = false;
                QuestGlow.RequireFreshPosesForAll();
                current.InvalidateLocationCache();
                _rebuildJob = current.RebuildJob(2);
            }

            if (_rebuildJob == null)
            {
                _displayReady = true;
                return;
            }

            bool continuing;
            bool failed = false;
            try
            {
                continuing = _rebuildJob.MoveNext();
            }
            catch (Exception exception)
            {
                continuing = false;
                failed = true;
                Plugin.Log.Warn("quest availability refresh failed: " + exception);
            }

            if (!continuing)
            {
                _rebuildJob = null;
                if (failed)
                {
                    _indexDirty = true;
                    _displayReady = false;
                    _nextRebuildAttemptAt = Time.unscaledTime + 5f;
                }
                else
                {
                    _displayReady = true;
                    _sweepNeeded = true;
                    MarkAvailabilityRefreshed();
                }
            }
        }

        private void MarkAvailabilityRefreshed()
        {
            _lastAvailabilityRefreshAt = Time.unscaledTime;
            _nextAvailabilityRefreshAt = _lastAvailabilityRefreshAt + _availabilityRefreshSeconds;
            _nextRebuildAttemptAt = 0f;
        }

        private void OnNpcInitialized(Model model)
        {
            NpcElement npc = model as NpcElement;
            if (npc != null && _active)
            {
                Enqueue(npc);
            }
        }

        private void OnNpcDiscarded(Model model)
        {
            NpcElement npc = model as NpcElement;
            if (npc == null)
            {
                return;
            }

            try
            {
                Location location = ((Element<Location>)npc).ParentModel;
                QuestGlow glow = location == null ? null : location.TryGetElement<QuestGlow>();
                if (glow != null && !glow.HasBeenDiscarded)
                {
                    glow.Discard();
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnSceneReady(LoadingScreenUI unused)
        {
            _queue.Clear();
            _queued.Clear();
            QuestGlow.RemoveAll();
            _indexDirty = true;
            _displayReady = false;
            _sweepNeeded = true;
            _nextIdleScanPollAt = 0f;
        }

        private void OnQuestChanged(QuestUtils.QuestStateChange unused)
        {
            _indexDirty = true;
            _displayReady = false;
        }

        private void OnConsciousnessChanged(UnconsciousElement element)
        {
            if (_active && element != null)
            {
                Enqueue(element.ParentModel);
            }
        }

        private void OnAntagonismChanged(ICharacter character)
        {
            if (_active)
            {
                Enqueue(character as NpcElement);
            }
        }

        private void OnNpcDeath(DamageOutcome outcome)
        {
            if (_active)
            {
                Enqueue(outcome.TargetPure as NpcElement);
            }
        }

        private void Enqueue(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }

            string id = npc.ID;
            if (!string.IsNullOrEmpty(id) && _queued.Add(id))
            {
                _queue.Enqueue(new Pending { Id = id, Npc = npc });
            }
        }

        private void EnqueueAll()
        {
            if (World.Services == null)
            {
                return;
            }

            try
            {
                foreach (NpcElement npc in World.All<NpcElement>())
                {
                    Enqueue(npc);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("NPC enumeration failed: " + exception.Message);
            }
        }

        private void DrainQueue()
        {
            int remaining = 4;
            while (remaining-- > 0 && _queue.Count > 0)
            {
                Pending pending = _queue.Dequeue();
                _queued.Remove(pending.Id);
                Evaluate(pending.Npc);
            }
        }

        private void Evaluate(NpcElement npc)
        {
            if (npc == null || npc.HasBeenDiscarded)
            {
                return;
            }

            Location location;
            try
            {
                location = ((Element<Location>)npc).ParentModel;
            }
            catch (Exception)
            {
                return;
            }

            if (location == null || location.HasBeenDiscarded)
            {
                return;
            }

            bool shouldGlow = ShouldGlow(npc, location);
            QuestGlow glow = location.TryGetElement<QuestGlow>();
            if (shouldGlow)
            {
                if (glow == null)
                {
                    location.AddElement<QuestGlow>(
                        new QuestGlow(GoldenColour, 4f, 1f, GlowRoute.Outline, EmissiveMapMode.Always));
                }
                else
                {
                    glow.Refresh();
                }
            }
            else if (glow != null && !glow.HasBeenDiscarded)
            {
                glow.Discard();
            }
        }

        private bool ShouldGlow(NpcElement npc, Location location)
        {
            QuestGiverIndex current = QuestGiverIndex.Current;
            if (current == null || !current.IsReady || !current.BoundToCurrentMemory())
            {
                return false;
            }

            try
            {
                if (!npc.IsAlive || npc.IsUnconscious)
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }

            try
            {
                if (Hero.Current != null && WithFactionUtils.IsHostileToHero(npc))
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }

            return current.LocationHasAvailableQuest(location, _mode == GlowMode.Precise);
        }

        private void PumpOutline()
        {
            List<QuestGlow> live = QuestGlow.Live;
            if (live.Count == 0 || !OutlinePass.Installed)
            {
                return;
            }

            Camera camera = MainCamera();
            Vector3 cameraPosition = Vector3.zero;
            Vector3 cameraForward = Vector3.forward;
            if (camera != null)
            {
                cameraPosition = camera.transform.position;
                cameraForward = camera.transform.forward;
            }

            float maxDistanceSquared = _maxDistance * _maxDistance;
            float perspectiveScale = 0f;
            float orthographicScale = 0f;
            bool orthographic = false;
            if (camera != null)
            {
                int screenHeight = OutlinePass.ScreenHeight;
                if (screenHeight <= 0)
                {
                    screenHeight = camera.pixelHeight > 0 ? camera.pixelHeight : 1080;
                }

                orthographic = camera.orthographic;
                if (orthographic)
                {
                    orthographicScale = 2f * camera.orthographicSize / screenHeight;
                }
                else
                {
                    perspectiveScale = 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / screenHeight;
                }
            }

            float width = Mathf.Max(OutlinePass.WidthPixels, 0.5f);
            int visibleCount = 0;
            for (int i = 0; i < live.Count; i++)
            {
                QuestGlow glow = live[i];
                if (glow == null)
                {
                    continue;
                }

                bool visible = false;
                Transform root = glow.OutlineRoot;
                if (camera != null && root != null)
                {
                    Vector3 delta = root.position - cameraPosition;
                    float depth = Vector3.Dot(delta, cameraForward);
                    visible = delta.sqrMagnitude <= maxDistanceSquared && depth > -1f;
                    if (visible)
                    {
                        float offset = orthographic
                            ? orthographicScale * width
                            : Mathf.Max(depth, camera.nearClipPlane) * perspectiveScale * width;
                        glow.SetHullOffset(Mathf.Clamp(offset, 0.004f, 0.009f * width));
                    }
                }

                glow.SetOutlineVisible(visible);
                if (visible)
                {
                    visibleCount++;
                }
            }

            if (visibleCount == 0)
            {
                return;
            }

            _bakeClock.Restart();
            int idleSlices = 0;
            while (idleSlices < live.Count
                && _bakeClock.Elapsed.TotalMilliseconds < _bakeBudgetMilliseconds)
            {
                if (_bakeCursor >= live.Count)
                {
                    _bakeCursor = 0;
                }

                QuestGlow glow = live[_bakeCursor++];
                bool didWork = false;
                if (glow != null && glow.OutlineDrawableCandidate)
                {
                    didWork = glow.BakeOutlineSlice();
                }

                idleSlices = didWork ? 0 : idleSlices + 1;
            }

            _bakeClock.Stop();
        }

        private Camera MainCamera()
        {
            if (_cachedCamera != null
                && _cachedCamera.isActiveAndEnabled
                && Time.unscaledTime < _nextCameraProbeAt)
            {
                return _cachedCamera;
            }

            _nextCameraProbeAt = Time.unscaledTime + 2f;
            Camera main = Camera.main;
            if (main != null && main.targetTexture == null)
            {
                _cachedCamera = main;
                return main;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate != null
                    && candidate.isActiveAndEnabled
                    && candidate.cameraType == CameraType.Game
                    && candidate.targetTexture == null)
                {
                    _cachedCamera = candidate;
                    return candidate;
                }
            }

            _cachedCamera = null;
            return null;
        }

        internal void Disable()
        {
            if (!_installed
                && !_active
                && _scanJob == null
                && _rebuildJob == null
                && QuestGlow.LiveCount == 0
                && !OutlinePass.HasResources)
            {
                return;
            }

            DeactivateOutline();
            if (_installed && World.EventSystem != null)
            {
                World.EventSystem.RemoveAllListenersOwnedBy(this, false);
            }

            if (QuestGlow.LiveCount > 0)
            {
                QuestGlow.RemoveAll();
            }
            OutlinePass.Uninstall();
            _installed = false;
            _active = false;
            _displayReady = false;
            _indexDirty = false;
            _sweepNeeded = false;
            if (_scanner == null || !_scanner.ArchiveLoadPending)
            {
                _scanJob = null;
                _scanner = null;
            }
            _rebuildJob = null;
            _lastAvailabilityRefreshAt = 0f;
            _nextAvailabilityRefreshAt = 0f;
            _nextRebuildAttemptAt = 0f;
            _nextIdleScanPollAt = 0f;
            _queue.Clear();
            _queued.Clear();
        }
    }
}
