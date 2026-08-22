[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $modRoot "src\VersatileWeapons.cs"
$readmePath = Join-Path $modRoot "README.txt"
foreach ($path in @($sourcePath, $readmePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing contract input: $path"
    }
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$readme = Get-Content -LiteralPath $readmePath -Raw

$requiredPairTypes = @(
    "EquipmentType.Fists",
    "EquipmentType.OneHanded",
    "EquipmentType.Shield",
    "EquipmentType.Magic",
    "EquipmentType.Rod"
)
foreach ($pairType in $requiredPairTypes) {
    if (-not $source.Contains($pairType)) {
        throw "Missing supported one-slot pair type: $pairType"
    }
}

$requiredModes = @(
    "GripCombatMode.OneHanded",
    "GripCombatMode.OneHandedWithOffHandMelee",
    "GripCombatMode.OffHandMelee",
    "GripCombatMode.DualWielding",
    "GripCombatMode.TwoHanded"
)
foreach ($mode in $requiredModes) {
    if (-not $source.Contains($mode)) {
        throw "Missing grip combat mode: $mode"
    }
}

if ($source -match 'pairedItem == null\s*\|\|\s*IsShield\(pairedItem\)') {
    throw "A legacy empty-or-shield pairing whitelist remains in the source."
}
if ($source -notmatch 'private static readonly string\[\] OffHandMeleeLayers' -or
    $source -notmatch '"Magic_MeleeOffHand"') {
    throw "Offhand converted weapons must retain the game''s offhand-melee layer."
}
if ($source -notmatch '(?s)FindGripSwitchWeapon\(Hero hero\).+IsConvertedTwoHandedGripWeapon\(mainHand\).+IsNativeOneHandedGripWeapon\(mainHand\).+IsConvertedTwoHandedGripWeapon\(offHand\)') {
    throw "Grip ownership must prefer a grip-capable main hand before a converted offhand."
}
$nativeRefresh = [regex]::Match(
    $source,
    '(?s)private bool TryFinalizeNativeOneHandedAfterWeaponTransition\(.+?(?=\r?\n        private )'
).Value
if ($nativeRefresh -notmatch 'HandAnimatorsAreSettled\(\s*hero,\s*weapon,\s*true\)' -or
    $nativeRefresh -notmatch 'MagicVisualIsReady\(hero, pairedHand\)' -or
    $nativeRefresh -notmatch 'BeginEquipFsmReset' -or
    $nativeRefresh -match '_twoHandedGrip' -or
    $nativeRefresh -match 'RefreshNativeOneHandedWeaponAnimations') {
    throw "Native one-handed equipment transitions must wait for both hand visuals and enter the shared settled-equip barrier without restarting controllers."
}
$settledCheck = [regex]::Match(
    $source,
    '(?s)private bool HandAnimationsAreSettled\(.+?(?=\r?\n        private )'
).Value
if ($settledCheck -notmatch 'HandAnimatorsAreSettled' -or
    $settledCheck -notmatch 'MagicVisualIsReady\(hero, pairedHand\)') {
    throw "A paired hand must have both settled animators and a ready asynchronous magic visual before settling."
}
$pairedRefresh = [regex]::Match(
    $source,
    '(?s)private bool BeginPairedRefresh\(.+?(?=\r?\n        private )'
).Value
if ($pairedRefresh -match 'IsShield\(pairedItem\)' -or
    $pairedRefresh -notmatch 'CharacterHandBase pairedHand' -or
    $pairedRefresh -notmatch '_magicVisualRecoveryHand = null' -or
    $source -notmatch 'PairedRefreshStage\.WaitingForPairedHand') {
    throw "Explicit ordered grip restoration must support every paired hand rather than shields alone."
}
$updateLoop = [regex]::Match(
    $source,
    '(?s)private void Update\(\).+?(?=\r?\n        private )'
).Value
if ($updateLoop -match 'BeginPairedRefresh\(hero, weapon\)' -or
    $updateLoop -notmatch '_weaponTransitionRefreshPending\s*&&\s*Time\.timeScale > 0\.0f' -or
    $updateLoop -notmatch 'HandAnimatorsAreSettled' -or
    $updateLoop -notmatch 'TryRecoverMissingMagicVisualAfterTransition' -or
    $updateLoop -notmatch 'BeginEquipFsmReset' -or
    $updateLoop -notmatch 'ProcessEquipFsmReset') {
    throw "Normal equipment transitions must wait for the actual paired visual and route every pairing through the shared settled-equip barrier."
}
$controllerMismatch = $updateLoop.IndexOf(
    '_selectedGripControllerTwoHanded'
)
$controllerRefresh = $updateLoop.IndexOf(
    'RefreshWeaponAnimations('
)
$settledFsmReset = $updateLoop.IndexOf(
    'BeginEquipFsmReset('
)
if ($controllerMismatch -lt 0 -or
    $controllerRefresh -lt 0 -or
    $settledFsmReset -lt 0 -or
    $controllerMismatch -gt $controllerRefresh -or
    $controllerRefresh -gt $settledFsmReset -or
    $updateLoop -notmatch '_selectedGripControllerKnown' -or
    $updateLoop -notmatch 'ReferenceEquals\(\s*_selectedGripControllerItem,\s*weapon\.Item\)' -or
    $updateLoop -notmatch '_selectedGripControllerTwoHanded\s*!=\s*!desiredState') {
    throw "A settled equipment transition must correct a stale grip controller before starting its FSM-only reset."
}
$equipFsmReset = [regex]::Match(
    $source,
    '(?s)private bool BeginEquipFsmReset\(.+?(?=\r?\n        private )'
).Value
$equipFsmResetProcessor = [regex]::Match(
    $source,
    '(?s)private bool ProcessEquipFsmReset\(.+?(?=\r?\n        private )'
).Value
$equipFsmSelection = [regex]::Match(
    $source,
    '(?s)private static List<HeroAnimatorSubstateMachine> GetEquipFsms\(.+?(?=\r?\n        private )'
).Value
foreach ($fsmType in @(
    'OneHandedFSM',
    'TwoHandedFSM',
    'DualHandedFSM',
    'MagicMeleeOffHandFSM',
    'MagicMainHandFSM',
    'MagicOffHandFSM'
)) {
    if ($equipFsmSelection -notmatch $fsmType) {
        throw "Settled-equip FSM selection is missing $fsmType."
    }
}
if ($equipFsmReset -notmatch 'GetEquipFsms' -or
    $equipFsmReset -notmatch 'DisableActiveGripFsms' -or
    $equipFsmReset -notmatch '_equipFsmResetGeneration\s*=\s*_weaponTransitionGeneration' -or
    $equipFsmReset -match 'HideWeapon|ShowWeapon|MarkHandAnimatorLoading|SetPairedHandHiddenPreservingDrawnState' -or
    $equipFsmResetProcessor -notmatch '_equipFsmResetGeneration\s*!=\s*_weaponTransitionGeneration' -or
    $equipFsmResetProcessor -notmatch 'EnableEquipFsms' -or
    $equipFsmResetProcessor -notmatch 'WaitingForStableFsms' -or
    $equipFsmResetProcessor -notmatch 'EquipFsmsAreStable' -or
    $equipFsmResetProcessor -match 'HideWeapon|ShowWeapon|MarkHandAnimatorLoading|SetPairedHandHiddenPreservingDrawnState' -or
    $source -notmatch 'Controller diagnostic: selectedProfile=' -or
    $source -notmatch 'Settled equip-FSM diagnostic:' -or
    $source -match 'Temporary spell VFX diagnostic|aliveParticleCount|HasAnySystemAwake') {
    throw "Settled equipment must restart the exact melee and magic FSM set together, preserve loaded controllers and hand visuals, own one transition generation, and emit compact controller and FSM diagnostics."
}
$magicLayersPatch = [regex]::Match(
    $source,
    '(?s)class CharacterMagicToggleAnimatorLayersPatch.+?(?=\r?\n    \[HarmonyPatch\])'
).Value
if ($magicLayersPatch -notmatch 'RecordAnimatorLayersApplied\(\s*__instance,\s*activate\)') {
    throw "Spell hands must report animator-layer readiness."
}
$animatorLoadPatch = [regex]::Match(
    $source,
    '(?s)class AnimatorLoadPatch.+?(?=\r?\n    \[HarmonyPatch\])'
).Value
if ($animatorLoadPatch -notmatch 'typeof\(CharacterMagic\)') {
    throw "Spell hands must report animator-load starts."
}
$magicVisualPatch = [regex]::Match(
    $source,
    '(?s)class CharacterMagicSetupMagicGauntletPatch.+?(?=\r?\n    \[HarmonyPatch\])'
).Value
if ($magicVisualPatch -notmatch '"SetupMagicGauntlet"' -or
    $magicVisualPatch -notmatch 'RecordMagicVisualLoadCompleted') {
    throw "Spell hands must report completion of their separately loaded visible magic gauntlet."
}
$magicVisualStartPatch = [regex]::Match(
    $source,
    '(?s)class CharacterMagicEquipMagicGlovePatch.+?(?=\r?\n    \[HarmonyPatch\])'
).Value
if ($magicVisualStartPatch -notmatch '"EquipMagicGloveToHero"' -or
    $magicVisualStartPatch -notmatch 'RecordMagicVisualLoadStarted') {
    throw "Spell visual readiness must be cleared when the visual load starts, before any cached completion can occur."
}
$magicVisualReady = [regex]::Match(
    $source,
    '(?s)private bool MagicVisualIsReady\(.+?(?=\r?\n        private )'
).Value
$magicVisualRecovery = [regex]::Match(
    $source,
    '(?s)private bool TryRecoverMissingMagicVisualAfterTransition\(.+?(?=\r?\n        private )'
).Value
if ($magicVisualReady -notmatch 'activeInHierarchy' -or
    $magicVisualReady -notmatch '_mainHandMagicVisualLoads == 0' -or
    $magicVisualReady -notmatch '_offHandMagicVisualLoads == 0' -or
    $magicVisualReady -notmatch 'ReferenceEquals\(hero\.MainHandWeapon, magicHand\)' -or
    $magicVisualReady -notmatch 'ReferenceEquals\(hero\.OffHandWeapon, magicHand\)' -or
    $magicVisualRecovery -notmatch 'MagicVisualIsLoading\(hero, magicHand\)' -or
    $magicVisualRecovery -notmatch 'Time\.timeScale <= 0\.0f') {
    throw "Visual recovery must require the exact current hand, no outstanding loads, and an active gauntlet."
}
$magicVisualStart = [regex]::Match(
    $source,
    '(?s)internal void RecordMagicVisualLoadStarted\(.+?(?=\r?\n        internal )'
).Value
$magicVisualCompletion = [regex]::Match(
    $source,
    '(?s)internal void RecordMagicVisualLoadCompleted\(.+?(?=\r?\n        internal )'
).Value
if ($magicVisualStart -notmatch '_mainHandMagicVisualLoads\+\+' -or
    $magicVisualStart -notmatch '_offHandMagicVisualLoads\+\+' -or
    $magicVisualStart -match 'Hero\.Current|MainHandWeapon|OffHandWeapon' -or
    $magicVisualCompletion -notmatch '_mainHandMagicVisualLoads--' -or
    $magicVisualCompletion -notmatch '_offHandMagicVisualLoads--' -or
    $magicVisualCompletion -notmatch 'ReferenceEquals\(hero\.MainHandWeapon, hand\)' -or
    $magicVisualCompletion -notmatch 'ReferenceEquals\(hero\.OffHandWeapon, hand\)') {
    throw "Spell visual tracking must count pre-registration requests and reject completions from obsolete hand instances."
}
$pairedRefreshProcessor = [regex]::Match(
    $source,
    '(?s)private bool ProcessPairedRefresh\(.+?(?=\r?\n        private )'
).Value
if ($pairedRefreshProcessor -notmatch 'Time\.timeScale <= 0\.0f' -or
    $pairedRefreshProcessor -notmatch 'TryRecoverMissingMagicVisualAfterTransition\(\s*hero,\s*_pairedRefreshPairedHand,\s*_pairedRefreshStartedAt\)' -or
    $pairedRefreshProcessor -notmatch 'BeginEquipFsmReset') {
    throw "Explicit grip restoration must receive the same visual-only spell recovery and settled-equip synchronization as menu transitions."
}
$transitionRecorder = [regex]::Match(
    $source,
    '(?s)internal void RecordWeaponTransition\(.+?(?=\r?\n        private )'
).Value
if ($transitionRecorder -notmatch '_pairedRefreshStage != PairedRefreshStage\.None' -or
    $transitionRecorder -notmatch '_pairedHandVisibilityRecoveryCandidate\s*=\s*_pairedRefreshPairedHand' -or
    $transitionRecorder -notmatch 'CancelPairedRefresh\(\)' -or
    $transitionRecorder -notmatch 'CancelEquipFsmReset\(\)' -or
    $transitionRecorder -notmatch '_weaponTransitionGeneration\+\+') {
    throw "A newer equipment transition must cancel stale grip work, advance ownership, and retain only its hidden paired hand for recovery."
}
$pairedVisibilityRecovery = [regex]::Match(
    $source,
    '(?s)private void MonitorCanceledPairedHandVisibility\(.+?(?=\r?\n        private )'
).Value
if ($pairedVisibilityRecovery -notmatch 'HiddenDrawnWeaponRecoverySeconds' -or
    $pairedVisibilityRecovery -notmatch 'Time\.timeScale <= 0\.0f' -or
    $pairedVisibilityRecovery -notmatch 'hero\.MainHandWeapon' -or
    $pairedVisibilityRecovery -notmatch 'hero\.OffHandWeapon' -or
    $pairedVisibilityRecovery -notmatch '_weaponTransitionRefreshPending' -or
    $pairedVisibilityRecovery -notmatch 'MagicVisualIsLoading' -or
    $pairedVisibilityRecovery -notmatch 'SetPairedHandHiddenPreservingDrawnState' -or
    $source -match 'RestoreCanceledRefreshVisibilityAfterTransition') {
    throw "Canceled grip work must use the settled drawn paired-hand watchdog instead of a fixed-delay restore."
}
$missingGripBranch = [regex]::Match(
    $updateLoop,
    '(?s)if \(gripWeapon == null \|\| gripWeapon\.Item == null\)\s*\{.+?\r?\n            \}\s*else'
).Value
$clearObservedWeapon = [regex]::Match(
    $source,
    '(?s)private void ClearObservedWeapon\(\)\s*\{.+?\r?\n        \}'
).Value
if ($updateLoop -notmatch 'MonitorCanceledPairedHandVisibility\(hero\)' -or
    $missingGripBranch -match '_pairedHandVisibilityRecoveryCandidate' -or
    $clearObservedWeapon -match '_pairedHandVisibilityRecoveryCandidate') {
    throw "Owned paired-hand recovery must run independently and survive temporary loss of a supported grip weapon."
}
if ($source -notmatch '(?s)UsesParallelOffHandMeleeMode\(.+?GripCombatMode\.OneHandedWithOffHandMelee') {
    throw "Independent offhand weapons must select the parallel offhand-melee mode."
}
$hybridModeReferences = [regex]::Matches(
    $source,
    'combatMode\s*==\s*GripCombatMode\.OneHandedWithOffHandMelee'
).Count
if ($hybridModeReferences -lt 3) {
    throw "The parallel offhand-melee mode must be preserved by FSM disabling, reconciliation, and validation."
}
if ($source -match 'ConfigureShootProjectileDiagnosticPatch|RecordProjectileConfigured|ProcessProjectileDiagnosticSamples|ProjectileDiagnosticSample|Temporary projectile diagnostic') {
    throw "Temporary projectile observation must not remain in the release build."
}
if ($source -notmatch '"ZeroRequirementFullPotencyStrength",\s*10\.0f' -or
    $source -notmatch 'GetZeroRequirementFullPotencyStrength\(\)' -or
    $source -notmatch 'fullPotencyStrength = strengthRequirement > 0\.0f' -or
    $source -notmatch 'currentStrength - strengthRequirement' -or
    $source -notmatch 'fullPotencyStrength - strengthRequirement' -or
    $readme -notmatch 'ZeroRequirementFullPotencyStrength = 10') {
    throw "Zero-requirement greatweapons must scale from 0 Strength to the configured absolute full-potency endpoint."
}
if ($source -notmatch '(?s)"DamageAtWeaponRequirement".+?AcceptableValueRange<float>\(0\.1f, 1\.5f\)' -or
    $source -notmatch '(?s)"DamageAtFullPotency".+?AcceptableValueRange<float>\(0\.1f, 1\.5f\)' -or
    $source -notmatch '(?s)"AttackSpeedAtWeaponRequirement".+?AcceptableValueRange<float>\(0\.25f, 1\.5f\)' -or
    $source -notmatch '(?s)"AttackSpeedAtFullPotency".+?AcceptableValueRange<float>\(0\.25f, 1\.5f\)' -or
    $source -notmatch 'private const int ConfigSchemaVersion = 13;') {
    throw "One-handed greatweapon damage must cap at 150 percent and attack-speed tuning must extend down to 25 percent under schema 13."
}
if ($source -notmatch '"RememberGripPerLoadout",\s*true' -or
    $source -notmatch 'Dictionary<string, GripMemoryRecord>' -or
    $source -notmatch 'GetGripMemoryContextKey' -or
    $source -notmatch 'GetGripMemoryItemId' -or
    $source -notmatch 'record\.PairedItemId' -or
    $source -notmatch 'GetGripOwnerHand' -or
    $source -notmatch 'ScheduleGripMemoryInvalidationIfNeeded' -or
    $source -notmatch 'SaveSlotFile' -or
    $source -notmatch 'TryLoadSlotFile' -or
    $source -notmatch 'GloriousUiPluginGuid' -or
    $source -notmatch '_currentVirtualWeaponSlot' -or
    $readme -notmatch 'RememberGripPerLoadout = true' -or
    $readme -notmatch 'Glorious UI weapon loadout') {
    throw "Grip memory must default on, persist per save, validate exact equipment and owning hand, invalidate changed loadouts, and distinguish Glorious UI virtual slots."
}
$rememberedGripRefresh = [regex]::Match(
    $source,
    '(?s)private bool ProcessRememberedGripAnimationRefresh\(.+?(?=\r?\n        private )'
).Value
if ($source -notmatch 'ScheduleRememberedGripAnimationRefresh' -or
    [string]::IsNullOrEmpty($rememberedGripRefresh) -or
    $source -notmatch '_selectedGripControllerTwoHanded' -or
    $source -notmatch 'selectedGripControllerTwoHanded\s*!=\s*_twoHandedGrip' -or
    $rememberedGripRefresh -notmatch 'waitForPairedHand\s*=\s*!_rememberedGripAnimationRefreshTwoHanded' -or
    $rememberedGripRefresh -notmatch 'HandAnimatorsAreSettled\(\s*hero,\s*weapon,\s*waitForPairedHand\)' -or
    $rememberedGripRefresh -notmatch 'if \(waitForPairedHand\s*&&\s*!MagicVisualIsReady\(hero, pairedHand\)\)' -or
    $source -notmatch 'TryGetRememberedGrip\(' -or
    $source -notmatch 'Applying the settled animator refresh for the remembered non-default grip') {
    throw "A remembered non-default grip must wait for the paired hand only when that grip leaves it active, then refresh the exact weapon controller after the required animators settle."
}
if ($readme -notmatch 'any one-slot hand item' -or
    $readme -notmatch 'Either hand order is supported') {
    throw "The installed-user README does not state the generalized pairing contract."
}
if ($source -match 'IsOffHandWeaponPairedWithMainHandSpell' -or
    $source -notmatch '(?s)CanClaimGripInput\(Hero hero\).+IsSupportedPairedHandItem\(pairedItem\)' -or
    $source -notmatch '(?s)TryToggleGrip\(Hero hero\).+IsSupportedPairedHandItem\(pairedItem\)' -or
    $readme -notmatch 'An offhand greatweapon owns the grip control when the main hand is a\s+spell' -or
    $readme -notmatch 'Changing the offhand weapon to two-handed grip stows the paired main-hand item') {
    throw "A supported offhand greatweapon must retain grip control beside a main-hand spell and stow that spell in two-handed grip."
}
if ($source -notmatch 'GetActiveOffHandTwoHandedGripWeapon' -or
    $source -notmatch 'class TwoHandedFsmStatsItemPatch' -or
    $source -notmatch 'class OffHandTwoHandedAnimationSpeedPatch' -or
    $source -notmatch 'weaponRestriction = WeaponRestriction\.OffHand' -or
    $source -notmatch 'class OffHandTwoHandedRestrictionPatch' -or
    $source -notmatch '__result = ReferenceEquals\(hand, weapon\)' -or
    $source -notmatch 'selectedProfile = "offhand-two-handed"' -or
    $source -notmatch '(?s)class AnimationLayersPatch.+GetActiveOffHandTwoHandedGripWeapon.+GetTwoHandedLayers' -or
    $source -notmatch '(?s)class DualWieldingOffHandPatch.+GetActiveOffHandTwoHandedGripWeapon.+__result = null') {
    throw "An offhand weapon in native two-handed grip must own the real two-handed controller, layers, stats, and animation events without a dual-wield override."
}
if ($source -notmatch 'UpdateOffHandTwoHandedPresentation' -or
    $source -notmatch 'RestoreOffHandTwoHandedPresentation' -or
    $source -notmatch 'weaponTransform\.SetParent\(mainHandSocket, false\)' -or
    $source -notmatch 'weaponTransform\.SetParent\(originalParent, false\)' -or
    $source -notmatch '(?s)ApplyObservedGripTransition.+StartGripEquipInputGuard\(weapon\.Item\);\s+UpdateOffHandTwoHandedPresentation\(hero\)' -or
    $readme -notmatch 'weapon view moves to the main-hand socket expected by the two-handed\s+animations') {
    throw "An offhand weapon in two-handed grip must move only its loaded view to the main-hand socket and restore that view to the offhand socket afterward."
}

Write-Host "Versatile Weapons pairing contracts passed."
