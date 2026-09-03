$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$pluginSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulAndService.cs") -Raw
$salvageSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageRuntime.cs") -Raw
$audioSource = Get-Content -LiteralPath (
    Join-Path $modRoot "src\SoulSalvageAudioRuntime.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw
$matrix = Get-Content -LiteralPath (
    Join-Path $modRoot "docs\TEST-MATRIX.md") -Raw

foreach ($required in @(
    'internal ConfigEntry<bool> PlaySoulSalvageAudio',
    'internal ConfigEntry<float> SoulSalvageAudioVolume',
    'internal ConfigEntry<float> SoulSalvageAudioRangeVolume',
    'internal ConfigEntry<bool> AvoidRecentSoulSalvageAudioRepeats',
    'internal ConfigEntry<int> RecentSoulSalvageAudioMemory',
    'internal ConfigEntry<float> SoulSalvageAudioRandomPitchSemitones',
    'internal ConfigEntry<float> FemaleSoulSalvageAudioPitchSemitones',
    'internal ConfigEntry<float> MaleSoulSalvageAudioPitchSemitones',
    'internal ConfigEntry<float> FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones',
    'internal ConfigEntry<float> MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones',
    'internal ConfigEntry<float> NonHumanoidSoulSalvageAudioPitchSemitones',
    'internal ConfigEntry<float> SoulSalvageAudioEchoAmount',
    'internal ConfigEntry<bool> PlaySoulRendImpactAudio',
    'internal ConfigEntry<float> SoulRendImpactAudioVolume',
    '"PlaySoulSalvageAudio",',
    '"SoulSalvageAudioVolume",',
    '0.85f,',
    '"SoulSalvageAudioRangeVolume",',
    '1.0f,',
    '"AvoidRecentSoulSalvageAudioRepeats",',
    '"RecentSoulSalvageAudioMemory",',
    '"SoulSalvageAudioRandomPitchSemitones",',
    '0.20f,',
    '"FemaleSoulSalvageAudioPitchSemitones",',
    '3.0f,',
    '"MaleSoulSalvageAudioPitchSemitones",',
    '-3.0f,',
    '"FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones",',
    '-1.0f,',
    '"MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones",',
    '"NonHumanoidSoulSalvageAudioPitchSemitones",',
    '-6.0f,')) {
    if (!$pluginSource.Contains($required)) {
        throw "Soul Salvage audio configuration is missing: $required"
    }
}

if ($pluginSource -notmatch '(?s)"SoulRendImpactAudioVolume",\s*0\.8f,' -or
    $audioSource -notmatch '(?s)plugin\.SoulRendImpactAudioVolume == null\s*\? 0\.8f') {
    throw "Soul Rend impact audio does not default to 0.8 in configuration and runtime fallback."
}

foreach ($required in @(
    'private const int TierSoundSlots = 10;',
    'FMOD.MODE.DEFAULT | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE',
    'AvoidRecentSoulSalvageAudioRepeats.Value',
    'RecentSoulSalvageAudioMemory.Value',
    'SoulSalvageAudioRandomPitchSemitones.Value',
    'FemaleSoulSalvageAudioPitchSemitones.Value',
    'MaleSoulSalvageAudioPitchSemitones.Value',
    'FemaleMonsterSoulSalvageAudioPitchAdjustmentSemitones',
    'MaleMonsterSoulSalvageAudioPitchAdjustmentSemitones',
    'NonHumanoidSoulSalvageAudioPitchSemitones.Value',
    'SoulSalvageAudioTargetClass.FemaleMonster',
    'SoulSalvageAudioTargetClass.MaleMonster',
    'SoulSalvageAudioTargetClass.UnknownMonster',
    'MaximumRangeDistance = 30.0f',
    'MinimumRangeVolume = 0.10f',
    'GetRangeVolumeMultiplier(',
    'Vector3.Distance(hero.Coords, sourcePosition)',
    'Mathf.Pow(2.0f, semitones / 12.0f)',
    'GetTierFallbacks(',
    'SoundsByPath[path] = sound;',
    'BusGroup.SFX.TryGetBus(out sfxBus)',
    'sfxBus.lockChannelGroup()',
    'sfxBus.getChannelGroup(out sfxChannelGroup)',
    'ReleaseSfxBus();',
    'RuntimeManager.CoreSystem.playSound(',
    'FMOD.Channel channel;',
    'pair.Value.release();')) {
    if (!$audioSource.Contains($required)) {
        throw "Soul Salvage FMOD runtime contract is missing: $required"
    }
}
if ($audioSource.Contains('getMasterChannelGroup(')) {
    throw "Soul Rend audio must not bypass the game's SFX mixer through the Core master channel group."
}
foreach ($required in @(
    'private const int MaximumPendingEchoes = 24;',
    'SoulSalvageAudioEchoAmount.Value',
    'Volume = volume * amount * 0.45f',
    'PlayAt = now + 0.16f',
    'Volume = volume * amount * 0.25f',
    'PlayAt = now + 0.34f',
    'PendingEchoes.Clear();')) {
    if (!$audioSource.Contains($required)) {
        throw "Soul Rend echo contract is missing: $required"
    }
}
foreach ($required in @(
    'private const int ImpactSoundSlots = 4;',
    'private const int MaximumImpactVoices = 4;',
    'ImpactDuplicateCooldownSeconds = 0.10f;',
    '"soul_salvage_" + name + "_"',
    'internal static void PlayImpact(',
    'PruneImpactChannels();',
    'channel.setPitch(1.0f);')) {
    if (!$audioSource.Contains($required)) {
        throw "Soul Rend impact contract is missing: $required"
    }
}
if ($audioSource -notmatch '(?s)internal static void PlayImpact\(.+?float volume = baseVolume \* GetRangeVolumeMultiplier\(\s*plugin,\s*hasSourcePosition,\s*sourcePosition\);') {
    throw "Soul Rend impact audio does not use target-distance volume attenuation."
}
if ($salvageSource -notmatch '(?s)Vector3 targetCoords = target\.Coords;.*?Vector3 impactPosition =.*?target\.HealthElement\.TakeDamage\(damage\);.*?SoulSalvageAudioRuntime\.PlayImpact\(\s*false,\s*true,\s*impactPosition\);' -or
    $salvageSource -match '(?s)target\.HealthElement\.TakeDamage\(damage\);.*?SoulSalvageAudioRuntime\.PlayImpact\(\s*false,\s*true,\s*target\.Coords\);') {
    throw "Living-target Soul Rend does not preserve a safe impact position before damage can discard the target."
}

if ($audioSource -notmatch '(?s)for \(int index = 0; index < TierSoundSlots; index\+\+\).*?"soul_salvage_" \+ tier \+ "_"') {
    throw "Soul Salvage audio does not scan numbered 0-9 tier slots."
}
if ($audioSource -notmatch '(?s)case Grailwright\.Shared\.CorpseQualityTier\.Worthy:.*?return MediumTier;.*?case Grailwright\.Shared\.CorpseQualityTier\.Potent:.*?return HighTier;.*?case Grailwright\.Shared\.CorpseQualityTier\.Prime:.*?return MaxTier;') {
    throw "Corpse quality does not map to low, medium, high, and max audio tiers."
}

foreach ($required in @(
    'SoulSalvageAudioRuntime.Shutdown();',
    'CompleteLightSummonHarvest(__instance);',
    'CompleteLightSummonHarvest(_lightTarget);',
    'GetSoulSalvageAudioTargetClass(',
    'NonHumanoidSoulAudioTerms',
    'sourceNpc.GetGender()',
    'dummy.GetGender()',
    'hasAudioPosition,',
    'audioTargetClass);')) {
    if (!$salvageSource.Contains($required)) {
        throw "Soul Salvage playback hook is missing: $required"
    }
}
if ($salvageSource -notmatch '(?s)CompleteLightSummonHarvest\(.*?PlayServantSoulRendFeedback\(summon, raisedRecord\)' -or
    $salvageSource -notmatch '(?s)PlayServantSoulRendFeedback\(.*?SoulSalvageAudioRuntime\.Play\(\s*audioTier,\s*hasAudioPosition,\s*audioPosition,\s*GetSoulSalvageAudioTargetClass') {
    throw "Each staged Soul Rend servant resolution does not share immediate quality-matched ritual audio."
}
if ($salvageSource -match '(?s)if \(record\.Sacrificed\)\s*\{\s*SoulSalvageAudioRuntime\.Play') {
    throw "Raised-servant audio is still deferred to remains cleanup."
}
$corpseHarvestBlock = [regex]::Match(
    $salvageSource,
    '(?s)private static void TryHarvestCorpse\(.+?(?=\r?\n\s*private static void ApplySoulRend\()')
$classificationIndex = $corpseHarvestBlock.Value.IndexOf(
    'SoulSalvageAudioTargetClass audioTargetClass =')
$remainsIndex = $corpseHarvestBlock.Value.IndexOf('TryCreateRemains(')
$classificationCapturedBeforeRemains = $corpseHarvestBlock.Success -and
    $classificationIndex -ge 0 -and $classificationIndex -lt $remainsIndex
if (!$classificationCapturedBeforeRemains) {
    throw "Corpse audio classification must be captured before remains replacement discards its source."
}
$livingBlock = [regex]::Match(
    $salvageSource,
    '(?s)private static void ApplySoulRend\(.+?(?=\r?\n\s*private static float GetSoulRendPowerMultiplier\()')
if (!$livingBlock.Success -or $livingBlock.Value.Contains('SoulSalvageAudioRuntime.Play(')) {
    throw "Soul Rend must not use the ritual audio bank."
}
$claimBlock = [regex]::Match(
    $salvageSource,
    '(?s)private static void TryClaimLivingTarget\(.+?(?=\r?\n\s*private static float GetSoulClaimPowerThreshold\()')
if (!$claimBlock.Success -or $claimBlock.Value.Contains('SoulSalvageAudioRuntime.Play(')) {
    throw "Soul Claim must not use the ritual audio bank."
}

if (!$manifest.Contains('"src/SoulSalvageAudioRuntime.cs"')) {
    throw "Soul Salvage audio runtime is missing from mod.json sourceFiles."
}

$audioDirectory = Join-Path $modRoot "audio"
$wavFiles = @(Get-ChildItem -LiteralPath $audioDirectory -Filter "*.wav" -File)
if ($wavFiles.Count -ne 48) {
    throw "Expected exactly 48 Soul Rend WAV files, found $($wavFiles.Count)."
}

foreach ($tier in @('low', 'medium', 'high', 'max')) {
    foreach ($index in 0..9) {
        $path = Join-Path $audioDirectory "soul_salvage_${tier}_${index}.wav"
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing Soul Salvage audio slot: $path"
        }
    }
}
foreach ($kind in @('impactlight', 'impactheavy')) {
    foreach ($index in 0..3) {
        $path = Join-Path $audioDirectory "soul_salvage_${kind}_${index}.wav"
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing Soul Rend impact slot: $path"
        }
    }
}

foreach ($file in $wavFiles) {
    $stream = [IO.File]::OpenRead($file.FullName)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ([Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'RIFF') {
            throw "$($file.Name) is not a RIFF WAV."
        }
        [void]$reader.ReadUInt32()
        if ([Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'WAVE') {
            throw "$($file.Name) is not a WAVE file."
        }
        $formatFound = $false
        while ($stream.Position + 8 -le $stream.Length) {
            $chunk = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
            $size = $reader.ReadUInt32()
            $next = $stream.Position + $size + ($size % 2)
            if ($chunk -eq 'fmt ') {
                $format = $reader.ReadUInt16()
                $channels = $reader.ReadUInt16()
                $sampleRate = $reader.ReadUInt32()
                [void]$reader.ReadUInt32()
                [void]$reader.ReadUInt16()
                $bits = $reader.ReadUInt16()
                if ($format -ne 1 -or $channels -ne 2 -or
                    $sampleRate -ne 48000 -or $bits -ne 16) {
                    throw "$($file.Name) must remain PCM 16-bit, 48 kHz stereo."
                }
                $formatFound = $true
            }
            $stream.Position = $next
        }
        if (!$formatFound) {
            throw "$($file.Name) has no readable fmt chunk."
        }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

foreach ($document in @($readme, $matrix)) {
    foreach ($required in @(
        '0.85',
        '0.20',
        '+3',
        '-3',
        '+2',
        '-6',
        '30',
        'Meager',
        'Worthy',
        'Potent',
        'Prime')) {
        if (!$document.Contains($required)) {
            throw "Soul Salvage audio documentation is missing: $required"
        }
    }
}
foreach ($required in @('impact', 'light', 'heavy')) {
    if (!$nexus.Contains($required)) {
        throw "Nexus description is missing concise Soul Rend impact audio coverage: $required"
    }
}

Write-Host "Soul Salvage tiered FMOD audio contracts passed: 40 authored WAV files."
