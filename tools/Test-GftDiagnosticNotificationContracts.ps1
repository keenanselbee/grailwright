$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$helper = Get-Content -LiteralPath (
    Join-Path $repoRoot "tools\shared\GrailFloatingTextLoadErrorNotifier.cs") -Raw

foreach ($required in @(
    "TryShowDiagnosticNotification(",
    '"System"',
    '"Low"',
    '"system"',
    '"Short"')) {
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
            "ShowRespawnBlockDiagnostic(",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            '"enemy-respawn-control-state"',
            '"enemy-respawn-control-diagnostics"',
            "_hasShownGftRespawnBlockDiagnostic")
    },
    @{
        Name = "Full Enemy XP"
        Path = "mods\FullEnemyXP\src\FullEnemyXP.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics", true',
            "ShowAdjustmentDiagnostic(",
            "_diagnostics.Value",
            "_showGrailFloatingTextDiagnostics.Value",
            '"full-enemy-xp-adjustment"',
            '"full-enemy-xp-diagnostics"',
            "SummaryEveryAdjustedKills")
    },
    @{
        Name = "Blood Magic Expansion"
        Path = "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs"
        Tokens = @(
            '"ShowGrailFloatingTextDiagnostics", true',
            '"LogBloodSpellInnerLight", false',
            "private const int ConfigSchemaVersion = 16;",
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
    foreach ($required in $contract.Tokens) {
        if (!$source.Contains($required)) {
            throw "$($contract.Name) GFT diagnostic contract is missing token: $required"
        }
    }
    if ($contract.ContainsKey("ForbiddenTokens")) {
        foreach ($forbidden in $contract.ForbiddenTokens) {
            if ($source.Contains($forbidden)) {
                throw "$($contract.Name) GFT diagnostic contract contains retired token: $forbidden"
            }
        }
    }
}

Write-Host "Cross-mod GFT diagnostic notification contracts passed for 8 routine emitters."
