using System;
using System.Collections.Generic;

namespace EyesInTheDark
{
    internal enum ThreatStage
    {
        Unnoticed,
        Watched,
        Hunted,
        Marked
    }

    internal enum ThreatChangeCause
    {
        None,
        NightStarted,
        LoadReconstruction,
        PassiveExposure,
        ProtectedDecay,
        InteriorDecay,
        DawnReset,
        SprintOrFastSwim,
        Combat,
        WyrdKill,
        Acquisition,
        Battlecry,
        StalkerProvoked,
        OfficialHunterKilled,
        HunterEscaped,
        DiagnosticOverride
    }

    internal struct ThreatTuning
    {
        public float PassiveThreatPerNight;
        public float ProtectedDecayPerMinute;
        public float InteriorDecayPerMinute;
        public float LoadReconstructionAtDawn;
        public float GraceSeconds;
    }

    internal struct ThreatFrame
    {
        public bool IsKnownDaylight;
        public bool IsValidWyrdNight;
        public bool IsOutdoor;
        public bool IsProtected;
        public bool CanAdvanceActiveTime;
        public float NightProgress;
        public float ActiveSeconds;
    }

    internal struct ThreatUpdateResult
    {
        public readonly ThreatChangeCause Cause;
        public readonly float PreviousThreat;
        public readonly float CurrentThreat;
        public readonly ThreatStage PreviousStage;
        public readonly ThreatStage CurrentStage;

        public bool Changed
        {
            get
            {
                return Math.Abs(CurrentThreat - PreviousThreat)
                    > 0.0001f;
            }
        }

        public bool StageChanged
        {
            get { return CurrentStage != PreviousStage; }
        }

        public ThreatUpdateResult(
            ThreatChangeCause cause,
            float previousThreat,
            float currentThreat,
            ThreatStage previousStage,
            ThreatStage currentStage)
        {
            Cause = cause;
            PreviousThreat = previousThreat;
            CurrentThreat = currentThreat;
            PreviousStage = previousStage;
            CurrentStage = currentStage;
        }
    }

    internal sealed class ThreatState
    {
        private bool _nightEstablished;
        private bool _pendingLoadReconstruction = true;
        private bool _progressNeedsResync;
        private bool _wasInterior;
        private bool _diagnosticOverrideActive;
        private float _lastNightProgress;

        public float Value { get; private set; }
        public float GraceRemainingSeconds { get; private set; }
        public ThreatStage Stage
        {
            get { return GetStage(Value); }
        }
        public bool CanAcceptActivity { get; private set; }
        public bool DiagnosticOverrideActive
        {
            get { return _diagnosticOverrideActive; }
        }

        public void NotifyLoad()
        {
            Value = 0f;
            GraceRemainingSeconds = 0f;
            CanAcceptActivity = false;
            _nightEstablished = false;
            _pendingLoadReconstruction = true;
            _progressNeedsResync = false;
            _wasInterior = false;
            _diagnosticOverrideActive = false;
            _lastNightProgress = 0f;
        }

        public ThreatUpdateResult Advance(
            ThreatFrame frame,
            ThreatTuning tuning)
        {
            float previous = Value;
            ThreatStage previousStage = Stage;
            ThreatChangeCause cause = ThreatChangeCause.None;
            CanAcceptActivity = false;

            if (frame.IsKnownDaylight)
            {
                bool hadNightState = _nightEstablished
                    || Value > 0f
                    || GraceRemainingSeconds > 0f;
                Value = 0f;
                GraceRemainingSeconds = 0f;
                _nightEstablished = false;
                _pendingLoadReconstruction = false;
                _progressNeedsResync = false;
                _wasInterior = false;
                _lastNightProgress = 0f;
                cause = hadNightState
                    ? ThreatChangeCause.DawnReset
                    : ThreatChangeCause.None;
                return Result(cause, previous, previousStage);
            }

            if (!frame.IsValidWyrdNight)
            {
                if (_nightEstablished)
                {
                    _progressNeedsResync = true;
                }
                return Result(cause, previous, previousStage);
            }

            float progress = Clamp01(frame.NightProgress);
            if (_diagnosticOverrideActive)
            {
                _nightEstablished = true;
                _pendingLoadReconstruction = false;
                _progressNeedsResync = false;
                _wasInterior = !frame.IsOutdoor;
                _lastNightProgress = progress;
                CanAcceptActivity = false;
                return Result(
                    ThreatChangeCause.None,
                    previous,
                    previousStage);
            }

            float activeSeconds = IsFinitePositive(frame.ActiveSeconds)
                ? frame.ActiveSeconds
                : 0f;

            if (!_nightEstablished)
            {
                _nightEstablished = true;
                _lastNightProgress = progress;
                _wasInterior = !frame.IsOutdoor;
                if (_pendingLoadReconstruction)
                {
                    Value = ClampThreat(
                        progress
                        * ClampNonNegative(
                            tuning.LoadReconstructionAtDawn));
                    GraceRemainingSeconds = ClampNonNegative(
                        tuning.GraceSeconds);
                    cause = ThreatChangeCause.LoadReconstruction;
                }
                else
                {
                    float passive = progress
                        * ClampNonNegative(
                            tuning.PassiveThreatPerNight);
                    Value = ClampThreat(passive);
                    cause = passive > 0f
                        ? ThreatChangeCause.PassiveExposure
                        : ThreatChangeCause.NightStarted;
                }
                _pendingLoadReconstruction = false;
                _progressNeedsResync = false;
            }
            else
            {
                if (_progressNeedsResync)
                {
                    _lastNightProgress = progress;
                    _progressNeedsResync = false;
                }

                if (_wasInterior && frame.IsOutdoor)
                {
                    GraceRemainingSeconds = Math.Max(
                        GraceRemainingSeconds,
                        ClampNonNegative(tuning.GraceSeconds));
                }

                if (frame.CanAdvanceActiveTime && activeSeconds > 0f)
                {
                    GraceRemainingSeconds = Math.Max(
                        0f,
                        GraceRemainingSeconds - activeSeconds);

                    if (!frame.IsOutdoor)
                    {
                        float decay = PerMinute(
                            tuning.InteriorDecayPerMinute,
                            activeSeconds);
                        if (decay > 0f)
                        {
                            Value = ClampThreat(Value - decay);
                            cause = ThreatChangeCause.InteriorDecay;
                        }
                    }
                    else if (frame.IsProtected)
                    {
                        float decay = PerMinute(
                            tuning.ProtectedDecayPerMinute,
                            activeSeconds);
                        if (decay > 0f)
                        {
                            Value = ClampThreat(Value - decay);
                            cause = ThreatChangeCause.ProtectedDecay;
                        }
                    }
                }

                if (frame.IsOutdoor && !frame.IsProtected)
                {
                    float progressDelta = Math.Max(
                        0f,
                        progress - _lastNightProgress);
                    float passive = progressDelta
                        * ClampNonNegative(
                            tuning.PassiveThreatPerNight);
                    if (passive > 0f)
                    {
                        Value = ClampThreat(Value + passive);
                        cause = ThreatChangeCause.PassiveExposure;
                    }
                }

                _lastNightProgress = progress;
                _wasInterior = !frame.IsOutdoor;
            }

            CanAcceptActivity = frame.IsOutdoor
                && !frame.IsProtected
                && frame.CanAdvanceActiveTime
                && GraceRemainingSeconds <= 0f;
            return Result(cause, previous, previousStage);
        }

        public ThreatUpdateResult AddActivity(
            float amount,
            ThreatChangeCause cause)
        {
            float previous = Value;
            ThreatStage previousStage = Stage;
            if (_diagnosticOverrideActive
                || !CanAcceptActivity
                || !IsFinitePositive(amount)
                || cause == ThreatChangeCause.None)
            {
                return Result(
                    ThreatChangeCause.None,
                    previous,
                    previousStage);
            }

            Value = ClampThreat(Value + amount);
            return Result(cause, previous, previousStage);
        }

        public ThreatUpdateResult Reduce(
            float amount,
            ThreatChangeCause cause)
        {
            float previous = Value;
            ThreatStage previousStage = Stage;
            if (_diagnosticOverrideActive
                || !IsFinitePositive(amount)
                || (cause != ThreatChangeCause.OfficialHunterKilled
                    && cause != ThreatChangeCause.HunterEscaped))
            {
                return Result(
                    ThreatChangeCause.None,
                    previous,
                    previousStage);
            }

            Value = ClampThreat(Value - amount);
            return Result(cause, previous, previousStage);
        }

        public ThreatUpdateResult SetDiagnosticOverride(
            bool active,
            float value)
        {
            float previous = Value;
            ThreatStage previousStage = Stage;
            _diagnosticOverrideActive = active;
            if (!active)
            {
                return Result(
                    ThreatChangeCause.None,
                    previous,
                    previousStage);
            }

            Value = ClampThreat(value);
            CanAcceptActivity = false;
            return Result(
                ThreatChangeCause.DiagnosticOverride,
                previous,
                previousStage);
        }

        public static ThreatStage GetStage(float threat)
        {
            float value = ClampThreat(threat);
            if (value >= 75f)
            {
                return ThreatStage.Marked;
            }

            if (value >= 50f)
            {
                return ThreatStage.Hunted;
            }

            return value >= 25f
                ? ThreatStage.Watched
                : ThreatStage.Unnoticed;
        }

        private ThreatUpdateResult Result(
            ThreatChangeCause cause,
            float previous,
            ThreatStage previousStage)
        {
            return new ThreatUpdateResult(
                cause,
                previous,
                Value,
                previousStage,
                Stage);
        }

        private static float PerMinute(float rate, float seconds)
        {
            return ClampNonNegative(rate) * seconds / 60f;
        }

        private static float ClampThreat(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            return float.IsInfinity(value) || value >= 100f
                ? 100f
                : value;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            return float.IsInfinity(value) || value >= 1f
                ? 1f
                : value;
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || value <= 0f
                ? 0f
                : float.IsInfinity(value) ? float.MaxValue : value;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f
                && !float.IsNaN(value)
                && !float.IsInfinity(value);
        }
    }

    internal sealed class ThreatActivityLimiter
    {
        private const float MovementCommitSeconds = 15f;
        private const float AcquisitionWindowSeconds = 5f;
        private const float CombatImmediateRepeatSeconds = 0.1f;

        private readonly HashSet<string> _acquiredItemIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _killedWyrdNpcIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly WindowedAccumulator _combat =
            new WindowedAccumulator();
        private readonly WindowedAccumulator _acquisition =
            new WindowedAccumulator();

        private float _movementSeconds;
        private string _lastCombatFingerprint;
        private double _lastCombatFingerprintSeconds = double.NegativeInfinity;

        public void ResetNight()
        {
            _movementSeconds = 0f;
            _lastCombatFingerprint = null;
            _lastCombatFingerprintSeconds = double.NegativeInfinity;
            _combat.Reset();
            _acquisition.Reset();
            _acquiredItemIds.Clear();
            _killedWyrdNpcIds.Clear();
        }

        public void Suspend()
        {
            _movementSeconds = 0f;
            _combat.Reset();
            _acquisition.Reset();
        }

        public float AdvanceMovement(
            bool sustainedMovement,
            float activeSeconds,
            float threatPerMinute)
        {
            if (!sustainedMovement || !IsFinitePositive(activeSeconds))
            {
                _movementSeconds = 0f;
                return 0f;
            }

            _movementSeconds += activeSeconds;
            if (_movementSeconds < MovementCommitSeconds)
            {
                return 0f;
            }

            int commits = (int)(_movementSeconds / MovementCommitSeconds);
            _movementSeconds -= commits * MovementCommitSeconds;
            return ClampNonNegative(threatPerMinute)
                * MovementCommitSeconds
                / 60f
                * commits;
        }

        public bool RecordCombat(
            float amount,
            string fingerprint,
            double activeSeconds)
        {
            if (!IsFinitePositive(amount))
            {
                return false;
            }

            string safeFingerprint = fingerprint ?? string.Empty;
            if (safeFingerprint.Length > 0
                && string.Equals(
                    safeFingerprint,
                    _lastCombatFingerprint,
                    StringComparison.Ordinal)
                && activeSeconds - _lastCombatFingerprintSeconds
                    < CombatImmediateRepeatSeconds)
            {
                return false;
            }

            _lastCombatFingerprint = safeFingerprint;
            _lastCombatFingerprintSeconds = activeSeconds;
            _combat.Record(amount, activeSeconds);
            return true;
        }

        public bool RecordAcquisition(
            float amount,
            string itemId,
            double activeSeconds)
        {
            if (!IsFinitePositive(amount)
                || string.IsNullOrEmpty(itemId)
                || !_acquiredItemIds.Add(itemId))
            {
                return false;
            }

            _acquisition.Record(amount, activeSeconds);
            return true;
        }

        public bool RecordWyrdKill(string npcId)
        {
            return !string.IsNullOrEmpty(npcId)
                && _killedWyrdNpcIds.Add(npcId);
        }

        public float FlushCombat(
            double activeSeconds,
            float maximumPerWindow,
            float responseSeconds)
        {
            return _combat.Flush(
                activeSeconds,
                IsFinitePositive(responseSeconds)
                    ? responseSeconds
                    : 0.1f,
                maximumPerWindow);
        }

        public float FlushAcquisition(
            double activeSeconds,
            float maximumPerWindow)
        {
            return _acquisition.Flush(
                activeSeconds,
                AcquisitionWindowSeconds,
                maximumPerWindow);
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || value <= 0f
                ? 0f
                : float.IsInfinity(value) ? float.MaxValue : value;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f
                && !float.IsNaN(value)
                && !float.IsInfinity(value);
        }

        private sealed class WindowedAccumulator
        {
            private float _pending;
            private double _windowStartedSeconds;

            public void Record(float amount, double activeSeconds)
            {
                if (_pending <= 0f)
                {
                    _windowStartedSeconds = activeSeconds;
                }
                _pending += amount;
            }

            public float Flush(
                double activeSeconds,
                float windowSeconds,
                float maximumPerWindow)
            {
                if (_pending <= 0f
                    || activeSeconds - _windowStartedSeconds
                        < windowSeconds)
                {
                    return 0f;
                }

                float result = Math.Min(
                    _pending,
                    ClampNonNegative(maximumPerWindow));
                Reset();
                return result;
            }

            public void Reset()
            {
                _pending = 0f;
                _windowStartedSeconds = 0d;
            }
        }
    }
}
