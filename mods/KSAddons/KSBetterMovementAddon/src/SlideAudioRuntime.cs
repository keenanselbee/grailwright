using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Awaken.TG.Main.AudioSystem;
using BepInEx.Logging;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Keenan.TGFoA.BetterMovementAddon
{
    internal sealed class SlideAudioRuntime : IDisposable
    {
        private sealed class SurfaceAudioSet
        {
            internal readonly List<Sound> Loops = new List<Sound>();
            internal readonly List<Sound> Starts = new List<Sound>();
            internal readonly List<Sound> Stops = new List<Sound>();
        }

        private sealed class LoopVoice
        {
            internal string Surface;
            internal Channel Channel;
            internal float Fade;
            internal bool FadingOut;
        }

        private static readonly Dictionary<string, string> FolderBySurface =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "FTS_Grass", "grass" },
                { "FTS_Gravel", "gravel" },
                { "FTS_Ground", "ground" },
                { "FTS_Mud", "mud" },
                { "FTS_Puddle", "puddle" },
                { "FTS_Snow", "snow" },
                { "FTS_Stone", "stone" },
                { "FTS_Sand", "sand" },
                { "FTS_Wood", "wood" },
                { "FTS_Cloth", "cloth" },
                { "FTS_Metal", "metal" }
            };

        private readonly BetterMovementAddonPlugin _plugin;
        private readonly ManualLogSource _logger;
        private readonly string _audioRoot;
        private readonly Dictionary<string, SurfaceAudioSet> _sets =
            new Dictionary<string, SurfaceAudioSet>(StringComparer.Ordinal);
        private readonly List<Sound> _ownedSounds = new List<Sound>();
        private readonly List<LoopVoice> _loopVoices = new List<LoopVoice>();
        private readonly System.Random _random = new System.Random();

        private ChannelGroup _channelGroup;
        private Bus _sfxBus;
        private bool _sfxBusLocked;
        private bool _initialized;
        private bool _initializationFailed;
        private bool _disposed;
        private string _currentSurface;
        private Vector3 _lastPosition;
        private int _playbackWarnings;

        internal SlideAudioRuntime(
            BetterMovementAddonPlugin plugin,
            ManualLogSource logger,
            string pluginLocation)
        {
            _plugin = plugin;
            _logger = logger;
            string pluginDirectory = Path.GetDirectoryName(pluginLocation)
                ?? AppContext.BaseDirectory;
            _audioRoot = Path.Combine(pluginDirectory, "audio", "slide");
        }

        internal void TryInitialize()
        {
            if (_disposed || _initialized || _initializationFailed)
            {
                return;
            }
            if (!RuntimeManager.IsInitialized)
            {
                return;
            }

            try
            {
                ResolveChannelGroup();
                LoadSurfaceAudio();
                _initialized = true;
                _logger.LogInfo(
                    "Loaded "
                    + _ownedSounds.Count
                    + " slide WAV(s) across "
                    + _sets.Count
                    + " terrain surface(s) from '"
                    + _audioRoot
                    + "'.");
            }
            catch (Exception exception)
            {
                _initializationFailed = true;
                _logger.LogError(
                    "Slide audio initialization failed: "
                    + exception.GetBaseException().Message);
            }
        }

        internal void SwitchSurface(string requestedSurface, Vector3 position)
        {
            if (!_initialized || _disposed || string.IsNullOrWhiteSpace(requestedSurface))
            {
                return;
            }

            string surface = ResolvePlayableSurface(requestedSurface);
            if (surface == null || string.Equals(surface, _currentSurface, StringComparison.Ordinal))
            {
                return;
            }

            _lastPosition = position;
            MarkAllLoopsForFadeOut();
            SurfaceAudioSet set = _sets[surface];
            PlayOneShot(set.Starts, position, _plugin.Volume);

            Sound loopSound;
            if (TrySelect(set.Loops, out loopSound))
            {
                Channel channel;
                if (TryPlaySound(loopSound, true, position, 0f, 1f, out channel))
                {
                    _loopVoices.Add(new LoopVoice
                    {
                        Surface = surface,
                        Channel = channel,
                        Fade = _plugin.CrossfadeSeconds <= 0f ? 1f : 0f,
                        FadingOut = false
                    });
                }
            }

            _currentSurface = surface;
            if (_plugin.DiagnosticsEnabled)
            {
                _logger.LogInfo(
                    "Slide surface changed from "
                    + requestedSurface
                    + " to playable set "
                    + surface
                    + ".");
            }
        }

        internal void EndSlide(Vector3 position)
        {
            _lastPosition = position;
            if (_initialized
                && _currentSurface != null
                && _sets.TryGetValue(_currentSurface, out SurfaceAudioSet set))
            {
                PlayOneShot(set.Stops, position, _plugin.Volume);
            }

            _currentSurface = null;
            MarkAllLoopsForFadeOut();
        }

        internal void Update(
            Vector3 position,
            Vector3 velocity,
            float horizontalSpeed,
            bool paused)
        {
            if (!_initialized || _disposed)
            {
                return;
            }

            _lastPosition = position;
            float speedT = Mathf.InverseLerp(3f, 12f, horizontalSpeed);
            float volumeScale = Mathf.Lerp(
                _plugin.MinimumSpeedVolumeScale,
                1f,
                speedT);
            float pitchOffset = _plugin.PitchBySpeed;
            float pitch = Mathf.Lerp(1f - pitchOffset, 1f + pitchOffset, speedT);
            float targetVolume = paused ? 0f : _plugin.Volume * volumeScale;
            float crossfade = _plugin.CrossfadeSeconds;
            float fadeStep = crossfade <= 0f
                ? 1f
                : Time.unscaledDeltaTime / crossfade;

            VECTOR fmodPosition = ToFmodVector(position);
            VECTOR fmodVelocity = ToFmodVector(velocity);
            for (int index = _loopVoices.Count - 1; index >= 0; index--)
            {
                LoopVoice voice = _loopVoices[index];
                if (!voice.Channel.hasHandle())
                {
                    _loopVoices.RemoveAt(index);
                    continue;
                }

                voice.Fade = voice.FadingOut
                    ? Mathf.Max(0f, voice.Fade - fadeStep)
                    : Mathf.Min(1f, voice.Fade + fadeStep);
                voice.Channel.set3DAttributes(ref fmodPosition, ref fmodVelocity);
                voice.Channel.set3DMinMaxDistance(
                    _plugin.MinimumDistance,
                    _plugin.MaximumDistance);
                voice.Channel.setPitch(Mathf.Max(0.01f, pitch));
                voice.Channel.setVolume(targetVolume * voice.Fade);

                if (voice.FadingOut && voice.Fade <= 0f)
                {
                    voice.Channel.stop();
                    _loopVoices.RemoveAt(index);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = 0; index < _loopVoices.Count; index++)
            {
                Channel channel = _loopVoices[index].Channel;
                if (channel.hasHandle())
                {
                    channel.stop();
                }
            }
            _loopVoices.Clear();

            for (int index = 0; index < _ownedSounds.Count; index++)
            {
                Sound sound = _ownedSounds[index];
                if (sound.hasHandle())
                {
                    sound.release();
                }
            }
            _ownedSounds.Clear();
            _sets.Clear();

            if (_sfxBusLocked && _sfxBus.isValid())
            {
                _sfxBus.unlockChannelGroup();
            }
            _sfxBusLocked = false;
            _sfxBus = default(Bus);
            _channelGroup = default(ChannelGroup);
        }

        private void ResolveChannelGroup()
        {
            Bus sfxBus;
            if (BusGroup.SFX.TryGetBus(out sfxBus))
            {
                RESULT lockResult = sfxBus.lockChannelGroup();
                if (lockResult == RESULT.OK)
                {
                    ChannelGroup sfxGroup;
                    RESULT groupResult = sfxBus.getChannelGroup(out sfxGroup);
                    if (groupResult == RESULT.OK && sfxGroup.hasHandle())
                    {
                        _sfxBus = sfxBus;
                        _sfxBusLocked = true;
                        _channelGroup = sfxGroup;
                        return;
                    }

                    sfxBus.unlockChannelGroup();
                }
            }

            RESULT masterResult = RuntimeManager.CoreSystem.getMasterChannelGroup(
                out _channelGroup);
            if (masterResult != RESULT.OK || !_channelGroup.hasHandle())
            {
                throw new InvalidOperationException(
                    "Could not access the game's SFX or master FMOD channel group: "
                    + masterResult
                    + ".");
            }

            _logger.LogWarning(
                "Could not attach slide audio directly to the SFX bus; using the FMOD master channel group.");
        }

        private void LoadSurfaceAudio()
        {
            if (!Directory.Exists(_audioRoot))
            {
                throw new DirectoryNotFoundException(
                    "Slide audio root does not exist: " + _audioRoot);
            }

            foreach (KeyValuePair<string, string> mapping in FolderBySurface)
            {
                string folder = Path.Combine(_audioRoot, mapping.Value);
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                SurfaceAudioSet set = new SurfaceAudioSet();
                string[] paths = Directory.EnumerateFiles(
                        folder,
                        "*.wav",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                for (int index = 0; index < paths.Length; index++)
                {
                    string path = paths[index];
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    bool start = fileName.IndexOf("_start_", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool stop = fileName.IndexOf("_stop_", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool loop = fileName.IndexOf("_loop_", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!start && !stop && !loop)
                    {
                        _logger.LogWarning(
                            "Ignoring slide WAV without _loop_, _start_, or _stop_ in its name: "
                            + path);
                        continue;
                    }

                    Sound sound;
                    if (!TryCreateSound(path, loop, out sound))
                    {
                        continue;
                    }

                    _ownedSounds.Add(sound);
                    if (start)
                    {
                        set.Starts.Add(sound);
                    }
                    else if (stop)
                    {
                        set.Stops.Add(sound);
                    }
                    else
                    {
                        set.Loops.Add(sound);
                    }
                }

                if (set.Loops.Count > 0)
                {
                    _sets[mapping.Key] = set;
                }
                else if (paths.Length > 0)
                {
                    _logger.LogWarning(
                        "Slide surface folder has no playable _loop_ WAV: " + folder);
                }
            }

            if (_sets.Count == 0)
            {
                throw new InvalidDataException(
                    "No slide surface folder contains a playable _loop_ WAV.");
            }
        }

        private bool TryCreateSound(string path, bool loop, out Sound sound)
        {
            MODE mode = MODE.DEFAULT
                | MODE._3D
                | MODE._3D_WORLDRELATIVE
                | MODE._3D_LINEARROLLOFF
                | MODE.CREATESAMPLE
                | (loop ? MODE.LOOP_NORMAL : MODE.LOOP_OFF);
            RESULT result = RuntimeManager.CoreSystem.createSound(path, mode, out sound);
            if (result == RESULT.OK && sound.hasHandle())
            {
                return true;
            }

            _logger.LogWarning(
                "FMOD could not load slide WAV '"
                + path
                + "': "
                + result
                + ".");
            sound = default(Sound);
            return false;
        }

        private string ResolvePlayableSurface(string requestedSurface)
        {
            if (_sets.ContainsKey(requestedSurface))
            {
                return requestedSurface;
            }
            if (_sets.ContainsKey("FTS_Ground"))
            {
                if (_plugin.DiagnosticsEnabled)
                {
                    _logger.LogInfo(
                        "No slide audio set is available for "
                        + requestedSurface
                        + "; using FTS_Ground.");
                }
                return "FTS_Ground";
            }
            return null;
        }

        private void MarkAllLoopsForFadeOut()
        {
            for (int index = 0; index < _loopVoices.Count; index++)
            {
                _loopVoices[index].FadingOut = true;
            }
        }

        private void PlayOneShot(List<Sound> sounds, Vector3 position, float volume)
        {
            Sound sound;
            if (!TrySelect(sounds, out sound))
            {
                return;
            }

            Channel ignored;
            TryPlaySound(sound, false, position, volume, 1f, out ignored);
        }

        private bool TryPlaySound(
            Sound sound,
            bool looping,
            Vector3 position,
            float volume,
            float pitch,
            out Channel channel)
        {
            channel = default(Channel);
            RESULT playResult = RuntimeManager.CoreSystem.playSound(
                sound,
                _channelGroup,
                true,
                out channel);
            if (playResult != RESULT.OK || !channel.hasHandle())
            {
                WarnPlayback(
                    "FMOD could not start "
                    + (looping ? "looping" : "one-shot")
                    + " slide audio: "
                    + playResult
                    + ".");
                return false;
            }

            VECTOR fmodPosition = ToFmodVector(position);
            VECTOR fmodVelocity = ToFmodVector(Vector3.zero);
            RESULT result = channel.set3DAttributes(ref fmodPosition, ref fmodVelocity);
            if (result == RESULT.OK)
            {
                result = channel.set3DMinMaxDistance(
                    _plugin.MinimumDistance,
                    _plugin.MaximumDistance);
            }
            if (result == RESULT.OK)
            {
                result = channel.setVolume(Mathf.Clamp01(volume));
            }
            if (result == RESULT.OK)
            {
                result = channel.setPitch(Mathf.Max(0.01f, pitch));
            }
            if (result == RESULT.OK)
            {
                result = channel.setPaused(false);
            }
            if (result == RESULT.OK)
            {
                return true;
            }

            channel.stop();
            WarnPlayback("FMOD could not configure slide audio: " + result + ".");
            channel = default(Channel);
            return false;
        }

        private bool TrySelect(List<Sound> sounds, out Sound sound)
        {
            if (sounds == null || sounds.Count == 0)
            {
                sound = default(Sound);
                return false;
            }

            sound = sounds[_random.Next(sounds.Count)];
            return true;
        }

        private static VECTOR ToFmodVector(Vector3 vector)
        {
            return new VECTOR
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
            };
        }

        private void WarnPlayback(string message)
        {
            if (_playbackWarnings >= 5)
            {
                return;
            }

            _playbackWarnings++;
            _logger.LogWarning(message);
            if (_playbackWarnings == 5)
            {
                _logger.LogWarning(
                    "Further slide-audio playback warnings are suppressed for this session.");
            }
        }
    }
}
