Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Dishonored interaction-icon contract failed: $Message"
    }
}

$modRoot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\Plugin.cs")
$readme = Get-Content -Raw -LiteralPath (Join-Path $modRoot "README.txt")
$manifestText = Get-Content -Raw -LiteralPath (Join-Path $modRoot "mod.json")

Assert-Contract ($source.Contains('ConfigSchemaVersion = 20')) "Config schema is not 20."
Assert-Contract ([regex]::IsMatch($source, '"Diagnostics",\s*"Diagnostics",\s*false,')) "The general Diagnostics switch is missing or enabled by default."
Assert-Contract (-not $source.Contains('LogBloodMagicScaleDiagnostics')) "The retired Blood-Magic-only diagnostic setting remains."
foreach ($token in @(
    '"Reticle diagnostics: context="',
    '"; targetState="',
    '"; interaction="',
    '"; bloodMagicState="',
    '"; soulHover="',
    '"; finalScale="')) {
    Assert-Contract ($source.Contains($token)) "General reticle diagnostics are missing token: $token"
}
Assert-Contract (-not $manifestText.Contains('TG.Main.dll')) "The reflected integration acquired a hard game-assembly reference."

Assert-Contract ([regex]::IsMatch($source, '"Interaction Icons",\s*"Enabled",\s*true,')) "Interaction icons are not enabled by default."
Assert-Contract ([regex]::IsMatch($source, '"IconScale",\s*1\.1f,')) "IconScale does not default to 1.1."
Assert-Contract ([regex]::IsMatch($source, '"IconOpacity",\s*0\.8f,')) "IconOpacity does not default to 0.8."
Assert-Contract ([regex]::IsMatch($source, '"CrosshairOpacityWhileActive",\s*0f,')) "The active-interaction crosshair opacity does not default to 0."
Assert-Contract ([regex]::IsMatch($source, '"HideVanillaInteractionKeyPrompts",\s*true,')) "Vanilla interaction-key glyph suppression is not enabled by default."
Assert-Contract ([regex]::IsMatch($source, '"VanillaTextVerticalOffset",\s*-120f,')) "Vanilla interaction text does not default to -120 UI units."
Assert-Contract (-not [regex]::IsMatch($source, 'Config\.Bind\(\s*"[0-9]+\. ')) "A numbered authored config section remains."
Assert-Contract ($source.Contains('new Grailwright.Shared.ConfigRecoveryUiMetadata')) "Visible settings lack FoA Mod Manager ordering metadata."
$visibleBindCount = [regex]::Matches($source, 'Config\.Bind\(').Count - 1
$metadataBindCount = ([regex]::Matches($source, 'ConfigUi\(').Count - 2) `
    + ([regex]::Matches($source, 'OpacityDescription\(').Count - 1)
Assert-Contract ($metadataBindCount -eq $visibleBindCount) "Not every visible authored Config.Bind call supplies ordering metadata."
foreach ($token in @(
    'CoreSectionOrder = 0',
    'ReticlesSectionOrder = 10',
    'ColorsSectionOrder = 20',
    'InteractionIconsSectionOrder = 30',
    'BloodMagicSectionOrder = 40',
    'HitMarkersSectionOrder = 50',
    'AmbushIntegritySectionOrder = 60',
    'AdvancedSectionOrder = 70',
    'DiagnosticsSectionOrder = 90',
    'SectionOrder = sectionOrder',
    'Order = order')) {
    Assert-Contract ($source.Contains($token)) "Config ordering metadata is missing token: $token"
}

Assert-Contract ($source.Contains('"Awaken.TG.Main.Heroes.Interactions.VHeroInteractionUI"')) "The authoritative interaction view is not resolved."
Assert-Contract ($source.Contains('"FillInfo"')) "The selected interaction refresh is not patched."
Assert-Contract ($source.Contains('"DefaultAction"')) "The icon does not follow the game-selected default action."
Assert-Contract ($source.Contains('"AvailableActions"')) "Locked-state inspection does not cover the location's available actions."
Assert-Contract ([regex]::IsMatch($source, 'bool locked = HasLockedAction[\s\S]*?bool illegal = !locked[\s\S]*?iconKind = locked\s*\? InteractionIconKind\.Lockpick[\s\S]*?: illegal\s*\? InteractionIconKind\.Hand')) "Locked and illegal interaction priorities changed."
Assert-Contract ($source.Contains('IsTypeOrBaseNamed(action, "ToolInteractAction")')) "Tool actions are not classified structurally."
Assert-Contract ($source.Contains('ReadReflectedProperty(') -and $source.Contains('requiredTool,') -and $source.Contains('"EnumName"')) "Tool actions do not read the rich enum's authoritative identity."
Assert-Contract ([regex]::IsMatch($source, '(?s)"Attack".*?\|\| String\.Equals\(\s*actionName as string,\s*"Swarm".*?\|\| String\.Equals\(\s*actionName as string,\s*"Hunt".*?InteractionIconKind\.Attack')) "Swarm and terrain Hunt are not classified with the Attack command icon."
Assert-Contract ($source.Contains('SummonCommandPreviewHandoffMaximumSeconds = 0.25f')) "The summon-command preview handoff ceiling changed."
Assert-Contract ([regex]::IsMatch($source, '(?s)IsSummonCommandAction\(object action\).*?IsTypeOrBaseNamed\(action, "SummonCommandAction"\)')) "Summon command actions are not identified for seamless handoff."
Assert-Contract ([regex]::IsMatch($source, '(?s)OnInteractionViewFilled\(.*?if \(targetVisible is bool && !\(bool\)targetVisible\).*?_currentInteractionIsSummonCommandPreview.*?BeginSummonCommandPreviewHandoff\(\s*_currentInteractionIconKind\).*?_currentInteractionIsSummonCommandPreview\s*=\s*IsSummonCommandAction\(action\)')) "A disappearing summon-command preview does not enter the icon-preserving handoff."
Assert-Contract ([regex]::IsMatch($source, '(?s)CurrentInteractionIconKind\(\).*?_summonCommandPulseActive.*?return _summonCommandPulseKind;.*?IsSummonCommandPreviewHandoffActive\(\).*?return _summonCommandPreviewHandoffKind;')) "The summon-command preview handoff does not retain its classified icon until the pulse arrives."
Assert-Contract ([regex]::IsMatch($source, '(?s)ShouldShowInteractionIcon\(bool hitMarkerActive\).*?_currentInteractionView != null.*?_summonCommandPulseActive.*?IsSummonCommandPreviewHandoffActive\(\)')) "The interaction layer is not kept visible throughout summon-command handoffs."
Assert-Contract ([regex]::IsMatch($source, '(?s)BeginSummonCommandPreviewHandoff\(.*?_summonCommandPreviewHandoffKind = kind;.*?SummonCommandPreviewHandoffMaximumSeconds.*?UpdateSoulAndServiceCommandPresentation\(true\)')) "Summon-command handoff does not immediately check for its command pulse."
Assert-Contract ([regex]::IsMatch($source, '(?s)_summonCommandPulseEndsAt = Time\.unscaledTime.*?_summonCommandPreviewHandoffKind = InteractionIconKind\.None;.*?_summonCommandPreviewHandoffUntil = 0\.0f;.*?UpdateSoulAndServiceCommandPresentation\(\);\s*UpdateSummonCommandPulse\(\);\s*UpdateSummonCommandPreviewHandoff\(\);')) "An observed summon-command pulse does not atomically replace the preview handoff."
foreach ($token in @(
    'case "Mining":',
    'case "Lumbering":',
    'case "Digging":',
    'case "Fishing":',
    '"WaterFishingAction"',
    '"ReadAction"',
    '"DialogueAction"',
    '"SummonCommandAction"',
    '"BedElement"',
    '"MountAction"',
    '"StartFireplaceBaseAction"')) {
    Assert-Contract ($source.Contains($token)) "Action mapping is missing token: $token"
}

Assert-Contract ([regex]::IsMatch($source, '"DishonoredStealthPupil"[\s\S]*?"DishonoredInteractionIcon"[\s\S]*?"DishonoredHitMarkerBase"[\s\S]*?"DishonoredBackstabReadyOverlay"')) "Interaction icon layer is not between center visuals and hit markers."
Assert-Contract ([regex]::IsMatch($source, 'ShouldShowInteractionIcon\(bool hitMarkerActive\)[\s\S]*?!_backstabPresentationActive[\s\S]*?&& !hitMarkerActive;')) "Hit markers or backstab readiness no longer suppress the routine interaction icon."
Assert-Contract ([regex]::IsMatch($source, '_interactionPresentationActive\s*\? Mathf\.Clamp01\(_interactionCrosshairOpacity\.Value\)')) "The routine interaction state does not dim the underlying crosshair."
Assert-Contract ([regex]::IsMatch($source, 'context == ReticleContext\.BloodMagic\s*\|\| _interactionPresentationActive')) "Blood Magic and interaction presentations do not suppress the awareness eye."
Assert-Contract ($source.Contains('"Awaken.TG.Main.Locations.Containers.ContainerUI"')) "Quick-loot container state is not resolved."
Assert-Contract ($source.Contains('"IsEmpty"')) "Quick-loot emptiness is not read."
Assert-Contract ([regex]::IsMatch($source, '_quickLootContainer != null[\s\S]*?_quickLootHasItems\s*\? InteractionIconKind\.Hand\s*:\s*InteractionIconKind\.None')) "Quick loot does not show a hand only while non-empty."
Assert-Contract ($source.Contains('QuickLootOpenedPostfix')) "Quick-loot opening is not patched."
Assert-Contract ($source.Contains('QuickLootDiscardingPrefix')) "Quick-loot closing is not patched."
Assert-Contract ($source.Contains('new Color32(0x8C, 0x00, 0x03, 0xFF)')) "Illegal interactions do not use killing-blow dark red."
Assert-Contract ([regex]::IsMatch($source, '(?s)_currentInteractionTarget = target;.*?_currentInteractionIsIllegal = illegal;.*?IsTypeOrBaseNamed\(\s*target,\s*"HeroInteractionHoldUI"\)')) "The selected crosshair interaction does not retain structural hold state."
Assert-Contract ([regex]::IsMatch($source, '(?s)ApplyHoldToStealPromptText\(object interactionView\).*?_currentInteractionIsIllegal.*?_currentInteractionIsHold.*?currentText.*?\.Trim\(\).*?"Steal".*?StringComparison\.OrdinalIgnoreCase.*?"Hold to Steal"')) "Vanilla held theft does not receive the display-only Hold to Steal label."
Assert-Contract ([regex]::IsMatch($source, '(?s)TryGetIllegalInteractionHoldProgress\(.*?_quickLootContainer != null.*?!_currentInteractionIsIllegal.*?!_currentInteractionIsHold.*?_currentInteractionTarget == null.*?"HeldButton".*?"HoldPercent"')) "The theft pulse is not limited to an active held crosshair interaction outside quick loot."
Assert-Contract ([regex]::IsMatch($source, '0\.15f \* Mathf\.Sin\(holdProgress \* Mathf\.PI\)')) "Held theft does not use the approved single 1.0x-to-1.15x-to-1.0x pulse."
Assert-Contract ($source.Contains('UpdateInteractionHoldPulse();')) "Held theft presentation is not refreshed every frame."
Assert-Contract (-not $source.Contains('kane.tgfoa.hold-to-steal')) "Dishonored acquired a hard Hold to Steal plugin dependency."
foreach ($containerPatchToken in @(
    'Prompt.Tap',
    'Prompt.Hold',
    'TakeItemFromContainer',
    'TakeAllItems',
    'VReadablePopupUI')) {
    Assert-Contract (-not $source.Contains($containerPatchToken)) "Dishonored acquired Hold to Steal gameplay or container patch surface: $containerPatchToken"
}
Assert-Contract ($source.Contains('SetReflectedGraphicEnabled(keyIcon, "icon", enabled);')) "The vanilla key-icon image is not selectively suppressed."
Assert-Contract ($source.Contains('SetReflectedGraphicEnabled(keyIcon, "text", enabled);')) "The vanilla keyboard-letter text is not selectively suppressed."
Assert-Contract ($source.Contains('buttonParent.SetActive(false);')) "Prompt suppression does not collapse the unused button container."
Assert-Contract ($source.Contains('ApplyInteractionPromptLayout(interactionView);')) "The configurable interaction-text vertical layout is not applied."
Assert-Contract ($source.Contains('RefreshInteractionPromptView();')) "Vanilla prompt state is not refreshed when settings or the plugin change."

$assetNames = @(
    "interaction_backstab.png",
    "interaction_campfire.png",
    "interaction_digging.png",
    "interaction_fishing.png",
    "interaction_hand.png",
    "interaction_lockpick.png",
    "interaction_lumbering.png",
    "interaction_mining.png",
    "interaction_mount.png",
    "interaction_read.png",
    "interaction_rest.png",
    "interaction_talk.png",
    "interaction_command_attack.png",
    "interaction_command_hold.png",
    "interaction_command_follow.png",
    "interaction_command_behavior.png",
    "interaction_command_raiseall.png"
)

Add-Type -AssemblyName System.Drawing
foreach ($assetName in $assetNames) {
    $assetPath = Join-Path $modRoot $assetName
    Assert-Contract (Test-Path -LiteralPath $assetPath -PathType Leaf) "$assetName is missing."
    Assert-Contract ($source.Contains($assetName)) "$assetName is not referenced by the plugin."

    $bitmap = [System.Drawing.Bitmap]::new($assetPath)
    try {
        Assert-Contract ($bitmap.Width -eq 512 -and $bitmap.Height -eq 512) "$assetName is not 512x512."
        Assert-Contract ($bitmap.GetPixel(0, 0).A -eq 0) "$assetName does not have a transparent corner."
    } finally {
        $bitmap.Dispose()
    }
}

Assert-Contract ($readme.Contains("Interaction Icons")) "README lacks the interaction-icon section."
Assert-Contract ($readme.Contains("Locked and blocked explanations")) "README does not document preserved prompt information."
Assert-Contract ($readme.Contains("interaction_backstab.png")) "README lacks the renamed backstab asset."

Write-Output "Dishonored Dynamic Crosshair interaction-icon contracts passed."
