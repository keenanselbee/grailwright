$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$bridgePath = Join-Path $modRoot "src\GrailFloatingTextBridge.cs"
$sourcePath = Join-Path $modRoot "src\AmbushIntegrity.cs"
$manifestPath = Join-Path $modRoot "mod.json"

if (!(Test-Path -LiteralPath $bridgePath)) {
    throw "Ambush Integrity Grail Floating Text bridge is missing."
}

$bridge = Get-Content -LiteralPath $bridgePath -Raw
foreach ($required in @(
    '"ks.tgfoa.grail-floating-text"',
    '"GrailFloatingText.NotificationApi"',
    'Chainloader.PluginInfos.TryGetValue',
    'AccessTools.Method',
    '"TryShowEvent"',
    '"Immediate"',
    'private void Disable(Exception exception)',
    '_failureLogged',
    'public void Release()',
    'public bool IsAvailable()',
    '_tryShowImmediateEvent = null;',
    '_tryShowEvent = null;',
    '"ambush-integrity-awareness"',
    '"ambush-integrity-clean-ambush"',
    '"ambush-integrity-ambush-resisted"',
    '"ambush-integrity-diagnostic"',
    '"ambush-integrity-diagnostics"',
    '"one_handed_dagger"',
    '"warning"',
    '"debug"')) {
    if (!$bridge.Contains($required)) {
        throw "Ambush Integrity GFT bridge is missing required token: $required"
    }
}

if ($bridge.Contains('TrySetBuiltInEventClaim') -or
    $bridge.Contains('TrySetBuiltInEventPresentationClaim')) {
    throw "Ambush Integrity must not claim Grail Floating Text built-in events for its custom messages."
}

$immediateMatch = [regex]::Match(
    $bridge,
    '(?s)_tryShowImmediateEvent\s*=\s*AccessTools\.Method\(.+?\);\s*_tryShowEvent')
if (!$immediateMatch.Success) {
    throw "Ambush Integrity is missing the current 12-argument immediate GFT event probe."
}
if ([regex]::Matches($immediateMatch.Value, 'typeof\(string\)').Count -ne 10 -or
    [regex]::Matches($immediateMatch.Value, 'typeof\(float\)').Count -ne 2) {
    throw "Ambush Integrity immediate GFT event probe must use the 12-argument signature."
}

$fallbackMatch = [regex]::Match(
    $bridge,
    '(?s)_tryShowEvent\s*=\s*AccessTools\.Method\(.+?\);\s*if \(_tryShowImmediateEvent')
if (!$fallbackMatch.Success) {
    throw "Ambush Integrity is missing the 11-argument GFT event fallback probe."
}
if ([regex]::Matches($fallbackMatch.Value, 'typeof\(string\)').Count -ne 9 -or
    [regex]::Matches($fallbackMatch.Value, 'typeof\(float\)').Count -ne 2) {
    throw "Ambush Integrity fallback GFT event probe must use the 11-argument signature."
}

if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    foreach ($required in @(
        '"displayName": "Ambush Integrity - Stealth Overhaul"',
        '"packageName": "AmbushIntegrity"',
        '"version": "0.1.8"',
        '"pluginGuid": "ks.tgfoa.ambush-integrity"',
        '"dll": "AmbushIntegrity.dll"',
        '"src/AmbushIntegrity.cs"',
        '"src/GrailFloatingTextBridge.cs"',
        '"../../tools/shared/GrailFloatingTextLoadErrorNotifier.cs"',
        '"../../tools/shared/ConfigPreviousSettingsRecovery.cs"')) {
        if (!$manifest.Contains($required)) {
            throw "Ambush Integrity manifest is missing required token: $required"
        }
    }
    if ($manifest.Contains('GrailFloatingText.dll')) {
        throw "Ambush Integrity must not take a direct Grail Floating Text assembly reference."
    }
}
else {
    Write-Host "Ambush Integrity manifest is not present yet; manifest integration checks deferred."
}

if (Test-Path -LiteralPath $sourcePath) {
    $source = Get-Content -LiteralPath $sourcePath -Raw
    if ($source.Contains('using GrailFloatingText;') -or
        $source.Contains('typeof(NotificationApi)')) {
        throw "Ambush Integrity main source must use the GFT bridge rather than a direct provider reference."
    }
    if ($source -notmatch '(?s)BepInDependency\s*\(\s*.*ks\.tgfoa\.grail-floating-text.*SoftDependency') {
        throw "Ambush Integrity main source is missing the Grail Floating Text soft dependency."
    }
    foreach ($required in @(
        'new GrailFloatingTextBridge(',
        'DisableAfterStartupFailure();',
        'private void DisableAfterStartupFailure()',
        '_harmony.UnpatchSelf();',
        'Instance = null;',
        'TryShowAwarenessState(',
        'TryShowDiagnostic(',
        '_gftNotificationsEnabled.Value',
        '_diagnostics.Value',
        '_showGrailFloatingTextDiagnostics.Value',
        'AccessTools.PropertyGetter(typeof(HeroControllerData), "BackStabRangeSqr")',
        'AccessTools.PropertyGetter(typeof(MeleeFSM), "IsBackStabAvailable")',
        'typeof(AINoises)',
        '"MakeHeroFootstepNoise"',
        'new[] { typeof(float), typeof(float), typeof(float), typeof(Vector3) }',
        '"Footstep Awareness"',
        'ApplyFootstepAwareness(ref float noiseStrength)',
        'hero.IsCrouching',
        'hero.TryGetElement<ArmorWeight>()',
        'ItemWeight.Medium',
        'ItemWeight.Heavy || armorTier == ItemWeight.Overload',
        'multiplier = 1.2f;',
        'multiplier = 1.6f;',
        'multiplier = 2.0f;',
        'plugin.ApplyFootstepAwareness(ref __1);',
        '"[diagnostic] Footstep awareness:',
        'private const string SteelAndBonePluginGuid = "ks.tgfoa.steel-and-bone";',
        'AccessTools.Method(typeof(HealthElement), "ApplyDamageModifiers")',
        'SteelAndBonePluginGuid);',
        'harmonyPatch.before = new[] { beforeOwner };',
        'AccessTools.Method(typeof(HealthElement), "AfterHealthDecreaseEvents")',
        'AccessTools.Method(typeof(NpcAI), "OnDamageTaken")',
        'ReferenceEquals(target, _committedTarget)',
        'HasExecutionWitness(',
        'damage.Item == null',
        '!damage.Item.IsMelee',
        'hero.HeroCombat.MaxEnemiesAlert >= 5.0f',
        'public static class AmbushIntegrityApi',
        'public const int ApiVersion = 1;',
        'public static int GetBackstabOpportunityState()',
        'NpcElement raycastTarget = GetCurrentRaycastNpc();',
        'ReferenceEquals(_backstabReadyTarget, raycastTarget)',
        'Time.unscaledTime > _backstabReadyUntil',
        '"Backstab range:',
        '"Backstab opportunity',
        '"Backstab API state:',
        '"Damage decision:',
        '"Clean Execution decision:',
        '"Awareness transition:',
        '"GFT dispatch:')) {
        if (!$source.Contains($required)) {
            throw "Ambush Integrity main source is missing required contract token: $required"
        }
    }
}
else {
    Write-Host "Ambush Integrity main source is not present yet; main-source integration checks deferred."
}

Write-Host "Ambush Integrity Grail Floating Text contracts passed."
