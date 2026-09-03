$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (
    Join-Path $modRoot "src\MainMenuMusic.cs") -Raw
$readme = Get-Content -LiteralPath (Join-Path $modRoot "README.txt") -Raw
$nexus = Get-Content -LiteralPath (
    Join-Path $modRoot "nexus-full-desc.txt") -Raw

foreach ($required in @(
    'private const string MusicBusPath = "bus:/MUSIC";',
    'RuntimeManager.StudioSystem.getBus(',
    'musicBus.lockChannelGroup()',
    'musicBus.getChannelGroup(out musicChannelGroup)',
    'RuntimeManager.CoreSystem.playSound(',
    'ReleaseMusicBus();')) {
    if (!$source.Contains($required)) {
        throw "Main Menu Music mixer routing is missing: $required"
    }
}

if ($source.Contains('getMasterChannelGroup(')) {
    throw "Main Menu Music must not bypass the game's Music mixer through the Core master channel group."
}

foreach ($document in @($readme, $nexus)) {
    if ($document -notmatch 'Master and\s+Music volume controls') {
        throw "Main Menu Music documentation must describe Master and Music mixer control."
    }
}

$audioFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $modRoot "audio") -File |
        Where-Object { $_.Extension -eq ".ksaudio" })
$customFile = Join-Path $modRoot "main_menu_music.wav"
if ($audioFiles.Count -ne 3 -or !(Test-Path -LiteralPath $customFile -PathType Leaf)) {
    throw "Expected three layered .ksaudio files and the custom main_menu_music.wav file."
}

Write-Host "Main Menu Music Music-bus routing contract passed: 4 replacement audio files."
