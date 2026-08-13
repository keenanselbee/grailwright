namespace TorchlightRekindled
{
    internal struct HaloAnimationFrame
    {
        internal readonly float LightParryScaleMultiplier;
        internal readonly float BlockVisibilityMultiplier;

        internal HaloAnimationFrame(
            float lightParryScaleMultiplier,
            float blockVisibilityMultiplier)
        {
            LightParryScaleMultiplier = lightParryScaleMultiplier;
            BlockVisibilityMultiplier = blockVisibilityMultiplier;
        }
    }

    internal sealed class HaloAnimationState
    {
        private const float LightParryRecoverySeconds = 0.3f;
        private const float BlockFadeOutSeconds = 0.2f;
        private const float BlockRecoverySeconds = 0.2f;
        private const float BlockPommelRecoverySeconds = 0.4f;

        private float _lightParryRecoveryStartedAt = -1f;
        private float _blockFadeOutStartedAt = -1f;
        private float _blockFadeOutStartedFrom = 1f;
        private float _blockRecoveryStartedAt = -1f;
        private float _blockRecoveryStartedFrom;
        private float _blockRecoverySeconds = BlockRecoverySeconds;
        private float _blockVisibilityMultiplier = 1f;
        private bool _wasLightParryActive;
        private bool _wasBlockActive;
        private bool _blockSequenceHadPommel;

        internal HaloAnimationFrame Update(
            float time,
            bool lightParryActive,
            bool blockActive,
            bool blockPommelActive)
        {
            if (blockActive && blockPommelActive)
            {
                _blockSequenceHadPommel = true;
            }

            float lightParryScaleMultiplier;
            if (lightParryActive)
            {
                lightParryScaleMultiplier = 0.5f;
                _lightParryRecoveryStartedAt = -1f;
            }
            else
            {
                if (_wasLightParryActive)
                {
                    _lightParryRecoveryStartedAt = time;
                }

                if (_lightParryRecoveryStartedAt >= 0f)
                {
                    float recoveryProgress = Clamp01(
                        (time - _lightParryRecoveryStartedAt)
                        / LightParryRecoverySeconds);
                    lightParryScaleMultiplier = SmoothStep(
                        0.5f,
                        1f,
                        recoveryProgress);
                    if (recoveryProgress >= 1f)
                    {
                        _lightParryRecoveryStartedAt = -1f;
                    }
                }
                else
                {
                    lightParryScaleMultiplier = 1f;
                }
            }
            _wasLightParryActive = lightParryActive;

            float blockVisibilityMultiplier;
            if (lightParryActive)
            {
                blockVisibilityMultiplier = 1f;
                _blockFadeOutStartedAt = -1f;
                _blockRecoveryStartedAt = -1f;
                _blockSequenceHadPommel = false;
            }
            else if (blockActive)
            {
                if (!_wasBlockActive)
                {
                    _blockFadeOutStartedAt = time;
                    _blockFadeOutStartedFrom =
                        _blockVisibilityMultiplier;
                }

                if (_blockFadeOutStartedAt >= 0f)
                {
                    float fadeOutProgress = Clamp01(
                        (time - _blockFadeOutStartedAt)
                        / BlockFadeOutSeconds);
                    blockVisibilityMultiplier = SmoothStep(
                        _blockFadeOutStartedFrom,
                        0f,
                        fadeOutProgress);
                    if (fadeOutProgress >= 1f)
                    {
                        _blockFadeOutStartedAt = -1f;
                    }
                }
                else
                {
                    blockVisibilityMultiplier = 0f;
                }
                _blockRecoveryStartedAt = -1f;
            }
            else
            {
                if (_wasBlockActive)
                {
                    _blockFadeOutStartedAt = -1f;
                    _blockRecoveryStartedAt = time;
                    _blockRecoveryStartedFrom =
                        _blockVisibilityMultiplier;
                    _blockRecoverySeconds = _blockSequenceHadPommel
                        ? BlockPommelRecoverySeconds
                        : BlockRecoverySeconds;
                    _blockSequenceHadPommel = false;
                }

                if (_blockRecoveryStartedAt >= 0f)
                {
                    float recoveryProgress = Clamp01(
                        (time - _blockRecoveryStartedAt)
                        / _blockRecoverySeconds);
                    blockVisibilityMultiplier = SmoothStep(
                        _blockRecoveryStartedFrom,
                        1f,
                        recoveryProgress);
                    if (recoveryProgress >= 1f)
                    {
                        _blockRecoveryStartedAt = -1f;
                    }
                }
                else
                {
                    blockVisibilityMultiplier = 1f;
                }
            }
            _blockVisibilityMultiplier = blockVisibilityMultiplier;
            _wasBlockActive = blockActive;

            return new HaloAnimationFrame(
                lightParryScaleMultiplier,
                blockVisibilityMultiplier);
        }

        internal void Reset()
        {
            _lightParryRecoveryStartedAt = -1f;
            _blockFadeOutStartedAt = -1f;
            _blockFadeOutStartedFrom = 1f;
            _blockRecoveryStartedAt = -1f;
            _blockRecoveryStartedFrom = 0f;
            _blockRecoverySeconds = BlockRecoverySeconds;
            _blockVisibilityMultiplier = 1f;
            _wasLightParryActive = false;
            _wasBlockActive = false;
            _blockSequenceHadPommel = false;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }
            return value >= 1f ? 1f : value;
        }

        private static float SmoothStep(float from, float to, float progress)
        {
            float t = Clamp01(progress);
            t = t * t * (3f - 2f * t);
            return from + (to - from) * t;
        }
    }
}
