using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Awaken.TG.Main.Heroes;
using FMODUnity;
using UnityEngine;

namespace SoulAndService
{
    internal enum SoulSalvageAudioTargetClass
    {
        Unknown,
        Female,
        Male,
        UnknownMonster,
        FemaleMonster,
        MaleMonster
    }

    internal static class SoulSalvageAudioRuntime
    {
        private const string LowTier = "low";
        private const string MediumTier = "medium";
        private const string HighTier = "high";
        private const string MaxTier = "max";
        private const int TierSoundSlots = 10;
        private const float MaximumRangeDistance = 30.0f;
        private const float MinimumRangeVolume = 0.10f;
        private const int MaximumPendingEchoes = 24;

        private struct PendingEcho
        {
            internal string Path;
            internal float Volume;
            internal float Pitch;
            internal float PlayAt;
        }

        private static readonly Dictionary<string, FMOD.Sound> SoundsByPath =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> PathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> RecentPathsByTier =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Random Random = new System.Random();
        private static readonly List<PendingEcho> PendingEchoes =
            new List<PendingEcho>();

        private static bool _pathsResolved;
        private static bool _loggedMissingSounds;

        internal static void Play(
            Grailwright.Shared.CorpseQualityTier qualityTier,
            bool hasSourcePosition,
            Vector3 sourcePosition,
            SoulSalvageAudioTargetClass targetClass)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.PlaySoulSalvageAudio == null
                || !plugin.PlaySoulSalvageAudio.Value)
            {
                return;
            }

            EnsurePathsResolved();
            if (CountPaths() == 0)
            {
                if (!_loggedMissingSounds)
                {
                    plugin.LogWarning(
                        "Soul Rend audio is enabled, but no tiered WAV files were found.");
                    _loggedMissingSounds = true;
                }
                return;
            }

            string selectedTier;
            string path = PickPath(GetTierName(qualityTier), out selectedTier);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            float baseVolume = plugin.SoulSalvageAudioVolume == null
                ? 0.85f
                : Math.Max(0.0f, plugin.SoulSalvageAudioVolume.Value);
            float rangeMultiplier = GetRangeVolumeMultiplier(
                plugin,
                hasSourcePosition,
                sourcePosition);
            float volume = baseVolume * rangeMultiplier;
            float pitch = GetPitchMultiplier(plugin, targetClass);
            if (TryPlay(path, volume, pitch))
            {
                RememberRecentPath(plugin, selectedTier, path);
                ScheduleEchoes(plugin, path, volume, pitch);
            }
        }

        internal static void Update()
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin == null
                || !plugin.IsEnabled
                || plugin.PlaySoulSalvageAudio == null
                || !plugin.PlaySoulSalvageAudio.Value)
            {
                PendingEchoes.Clear();
                return;
            }
            float now = Time.unscaledTime;
            for (int index = 0; index < PendingEchoes.Count;)
            {
                PendingEcho echo = PendingEchoes[index];
                if (now < echo.PlayAt)
                {
                    index++;
                    continue;
                }
                PendingEchoes.RemoveAt(index);
                TryPlay(echo.Path, echo.Volume, echo.Pitch);
            }
        }

        private static void ScheduleEchoes(
            SoulAndServicePlugin plugin,
            string path,
            float volume,
            float pitch)
        {
            float amount = plugin.SoulSalvageAudioEchoAmount == null
                ? 0.35f
                : Mathf.Clamp01(plugin.SoulSalvageAudioEchoAmount.Value);
            if (amount <= 0.001f || PendingEchoes.Count > MaximumPendingEchoes - 2)
            {
                return;
            }
            float now = Time.unscaledTime;
            PendingEchoes.Add(new PendingEcho
            {
                Path = path,
                Volume = volume * amount * 0.45f,
                Pitch = pitch * 0.985f,
                PlayAt = now + 0.16f
            });
            PendingEchoes.Add(new PendingEcho
            {
                Path = path,
                Volume = volume * amount * 0.25f,
                Pitch = pitch * 0.97f,
                PlayAt = now + 0.34f
            });
        }

        private static float GetRangeVolumeMultiplier(
            SoulAndServicePlugin plugin,
            bool hasSourcePosition,
            Vector3 sourcePosition)
        {
            float strength = plugin.SoulSalvageAudioRangeVolume == null
                ? 1.0f
                : Mathf.Clamp01(plugin.SoulSalvageAudioRangeVolume.Value);
            if (strength <= 0.001f)
            {
                return 1.0f;
            }
            Hero hero = Hero.Current;
            if (!hasSourcePosition || hero == null)
            {
                plugin.LogDiagnostic(
                    "Soul Rend audio range could not resolve both positions; using full volume.");
                return 1.0f;
            }
            float distance = Vector3.Distance(hero.Coords, sourcePosition);
            float progress = Mathf.Clamp01(distance / MaximumRangeDistance);
            float fullCurveVolume = 1.0f
                - ((1.0f - MinimumRangeVolume) * progress);
            float multiplier = Mathf.Lerp(1.0f, fullCurveVolume, strength);
            plugin.LogDiagnostic(
                "Soul Rend audio distance="
                + distance.ToString("0.##", CultureInfo.InvariantCulture)
                + "m; rangeMultiplier="
                + multiplier.ToString("0.###", CultureInfo.InvariantCulture)
                + ".");
            return multiplier;
        }

        internal static void Shutdown()
        {
            foreach (KeyValuePair<string, FMOD.Sound> pair in SoundsByPath)
            {
                try
                {
                    pair.Value.release();
                }
                catch
                {
                }
            }
            SoundsByPath.Clear();
            PathsByTier.Clear();
            RecentPathsByTier.Clear();
            PendingEchoes.Clear();
            _pathsResolved = false;
            _loggedMissingSounds = false;
        }

        private static void EnsurePathsResolved()
        {
            if (_pathsResolved)
            {
                return;
            }
            _pathsResolved = true;
            PathsByTier.Clear();
            AddTierFiles(LowTier);
            AddTierFiles(MediumTier);
            AddTierFiles(HighTier);
            AddTierFiles(MaxTier);

            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null)
            {
                plugin.LogDiagnostic(
                    "Resolved " + CountPaths().ToString(CultureInfo.InvariantCulture)
                    + " tiered Soul Rend audio file(s).");
            }
        }

        private static void AddTierFiles(string tier)
        {
            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(pluginDirectory))
            {
                return;
            }
            string audioDirectory = Path.Combine(pluginDirectory, "audio");
            for (int index = 0; index < TierSoundSlots; index++)
            {
                string path = Path.Combine(
                    audioDirectory,
                    "soul_salvage_" + tier + "_"
                        + index.ToString(CultureInfo.InvariantCulture) + ".wav");
                if (!File.Exists(path))
                {
                    continue;
                }
                List<string> paths;
                if (!PathsByTier.TryGetValue(tier, out paths))
                {
                    paths = new List<string>();
                    PathsByTier[tier] = paths;
                }
                paths.Add(path);
            }
        }

        private static string GetTierName(
            Grailwright.Shared.CorpseQualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case Grailwright.Shared.CorpseQualityTier.Worthy:
                    return MediumTier;
                case Grailwright.Shared.CorpseQualityTier.Potent:
                    return HighTier;
                case Grailwright.Shared.CorpseQualityTier.Prime:
                    return MaxTier;
                case Grailwright.Shared.CorpseQualityTier.Meager:
                default:
                    return LowTier;
            }
        }

        private static string PickPath(
            string preferredTier,
            out string selectedTier)
        {
            selectedTier = string.Empty;
            string[] fallbacks = GetTierFallbacks(preferredTier);
            foreach (string tier in fallbacks)
            {
                List<string> paths;
                if (!PathsByTier.TryGetValue(tier, out paths)
                    || paths.Count == 0)
                {
                    continue;
                }
                selectedTier = tier;
                return PickPathFromTier(tier, paths);
            }
            return string.Empty;
        }

        private static string PickPathFromTier(
            string tier,
            List<string> paths)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            if (plugin != null
                && plugin.AvoidRecentSoulSalvageAudioRepeats != null
                && plugin.AvoidRecentSoulSalvageAudioRepeats.Value)
            {
                int memory = GetRecentMemory(plugin);
                List<string> recent;
                if (memory > 0
                    && RecentPathsByTier.TryGetValue(tier, out recent)
                    && recent.Count > 0)
                {
                    List<string> candidates = paths.FindAll(path =>
                        !recent.Exists(item => string.Equals(
                            item,
                            path,
                            StringComparison.OrdinalIgnoreCase)));
                    if (candidates.Count > 0)
                    {
                        return candidates[Random.Next(candidates.Count)];
                    }
                }
            }
            return paths[Random.Next(paths.Count)];
        }

        private static void RememberRecentPath(
            SoulAndServicePlugin plugin,
            string tier,
            string path)
        {
            if (plugin.AvoidRecentSoulSalvageAudioRepeats == null
                || !plugin.AvoidRecentSoulSalvageAudioRepeats.Value)
            {
                return;
            }
            int memory = GetRecentMemory(plugin);
            if (memory <= 0)
            {
                return;
            }
            List<string> recent;
            if (!RecentPathsByTier.TryGetValue(tier, out recent))
            {
                recent = new List<string>();
                RecentPathsByTier[tier] = recent;
            }
            recent.RemoveAll(item => string.Equals(
                item,
                path,
                StringComparison.OrdinalIgnoreCase));
            recent.Add(path);
            while (recent.Count > memory)
            {
                recent.RemoveAt(0);
            }
        }

        private static int GetRecentMemory(SoulAndServicePlugin plugin)
        {
            return plugin.RecentSoulSalvageAudioMemory == null
                ? 2
                : Math.Max(
                    0,
                    Math.Min(20, plugin.RecentSoulSalvageAudioMemory.Value));
        }

        private static float GetPitchMultiplier(
            SoulAndServicePlugin plugin,
            SoulSalvageAudioTargetClass targetClass)
        {
            float femalePitch = plugin.FemaleSoulSalvageAudioPitchSemitones == null
                ? 3.0f
                : plugin.FemaleSoulSalvageAudioPitchSemitones.Value;
            float malePitch = plugin.MaleSoulSalvageAudioPitchSemitones == null
                ? -3.0f
                : plugin.MaleSoulSalvageAudioPitchSemitones.Value;
            float semitones;
            switch (targetClass)
            {
                case SoulSalvageAudioTargetClass.Female:
                    semitones = femalePitch;
                    break;
                case SoulSalvageAudioTargetClass.Male:
                    semitones = malePitch;
                    break;
                case SoulSalvageAudioTargetClass.FemaleMonster:
                    semitones = femalePitch
                        + (plugin.FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones
                                == null
                            ? -1.0f
                            : plugin.FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones
                                .Value);
                    break;
                case SoulSalvageAudioTargetClass.MaleMonster:
                    semitones = malePitch
                        + (plugin.MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones
                                == null
                            ? -3.0f
                            : plugin.MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones
                                .Value);
                    break;
                case SoulSalvageAudioTargetClass.UnknownMonster:
                    semitones = plugin.NonHumanoidSoulSalvageAudioPitchSemitones == null
                        ? -6.0f
                        : plugin.NonHumanoidSoulSalvageAudioPitchSemitones.Value;
                    break;
                default:
                    semitones = 0.0f;
                    break;
            }
            semitones = Mathf.Clamp(semitones, -12.0f, 12.0f);
            float semitoneRange = plugin.SoulSalvageAudioRandomPitchSemitones == null
                ? 0.20f
                : Math.Max(
                    0.0f,
                    plugin.SoulSalvageAudioRandomPitchSemitones.Value);
            if (semitoneRange > 0.0f)
            {
                semitones += (float)(
                    (Random.NextDouble() * 2.0 - 1.0) * semitoneRange);
            }
            return Mathf.Pow(2.0f, semitones / 12.0f);
        }

        private static bool TryPlay(
            string path,
            float volume,
            float pitch)
        {
            SoulAndServicePlugin plugin = SoulAndServicePlugin.Instance;
            try
            {
                FMOD.Sound sound;
                if (!SoundsByPath.TryGetValue(path, out sound))
                {
                    FMOD.RESULT createResult = RuntimeManager.CoreSystem.createSound(
                        path,
                        FMOD.MODE.DEFAULT | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE,
                        out sound);
                    if (createResult != FMOD.RESULT.OK)
                    {
                        if (plugin != null)
                        {
                            plugin.LogWarning(
                                "FMOD createSound failed for Soul Rend audio "
                                + path + ": " + createResult + ".");
                        }
                        return false;
                    }
                    SoundsByPath[path] = sound;
                }

                FMOD.ChannelGroup channelGroup;
                if (RuntimeManager.CoreSystem.getMasterChannelGroup(out channelGroup)
                    != FMOD.RESULT.OK)
                {
                    channelGroup = default(FMOD.ChannelGroup);
                }
                FMOD.Channel channel;
                FMOD.RESULT playResult = RuntimeManager.CoreSystem.playSound(
                    sound,
                    channelGroup,
                    true,
                    out channel);
                if (playResult != FMOD.RESULT.OK)
                {
                    if (plugin != null)
                    {
                        plugin.LogWarning(
                            "FMOD playSound failed for Soul Rend audio "
                            + path + ": " + playResult + ".");
                    }
                    return false;
                }
                channel.setVolume(volume);
                channel.setPitch(Math.Max(0.01f, pitch));
                FMOD.RESULT pauseResult = channel.setPaused(false);
                if (pauseResult != FMOD.RESULT.OK)
                {
                    if (plugin != null)
                    {
                        plugin.LogDiagnostic(
                            "FMOD could not unpause Soul Rend audio "
                            + path + ": " + pauseResult + ".");
                    }
                    return false;
                }
                if (plugin != null)
                {
                    plugin.LogDiagnostic(
                        "Played " + Path.GetFileName(path) + " at pitch "
                        + pitch.ToString("0.###", CultureInfo.InvariantCulture)
                        + "x.");
                }
                return true;
            }
            catch (Exception exception)
            {
                if (plugin != null)
                {
                    plugin.LogWarning(
                        "Soul Rend audio playback failed for " + path + ": "
                        + exception.GetBaseException().Message);
                }
                return false;
            }
        }

        private static string[] GetTierFallbacks(string preferredTier)
        {
            if (string.Equals(preferredTier, MaxTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MaxTier, HighTier, MediumTier, LowTier };
            }
            if (string.Equals(preferredTier, HighTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { HighTier, MediumTier, LowTier, MaxTier };
            }
            if (string.Equals(preferredTier, MediumTier, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { MediumTier, LowTier, HighTier, MaxTier };
            }
            return new[] { LowTier, MediumTier, HighTier, MaxTier };
        }

        private static int CountPaths()
        {
            int count = 0;
            foreach (List<string> paths in PathsByTier.Values)
            {
                count += paths.Count;
            }
            return count;
        }
    }
}
