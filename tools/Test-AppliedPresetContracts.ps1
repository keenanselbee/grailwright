[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$contracts = @(
    'mods\SoulAndService\tools\Test-BalanceProfileContracts.ps1'
    'mods\BloodMagicExpansion\tools\Test-PresetContracts.ps1'
    'mods\BattlecryVoiceTuner\tools\Test-DemonicPresetContracts.ps1'
    'mods\EyesInTheDark\tools\Test-GameplayPresetContracts.ps1'
    'mods\SteelAndBone\tools\Test-AppliedPresetContracts.ps1'
)

foreach ($relativePath in $contracts) {
    $contractPath = Join-Path $repoRoot $relativePath
    if (!(Test-Path -LiteralPath $contractPath -PathType Leaf)) {
        throw "Missing applied-preset contract: $relativePath"
    }

    & $contractPath
}

Write-Host "Applied-preset contracts passed for $($contracts.Count) mods."
