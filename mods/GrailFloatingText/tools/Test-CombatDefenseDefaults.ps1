[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\GrailFloatingText.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw

if (-not [regex]::IsMatch(
    $source,
    'Config\.Bind\("9\. Default Game Events",\s*"NotifyBlockedDamage",\s*false,')) {
    throw "NotifyBlockedDamage must be disabled by default."
}

if (-not [regex]::IsMatch(
    $source,
    'Config\.Bind\("9\. Default Game Events",\s*"NotifyParriedDamage",\s*true,')) {
    throw "NotifyParriedDamage must remain enabled by default."
}

$parryStart = $source.IndexOf("private void OnHeroParriedDamage(", [StringComparison]::Ordinal)
$parryEnd = $source.IndexOf("private void OnPickpocketSuccess(", $parryStart, [StringComparison]::Ordinal)
if ($parryStart -lt 0 -or $parryEnd -le $parryStart) {
    throw "Could not locate the parry notification method."
}

$parryMethod = $source.Substring($parryStart, $parryEnd - $parryStart)
if ($parryMethod.IndexOf('"parry"', [StringComparison]::Ordinal) -lt 0) {
    throw "Parried damage notifications must use the dedicated parry icon."
}
if (-not [regex]::IsMatch($source, 'BuiltInIconIds\s*=.*?"parry"', [Text.RegularExpressions.RegexOptions]::Singleline)) {
    throw "The parry icon must be registered as a public built-in icon ID."
}

Write-Output "Combat defense default contract passed."
