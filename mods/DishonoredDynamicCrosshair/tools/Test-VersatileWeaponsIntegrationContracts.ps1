Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Dishonored Versatile Weapons contract failed: $Message"
    }
}

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\Plugin.cs")
$manifestText = Get-Content -Raw -LiteralPath (Join-Path $modRoot "mod.json")
$manifest = $manifestText | ConvertFrom-Json
$versatileWeaponsSource = Get-Content -Raw -LiteralPath (
    Join-Path $repoRoot "mods\VersatileWeapons\src\VersatileWeapons.cs")

Assert-Contract ($manifest.version -eq "3.3.4") "mod.json is not version 3.3.4."
Assert-Contract ($source.Contains('PluginVersion = "3.3.4"')) "PluginVersion is not 3.3.4."
Assert-Contract ($source.Contains('[BepInDependency(VersatileWeaponsPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]')) "Versatile Weapons is not a soft dependency."
Assert-Contract (-not $manifestText.Contains("VersatileWeapons.dll")) "The integration must not take a hard Versatile Weapons assembly reference."

foreach ($token in @(
    '"ks.tgfoa.versatile-weapons"',
    '"VersatileWeapons.VersatileWeaponsApi"',
    '"IsMainHandSuppressed"',
    '"IsOffHandSuppressed"',
    'ReadVersatileWeaponsSuppressedItems(',
    'ReferenceEquals(item, suppressedMainHandItem)',
    'ReferenceEquals(item, suppressedOffHandItem)')) {
    Assert-Contract ($source.Contains($token)) "Dishonored source is missing token: $token"
}

Assert-Contract ([regex]::IsMatch(
    $source,
    'ReadVersatileWeaponsSuppressedItems\([\s\S]*?foreach \(object slot in slots\)[\s\S]*?ReferenceEquals\(item, suppressedMainHandItem\)[\s\S]*?continue;[\s\S]*?_isRangedGetter')) "Suppressed equipment is not excluded before Bow and Magic context detection."

Assert-Contract ($versatileWeaponsSource.Contains('public static class VersatileWeaponsApi')) "Versatile Weapons API is missing."
Assert-Contract ($versatileWeaponsSource.Contains('public const int ApiVersion = 2;')) "Versatile Weapons API is not v2."
Assert-Contract ($versatileWeaponsSource.Contains('public static bool IsMainHandSuppressed()')) "Main-hand suppression query is missing."
Assert-Contract ($versatileWeaponsSource.Contains('public static bool IsOffHandSuppressed()')) "Offhand suppression query is missing."

Write-Output "Dishonored Dynamic Crosshair Versatile Weapons contracts passed."
