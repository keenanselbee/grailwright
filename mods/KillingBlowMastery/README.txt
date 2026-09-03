Killing Blow Mastery

Version 1.9.7

Platforms: Windows and Linux through Proton.

Killing Blow Mastery is a standalone BepInEx plugin for Tainted Grail: The
Fall of Avalon. It gives a small extra proficiency bonus to the combat skill
that caused an enemy's killing blow and plays category-specific finisher sounds
when rewards are awarded.

Full Nexus name:

Killing Blow Mastery - Weapon Executions, Finisher Sounds, and Proficiency XP

The goal is to make side-skill training less tedious without replacing normal
combat progression. You can do most of the work with your main weapon, swap for
the finish, and get a modest bonus for the skill that actually landed the kill.

Configuration is created at:

BepInEx/config/ks.tgfoa.killing-blow-mastery.cfg

Killing Blow Mastery starts from a clean plugin identity and uses
ConfigSchemaVersion 19. Older KS Killing Blow configs are ignored. Schema 19
makes proficiency-scaled Executions and expanded enemy selection the defaults.
The earlier schema reset renamed the previous execution controls and replaced
the fixed health threshold with weapon-proficiency progression. Future schema
resets preserve the new Execution progression settings, combat-finisher mode,
expanded target controls, finisher distance fade, reward volume and pitch,
notification format, and bloodless whitelist terms by exact current setting
name. Numeric values are clamped to their current supported ranges and invalid
values are skipped.

Default behavior:

Enabled = true
ConfigSchemaVersion = 19
AutomaticCombatFinishersEnabled = true
CombatExecutionMode = Execution
ExecutionMinimumProficiency = 25
ExecutionHealthPercentAtUnlock = 10
ExecutionHealthPercentAtMastery = 25
ExpandedExecutionTargets = true
ExpandedExecutionExcludedAbstracts = Animal;Animal_Prey
FinisherSoundMode = WeaponSpecific
FinisherSoundRangeVolume = 1
BonusPercentOfEnemyXP = 4
MaximumBonusXP = 100
MinimumBonusXP = 1
RoundBonusXPTo = 1
AllowDamageOverTimeKills = true
DamageOverTimeMemorySeconds = 12
NotificationsEnabled = true
NotificationMinimumXP = 1
NotificationTextFormat = Killing blow: +{xp} {skill}
NotificationMode = GrailFloatingText
RewardSoundVolume = 0.65
UseNonCorporealEnemySounds = true
NonCorporealSoundTerms = Wyrdspirit plus selected Wyrdspawn, Mistling, Banshee, Melancholy, and ghost templates
NonCorporealSoundExclusionTerms = MistBearer boss/mimic, Tidewraith, and excluded Wyrdspawn variants
UseKillingBlowFallbackForClassifiedKills = false
UseBloodlessSoundVariants = true
BloodlessSoundBlacklistTerms = Stone;Golem;Statue;Construct;Automaton;Crystal;Wisp;Spirit;Ghost;Wraith;Specter;Spectre;Skeleton;Skull;Bone;Animated Armor;Elemental;Wyrdspawn;Wyrdspirit;Wyrd Spirit;WyrdSlime;Wyrd Slime;Wyrdness
BloodlessSoundWhitelistTerms =
AvoidRecentSoundRepeats = true
RecentSoundMemory = 2
RandomPitchSemitones = 0.35
Diagnostics = false
FullPotencyExecutions = false

FoA Mod Manager section order:
General, Combat Finishers, Weapon Skills, Notifications, Reward Audio, Advanced
Audio Routing, Advanced, Diagnostics, and the final Import Previous Settings
section. These player-facing groups do not change the underlying config keys or
their stored sections.

The Mod Manager uses shorter labels for the longest settings. Hover descriptions
state when Execution-only controls apply and distinguish Sound Distance Fade from
the separate Reward Sound Volume control.

Eligible combat skills:

One-Handed
Two-Handed
Unarmed
Archery
Shield
Magic

Rules:

Only hero-caused NPC deaths count.
Only the skill that caused the killing blow receives the bonus.
Primary damage is required by default.
Damage-over-time kills can count when traced to a recent supported hero damage
source.
Thrown items are ignored by default.
Targets with XpRewardAllowed = false are ignored by default.
Magic kills can award Magic proficiency when the source is a spell, rod, magic
item, or magical damage event.

Combat finishers:

AutomaticCombatFinishersEnabled controls the killing-blow animations that the
game can start automatically when a normal melee attack begins. Disable it to
make those attacks continue normally instead.

CombatExecutionMode controls only the combat interaction attached to a living
enemy:

Vanilla -> keep the game's normal combat execution rules.
Execution -> default; unlock Execute through the selected melee weapon's
proficiency, then expand its low-health window as that proficiency rises.
Off -> remove the combat Execute interaction.

Executions unlock at 25 proficiency with the weapon that supplies the selected
animation. Their health window then grows linearly from 10% at proficiency 25
to 25% at proficiency 100:

Proficiency below 25 -> Locked
Proficiency 25 -> 10% health
Proficiency 50 -> 15% health
Proficiency 75 -> 20% health
Proficiency 100 -> 25% health

The prompt uses current health as a percentage of maximum health. The game's
normal distance, external-death, and kill-prevention checks still apply.
Eligible Executions use the game's existing finisher animations and still
produce normal killing-blow rewards.

With Steel and Bone 4.0.1 and Dishonored Dynamic Crosshair 3.6.2, an eligible
Execute prompt previews the target's actual Meager, Worthy, Potent, or Prime
corpse-quality skull in white. During the finisher, that same skull follows the
real animation progress toward killing-blow dark red. Confirmed target-matched
feedback hands off near the end to Dishonored's normal layered killing-blow
marker; cancelled finishers never create a false kill marker.

In Execution mode, KBM first selects from the equipped melee weapon's loaded
execution animations after its explicit health and safety checks. If that list
has no playable hero animation, KBM falls back to the weapon's loaded normal
finisher animations. Situational animation filters such as attack direction,
stagger state, and random chance do not block the prompt. Loaded animation
assets are still required, and the native humanoid target-template restriction
remains unless ExpandedExecutionTargets is enabled.

The proficiency check follows the weapon the game actually selects, including
main-hand, off-hand, normal-finisher fallback, and Versatile Weapons' effective
One-Handed or Two-Handed grip. A different high-level equipped weapon cannot
authorize a lower-level weapon's Execution.

ExpandedExecutionTargets is on by default. It lets additional hostile enemy
templates try the game's humanoid execution animations after applying
ExpandedExecutionExcludedAbstracts. The default exclusions are Animal and
Animal_Prey, covering wolves, bears, wildlife, and other animal-classified
creatures whose rigs do not align reliably. Matching is exact and
case-insensitive. Remove a family name to allow it, or clear the list to allow
all expanded targets.

The known abstract-family names are Animal, Animal_Prey, Bandit, BigHumanoid,
Bloody, BoneMask, Boss, ChallengeModeSpawn, Cultist, DalRiataBody, Female,
Foredweller, Ghost, Giant, Human, Humanoid, Male, MiniBoss, Monster,
ReefboundBody, Scourge, Skeleton, Summon, Tainted, WyrdnessBound, and Zombie.

KBM Executions suppress the selected execution asset's slow-motion flag only
for the scoped Execution start, then restore its exact original value. KBM does
not add visual or audio slow motion. The game's ordinary automatic kill cams
remain untouched and can use their native slow motion and audio normally.

These controls do not patch the separate hold-interact execution used for
important unconscious NPCs. To remove all combat finisher animations while
preserving those story executions, disable AutomaticCombatFinishersEnabled and
set CombatExecutionMode to Off.

Audio:

FinisherSoundMode controls the whole reward-sound style:

WeaponSpecific -> use weapon, shield, and magic category pools.
Soulslike -> always use killing_blow1.wav for every awarded killing blow.
GoatTest -> always use goat.wav for every awarded killing blow.
Off -> no finisher sound.

FinisherSoundRangeVolume controls realistic distance fade for active finisher
sound modes. Zero disables distance fade. The default 1 uses the full 0m = 100%,
30m+ = 10% curve:

0m -> 100%
7.5m -> 78%
15m -> 55%
22.5m -> 33%
30m+ -> 10%

FMOD reward sounds are routed through the game's SFX bus, so both the game
Master and SFX volume or mute controls apply. If that bus is unavailable, the
mod safely skips the sound and logs the failure when Diagnostics is enabled.

The mod looks for numbered WAV files beside KillingBlowMastery.dll or inside an
audio folder beside it. It loads only files that exist, with up to five files per
pool.

The included reward WAVs are processed as mono 44.1 kHz 16-bit PCM, with
conservative leading-silence trimming and about -3 dB peak headroom.

For audio testing, set FinisherSoundMode = GoatTest. When enabled, every awarded
killing blow tries goat.wav first, regardless of weapon, shield, or magic type.
goat.wav can live beside the DLL or inside the audio folder.

Fallback order:

matched non-corporeal enemies -> non_corporeal only
classified kills -> specific pool only
unclassified kills -> killing_blow

When UseNonCorporealEnemySounds is true, matched non-corporeal targets use
non_corporeal1.wav through non_corporeal5.wav only. This target route overrides
weapon, magic, Soulslike, and _dry routing, while Off remains silent and GoatTest
still uses goat.wav for diagnostics.

For example, a fire spell kill tries magic_fire1.wav through magic_fire5.wav
first. If none exist, it does not fall through to killing_blow unless
UseKillingBlowFallbackForClassifiedKills is enabled.

Bloodless sound variants:

When UseBloodlessSoundVariants is true, targets whose names, templates, or type
text match BloodlessSoundBlacklistTerms try matching _dry files before
their normal pool sound. BloodlessSoundWhitelistTerms can force normal sounds
for specific targets. Detection only changes sound routing; it does not change
XP or whether the kill is eligible.

Examples:

two_handed_blade1_dry.wav replaces two_handed_blade1.wav for bloodless
two-handed blade kills when it exists.
magic_fire3_dry.wav replaces magic_fire3.wav for bloodless fire kills
when it exists.
killing_blow1_dry.wav replaces killing_blow1.wav for bloodless global or
Soulslike fallback sounds when it exists.

Normal weapon families are routed to a specific pool:

one-handed blunt -> one_handed_blunt
one-handed axe -> one_handed_axe
one-handed dagger/sword/sickle/polearm/unknown -> one_handed_blade
two-handed blunt -> two_handed_blunt
two-handed axe -> two_handed_axe
two-handed sword/polearm/unknown -> two_handed_blade
short bow -> archery_short
medium or unknown bow -> archery_medium
heavy bow -> archery_heavy
shield bash -> shield_bash
unarmed -> unarmed
magic blood/fire/frost/poison/electric/wyrdness/water -> matching magic pool
other magic -> magic_arcane

Set FinisherSoundMode = Soulslike to ignore category pools and always use
killing_blow1.wav for every awarded killing blow. This is meant for a single big
finisher sound.

AvoidRecentSoundRepeats = true avoids sounds recently used in the same pool.
RecentSoundMemory = 2 means the next sound will try not to repeat either of
the previous two sounds from that pool. RandomPitchSemitones adds a subtle
per-playback FMOD pitch variation; set it to 0 to disable it.

Global fallback pool:

killing_blow1.wav ... killing_blow5.wav

Specific pools:

one_handed_blade1.wav ... one_handed_blade5.wav
one_handed_axe1.wav ... one_handed_axe5.wav
one_handed_blunt1.wav ... one_handed_blunt5.wav
two_handed_blade1.wav ... two_handed_blade5.wav
two_handed_axe1.wav ... two_handed_axe5.wav
two_handed_blunt1.wav ... two_handed_blunt5.wav
unarmed1.wav ... unarmed5.wav
archery_short1.wav ... archery_short5.wav
archery_medium1.wav ... archery_medium5.wav
archery_heavy1.wav ... archery_heavy5.wav
shield_bash1.wav ... shield_bash5.wav
non_corporeal1.wav ... non_corporeal5.wav
magic_blood1.wav ... magic_blood5.wav
magic_fire1.wav ... magic_fire5.wav
magic_frost1.wav ... magic_frost5.wav
magic_poison1.wav ... magic_poison5.wav
magic_electric1.wav ... magic_electric5.wav
magic_wyrdness1.wav ... magic_wyrdness5.wav
magic_water1.wav ... magic_water5.wav
magic_arcane1.wav ... magic_arcane5.wav

For bloodless variants, append _dry before .wav after the slot number:

one_handed_blade1_dry.wav ... one_handed_blade5_dry.wav
two_handed_blunt1_dry.wav ... two_handed_blunt5_dry.wav
magic_blood1_dry.wav ... magic_blood5_dry.wav

Broad one_handed, two_handed, archery, shield, and magic fallback pools are not
used in this release. Runtime audio should live in the audio folder.

Audio prep tool:

tools/audio/Convert-RewardSounds.ps1 converts MP3/WAV/etc. files into numbered
44.1 kHz 16-bit PCM WAV reward sounds, trims leading silence by default, and
peak-normalizes them to a target level. It requires ffmpeg on PATH, FFMPEG_PATH,
or -FfmpegPath.

Example:

powershell -ExecutionPolicy Bypass -File tools/audio/Convert-RewardSounds.ps1 -InputFiles "input1.mp3","input2.mp3" -Prefix magic_blood -TargetPeakDb -3

Before redistributing replacement sounds publicly, verify the source licenses
and credit requirements.

Notifications:

Notifications are on by default. The default notification is:

Killing blow: +{xp} {skill}

NotificationMode defaults to GrailFloatingText, the optional shared Grail
Floating Text overlay. With Grail Floating Text 2.3.0 or newer, recognized
one-handed and two-handed kills use specific sword, axe, blunt, dagger, or spear
icons while the awarded proficiency remains One-Handed or Two-Handed. Sickles
use the One-Handed Axe icon while retaining their existing blade-pool audio.
Older GFT versions and unknown subtypes use the broad proficiency icon.
Archery, Shield, Unarmed, and Magic retain their skill-level icons. When Grail
Floating Text 1.4.7 or newer is installed,
killing-blow rewards use the killing-blow event ID and are red by default through
Grail Floating Text's editable RedEvents group. Set NotificationMode = GameHud to
use the original Wyrd/lower HUD notification route, Both to use both routes at
once, or Off to suppress notification display while leaving reward audio
available.

Grail Floating Text is an optional dependency for the default shared
notification route. Killing Blow Mastery still loads without it, but reward text
using NotificationMode = GrailFloatingText is unavailable until Grail Floating
Text is installed.

Set NotificationsEnabled = false to disable reward text. NotificationTextFormat
supports {xp}, {skill}, {enemy}, {weapon}, and {enemyXP}; add {enemy} to a
custom format if you want the target name shown.

Diagnostics:

Turn Diagnostics on to log kill source, resolved proficiency, enemy XP, awarded
bonus, notification route, reward sound pool, and throttled per-target Execution
eligibility decisions. Execution diagnostics identify the rejected safety or
health gate, selected weapon proficiency, calculated health threshold, missing
finisher lists, execution and fallback animation-handle readiness, the native
0.6-second activation delay, and when the Execute prompt should be available.
Expanded-target rejections identify the exact excluded abstract family or a
target-classification inspection failure.

Diagnostics also traces every combat finisher, including automatic vanilla kill
cams. Start, native RemoveSlowdowns cleanup, and OnExit entries include the
origin, target, native slowdown flag, normalized progress, real elapsed time,
gameplay time scale, and attached native slowdown-handle count. A warning is
written if a native Finisher slowdown remains attached near zero time scale for
six real-time seconds or if cleanup or exit throws.

FullPotencyExecutions is a Diagnostics-only test control. When both settings are
enabled, Execution eligibility treats the selected weapon proficiency as 100
and uses ExecutionHealthPercentAtMastery. It does not change the character's
actual proficiency, killing-blow rewards, or save data. Disable Diagnostics to
restore actual-proficiency behavior immediately.

Compatibility:

Versatile Weapons 0.3.0+ is an optional soft integration. Its current grip
selects the One-Handed or Two-Handed Execution progression, killing-blow bonus,
notification skill, weapon-family icon, and finisher sound pool. Native weapon
proficiency remains the fallback when Versatile Weapons is absent, disabled, or
unavailable.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the
package. Release zips contain only the runtime payload, README, and changelog.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
