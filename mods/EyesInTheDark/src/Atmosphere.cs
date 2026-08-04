using System;
using System.Collections.Generic;

namespace EyesInTheDark
{
    internal enum GftNotificationPreset
    {
        Minimal,
        Atmospheric,
        Detailed
    }

    internal enum AtmosphereEventKind
    {
        NightBegin,
        NightEnd,
        UpwardStage,
        DownwardStage,
        ProtectionEntered,
        ProtectionLeft,
        MajorThreatSurge,
        HuntCommitted,
        HunterKilled,
        HunterEscaped
    }

    internal static class AtmospherePolicy
    {
        public static bool ShouldNotify(
            GftNotificationPreset preset,
            AtmosphereEventKind eventKind)
        {
            if (preset == GftNotificationPreset.Minimal)
            {
                return eventKind == AtmosphereEventKind.HuntCommitted
                    || eventKind == AtmosphereEventKind.HunterKilled
                    || eventKind == AtmosphereEventKind.HunterEscaped;
            }

            if (preset == GftNotificationPreset.Atmospheric)
            {
                return eventKind == AtmosphereEventKind.NightBegin
                    || eventKind == AtmosphereEventKind.NightEnd
                    || eventKind == AtmosphereEventKind.UpwardStage
                    || eventKind == AtmosphereEventKind.HuntCommitted
                    || eventKind == AtmosphereEventKind.HunterKilled
                    || eventKind == AtmosphereEventKind.HunterEscaped;
            }

            return true;
        }
    }

    internal sealed class AtmosphereTextPools
    {
        private static readonly string[] NightBeginTexts =
        {
            "The Wyrd Night opens its eyes.",
            "Darkness gathers beyond the firelight.",
            "Something stirs beneath the Wyrd."
        };

        private static readonly string[] NightEndTexts =
        {
            "Dawn loosens the Wyrd's gaze.",
            "The night recedes.",
            "Morning finds you still standing."
        };

        private static readonly string[] WatchedTexts =
        {
            "The dark has noticed you.",
            "A distant attention turns your way.",
            "The Wyrd begins to listen."
        };

        private static readonly string[] HuntedTexts =
        {
            "The Wyrd presses closer.",
            "The silence around you tightens.",
            "Your trail grows louder in the dark."
        };

        private static readonly string[] MarkedTexts =
        {
            "Your presence burns against the night.",
            "The Wyrd remembers your shape.",
            "The darkness gathers around your trail."
        };

        private static readonly string[] DownwardStageTexts =
        {
            "The pressure in the dark eases.",
            "Your trail grows faint.",
            "Silence returns, for now."
        };

        private static readonly string[] ProtectionEnteredTexts =
        {
            "The boundary dulls the Wyrd's attention.",
            "Shelter softens the pressure outside.",
            "The Wyrd recoils from this refuge."
        };

        private static readonly string[] ProtectionLeftTexts =
        {
            "Beyond the boundary, the night listens again.",
            "The refuge falls away behind you.",
            "The Wyrd closes around your path once more."
        };

        private static readonly string[] MajorSurgeTexts =
        {
            "Your disturbance carries into the dark.",
            "The Wyrd recoils and remembers.",
            "The night answers your sudden noise."
        };

        private static readonly string[] HuntCommittedTexts =
        {
            "Something in the dark has taken your trail.",
            "A hunter answers the Wyrd's call.",
            "The night closes around you."
        };

        private static readonly string[] HunterKilledTexts =
        {
            "The hunter falls, and the pressure breaks.",
            "The Wyrd recoils from its fallen hunter.",
            "For now, the night has lost your trail."
        };

        private static readonly string[] HunterEscapedTexts =
        {
            "The pursuit fades behind you.",
            "Your trail thins, but the night remembers.",
            "The hunter loses you in the dark."
        };

        private readonly Random _random;
        private readonly Dictionary<string, int> _lastIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public AtmosphereTextPools(int seed)
        {
            _random = new Random(seed);
        }

        public string Select(
            AtmosphereEventKind eventKind,
            ThreatStage stage)
        {
            string key;
            string[] pool;
            switch (eventKind)
            {
                case AtmosphereEventKind.NightBegin:
                    key = "night-begin";
                    pool = NightBeginTexts;
                    break;
                case AtmosphereEventKind.NightEnd:
                    key = "night-end";
                    pool = NightEndTexts;
                    break;
                case AtmosphereEventKind.UpwardStage:
                    key = "stage-up-" + stage;
                    pool = stage == ThreatStage.Marked
                        ? MarkedTexts
                        : stage == ThreatStage.Hunted
                            ? HuntedTexts
                            : WatchedTexts;
                    break;
                case AtmosphereEventKind.DownwardStage:
                    key = "stage-down";
                    pool = DownwardStageTexts;
                    break;
                case AtmosphereEventKind.ProtectionEntered:
                    key = "protection-entered";
                    pool = ProtectionEnteredTexts;
                    break;
                case AtmosphereEventKind.ProtectionLeft:
                    key = "protection-left";
                    pool = ProtectionLeftTexts;
                    break;
                case AtmosphereEventKind.HuntCommitted:
                    key = "hunt-committed";
                    pool = HuntCommittedTexts;
                    break;
                case AtmosphereEventKind.HunterKilled:
                    key = "hunter-killed";
                    pool = HunterKilledTexts;
                    break;
                case AtmosphereEventKind.HunterEscaped:
                    key = "hunter-escaped";
                    pool = HunterEscapedTexts;
                    break;
                default:
                    key = "major-surge";
                    pool = MajorSurgeTexts;
                    break;
            }

            int previous;
            _lastIndices.TryGetValue(key, out previous);
            int index = _random.Next(pool.Length);
            if (pool.Length > 1 && _lastIndices.ContainsKey(key))
            {
                index = (previous + 1
                    + _random.Next(pool.Length - 1)) % pool.Length;
            }

            _lastIndices[key] = index;
            return pool[index];
        }
    }

    internal sealed class NotificationCooldowns
    {
        private readonly Dictionary<string, double> _lastTimes =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastTexts =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public bool CanEmit(
            string lane,
            string text,
            double activeSeconds,
            float cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(lane)
                || string.IsNullOrWhiteSpace(text)
                || double.IsNaN(activeSeconds)
                || double.IsInfinity(activeSeconds))
            {
                return false;
            }

            double lastTime;
            string lastText;
            if (_lastTimes.TryGetValue(lane, out lastTime)
                && activeSeconds - lastTime
                    < Math.Max(0f, cooldownSeconds))
            {
                return false;
            }

            if (_lastTexts.TryGetValue(lane, out lastText)
                && string.Equals(lastText, text, StringComparison.Ordinal)
                && _lastTimes.TryGetValue(lane, out lastTime)
                && activeSeconds - lastTime
                    < Math.Max(5f, cooldownSeconds * 3f))
            {
                return false;
            }

            _lastTimes[lane] = activeSeconds;
            _lastTexts[lane] = text;
            return true;
        }

        public void Reset()
        {
            _lastTimes.Clear();
            _lastTexts.Clear();
        }
    }
}
