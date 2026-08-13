Killing Blow Mastery

Version 1.6.3

Platforms: Windows and Linux through Proton.

Killing Blow Mastery is a standalone BepInEx plugin for Tainted Grail: The
Fall of Avalon. It gives a small extra proficiency bonus to the combat skill
that caused an enemy's killing blow and plays category-specific finisher sounds
when rewards are awarded.

Full Nexus name:

Killing Blow Mastery - Finisher Sounds and Proficiency XP

The goal is to make side-skill training less tedious without replacing normal
combat progression. You can do most of the work with your main weapon, swap for
the finish, and get a modest bonus for the skill that actually landed the kill.

Configuration is created at:

BepInEx/config/ks.tgfoa.killing-blow-mastery.cfg

Killing Blow Mastery starts from a clean plugin identity and uses
ConfigSchemaVersion 14. Older KS Killing Blow configs are ignored. Future
schema resets preserve finisher distance fade, reward volume and pitch,
notification format, and bloodless whitelist terms by exact current setting
name. Numeric values are clamped to their
current supported ranges and invalid values are skipped.

Default behavior:

Enabled = true
ConfigSchemaVersion = 14
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

Audio:

FinisherSoundMode controls the whole reward-sound style:

WeaponSpecific -> use weapon, shield, and magic category pools.
Soulslike -> always use killing_blow1.wav for every awarded killing blow.
GoatTest -> always use goat.wav for every awarded killing blow.
Off -> no finisher sound.

FinisherSoundRangeVolume controls realistic distance fade for active finisher
sound modes. Zero disables distance fade. The default 1 uses the full 0m = 100%,
50m+ = 10% curve:

0m -> 100%
12.5m -> 78%
25m -> 55%
37.5m -> 33%
50m+ -> 10%

FMOD is used for reward sounds. If FMOD playback fails, the mod falls back to the
older Unity AudioSource path and logs the failure when Diagnostics is enabled.

The mod looks for numbered WAV files beside KillingBlowMastery.dll or inside an
audio folder beside it. It loads only files that exist, with up to five files per
pool.

The included runtime WAVs are processed as mono 44.1 kHz 16-bit PCM, with
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
bonus, notification route, and reward sound pool.

Compatibility:

Versatile Weapons 0.3.0+ is an optional soft integration. Its current grip
selects the One-Handed or Two-Handed killing-blow bonus, notification skill,
weapon-family icon, and finisher sound pool. Native weapon proficiency remains
the fallback when Versatile Weapons is absent, disabled, or unavailable.

Build:

Use the repository-level tools/Build-Mod.ps1 script to compile and export the
package. Release zips contain only the runtime payload, README, and changelog.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
