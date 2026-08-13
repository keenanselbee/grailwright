Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Dishonored hit-marker contract failed: $Message"
    }
}

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$source = Get-Content -Raw -LiteralPath (Join-Path $modRoot "src\Plugin.cs")
$steelSource = Get-Content -Raw -LiteralPath (Join-Path $repoRoot "mods\SteelAndBone\src\SteelAndBone.cs")
$bloodMagicSource = Get-Content -Raw -LiteralPath (Join-Path $repoRoot "mods\BloodMagicExpansion\src\BloodMagicExpansion.cs")
$readme = Get-Content -Raw -LiteralPath (Join-Path $modRoot "README.txt")
$manifest = Get-Content -Raw -LiteralPath (Join-Path $modRoot "mod.json") | ConvertFrom-Json

Assert-Contract ($manifest.version -eq "3.1.4") "mod.json is not version 3.1.4."
Assert-Contract ($source.Contains('PluginVersion = "3.1.4"')) "PluginVersion is not 3.1.4."
Assert-Contract ($source.Contains('ConfigSchemaVersion = 9')) "Config schema is not 9."
Assert-Contract ($source.Contains('ConfigRecoveryBaselineSchema = 3')) "Config recovery baseline moved from 3."
Assert-Contract ($source.Contains('"custom_reticle_4.png"')) "The general reticle default is not custom_reticle_4.png."
Assert-Contract ($source.Contains('[BepInDependency("ks.tgfoa.steel-and-bone", BepInDependency.DependencyFlags.SoftDependency)]')) "Steel and Bone is not a soft dependency."
Assert-Contract ($source.Contains('"SizeMultiplier",') -and $source.Contains('1.15f,')) "The 1.15x default hit-marker size is missing."
Assert-Contract ($source.Contains('"DamageOverTimeSizeMultiplier",') -and $source.Contains('1.1f,')) "The 1.1x default DoT hit-marker size is missing."
Assert-Contract ([regex]::IsMatch($source, '"KillingBlowSizeMultiplier",\s*1\.3f,')) "The 1.3x default killing-blow hit-marker size is missing."
Assert-Contract ($source.Contains('"DurationMultiplier",') -and $source.Contains('1f,')) "The 1x default hit-marker duration is missing."
Assert-Contract ($source.Contains('ResolveSteelAndBoneHitFeedbackApi')) "The optional API resolver is missing."
Assert-Contract ($source.Contains('GetRawConstantValue(), 5')) "Dishonored does not require Steel and Bone hit-feedback API v5."
Assert-Contract ($source.Contains('OnSteelAndBoneHitResolved')) "The hit-feedback receiver is missing."
Assert-Contract ($source.Contains('_activeHitMarkerDamageOverTime = damageOverTime;')) "Dishonored does not retain the DoT hit flag."
Assert-Contract ($source.Contains('? _hitMarkerDamageOverTimeSizeMultiplier.Value')) "DoT hits do not use their separate size setting."
Assert-Contract ([regex]::IsMatch($source, '_activeKillingBlowTier >= 1\s*\? _killingBlowSizeMultiplier\.Value')) "Killing blows do not replace normal and DoT sizing with their separate size setting."
Assert-Contract ($source.Contains('ApplyHitMarkerVisual')) "The reticle replacement path is missing."
Assert-Contract ($source.Contains('ResolveHitMarkerFrame')) "The numbered effectiveness mapping is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier < 0.35f')) "Extreme resistance threshold is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier < 0.70f')) "Strong resistance threshold is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier < 0.95f')) "Mild resistance threshold is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier <= 1.05f')) "Neutral threshold is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier <= 1.10f')) "Mild weakness threshold is missing."
Assert-Contract ($source.Contains('effectivenessMultiplier <= 1.20f')) "Strong weakness threshold is missing."
Assert-Contract ($source.Contains('visualEffectivenessMultiplier,')) "Dishonored does not receive presentation-adjusted effectiveness."
Assert-Contract ([regex]::IsMatch($source, 'ResolveHitMarkerFrame\(\s*visualEffectivenessMultiplier,\s*immune\)')) "Hit-marker tiers do not use presentation-adjusted effectiveness."
Assert-Contract ($source.Contains('HitMarkerInitialScale')) "The hit-marker impact animation is missing."
Assert-Contract ($source.Contains('SetHitMarkerOverlaysEnabled(false, false, false, false)')) "Normal-reticle overlay cleanup is missing."
Assert-Contract ($source.Contains('UnsubscribeSteelAndBoneHitFeedback();')) "API cleanup is missing."
Assert-Contract ([regex]::IsMatch($source, 'hitResolvedEvent\.AddEventHandler\(null, handler\);\s*_steelAndBoneHitResolvedEvent = hitResolvedEvent;\s*_steelAndBoneHitResolvedHandler = handler;\s*killingBlowResolvedEvent\.AddEventHandler')) "The first Steel and Bone subscription is not tracked before the second subscription can fail."
Assert-Contract ($source.Contains('"BloodMagicQualityCrosshairsEnabled"') -and $source.Contains('true,')) "Blood Magic quality crosshairs are not enabled by default."
Assert-Contract (-not $source.Contains('UnavailableCorpseColor')) "The obsolete unavailable-corpse color setting still exists."
Assert-Contract ([regex]::IsMatch($source, 'ColorForBloodMagicCorpseState\(string fallback\)[\s\S]*?BloodMagicCorpseUsesUsableVisuals[\s\S]*?_bloodMagicUsableCorpseColor\.Value;[\s\S]*?_general\.DefaultColor\.Value;')) "Unavailable corpses do not inherit the ordinary default-reticle color."
Assert-Contract ([regex]::IsMatch($source, 'TargetState displayTargetState = bloodMagicActive[\s\S]*?BloodMagicCorpseUsesUsableVisuals[\s\S]*?TargetState\.Hostile\s*:\s*TargetState\.Default')) "Unavailable corpses do not use the ordinary idle-opacity state."
Assert-Contract ([regex]::IsMatch($source, 'BloodMagicExpansionApiTypeName[\s\S]*GetRawConstantValue\(\),\s*9\)\)')) "Dishonored does not require Blood Magic Expansion API v9."
Assert-Contract ($source.Contains('GetFocusedCorpseQualityTier')) "Blood Magic corpse-quality tier API resolution is missing."
Assert-Contract ($source.Contains('custom_reticle_bloodmagic_meager.png')) "The meager Blood Magic quality reticle is not loaded."
Assert-Contract ($source.Contains('custom_reticle_bloodmagic_worthy.png')) "The worthy Blood Magic quality reticle is not loaded."
Assert-Contract ($source.Contains('custom_reticle_bloodmagic_potent.png')) "The potent Blood Magic quality reticle is not loaded."
Assert-Contract ($source.Contains('custom_reticle_bloodmagic_prime.png')) "The prime Blood Magic quality reticle is not loaded."
Assert-Contract ($source.Contains('ResolveBloodMagicQualitySprite')) "Blood Magic quality reticle fallback is missing."
Assert-Contract ($source.Contains('"KillingBlowOverlaysEnabled"') -and $source.Contains('true,')) "Killing-blow overlays are not enabled by default."
Assert-Contract ($source.Contains('"KillingBlowDurationMultiplier"') -and $source.Contains('1.5f,')) "The 1.5x killing-blow duration default is missing."
Assert-Contract ($source.Contains('KillingBlowResolved')) "The killing-blow feedback event is not resolved."
Assert-Contract ($source.Contains('OnSteelAndBoneKillingBlowResolved')) "The killing-blow feedback receiver is missing."
Assert-Contract ($source.Contains('_activeKillingBlowTier = 0;')) "Normal hits do not clear the active killing-blow tier."
Assert-Contract ($source.Contains('normalDurationMultiplier') -and $source.Contains('killingBlowDurationMultiplier')) "Killing blows do not use both duration multipliers."
Assert-Contract ($source.Contains('_activeHitMarkerColor = new Color32(0x8C, 0x00, 0x03, 0xFF);')) "Killing blows do not force the complete marker composition to #8C0003."
Assert-Contract ($source.Contains('_activeHitMarkerColor = ParseColor(color);')) "Nonlethal hits no longer retain Steel and Bone's calculated color."
Assert-Contract ($source.Contains('ResolveHitMarkerPath("hitmarker.png")')) "The direct-hit diamond asset is not loaded."
Assert-Contract ([regex]::IsMatch($source, '_directHitMarkerOverlay\.Sprite,\s*!_activeHitMarkerDamageOverTime\s*&& _activeKillingBlowTier == 0')) "The direct-hit diamond is not limited to direct nonlethal hits."
Assert-Contract ($source.Contains('_directHitMarkerImage.color = color;')) "The direct-hit diamond does not use the active marker color."
Assert-Contract ($source.Contains('_weakSpotHitMarkerImage.color = color;')) "Weak-spot overlays do not use the active marker color."
Assert-Contract ($source.Contains('_criticalHitMarkerImage.color = color;')) "Critical overlays do not use the active marker color."
Assert-Contract ($source.Contains('_killingBlowHitMarkerImage.color = color;')) "Killing-blow overlays do not use the active marker color."
Assert-Contract ($source.Contains('GetKillingBlowTierDurationMultiplier(_activeKillingBlowTier)')) "Killing blows do not apply corpse-tier duration scaling."
Assert-Contract ($source.Contains('case 2: return 1.33f;')) "Worthy killing blows do not use 1.33x tier duration."
Assert-Contract ($source.Contains('case 3: return 1.67f;')) "Potent killing blows do not use 1.67x tier duration."
Assert-Contract ($source.Contains('case 4: return 2.00f;')) "Prime killing blows do not use 2.00x tier duration."
Assert-Contract ($source.Contains('default: return 1.0f;')) "Meager killing blows do not retain the current duration."
Assert-Contract ($source.Contains('hitmarker_killingblow_meager_overlay.png')) "The meager killing-blow overlay is not loaded."
Assert-Contract ($source.Contains('hitmarker_killingblow_worthy_overlay.png')) "The worthy killing-blow overlay is not loaded."
Assert-Contract ($source.Contains('hitmarker_killingblow_potent_overlay.png')) "The potent killing-blow overlay is not loaded."
Assert-Contract ($source.Contains('hitmarker_killingblow_prime_overlay.png')) "The prime killing-blow overlay is not loaded."
Assert-Contract ($source.Contains('_killingBlowHitMarkerImage.transform.SetAsLastSibling();')) "The killing-blow overlay is not kept above other hit-marker layers."

Assert-Contract ($steelSource.Contains('public static class SteelAndBoneHitFeedbackApi')) "Steel and Bone's public feedback API is missing."
Assert-Contract ($steelSource.Contains('public const int ApiVersion = 5;')) "Steel and Bone's feedback API is not v5."
Assert-Contract ([regex]::IsMatch($steelSource, 'public static event Action<float, float, bool, bool, bool, bool, string, float>\s+HitResolved;')) "Steel and Bone's feedback event signature changed."
Assert-Contract ([regex]::IsMatch($steelSource, 'public static event Action<int, float, float, bool, bool, bool, bool, string, float>\s+KillingBlowResolved;')) "Steel and Bone's killing-blow feedback event signature changed."
Assert-Contract ($steelSource.Contains('SteelAndBoneHitFeedbackApi.Publish(')) "Steel and Bone does not publish resolved hits."
Assert-Contract ($steelSource.Contains('float effectivenessMultiplier = feedback == null ? 1.0f : feedback.Multiplier;')) "Steel and Bone does not preserve the actual effectiveness multiplier."
Assert-Contract ($steelSource.Contains('float visualEffectivenessMultiplier = ApplyEffectivenessFeedbackSensitivity(effectivenessMultiplier);')) "Steel and Bone does not calculate presentation-adjusted effectiveness."
Assert-Contract ($steelSource.Contains('bool damageOverTime = IsDamageOverTime(damage);')) "Steel and Bone does not publish the resolved DoT flag."
Assert-Contract ($steelSource.Contains('if (DamageNumbersActive()')) "Hit publication is not independent from floating-number rendering."
Assert-Contract (-not $steelSource.Contains('if (_damageNumbersEnabled == null || !_damageNumbersEnabled.Value || damage == null)')) "Damage feedback is still gated by DamageNumbersEnabled."
Assert-Contract ($bloodMagicSource.Contains('public static int GetFocusedCorpseQualityTier()')) "Blood Magic Expansion does not expose focused corpse-quality tiers."
Assert-Contract ([regex]::IsMatch($bloodMagicSource, 'GetFocusedCorpseQualityTierForInterop\(\)[\s\S]*?return GetCorpseQualityTier\(CalculateCorpseQuality01\(state, false\)\);')) "Blood Magic Expansion does not expose quality tiers for resolved unavailable corpses."

$assetNames = @(
    "custom_reticle_0.png",
    "custom_reticle_1.png",
    "custom_reticle_2.png",
    "custom_reticle_3.png",
    "custom_reticle_4.png",
    "custom_reticle_5.png",
    "custom_reticle_6.png",
    "custom_reticle_7.png",
    "hitmarker.png",
    "hitmarker_weakspot_overlay.png",
    "hitmarker_critical_overlay.png",
    "custom_reticle_bloodmagic.png",
    "custom_reticle_bloodmagic_meager.png",
    "custom_reticle_bloodmagic_worthy.png",
    "custom_reticle_bloodmagic_potent.png",
    "custom_reticle_bloodmagic_prime.png",
    "hitmarker_killingblow_meager_overlay.png",
    "hitmarker_killingblow_worthy_overlay.png",
    "hitmarker_killingblow_potent_overlay.png",
    "hitmarker_killingblow_prime_overlay.png"
)

Add-Type -AssemblyName System.Drawing
foreach ($assetName in $assetNames) {
    $assetPath = Join-Path $modRoot $assetName
    Assert-Contract (Test-Path -LiteralPath $assetPath -PathType Leaf) "$assetName is missing."

    $bitmap = [System.Drawing.Bitmap]::new($assetPath)
    try {
        Assert-Contract ($bitmap.Width -eq 512 -and $bitmap.Height -eq 512) "$assetName is not 512x512."
        Assert-Contract ($bitmap.GetPixel(0, 0).A -eq 0) "$assetName does not have a transparent corner."

        $visible = $false
        for ($y = 0; $y -lt $bitmap.Height -and -not $visible; $y += 8) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 8) {
                if ($bitmap.GetPixel($x, $y).A -gt 0) {
                    $visible = $true
                    break
                }
            }
        }
        Assert-Contract $visible "$assetName contains no visible marker pixels."
    } finally {
        $bitmap.Dispose()
    }
}

Assert-Contract ($readme.Contains("Steel and Bone Hit Markers")) "README lacks the Steel and Bone hit-marker section."
Assert-Contract ($readme.Contains("custom_reticle_0.png") -and $readme.Contains("custom_reticle_7.png")) "README lacks the complete numbered frame list."
Assert-Contract ($readme.Contains("hitmarker.png")) "README lacks the direct-hit diamond."
Assert-Contract ($readme.Contains("hitmarker_critical_overlay.png")) "README lacks the critical overlay."

Write-Output "Dishonored Dynamic Crosshair hit-marker contracts passed."
