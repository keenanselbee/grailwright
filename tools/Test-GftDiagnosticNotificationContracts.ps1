$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$helper = Get-Content -LiteralPath (
    Join-Path $repoRoot "tools\shared\GrailFloatingTextLoadErrorNotifier.cs") -Raw

foreach ($required in @(
    "TryShowEventNotification(",
    "TryShowDiagnosticNotification(",
    "TryShowCompatibilityWarning(",
    '"System"',
    '"Low"',
    '"system"',
    '"Short"',
    '"Warning"',
    '"High"',
    '"warning"',
    '"OnMainMenu"')) {
    if (!$helper.Contains($required)) {
        throw "Shared GFT diagnostic helper is missing token: $required"
    }
}

$contracts = @(
    @{
        Name = "Steel and Bone"
        Path = "mods\SteelAndBone\src\SteelAndBone.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics", true',
            "ShowDamageDecisionDiagnostic(",
            "DiagnosticsEnabled()",
            "_showGrailFloatingTextDiagnostics.Value",
            '"steel-and-bone-damage-decision"',
            '"steel-and-bone-diagnostics"',
            "_lastGftDamageDiagnosticSignature",
            "_nextGftDamageDiagnosticTime")
    },
    @{
        Name = "Enemy Respawn Control"
        Path = "mods\EnemyRespawnControl\src\EnemyRespawnControl.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics", true',
            "CanShowGftLifecycleDiagnostics(",
            "ShowGftLifecycleDiagnostic(",
            "QueueGftLifecycleDiagnostic(",
            "FlushGftLifecycleDiagnostics(",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            '"enemy-respawn-control-lifecycle"',
            '"enemy-respawn-control-diagnostics"',
            "GftLifecycleBatchDelayMilliseconds",
            "_pendingGftRestBlocks",
            "_pendingGftEligible",
            "_pendingGftRespawned")
    },
    @{
        Name = "Blood Magic Expansion"
        Path = "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics", true',
            '"LogBloodSpellInnerLight", false',
            "ShowBloodMagicDiagnostic(",
            "matchingDiagnostic.Value",
            '"blood-magic-diagnostics"',
            "_lastGftCorpseQualitySignature")
        ForbiddenTokens = @(
            '"ShowGrailFloatingTextDiagnostics", false')
    },
    @{
        Name = "Eyes in the Dark"
        Path = "mods\EyesInTheDark\src\EyesInTheDark.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics"',
            "ShowDiagnosticSystem(",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            "_gft.TryShowDiagnostic(",
            '"eyes-in-the-dark-diagnostics"')
    },
    @{
        Name = "Versatile Weapons"
        Path = "mods\VersatileWeapons\src\VersatileWeapons.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics"',
            "DiagnosticNotificationsEnabled()",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            "TryShowSystemNotification(",
            '"vw-grip-state"')
        ForbiddenTokens = @(
            '"ShowGrailFloatingTextNotifications"')
    },
    @{
        Name = "KS All Lights Cast Shadows Addon"
        Path = "mods\KSAddons\KSTGAllLightsCastShadowsAddon\src\TGAllLightsCastShadowsAddon.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics"',
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            "TryShowSystemNotification(",
            '"shadow-atlas-diagnostics"',
            '"ShowToggleNotifications"')
    },
    @{
        Name = "KS Global Illumination Addon"
        Path = "mods\KSAddons\KSTGGlobalIlluminationAddon\src\TGGlobalIlluminationAddon.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics"',
            "ShowAdaptiveTierNotification(",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            "TryShowSystemNotification(",
            '"gi-adaptive-tier"',
            '"ShowToggleNotifications"')
    })

foreach ($contract in $contracts) {
    $source = Get-Content -LiteralPath (
        Join-Path $repoRoot $contract.Path) -Raw
    $normalizedSource = [regex]::Replace($source, '\s+', ' ')
    foreach ($required in $contract.Tokens) {
        if (!$normalizedSource.Contains($required)) {
            throw "$($contract.Name) GFT diagnostic contract is missing token: $required"
        }
    }
    if ($contract.ContainsKey("ForbiddenTokens")) {
        foreach ($forbidden in $contract.ForbiddenTokens) {
            if ($normalizedSource.Contains($forbidden)) {
                throw "$($contract.Name) GFT diagnostic contract contains retired token: $forbidden"
            }
        }
    }
}

Write-Host "Cross-mod GFT diagnostic notification contracts passed for 8 routine emitters."
