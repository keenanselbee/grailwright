[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\GrailFloatingText.cs") -Raw
$deedsSource = Get-Content -LiteralPath (
    Join-Path $repoRoot "mods\DeedsOfAvalon\src\DeedsOfAvalon.cs") -Raw
$iconDirectory = Join-Path $modRoot "icons"
$iconIds = @(
    "magic_blood",
    "magic_fire",
    "magic_cold",
    "magic_poison",
    "magic_electric",
    "magic_pure",
    "magic_wet"
)

Add-Type -AssemblyName System.Drawing

foreach ($iconId in $iconIds) {
    if (-not [regex]::IsMatch(
        $source,
        'BuiltInIconIds\s*=.*?"' + [regex]::Escape($iconId) + '"',
        [Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "The built-in icon list is missing '$iconId'."
    }

    $path = Join-Path $iconDirectory ($iconId + ".png")
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The built-in magic icon is missing: $path"
    }

    $bitmap = [Drawing.Bitmap]::FromFile($path)
    try {
        if ($bitmap.Width -ne 128 -or $bitmap.Height -ne 128) {
            throw "$iconId must be 128x128, but is $($bitmap.Width)x$($bitmap.Height)."
        }
        foreach ($corner in @(
            @(0, 0),
            @(($bitmap.Width - 1), 0),
            @(0, ($bitmap.Height - 1)),
            @(($bitmap.Width - 1), ($bitmap.Height - 1))
        )) {
            if ($bitmap.GetPixel($corner[0], $corner[1]).A -gt 2) {
                throw "$iconId must keep transparent corners."
            }
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$categoryContracts = @{
    blood_magic = '"Blood", "magic_blood", "Red"'
    fire = '"Fire", "magic_fire", "Orange"'
    cold = '"Cold", "magic_cold", "Blue"'
    poison = '"Poison", "magic_poison", "Green"'
    electric = '"Electric", "magic_electric", "Gold"'
    wyrdness = '"Wyrdness", "wyrd", "Wyrd"'
    pure = '"Pure", "magic_pure", "Pale"'
    wet = '"Wet", "magic_wet", "Cyan"'
    other = '"Other", "magic", "White"'
}
foreach ($contract in $categoryContracts.GetEnumerator()) {
    if ($deedsSource.IndexOf($contract.Value, [StringComparison]::Ordinal) -lt 0) {
        throw "Deeds is missing the $($contract.Key) magic icon/color mapping."
    }
}

foreach ($contract in @(
    'string magicType = ResolveSpellMagicType(facts, key);',
    'MagicIcon(magicType)',
    'MagicStyle(magicType)',
    'LimitCountRows(magicRows, _maximumMagicRows.Value, "Other", "White", "magic");'
)) {
    if ($deedsSource.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Deeds is missing the named-spell magic presentation contract: $contract"
    }
}

foreach ($iconId in $iconIds) {
    if ($deedsSource.IndexOf('return "' + $iconId + '";', [StringComparison]::Ordinal) -lt 0) {
        throw "Deeds does not resolve the '$iconId' presentation icon."
    }
}

foreach ($reusedIconId in @("wyrd", "magic")) {
    if ($deedsSource.IndexOf('return "' + $reusedIconId + '";', [StringComparison]::Ordinal) -lt 0) {
        throw "Deeds does not reuse the established '$reusedIconId' presentation icon."
    }
}
foreach ($removedIconId in @("magic_wyrdness", "magic_other")) {
    if (Test-Path -LiteralPath (Join-Path $iconDirectory ($removedIconId + ".png"))) {
        throw "The redundant built-in icon still exists: $removedIconId.png"
    }
    if ($source.IndexOf('"' + $removedIconId + '"', [StringComparison]::Ordinal) -ge 0) {
        throw "The redundant built-in icon ID is still registered: $removedIconId"
    }
}

Write-Output "Magic icon contracts passed."
