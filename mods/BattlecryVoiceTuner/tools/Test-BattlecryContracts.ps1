$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\BattlecryVoiceTuner.cs") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw
$nexusFull = Get-Content -LiteralPath (Join-Path $modRoot "nexus-full-desc.txt") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($manifest.id -ne "BattlecryVoiceTuner" -or
    $manifest.displayName -ne "Battlecry Voice Tuner" -or
    $manifest.pluginGuid -ne "ks.tgfoa.battlecry-voice-tuner" -or
    $manifest.dll -ne "BattlecryVoiceTuner.dll") {
    throw "Battlecry Voice Tuner manifest identity is inconsistent."
}

foreach ($required in @(
    'MaximumBattlecryFilesPerGender = 15',
    'audio"),',
    '"battlecry");',
    '"hero_male_battlecry_*.wav"',
    '"hero_female_battlecry_*.wav"',
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
    'DSP_TYPE.PITCHSHIFT',
    'DSP_PITCHSHIFT.PITCH',
    'DSP_PITCHSHIFT.FFTSIZE',
    'ApplyPitchProcessingToChannel(',
    'RefreshPitchShiftDsps()',
    'BuildPlaybackOrder(',
    'RememberRecentPath(',
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

if ($source -notmatch '(?s)"PitchProcessingMode",\s*PitchProcessingMode\.Natural' -or
    $source -notmatch '(?s)_pitchProcessingMode == null\s*\? PitchProcessingMode\.Natural' -or
    $source -notmatch 'case PitchProcessingMode\.Natural:\s*rateShare = 1f' -or
    $source -notmatch 'case PitchProcessingMode\.TempoPreserving:\s*rateShare = 0f') {
    throw "Natural pitch processing must use the full playback-rate shift by default."
}

if ($source -notmatch 'rateShare = 0\.5f' -or
    $source -notmatch 'PitchDspFftSize = 2048f' -or
    $source -notmatch 'PitchDspAttachTimeoutSeconds = 0\.1f' -or
    $source -notmatch '(?s)QueueEventPitchDsp\(.+setPaused\(true\).+_pendingEventPitchDsps\.Add' -or
    $source -notmatch '(?s)pending\.EventInstance\.setPitch\(.+pending\.EventInstance\.setPaused\(false\)' -or
    $source -notmatch 'ResumePendingEventNaturally\(') {
    throw "Balanced and Tempo Preserving processing must retain their bounded DSP path and natural fallback."
}

foreach ($required in @(
    'VoiceGrowthFullDepthAttributeValue = 40f',
    'VoiceGrowthDeadZone = 0.10f',
    'VoiceGrowthCurvePower = 1.5f',
    'VoiceGrowthPreset.Warrior',
    'VoiceGrowthAttribute.Strength',
    'VoiceGrowthAttribute.Endurance',
    'VoiceGrowthAttribute.Dexterity',
    'VoiceGrowthAttribute.Spirituality',
    'VoiceGrowthAttribute.Practicality',
    'VoiceGrowthAttribute.Perception',
    'stat.BaseValue',
    'stat.ModifiedValue')) {
    if (!$source.Contains($required)) {
        throw "Attribute voice progression contract is missing: $required"
    }
}

foreach ($contract in @(
    @{ Pattern = '(?s)"VoiceGrowthEnabled",\s*true'; Message = 'enabled voice growth default' },
    @{ Pattern = '(?s)"NativeVoiceTuningEnabled",\s*true'; Message = 'enabled native voice tuning default' },
    @{ Pattern = '(?s)"VoiceGrowthPreset",\s*VoiceGrowthPreset\.Warrior'; Message = 'Warrior growth preset default' },
    @{ Pattern = '(?s)"VoiceGrowthMaximumSemitones",\s*-6\.0f'; Message = '-6 semitone growth floor' },
    @{ Pattern = '(?s)"UseTemporaryAttributeModifiers",\s*false'; Message = 'permanent attribute default' },
    @{ Pattern = '(?s)VoiceGrowthAttribute\.Strength,\s*VoiceGrowthAttribute\.Endurance,\s*0\.75f'; Message = '75/25 Warrior weighting' })) {
    if ($source -notmatch $contract.Pattern) {
        throw "Attribute voice progression is missing the $($contract.Message)."
    }
}

foreach ($document in @($readme, $nexusFull)) {
    foreach ($required in @(
        'Warrior',
        'Strength',
        'Endurance',
        'Mage',
        'Adventurer',
        'Custom',
        '-6',
        'Natural',
        'Balanced',
        '41%',
        '19%')) {
        if (!$document.Contains($required)) {
            throw "Voice progression documentation is missing: $required"
        }
    }
}

foreach ($required in @(
    'SoulAndService.SoulAndServiceApi',
    'BloodMagicExpansion.BloodMagicApi',
    '"GetNecromanticPower"',
    '"GetBloodPower"',
    '(0.75f * dominant) + (0.25f * convergence)',
    'blood * (0.75f + (0.25f * soul))',
    'soul * (0.75f + (0.25f * blood))',
    'DSP_TYPE.DISTORTION',
    'DSP_DISTORTION.LEVEL',
    'DSP_TYPE.LOWPASS',
    'DSP_LOWPASS.CUTOFF',
    'DSP_TYPE.ECHO',
    'DSP_ECHO.DELAY',
    'DSP_ECHO.FEEDBACK',
    'DSP_ECHO.WETLEVEL',
    'DemonicVoicePreset.Minimal',
    'DemonicVoicePreset.Demonic',
    'DemonicVoicePreset.Abyssal',
    'DemonicVoicePreset.Custom',
    'DemonicPresetSettings.Minimal',
    'DemonicPresetSettings.Demonic',
    'DemonicPresetSettings.Abyssal',
    'Math.Pow(rawSoul, curveExponent)',
    'Math.Pow(rawBlood, curveExponent)',
    'shadow.setWetDryMix(',
    'WithoutSoulLayers()',
    'TryAttachDemonicDsps(',
    'ActiveChannelDemonicDsps',
    'DemonicVoiceProfile')) {
    if (!$source.Contains($required)) {
        throw "Dynamic demonic voice contract is missing: $required"
    }
}

foreach ($contract in @(
    @{ Pattern = '(?s)"DynamicDemonicVoiceEnabled",\s*true'; Message = 'enabled dynamic demonic voice default' },
    @{ Pattern = '(?s)"DemonicVoicePreset",\s*DemonicVoicePreset\.Demonic'; Message = 'Demonic fresh-config profile' },
    @{ Pattern = '(?s)"MaximumDemonicStrength",\s*1\.0f'; Message = 'full effect-strength default' },
    @{ Pattern = '(?s)"IncludeNativeVocalEvents",\s*true'; Message = 'native vocal inclusion default' },
    @{ Pattern = '(?s)"IncludeBattlecries",\s*true'; Message = 'battlecry inclusion default' },
    @{ Pattern = '(?s)"DemonicProgressionCurveExponent",\s*0\.80f'; Message = 'Demonic custom curve starting point' },
    @{ Pattern = '(?s)"MaximumProgressionPitchSemitones",\s*-3\.25f'; Message = 'Demonic custom pitch starting point' },
    @{ Pattern = '(?s)"MaximumDemonicDistortion",\s*0\.18f'; Message = 'Demonic custom distortion starting point' },
    @{ Pattern = '(?s)"MinimumDemonicLowpassCutoffHz",\s*4200\.0f'; Message = 'Demonic custom low-pass starting point' },
    @{ Pattern = '(?s)"DemonicEchoDelayMs",\s*80\.0f'; Message = 'Demonic custom echo delay starting point' },
    @{ Pattern = '(?s)"MaximumDemonicEchoFeedbackPercent",\s*16\.0f'; Message = 'Demonic custom echo feedback starting point' },
    @{ Pattern = '(?s)"MaximumDemonicEchoWetLevelDb",\s*-25\.0f'; Message = 'Demonic custom echo wet starting point' },
    @{ Pattern = '(?s)"MaximumDemonicShadowPitchSemitones",\s*-7\.0f'; Message = 'Demonic custom shadow pitch starting point' },
    @{ Pattern = '(?s)"MaximumDemonicShadowMixDb",\s*-21\.0f'; Message = 'Demonic custom shadow mix starting point' })) {
    if ($source -notmatch $contract.Pattern) {
        throw "Dynamic demonic voice is missing the $($contract.Message)."
    }
}

foreach ($presetContract in @(
    @{ Pattern = '(?s)DemonicPresetSettings Minimal =.+?1\.0f,.+?-2\.5f,.+?0\.10f,.+?5500f,.+?100f,.+?10f,.+?-36f,.+?-80f'; Message = 'exact legacy Minimal profile' },
    @{ Pattern = '(?s)DemonicPresetSettings Demonic =.+?0\.80f,.+?-3\.25f,.+?0\.18f,.+?4200f,.+?80f,.+?16f,.+?-25f,.+?-7f,.+?-21f'; Message = 'balanced Demonic profile' },
    @{ Pattern = '(?s)DemonicPresetSettings Abyssal =.+?0\.65f,.+?-4\.0f,.+?0\.27f,.+?3200f,.+?60f,.+?22f,.+?-17f,.+?-12f,.+?-14f'; Message = 'strong Abyssal profile' })) {
    if ($source -notmatch $presetContract.Pattern) {
        throw "Dynamic demonic voice is missing the $($presetContract.Message)."
    }
}

if ($source -notmatch '(?s)WithoutSoulLayers\(\).+?false,\s*0f,\s*0f,\s*false' -or
    $source -notmatch '(?s)ScheduleAcousticReflections\(.+?WithoutSoulLayers\(\)') {
    throw 'Synthetic acoustic reflections must not duplicate the supernatural echo or shadow voice.'
}

if ($source -notmatch '(?s)ShouldApplyDemonicVoiceToNativeEvent\(.+?CategoryHitFeedback' -or
    $source -notmatch '(?s)TryTuneEvent\(.+?BuildDemonicVoiceProfile\(.+?QueueEventPitchDsp\(' -or
    $source -notmatch '(?s)TryPlayBattlecry\(.+?BuildDemonicVoiceProfile\(.+?TryPlayBattlecrySound\(') {
    throw 'Dynamic demonic voice must affect supported native vocal events and battlecries while excluding non-vocal hit feedback.'
}

$commandPlaybackBlock = [regex]::Match(
    $source,
    '(?s)private bool TryPlayCommand\(.+?(?=\r?\n\s*private bool HasAnyCommandFiles\()')
if (!$commandPlaybackBlock.Success -or
    $commandPlaybackBlock.Value.Contains('BuildDemonicVoiceProfile(') -or
    $commandPlaybackBlock.Value.Contains('TryAttachDemonicDsps(')) {
    throw 'Spoken commands must remain structurally excluded from dynamic demonic progression.'
}

foreach ($document in @($readme, $nexusFull)) {
    foreach ($required in @(
        'Soul Vigor',
        'Blood Essence',
        '5,000',
        'distortion',
        'low-pass',
        'echo',
        'commands')) {
        if (!$document.Contains($required)) {
            throw "Dynamic demonic voice documentation is missing: $required"
        }
    }
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
    'Voice Growth - Advanced',
    'Demonic Progression',
    'Demonic Progression - Advanced',
    'Native Voice Events',
    'Battlecry',
    'Battlecry Audio',
    'Command Voice',
    'Optional Integrations',
    'Diagnostics')) {
    if (!$source.Contains('"' + $displaySection + '"')) {
        throw "FoA Mod Manager display organization is missing section: $displaySection"
    }
}

if ($source -notmatch '(?s)"HoldTakeAllItemsForBattlecry".+?"Battlecry",\s*"Hold Take All Items"' -or
    $source -notmatch '(?s)"BattlecryAggroRangeMultiplier".+?"Battlecry",\s*"Outdoor Hearing Range Multiplier"') {
    throw "FoA Mod Manager must consolidate battlecry input and challenge controls under Battlecry."
}

if ($source -notmatch '(?s)"BattlecryReverbEnabled".+?"Environment Reverb"' -or
    $source -notmatch '(?s)"CommandVoiceReverbEnabled".+?"Environment Reverb"') {
    throw "Battlecry and command acoustics must use the shared Environment Reverb label."
}

if ($source.Contains('PlayRandomTestSound') -or
    $source.Contains('OnPlayRandomTestSoundChanged') -or
    $source.Contains('IsTestableCategory') -or
    $source.Contains('"Testing"')) {
    throw "The retired Testing config and random native-voice test path must remain removed."
}

if ($source -notmatch '(?s)TryTuneEvent\(.+?_nativeVoiceTuningEnabled == null.+?!_nativeVoiceTuningEnabled\.Value') {
    throw "Native event tuning must honor its independent master control."
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

if ($source -notmatch '(?s)"RecentBattlecryMemory",\s*2' -or
    $source -notmatch '(?s)RememberRecentPath\(\s*pool,\s*path,\s*GetRecentMemory\(_recentBattlecryMemory\)') {
    throw "Battlecries must avoid the previous two successfully played clips per gender by default."
}
if ($source -notmatch '(?s)if \(paths\.Count == 0\s*&& _maleBattlecryPaths\.Count == 0\s*&& _femaleBattlecryPaths\.Count == 0\)\s*\{\s*DiscoverBattlecryFiles\(\);') {
    throw "A missing battlecry gender pool must not reset the other gender's recent history."
}

foreach ($required in @(
    'nameof(VHeroKeys.PlayerKeyBindings)',
    'KeyBindings.UI.Items.TransferItems',
    'inputEvent is UIKeyDownAction',
    'inputEvent is UIKeyHeldAction',
    'inputEvent is UIKeyUpAction',
    'AppendTakeAllItemsBinding',
    'HoldTakeAllItemsForBattlecry',
    'BattlecryHotkey')) {
    if (!$source.Contains($required)) {
        throw "Hold-to-battlecry input contract is missing: $required"
    }
}

if ($source.Contains('KeyBindings.Gameplay.ToggleWeapon') -or
    $source.Contains('HoldToggleWeaponForBattlecry') -or
    $source.Contains('ToggleHeroWeapon(')) {
    throw "Battlecry input must no longer intercept or replay Toggle Weapon."
}

if ($source -notmatch '(?s)"BattlecryCooldownSeconds",\s*1\.5f') {
    throw "Battlecry action cooldown must default to 1.5 seconds."
}

if ($source -notmatch '(?s)"BattlecryAggroRangeMultiplier",\s*3\.0f' -or
    $source -notmatch '(?s)"IndoorBattlecryAggroRangeMultiplier",\s*4\.0f' -or
    $source -notmatch '(?s)ChallengeNearbyEnemies\(Hero hero\).+IsBattlecryIndoors\(.+indoors\s*\? _indoorBattlecryAggroRangeMultiplier\.Value\s*: _battlecryAggroRangeMultiplier\.Value') {
    throw "Battlecry hearing range must default to 3.0 outdoors and 4.0 in interiors."
}

if ($source -notmatch '(?s)new Grailwright\.Shared\.ConfigRecoveryKeepCurrentDefaultRule\(\s*6,\s*"3\. Battlecry",\s*"BattlecryAggroRangeMultiplier"') {
    throw "The former all-environment hearing multiplier must not be imported under its new outdoor-only meaning."
}

if ($source -notmatch 'CurrentConfigSchemaVersion = 10' -or
    $source -notmatch '(?s)"EyesInTheDarkThreat",\s*10\.0f' -or
    $source -notmatch '(?s)_eyesInTheDarkThreat == null\s*\? 10f') {
    throw "Eyes in the Dark integration must request 10 threat by default under schema 10."
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

$battlecryDirectory = Join-Path $modRoot "audio\battlecry"
$battlecrySubdirectories = @(Get-ChildItem -LiteralPath $battlecryDirectory -Directory)
if ($battlecrySubdirectories.Count -ne 0) {
    throw "Battlecry WAVs must remain in one flat audio/battlecry folder."
}

$maleWavs = @(Get-ChildItem -LiteralPath $battlecryDirectory -File |
    Where-Object {
        $_.Name -match '^hero_male_battlecry_\d{2}\.wav$'
    })
if ($maleWavs.Count -ne 15) {
    throw "Expected exactly 15 packaged male battlecries; found $($maleWavs.Count)."
}
foreach ($index in 0..14) {
    $name = 'hero_male_battlecry_{0:D2}.wav' -f $index
    if (!(Test-Path -LiteralPath (Join-Path $battlecryDirectory $name) -PathType Leaf)) {
        throw "Missing male battlecry slot: $name"
    }
}

$femaleWavs = @(Get-ChildItem -LiteralPath $battlecryDirectory -File |
    Where-Object {
        $_.Name -match '^hero_female_battlecry_\d{2}\.wav$'
    })
if ($femaleWavs.Count -ne 12) {
    throw "Expected exactly 12 packaged female battlecries; found $($femaleWavs.Count)."
}
foreach ($index in 0..11) {
    $name = 'hero_female_battlecry_{0:D2}.wav' -f $index
    if (!(Test-Path -LiteralPath (Join-Path $battlecryDirectory $name) -PathType Leaf)) {
        throw "Missing female battlecry slot: $name"
    }
}

$femalePlaceholders = @(Get-ChildItem -LiteralPath $battlecryDirectory -File |
    Where-Object {
        $_.Name -match '^hero_female_battlecry_\d{2}\.wav\.placeholder$'
    })
if ($femalePlaceholders.Count -ne 3) {
    throw "Expected exactly 3 open female battlecry placeholder slots; found $($femalePlaceholders.Count)."
}
foreach ($index in 12..14) {
    $name = 'hero_female_battlecry_{0:D2}.wav.placeholder' -f $index
    if (!(Test-Path -LiteralPath (Join-Path $battlecryDirectory $name) -PathType Leaf)) {
        throw "Missing female battlecry placeholder slot: $name"
    }
}

Write-Host "Battlecry Voice Tuner identity, audio, input, AI, and integration contracts passed."
