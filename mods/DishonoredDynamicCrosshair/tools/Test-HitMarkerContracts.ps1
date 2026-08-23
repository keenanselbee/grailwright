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

Assert-Contract ($source.Contains('ConfigSchemaVersion = 19')) "Config schema is not 19."
Assert-Contract ($source.Contains('ConfigRecoveryBaselineSchema = 3')) "Config recovery baseline moved from 3."
Assert-Contract ($source.Contains('"custom_reticle.png"')) "The shared General and neutral reticle asset is missing."
Assert-Contract ([regex]::IsMatch($source, 'ReticleContext\.Bow,\s*"Bow",\s*"custom_reticle\.png",\s*0\.9f\);')) "Bow does not default to the shared reticle at 0.9x."
Assert-Contract ([regex]::IsMatch($source, 'ReticleContext\.Magic,\s*"Magic",\s*"custom_reticle\.png",\s*1\.1f\);')) "Magic does not default to the shared reticle at 1.1x."
Assert-Contract ([regex]::IsMatch($source, 'context == ReticleContext\.BloodMagic[\s\S]*?magicScale = ContextScale\(_magic\);[\s\S]*?contextScale = ContextScale\(settings\);[\s\S]*?bloodMagicQualityScale = GetBloodMagicQualityScale\(\);')) "Blood Magic quality reticles do not inherit Magic scale before their context and quality scales."
Assert-Contract (-not (Test-Path -LiteralPath (Join-Path $modRoot "custom_reticle_bow.png"))) "The redundant Bow reticle asset remains."
Assert-Contract (-not (Test-Path -LiteralPath (Join-Path $modRoot "custom_reticle_magic.png"))) "The redundant Magic reticle asset remains."
Assert-Contract (-not $source.Contains('hitmarker_4.png')) "The removed numbered neutral hitmarker is still referenced."
Assert-Contract ([regex]::IsMatch($source, 'frame == HitMarkerFrame\.Neutral[\s\S]*?return "custom_reticle\.png";')) "Neutral hits do not always resolve to custom_reticle.png."
Assert-Contract ([regex]::IsMatch($source, '"ShowCenterDot",\s*true,')) "The center dot is not enabled by default."
Assert-Contract ($source.Contains('[BepInDependency("ks.tgfoa.steel-and-bone", BepInDependency.DependencyFlags.SoftDependency)]')) "Steel and Bone is not a soft dependency."
Assert-Contract ($source.Contains('"SizeMultiplier",') -and $source.Contains('1.15f,')) "The 1.15x default hit-marker size is missing."
Assert-Contract ($source.Contains('"DamageOverTimeSizeMultiplier",') -and $source.Contains('1.1f,')) "The 1.1x default DoT hit-marker size is missing."
Assert-Contract ([regex]::IsMatch($source, '"KillingBlowSizeMultiplier",\s*1\.3f,')) "The 1.3x default killing-blow hit-marker size is missing."
Assert-Contract ($source.Contains('"DurationMultiplier",') -and $source.Contains('1f,')) "The 1x default hit-marker duration is missing."
Assert-Contract ($source.Contains('ResolveSteelAndBoneHitFeedbackApi')) "The optional API resolver is missing."
Assert-Contract ($source.Contains('"IncludeSummonAttacks",') -and $source.Contains('"Include Summon Attacks"')) "The summon-attack toggle is missing."
Assert-Contract ([regex]::IsMatch($source, '"IncludeSummonAttacks",\s*true,')) "IncludeSummonAttacks does not default to enabled."
Assert-Contract (-not $source.Contains('PlayerAttacksOnly')) "The retired inverse summon-attack toggle remains."
Assert-Contract ($source.Contains('GetRawConstantValue(), 6')) "Dishonored does not require Steel and Bone hit-feedback API v6."
Assert-Contract ($source.Contains('OnSteelAndBoneHitResolved')) "The hit-feedback receiver is missing."
Assert-Contract ([regex]::Matches($source, '!ShouldAcceptHitMarker\(playerAttack\)').Count -eq 2) "Regular and killing-blow feedback do not share source-priority arbitration."
Assert-Contract ([regex]::IsMatch($source, 'ShouldAcceptHitMarker\(bool playerAttack\)[\s\S]*?!playerAttack[\s\S]*?!_includeSummonAttacks\.Value[\s\S]*?return playerAttack\s*\|\| !_hitMarkerActive\s*\|\| !_activeHitMarkerPlayerAttack\s*\|\| Time\.unscaledTime >= _activeHitMarkerEndsAt;')) "Summon hit markers are not lower priority than active player hit markers."
Assert-Contract ([regex]::Matches($source, '_activeHitMarkerPlayerAttack = playerAttack;').Count -eq 2) "Regular and killing-blow markers do not both retain source priority."
Assert-Contract ($source.Contains('_activeHitMarkerDamageOverTime = damageOverTime;')) "Dishonored does not retain the DoT hit flag."
Assert-Contract ($source.Contains('? _hitMarkerDamageOverTimeSizeMultiplier.Value')) "DoT hits do not use their separate size setting."
Assert-Contract ([regex]::IsMatch($source, '_activeKillingBlowTier >= 1\s*\? _killingBlowSizeMultiplier\.Value')) "Killing blows do not replace normal and DoT sizing with their separate size setting."
Assert-Contract ($source.Contains('ApplyHitMarkerVisual')) "The layered hit-marker path is missing."
Assert-Contract ($source.Contains('_hitMarkerImage.sprite = sprite;')) "The effectiveness frame does not use its dedicated layer."
Assert-Contract (-not $source.Contains('_reticleImage.sprite = sprite;\r\n            _reticleImage.enabled = true;')) "Hit feedback still replaces the center-eye layer through the base reticle image."
Assert-Contract ([regex]::IsMatch($source, '"DishonoredStealthEye"[\s\S]*?"DishonoredHitMarkerBase"')) "The hit-marker layer is not created above the stealth eye."
Assert-Contract ($source.Contains('StealthEyeFrameCount = 11')) "The eleven-frame stealth-eye sequence is missing."
Assert-Contract ($source.Contains('UneaseCrouchVisibility')) "The stealth eye does not use steadily normalized awareness."
Assert-Contract ([regex]::IsMatch($source, '"CrouchIndicatorOpacityMultiplier",\s*1f,')) "The stealth-eye opacity multiplier does not default to an exact crosshair match."
Assert-Contract ([regex]::IsMatch($source, 'color\.a = Mathf\.Clamp01\(reticleOpacity\)\s*\* Mathf\.Clamp01\(_crouchIndicatorOpacityMultiplier\.Value\);')) "The stealth eye does not multiply the resolved crosshair opacity."
Assert-Contract (-not $source.Contains('"CrouchIndicatorOpacity",')) "The obsolete absolute stealth-eye opacity setting remains."
Assert-Contract ([regex]::IsMatch($source, 'bool showStealthPupil = stealthEyeVisible\s*&& _currentStealthEyeFrame >= 2;')) "The contextual stealth pupil does not begin at frame 2."
Assert-Contract ([regex]::IsMatch($source, 'bool showOrdinaryDot = _showCenterDot != null\s*&& _showCenterDot\.Value[\s\S]*?&& !stealthEyeVisible[\s\S]*?&& !directHitMarkerVisible;')) "The ordinary dot no longer obeys its visibility and direct-hit rules."
Assert-Contract ([regex]::IsMatch($source, 'bool showOrdinaryDot = _showCenterDot != null[\s\S]*?&& !qualityRitualActive[\s\S]*?&& !stealthEyeVisible')) "Blood Magic and Soul and Service quality reticles no longer suppress the ordinary dot."
Assert-Contract ([regex]::IsMatch($source, '_stealthEyeImage = CreateHitMarkerOverlayImage\([\s\S]*?"DishonoredStealthEye"\);\s*_stealthPupilImage = CreateHitMarkerOverlayImage\([\s\S]*?"DishonoredStealthPupil"\);[\s\S]*?"DishonoredHitMarkerBase"')) "The stealth pupil is not layered above the eye and below hit markers."
Assert-Contract ([regex]::IsMatch($source, '_stealthPupilImage\.sprite = dotAsset\.Sprite;[\s\S]*?_stealthPupilImage\.color = _stealthEyeImage\.color;[\s\S]*?ApplySharedDotLayout\([\s\S]*?_stealthEyeImage\.rectTransform\.anchoredPosition\);')) "The shared dot does not follow the stealth eye as its pupil."
Assert-Contract ([regex]::IsMatch($source, '_stealthPupilImage\.enabled = dotAsset\.Sprite != null\s*&& showStealthPupil')) "The stealth pupil is incorrectly gated by the ordinary dot setting or hit-marker suppression."
Assert-Contract ($source.Contains('ReticleAsset dotAsset = _generalDot;')) "Center visuals do not always use the shared dot asset."
Assert-Contract (-not $source.Contains('_bowDot') -and -not $source.Contains('_magicDot')) "Context-specific dot asset slots remain."
Assert-Contract (-not $source.Contains('dot_bow.png') -and -not $source.Contains('dot_magic.png')) "Context-specific dot filenames remain."
Assert-Contract ([regex]::IsMatch($source, 'ApplySharedDotLayout\([\s\S]*?Mathf\.Clamp\(_baseSizePixels\.Value, 4f, 256f\)[\s\S]*?rect\.sizeDelta = new Vector2\(size, size\);')) "Dot sizing is not fixed to the unscaled ReticleSizePixels canvas size."
Assert-Contract ([regex]::IsMatch($source, '"SizeMode",\s*ReticleSizeMode\.Reference1440p,')) "Reference1440p is not the default size mode."
Assert-Contract ($source.Contains('ReferenceScreenHeight = 1440f')) "The 1440p reference height is missing."
Assert-Contract ([regex]::IsMatch($source, 'GetSizeUnitConversion\(float canvasScaleFactor\)[\s\S]*?Screen\.height[\s\S]*?/ ReferenceScreenHeight;')) "Reference1440p does not scale physical pixels by screen height."
Assert-Contract ([regex]::Matches($source, 'GetSizeUnitConversion\(').Count -eq 5) "Not every visual size path uses the shared size conversion."
Assert-Contract ([regex]::IsMatch($source, '_sizeMode\.Value != ReticleSizeMode\.UIUnits[\s\S]*?Screen\.width != _lastScreenWidth[\s\S]*?ApplyReticleState\(context, targetState\);')) "Resolution changes do not refresh the complete presentation."
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
Assert-Contract ([regex]::IsMatch($source, 'TargetState displayTargetState = soulSalvageActive[\s\S]*?: bloodMagicActive[\s\S]*?BloodMagicCorpseUsesUsableVisuals[\s\S]*?TargetState\.Hostile\s*:\s*TargetState\.Default')) "Unavailable corpses do not use the ordinary idle-opacity state."
Assert-Contract ($source.Contains('if (apiVersion < 9)')) "Dishonored does not accept compatible Blood Magic Expansion API v9 or newer."
Assert-Contract ($source.Contains('GetFocusedCorpseQualityTier')) "Blood Magic corpse-quality tier API resolution is missing."
Assert-Contract ($source.Contains('_nextBloodMagicCheckTime = now + 0.05f;')) "Blood Magic reticles do not refresh at the responsive 20 Hz cadence."
Assert-Contract ([regex]::IsMatch($source, 'case 1:\s*return "custom_reticle_bloodmagic_0\.png";')) "Meager Blood Magic does not use frame 0."
Assert-Contract ([regex]::IsMatch($source, 'case 2:\s*return "custom_reticle_bloodmagic_1\.png";')) "Worthy Blood Magic does not use frame 1."
Assert-Contract ([regex]::IsMatch($source, 'case 3:\s*return "custom_reticle_bloodmagic_2\.png";')) "Potent Blood Magic does not use frame 2."
Assert-Contract ([regex]::IsMatch($source, 'case 4:\s*return "custom_reticle_bloodmagic_3\.png";')) "Prime Blood Magic does not use frame 3."
Assert-Contract ([regex]::IsMatch($source, 'ReticleContext\.BloodMagic,\s*"BloodMagic",\s*"custom_reticle_bloodmagic_0\.png"')) "The Blood Magic fallback does not share frame 0 with Meager."
Assert-Contract (-not [regex]::IsMatch($source, 'custom_reticle_bloodmagic(_(meager|worthy|potent|prime))?\.png')) "Legacy Blood Magic asset names are still referenced."
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
Assert-Contract ([regex]::IsMatch($source, 'KillingBlowOverlayFileName\(int tier\)[\s\S]*?case 1:\s*return "hitmarker_killingblow_0_overlay\.png";[\s\S]*?case 2:\s*return "hitmarker_killingblow_1_overlay\.png";[\s\S]*?case 3:\s*return "hitmarker_killingblow_2_overlay\.png";[\s\S]*?case 4:\s*return "hitmarker_killingblow_3_overlay\.png";')) "Killing-blow overlays do not follow natural Meager-through-Prime file ordering."
Assert-Contract ($source.Contains('_killingBlowHitMarkerImage.transform.SetAsLastSibling();')) "The killing-blow overlay is not kept above other hit-marker layers."

Assert-Contract ($steelSource.Contains('public static class SteelAndBoneHitFeedbackApi')) "Steel and Bone's public feedback API is missing."
Assert-Contract ($steelSource.Contains('public const int ApiVersion = 6;')) "Steel and Bone's feedback API is not v6."
Assert-Contract ([regex]::IsMatch($steelSource, 'public static event Action<float, float, bool, bool, bool, bool, bool, string, float>\s+HitResolved;')) "Steel and Bone's feedback event signature changed."
Assert-Contract ([regex]::IsMatch($steelSource, 'public static event Action<int, float, float, bool, bool, bool, bool, bool, string, float>\s+KillingBlowResolved;')) "Steel and Bone's killing-blow feedback event signature changed."
Assert-Contract ($steelSource.Contains('SteelAndBoneHitFeedbackApi.Publish(')) "Steel and Bone does not publish resolved hits."
Assert-Contract ($steelSource.Contains('IsPlayerAttack = IsDirectHeroDamageSource(')) "Steel and Bone does not retain direct-player attribution with pending feedback."
Assert-Contract ([regex]::IsMatch($steelSource, 'SteelAndBoneHitFeedbackApi\.Publish\([\s\S]*?damageOverTime,\s*playerAttack,')) "Steel and Bone does not publish direct-player attribution."
Assert-Contract ($steelSource.Contains('float effectivenessMultiplier = feedback == null ? 1.0f : feedback.Multiplier;')) "Steel and Bone does not preserve the actual effectiveness multiplier."
Assert-Contract ($steelSource.Contains('float visualEffectivenessMultiplier = ApplyEffectivenessFeedbackSensitivity(effectivenessMultiplier);')) "Steel and Bone does not calculate presentation-adjusted effectiveness."
Assert-Contract ($steelSource.Contains('bool damageOverTime = IsDamageOverTime(damage);')) "Steel and Bone does not publish the resolved DoT flag."
Assert-Contract ([regex]::IsMatch($steelSource, 'SteelAndBoneHitFeedbackApi\.Publish\([\s\S]*?if \(!DamageNumbersActive\(\)\)')) "Hit publication is not independent from floating-number rendering."
Assert-Contract (-not $steelSource.Contains('if (_damageNumbersEnabled == null || !_damageNumbersEnabled.Value || damage == null)')) "Damage feedback is still gated by DamageNumbersEnabled."
Assert-Contract ($bloodMagicSource.Contains('public static int GetFocusedCorpseQualityTier()')) "Blood Magic Expansion does not expose focused corpse-quality tiers."
Assert-Contract ([regex]::IsMatch($bloodMagicSource, 'GetFocusedCorpseQualityTierForInterop\(\)[\s\S]*?TryGetFocusedCorpseInteropSnapshot\([\s\S]*?return GetCorpseQualityTier\(CalculateCorpseQuality01\(state, false\)\);')) "Blood Magic Expansion does not expose snapshot-consistent quality tiers for resolved unavailable corpses."

$assetNames = @(
    "custom_reticle.png",
    "hitmarker_0.png",
    "hitmarker_1.png",
    "hitmarker_2.png",
    "hitmarker_3.png",
    "hitmarker_5.png",
    "hitmarker_6.png",
    "hitmarker_7.png",
    "dot.png",
    "stealth_eye_0.png",
    "stealth_eye_1.png",
    "stealth_eye_2.png",
    "stealth_eye_3.png",
    "stealth_eye_4.png",
    "stealth_eye_5.png",
    "stealth_eye_6.png",
    "stealth_eye_7.png",
    "stealth_eye_8.png",
    "stealth_eye_9.png",
    "stealth_eye_10.png",
    "hitmarker.png",
    "hitmarker_weakspot_overlay.png",
    "hitmarker_critical_overlay.png",
    "custom_reticle_bloodmagic_0.png",
    "custom_reticle_bloodmagic_1.png",
    "custom_reticle_bloodmagic_2.png",
    "custom_reticle_bloodmagic_3.png",
    "hitmarker_killingblow_0_overlay.png",
    "hitmarker_killingblow_1_overlay.png",
    "hitmarker_killingblow_2_overlay.png",
    "hitmarker_killingblow_3_overlay.png"
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
Assert-Contract ($readme.Contains("hitmarker_0.png") -and $readme.Contains("hitmarker_7.png")) "README lacks the renamed effectiveness-frame list."
Assert-Contract ($readme.Contains("custom_reticle.png") -and -not $readme.Contains("hitmarker_4.png")) "README does not document the shared neutral reticle."
Assert-Contract ($readme.Contains("all use") -and $readme.Contains("dot.png") -and -not $readme.Contains("dot_bow.png") -and -not $readme.Contains("dot_magic.png")) "README does not document the shared dot asset."
Assert-Contract ($readme.Contains("SizeMode defaults to Reference1440p") -and $readme.Contains("becomes 120 at 4K")) "README does not document reference-resolution scaling."
Assert-Contract ($readme.Contains("stealth_eye_0.png") -and $readme.Contains("stealth_eye_10.png")) "README lacks the complete stealth-eye frame range."
Assert-Contract ($readme.Contains("frames 0 and 1 remain dotless") -and $readme.Contains("even when ShowCenterDot is false")) "README lacks the frame-2 contextual pupil behavior."
Assert-Contract ($readme.Contains("hitmarker.png")) "README lacks the direct-hit diamond."
Assert-Contract ($readme.Contains("hitmarker_critical_overlay.png")) "README lacks the critical overlay."
Assert-Contract ($readme.Contains("IncludeSummonAttacks defaults to true") -and $readme.Contains("cannot replace an active hero marker")) "README lacks summon hit-marker priority behavior."

Write-Output "Dishonored Dynamic Crosshair hit-marker contracts passed."
