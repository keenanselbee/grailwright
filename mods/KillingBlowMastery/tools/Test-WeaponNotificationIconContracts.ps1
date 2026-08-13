[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $modRoot "src\KillingBlowMastery.cs") -Raw
$contracts = @(
    'ResolveNotificationIconId(proficiency, item)',
    'GetBuiltInIconIds',
    '_grailFloatingTextSupportsSpecificWeaponIcons',
    'return "one_handed_dagger";',
    'return "one_handed_sword";',
    'return "one_handed_axe";',
    'return "one_handed_blunt";',
    'return "one_handed_spear";',
    'if (GetBoolProperty(item, "IsSickle", false)) return "one_handed_axe";',
    'return "two_handed_sword";',
    'return "two_handed_axe";',
    'return "two_handed_blunt";',
    'return "two_handed_spear";',
    'return fallback;'
)

foreach ($contract in $contracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Missing Killing Blow Mastery weapon notification icon contract: $contract"
    }
}

if ($source.IndexOf('private const string OneHandedBladeSoundPool = "one_handed_blade";', [StringComparison]::Ordinal) -lt 0 -or
    $source.IndexOf('private const string TwoHandedBladeSoundPool = "two_handed_blade";', [StringComparison]::Ordinal) -lt 0) {
    throw "The established spear-to-blade sound fallback pools changed unexpectedly."
}

Write-Output "Killing Blow Mastery weapon notification icon contracts passed."
