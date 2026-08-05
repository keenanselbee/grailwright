$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\BattlecryVoiceTuner.cs") -Raw
$manifest = Get-Content -LiteralPath (Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

if ($manifest.id -ne "BattlecryVoiceTuner" -or
    $manifest.displayName -ne "Battlecry Voice Tuner" -or
    $manifest.version -ne "1.0.0" -or
    $manifest.pluginGuid -ne "ks.tgfoa.battlecry-voice-tuner" -or
    $manifest.dll -ne "BattlecryVoiceTuner.dll") {
    throw "Battlecry Voice Tuner manifest identity is inconsistent."
}

foreach ($required in @(
    'MaximumBattlecryFilesPerGender = 10',
    'audio"),',
    '"battlecry"),',
    '"male")',
    '"female")',
    'hero.GetGender() == Gender.Female',
    'GetShiftedSemitones()',
    'channel.setPitch(',
    'channel.setVolume(',
    'PickBattlecryIndex(',
    'ReleaseBattlecrySounds()')) {
    if (!$source.Contains($required)) {
        throw "Gender-aware battlecry audio contract is missing: $required"
    }
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

if ($source -notmatch '(?s)"BattlecryCooldownSeconds",\s*3\.0f') {
    throw "Battlecry action cooldown must default to 3 seconds."
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

foreach ($gender in @("male", "female")) {
    $folder = Join-Path $modRoot "audio\battlecry\$gender"
    $placeholders = @(Get-ChildItem -LiteralPath $folder -File -Filter "*.wav.placeholder")
    if ($placeholders.Count -ne 10) {
        throw "Expected exactly 10 $gender battlecry placeholder slots; found $($placeholders.Count)."
    }
}

Write-Host "Battlecry Voice Tuner identity, audio, input, AI, and integration contracts passed."
