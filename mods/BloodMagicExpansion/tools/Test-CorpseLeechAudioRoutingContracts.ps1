[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\BloodMagicExpansion.cs")

foreach ($required in @(
    'BusGroup.SFX.TryGetBus(out sfxBus)',
    'sfxBus.lockChannelGroup()',
    'sfxBus.getChannelGroup(',
    'RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel)',
    'private bool TryGetCorpseLeechSfxChannelGroup(',
    'private void ReleaseCorpseLeechSfxBus()',
    '_corpseLeechSfxBus.unlockChannelGroup()',
    'ReleaseCorpseLeechSfxBus();')) {
    if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing corpse leech SFX routing contract: $required"
    }
}

if ($source.IndexOf('getMasterChannelGroup(', [StringComparison]::Ordinal) -ge 0) {
    throw 'Corpse leech playback must not use FMOD Core master routing.'
}

Write-Output 'Blood Magic Expansion corpse leech audio routing contracts passed.'
