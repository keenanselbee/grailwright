$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$modsRoot = Split-Path -Parent $modRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\BattlecryVoiceTuner.cs") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw
$audioReadme = Get-Content -LiteralPath (
    Join-Path $modRoot "audio\README.txt") -Raw
$soulSource = Get-Content -LiteralPath (
    Join-Path $modsRoot "SoulAndService\src\SoulAndService.cs") -Raw
$summonSource = Get-Content -LiteralPath (
    Join-Path $modsRoot "SoulAndService\src\SummonRuntime.cs") -Raw

foreach ($required in @(
    'public static class BattlecryVoiceTunerApi',
    'public const int ApiVersion = 2;',
    'public static bool TryPlayCommand(string commandId)',
    'SummonAttackCommandId = "summon_attack"',
    'SummonHoldCommandId = "summon_hold"',
    'SummonFollowCommandId = "summon_follow"',
    'SummonRecallCommandId = "summon_recall"',
    'SummonGuardCommandId = "summon_guard"',
    'SummonBulwarkCommandId = "summon_bulwark"',
    'SummonHuntCommandId = "summon_hunt"',
    'ShouldYieldTakeAllItemsToSoulAndService()',
    'found = found || object.Equals(',
    '"summon_male_attack_*.wav"',
    '"summon_male_hold_*.wav"',
    '"summon_male_follow_*.wav"',
    '"summon_male_recall_*.wav"',
    '"summon_male_guard_*.wav"',
    '"summon_male_bulwark_*.wav"',
    '"summon_male_hunt_*.wav"',
    '"summon_female_attack_*.wav"',
    '"summon_female_hold_*.wav"',
    '"summon_female_follow_*.wav"',
    '"summon_female_recall_*.wav"',
    '"summon_female_guard_*.wav"',
    '"summon_female_bulwark_*.wav"',
    '"summon_female_hunt_*.wav"',
    'MaximumCommandFilesPerPool = 15',
    'GetCommandPool(',
    'HasAnyCommandFiles()',
    'BuildPlaybackOrder(',
    'RememberRecentPath(',
    'string mostRecent = recent[recent.Count - 1];',
    'fallback.Add(mostRecent);',
    'TryPlayCommandSound(',
    'TryGetCommandChannelGroup(',
    'TryEnsureCommandReverbPaths()',
    'MaximumCommandReflectionTaps = 1',
    'ReleaseCommandReverbPaths()',
    'ReleaseCommandSounds()')) {
    if (!$source.Contains($required)) {
        throw "Command voice runtime contract is missing: $required"
    }
}

foreach ($contract in @(
    @{ Pattern = '(?s)"CommandVoiceEnabled",\s*true'; Message = 'command voice enabled default' },
    @{ Pattern = '(?s)"CommandVoiceVolumeMultiplier",\s*0\.50f'; Message = '0.50 command volume default' },
    @{ Pattern = '(?s)"CommandVoiceReverbEnabled",\s*true'; Message = 'smart reverb enabled default' },
    @{ Pattern = '(?s)"OutdoorCommandVoiceReverbAmount",\s*0\.10f'; Message = '0.10 outdoor reverb default' },
    @{ Pattern = '(?s)"IndoorCommandVoiceReverbAmount",\s*0\.45f'; Message = '0.45 indoor reverb default' },
    @{ Pattern = '(?s)"MaleCommandVoicePitchOffsetSemitones",\s*5\.0f'; Message = '+5 male pitch default' },
    @{ Pattern = '(?s)"FemaleCommandVoicePitchOffsetSemitones",\s*1\.0f'; Message = '+1 female pitch default' },
    @{ Pattern = '(?s)"RecentCommandVoiceMemory",\s*2'; Message = 'two-clip command memory default' },
    @{ Pattern = '(?s)"CommandVoiceCooldownSeconds",\s*0\.75f'; Message = '0.75-second command cooldown default' })) {
    if ($source -notmatch $contract.Pattern) {
        throw "Command voice configuration is missing the $($contract.Message)."
    }
}

$commandBlock = [regex]::Match(
    $source,
    '(?s)private bool TryPlayCommand\(.+?(?=\r?\n\s*private List<string> BuildPlaybackOrder\()')
if (!$commandBlock.Success) {
    throw "Could not isolate the command playback path."
}
if ($commandBlock.Value.Contains('BeginChallenge(') -or
    $commandBlock.Value.Contains('NotifyEyesInTheDark(')) {
    throw "Spoken commands must not trigger battlecry aggro or Wyrd Threat."
}
if ($commandBlock.Value -notmatch '(?s)TryPlayCommandSound\(path, pitch, hero\).+RememberRecentPath\(') {
    throw "Command history must update only after successful FMOD playback."
}
if ($commandBlock.Value -notmatch '(?s)if \(paths\.Count == 0 && !HasAnyCommandFiles\(\)\)\s*\{\s*DiscoverCommandFiles\(\);') {
    throw "A missing command-type pool must not reset the other pools' recent histories."
}

foreach ($required in @(
    'ks.tgfoa.battlecry-voice-tuner',
    'BepInDependency.DependencyFlags.SoftDependency')) {
    if (!$soulSource.Contains($required)) {
        throw "Soul and Service soft integration is missing: $required"
    }
}
foreach ($required in @(
    'BattlecryVoiceTuner.BattlecryVoiceTunerApi',
    'TryPlayCommand',
    'SummonAttackCommandId = "summon_attack"',
    'SummonHoldCommandId = "summon_hold"',
    'SummonFollowCommandId = "summon_follow"',
    'SummonRecallCommandId = "summon_recall"',
    'SummonGuardCommandId = "summon_guard"',
    'SummonBulwarkCommandId = "summon_bulwark"',
    'SummonHuntCommandId = "summon_hunt"',
    'TryPlayCommandVoice(plugin, commandId);')) {
    if (!$summonSource.Contains($required)) {
        throw "Soul and Service command request is missing: $required"
    }
}
$commandSummonsBlock = [regex]::Match(
    $summonSource,
    '(?s)private static int CommandSummons\(.+?(?=\r?\n\s*private static void PublishCommand\()')
if (!$commandSummonsBlock.Success -or
    $commandSummonsBlock.Value -notmatch '(?s)if \(plugin != null && commanded > 0\)\s*\{\s*PublishCommand\(') {
    throw "Soul and Service must request one voice only after at least one summon receives the order."
}
if ($summonSource -notmatch '(?s)RecallHost\(Hero hero\).*?recalled > 0.*?PublishCommand\(\s*plugin,\s*SummonCommandState\.Follow,\s*SummonRecallCommandId,') {
    throw "Soul and Service Recall Host must request the dedicated recall voice only after at least one summon is recalled."
}

$commandDirectory = Join-Path $modRoot "audio\command"
$subdirectories = @(Get-ChildItem -LiteralPath $commandDirectory -Directory)
if ($subdirectories.Count -ne 0) {
    throw "Command WAVs must remain in one flat audio/command folder."
}
$commandWavs = @(Get-ChildItem -LiteralPath $commandDirectory -File |
    Where-Object {
        $_.Name -match '^summon_(male|female)_(attack|hold|follow|recall|guard|bulwark|hunt)_\d+\.wav$'
    })
if ($commandWavs.Count -ne 43) {
    throw "Expected 43 flat command-type WAVs, found $($commandWavs.Count)."
}
$poolSizes = @{
    'male_attack' = 5
    'male_hold' = 5
    'male_follow' = 5
    'male_recall' = 2
    'female_attack' = 4
    'female_hold' = 4
    'female_follow' = 4
    'female_recall' = 2
    'male_guard' = 2
    'male_bulwark' = 2
    'male_hunt' = 2
    'female_guard' = 2
    'female_bulwark' = 2
    'female_hunt' = 2
}
foreach ($pool in $poolSizes.Keys) {
    foreach ($index in 0..($poolSizes[$pool] - 1)) {
        $name = 'summon_{0}_{1}.wav' -f $pool, $index
        if (!(Test-Path -LiteralPath (Join-Path $commandDirectory $name) -PathType Leaf)) {
            throw "Missing command voice slot: $name"
        }
    }
}

foreach ($file in $commandWavs) {
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

foreach ($document in @($readme, $audioReadme)) {
    foreach ($required in @(
        'summon_male_attack_0.wav',
        'summon_female_follow_0.wav',
        'summon_male_recall_0.wav',
        'summon_female_recall_0.wav',
        'summon_male_guard_0.wav',
        'summon_female_bulwark_0.wav',
        'summon_male_hunt_0.wav',
        '-15 LUFS',
        '-2 dBTP',
        'RecentCommandVoiceMemory',
        '0.50',
        '0.10',
        '0.45')) {
        if (!$document.Contains($required)) {
            throw "Command voice documentation is missing: $required"
        }
    }
}

Write-Host "Battlecry Voice Tuner command API, audio, repeat, and acoustic contracts passed: 43 WAV files."
