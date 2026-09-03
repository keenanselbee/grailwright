[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\KillingBlowMastery.cs")

foreach ($required in @(
    'BusGroup.SFX.TryGetBus(out sfxBus)',
    'sfxBus.lockChannelGroup()',
    'sfxBus.getChannelGroup(',
    'RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel)',
    'private bool TryGetRewardSfxChannelGroup(',
    'private void ReleaseRewardSfxBus()',
    '_rewardSfxBus.unlockChannelGroup()',
    'ReleaseRewardSfxBus();',
    'Skipped reward sound because the game''s FMOD SFX bus was unavailable.')) {
    if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing reward SFX routing contract: $required"
    }
}

foreach ($forbidden in @('getMasterChannelGroup(', 'AudioSource', 'PlayUnityRewardSound', 'EnsureRewardAudioSource')) {
    if ($source.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
        throw "Reward audio must not bypass the game SFX bus: $forbidden"
    }
}

Write-Output 'Killing Blow Mastery reward audio routing contracts passed.'
