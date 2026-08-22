[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$modRoot = Split-Path -Parent $PSScriptRoot
$plugin = Get-Content -LiteralPath (
    Join-Path $modRoot "src\EyesInTheDark.cs") -Raw
$runtime = Get-Content -LiteralPath (
    Join-Path $modRoot "src\FirstHunterRuntime.cs") -Raw
$guards = Get-Content -LiteralPath (
    Join-Path $modRoot "src\GuardAwarenessCoordinator.cs") -Raw
$manifest = Get-Content -LiteralPath (
    Join-Path $modRoot "mod.json") -Raw | ConvertFrom-Json

function Assert-GuardContract {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (!$Condition) {
        throw "Eyes in the Dark guard-awareness contract failed: $Message"
    }
}

Assert-GuardContract ($manifest.sourceFiles -contains `
    "src/GuardAwarenessCoordinator.cs") `
    "mod.json omits the guard-awareness source."
Assert-GuardContract ($plugin.Contains(
    'private const int ConfigSchemaVersion = 22;')) `
    "additive guard settings unexpectedly changed the config schema."

foreach ($required in @(
    '"EnableGuardAssistance"',
    '"GuardAssistRadiusMeters"',
    '"MaximumAssistingGuards"',
    'DefaultGuardAssistRadius = 24.0f',
    'DefaultMaximumAssistingGuards = 4',
    '_hunterRuntime.CopyLiveMembers(_liveOfficialHunters);',
    '_guardAwareness.Tick(',
    '_guardAwareness.IsAssistedEngagement')) {
    Assert-GuardContract ($plugin.Contains($required)) `
        "plugin integration omits $required."
}

foreach ($required in @(
    'public void CopyLiveMembers(List<NpcElement> destination)',
    'Func<NpcElement, NpcElement, bool> isAssistedEngagement',
    '&& isAssistedEngagement(',
    'currentNpcTarget')) {
    Assert-GuardContract ($runtime.Contains($required)) `
        "official-hunter runtime omits $required."
}

foreach ($required in @(
    'World.All<Location>().ToArraySlow()',
    'knownHunters.Contains(candidate.Npc)',
    'candidate.DistanceToHero <= radiusMeters',
    'if (!IsWithinGuardAssistRadius(',
    'Vector3.Distance(',
    'guard.Location.Coords,',
    'hunter.Location.Coords) <= radiusMeters',
    'NpcAttachment',
    '!attachment.IsUnique',
    'NpcAI.Working',
    'NpcAI.EnterCombatWith(target, false)',
    'if (!guardInCombat)',
    'AntagonismMarker.TryApplySingleton<',
    'FactionAntagonism,',
    'UntilIdle>',
    'AITargetingUtils.ForceAddCombatTarget(',
    'NpcAI.EnterCombatWith(target, true)',
    'HasExactHunterTarget(',
    '_assistingGuards.Count >= maximumGuards',
    'new GuardHunterPair(currentTarget, hunter)')) {
    Assert-GuardContract ($guards.Contains($required)) `
        "guard coordinator omits $required."
}

foreach ($blockedTerm in @(
    'Boss',
    'Unique',
    'Quest',
    'Story',
    'Tutorial',
    'Debug',
    'Challenge',
    'Interaction',
    'Bodyguard')) {
    Assert-GuardContract ($guards.Contains('"' + $blockedTerm + '"')) `
        "special-actor exclusion omits $blockedTerm."
}

$normalCombat = $guards.IndexOf(
    'NpcAI.EnterCombatWith(target, false)',
    [StringComparison]::Ordinal)
$fallback = $guards.IndexOf(
    'appliedTemporaryAntagonism = TryApplyTemporaryAntagonism(',
    [StringComparison]::Ordinal)
$forcedCombat = $guards.IndexOf(
    'NpcAI.EnterCombatWith(target, true)',
    [StringComparison]::Ordinal)
Assert-GuardContract ($normalCombat -ge 0 -and `
    $fallback -gt $normalCombat -and `
    $forcedCombat -gt $fallback) `
    "native-first engagement sequence is out of order."
$pairRadiusGate = $guards.IndexOf(
    'if (!IsWithinGuardAssistRadius(',
    [StringComparison]::Ordinal)
$engagementAttempt = $guards.IndexOf(
    'if (TryEngageGuard(',
    [StringComparison]::Ordinal)
Assert-GuardContract ($pairRadiusGate -ge 0 -and `
    $engagementAttempt -gt $pairRadiusGate) `
    "guard-to-hunter radius gate is missing or occurs after engagement."
Assert-GuardContract (!$guards.Contains('AmbientStalker')) `
    "guard coordinator references the ambient-stalker lane."

Write-Host "Eyes in the Dark guard-awareness contracts passed."
