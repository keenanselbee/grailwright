$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\BattlecryVoiceTuner.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($manifest.id -ne "BattlecryVoiceTuner" -or
    $manifest.displayName -ne "Battlecry Voice Tuner" -or
    $manifest.version -ne "1.1.0" -or
    $manifest.pluginGuid -ne "ks.tgfoa.battlecry-voice-tuner" -or
    $manifest.dll -ne "BattlecryVoiceTuner.dll") {
    throw "Battlecry Voice Tuner manifest identity is inconsistent."
}

foreach ($required in @(
    'MaximumBattlecryFilesPerGender = 15',
    'audio"),',
    '"battlecry"),',
    '"male")',
    '"female")',
    'hero.GetGender() == Gender.Female',
    '? _femaleBattlecryPitchOffsetSemitones.Value',
    ': _maleBattlecryPitchOffsetSemitones.Value',
    'BusGroup.SFX.TryGetBus(out sfxBus)',
    'sfxBus.lockChannelGroup()',
    'sfxBus.getChannelGroup(',
    '_battlecryVolumeMultiplier.Value',
    'typeof(VCRainChecker)',
    '"OnFirstVolumeEnter"',
    '"OnAllVolumesExit"',
    '"OnDiscard"',
    'World.Services.TryGet<SceneService>()',
    '!sceneService.IsOpenWorld',
    'createChannelGroup(',
    'DSP_TYPE.SFXREVERB',
    'DSP_SFXREVERB.WETLEVEL',
    'OutdoorProbeDirectionCount = 24',
    'OutdoorReflectionSpeedOfSound = 343f',
    'TryProbeOutdoorAcoustics(',
    'Physics.Raycast(',
    'QueryTriggerInteraction.Ignore',
    'MODE._3D',
    'set3DAttributes(',
    'setLowPassGain(',
    'setDelay(',
    'MaximumOutdoorReflectionTaps = 3',
    'InteriorProbeDirectionCount = 30',
    'InteriorDiscreteReflectionMinimumDelay = 0.06f',
    'TryProbeInteriorAcoustics(',
    'BuildInteriorProbeDirections()',
    '"open-roof"',
    '"corridor"',
    '"small-room"',
    '"medium-room"',
    '"large-hall"',
    '"large-cavern"',
    'ReleaseBattlecryReverbPaths()',
    'channel.setPitch(',
    'channel.setVolume(',
    'PickBattlecryIndex(',
    'ReleaseBattlecrySounds()')) {
    if (!$source.Contains($required)) {
        throw "Gender-aware battlecry audio contract is missing: $required"
    }
}

if ($source.Contains('getMasterChannelGroup(')) {
    throw "Battlecry playback must not bypass the game's SFX mixer bus through the master channel group."
}

if ($source -notmatch '(?s)"PitchSemitones",\s*0\.0f') {
    throw "Overall pitch must default to a neutral 0 semitones."
}

if ($source -notmatch '(?s)"RandomPitchSemitones",\s*0\.15f') {
    throw "Random pitch variation must default to 0.15 semitones."
}

if ($source -notmatch '(?s)"BattlecryVolumeMultiplier",\s*0\.5f') {
    throw "Battlecry volume scaling must have an independent 0.5 default."
}

if ($source -notmatch '(?s)"BattlecryReverbEnabled",\s*true' -or
    $source -notmatch '(?s)"OutdoorBattlecryReverbAmount",\s*0\.15f' -or
    $source -notmatch '(?s)"IndoorBattlecryReverbAmount",\s*0\.70f') {
    throw "Dynamic battlecry reverb must default to enabled with light 0.15 outdoor and heavy 0.70 indoor amounts."
}

if ($source.Contains('"OnStay"')) {
    throw "Dynamic battlecry reverb must not use continuous volume polling."
}

if ($source -notmatch '(?s)TryPlayBattlecrySound\(.+TryGetBattlecryChannelGroup\(.+TryProbeOutdoorAcoustics\(') {
    throw "Outdoor acoustic sampling must remain on the battlecry playback path rather than a continuous update loop."
}

if ($source -notmatch '(?s)TryGetBattlecryChannelGroup\(.+TryProbeInteriorAcoustics\(' -or
    $source -notmatch '(?s)"small-room".+\? 0') {
    throw "Interior acoustic sampling must remain event-driven and must suppress discrete echoes in small rooms."
}

foreach ($displaySection in @(
    'General',
    'Voice Tuning',
    'Native Voice Events',
    'Battlecry Audio',
    'Battlecry Input',
    'Battlecry Challenge',
    'Optional Integrations',
    'Testing',
    'Diagnostics')) {
    if (!$source.Contains('"' + $displaySection + '"')) {
        throw "FoA Mod Manager display organization is missing section: $displaySection"
    }
}

foreach ($required in @(
    'FoASettingUiMetadata',
    'DisplaySection = displaySection',
    'SectionOrder = sectionOrder',
    'Order = order')) {
    if (!$source.Contains($required)) {
        throw "FoA Mod Manager display metadata contract is missing: $required"
    }
}

if ($source -notmatch '(?s)"MaleBattlecryPitchOffsetSemitones",\s*0\.0f' -or
    $source -notmatch '(?s)"FemaleBattlecryPitchOffsetSemitones",\s*0\.0f') {
    throw "Gender-specific battlecry pitch offsets must default to 0 semitones."
}

foreach ($required in @(
    'KeyBindings.Gameplay.ToggleWeapon',
    'inputEvent is UIKeyDownAction',
    'inputEvent is UIKeyHeldAction',
    'inputEvent is UIKeyUpAction',
    'ToggleHeroWeapon(hero)',
    'HoldToggleWeaponForBattlecry',
    'BattlecryHotkey')) {
    if (!$source.Contains($required)) {
        throw "Tap-or-hold battlecry input contract is missing: $required"
    }
}

if ($source -notmatch '(?s)"BattlecryCooldownSeconds",\s*1\.5f') {
    throw "Battlecry action cooldown must default to 1.5 seconds."
}

if ($source -notmatch '(?s)"BattlecryAggroRangeMultiplier",\s*3\.0f') {
    throw "Battlecry hearing range multiplier must default to 3.0."
}

if ($source -notmatch 'CurrentConfigSchemaVersion = 5' -or
    $source -notmatch '(?s)"EyesInTheDarkThreat",\s*10\.0f' -or
    $source -notmatch '(?s)_eyesInTheDarkThreat == null\s*\? 10f') {
    throw "Eyes in the Dark integration must request 10 threat by default under schema 5."
}

foreach ($required in @(
    'NpcAI.AllWorkingAI',
    'npc.IsHostileToHero()',
    'ai.Data.perception.MaxHearingRange',
    'npc.NpcStats.Hearing',
    'AINoises.BlockedByWalls(',
    'ai.AlertStack.NewPoi(',
    'AlertStack.AlertStrength.Strong',
    'ai.EnterCombatWith(hero)',
    '_challengedNpcs.Add(ai)')) {
    if (!$source.Contains($required)) {
        throw "Enemy challenge contract is missing: $required"
    }
}

foreach ($required in @(
    'EyesInTheDark.EyesInTheDarkBattlecryApi, EyesInTheDark',
    'TryRegisterBattlecry',
    'EyesInTheDarkThreat')) {
    if (!$source.Contains($required)) {
        throw "Eyes in the Dark integration contract is missing: $required"
    }
}

$maleFolder = Join-Path $modRoot "audio\battlecry\male"
$maleWavs = @(Get-ChildItem -LiteralPath $maleFolder -File -Filter "*.wav")
if ($maleWavs.Count -ne 15) {
    throw "Expected exactly 15 packaged male battlecries; found $($maleWavs.Count)."
}

$femaleFolder = Join-Path $modRoot "audio\battlecry\female"
$femaleWavs = @(Get-ChildItem -LiteralPath $femaleFolder -File -Filter "*.wav")
if ($femaleWavs.Count -ne 12) {
    throw "Expected exactly 12 packaged female battlecries; found $($femaleWavs.Count)."
}

$femalePlaceholders = @(Get-ChildItem -LiteralPath $femaleFolder -File -Filter "*.wav.placeholder")
if ($femalePlaceholders.Count -ne 3) {
    throw "Expected exactly 3 open female battlecry placeholder slots; found $($femalePlaceholders.Count)."
}

Write-Host "Battlecry Voice Tuner identity, audio, input, AI, and integration contracts passed."
