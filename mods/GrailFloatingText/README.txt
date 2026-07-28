Grail Floating Text
Version 1.5.0

Grail Floating Text is a shared BepInEx 5 Mono floating text overlay any
Tainted Grail: The Fall of Avalon mod author can use.

It stacks active messages instead of letting multiple mod messages draw on top
of each other. Callers can send style, category, priority, icon, and collapse-key
intent so status messages update cleanly while reward and event messages still
stack normally. New messages use a short pop-in scale animation, and stacked
messages glide into their new positions when pushed.

It also includes a small optional default-game-event layer for useful facts the
game does not already show as ordinary notification text. It can show the actual
time rested after sleep, including short sleeps caused by interruptions, plus
encumbrance, location clear, pickpocket, bounty, throttled block/parry combat
feedback, optional weak spot/sneak attack combat feedback, XP gains, and
vanilla Wyrd state changes. XP gains are shown as separate short entries rather
than merged into a single running total. KS Wyrd Hunt Addon still owns Wyrd
Scent notifications. By
default, vanilla Safe/Exposed messages are suppressed while Wyrd Hunt Addon is
loaded so Wyrd Scent remains the authoritative Wyrd status line.

Config file:

BepInEx/config/ks.tgfoa.grail-floating-text.cfg

Default settings:

Enabled = true
ConfigSchemaVersion = 9
Scale = 1
FontSize = 20
CenterX = 0.5
BaseCenterY = 0.25
Width = 520
StackSpacing = 34
MaximumVisibleNotifications = 6
DefaultDurationSeconds = 2
DefaultFadeSeconds = 0.25
VeryShortDurationSeconds = 1
ShortDurationSeconds = 1.5
MediumDurationSeconds = 2
LongDurationSeconds = 2.5
VeryLongDurationSeconds = 3
GlobalOpacity = 0.9
SpawnAnimationEnabled = true
SpawnStartScale = 0.7
SpawnOvershootScale = 1.12
SpawnAnimationSeconds = 0.2
StackMoveAnimationSeconds = 0.16
DuplicateSuppressSeconds = 0.15
IconsEnabled = true
IconSize = 32
IconGap = 10
IconOpacity = 0.95
GeneralEnabled = true
CombatEnabled = true
RewardEnabled = true
StatusEnabled = true
SystemEnabled = true
DebugEnabled = false
PerSourceControlsEnabled = true
DefaultThrottleSeconds = 0.05
DefaultDurationMultiplier = 1
Diagnostics = false
NotifyRestDuration = true
NotifyInterruptedRestDuration = true
RestDurationTextFormat = Rested {duration}
RestInterruptedTextFormat = Rest interrupted: {duration} slept
RestNotificationMinimumMinutes = 1
NotifyBlockedDamage = true
NotifyParriedDamage = true
CombatDefenseMinimumDamage = 1
CombatDefenseCooldownSeconds = 0.75
NotifyEncumbranceChanged = true
NotifyLocationCleared = true
NotifyPickpocketSuccess = true
NotifyPickpocketFail = true
NotifyBountyChanged = true
NotifyBountyCleared = true
NotifyUnforgivableCrime = true
CrimeEventCooldownSeconds = 0.5
NotifyWeakspotHit = false
NotifySneakAttack = false
CombatHitMinimumDamage = 1
CombatHitCooldownSeconds = 1
NotifyXpGained = true
SuppressVanillaXpNotifications = true
XpTextFormat = +{xp} XP
XpDurationBucket = Short
VanillaWyrdEventsEnabled = true
NotifyWyrdNightChange = true
NotifyWyrdSafetyChange = true
SuppressWyrdSafetyWhenWyrdHuntAddonLoaded = true
NotifyWyrdSoulFragmentCollected = true
NotifyWyrdSkillToggle = false
VanillaWyrdEventCooldownSeconds = 0.75
RedColor = #FF3D2E
RedEvents = killing-blow; blood-magic-corpse-xp; default-unforgivable-crime; default-combat-weakspot; default-combat-sneak-attack
GoldColor = #FFDB47
GoldEvents = default-location-cleared; default-pickpocket-success; default-bounty-cleared; vanilla-wyrd-fragment
BlueColor = #9EE0FF
BlueEvents = default-burden-lifted
PurpleColor = #C294FF
PurpleEvents = wyrd-hunt-status; vanilla-wyrd-night; vanilla-wyrd-safety; vanilla-wyrd-skill
OrangeColor = #FFB87A
OrangeEvents = default-rest-interrupted; default-over-encumbered; default-combat-blocked; default-combat-parried; default-pickpocket-fail; default-bounty-changed
PaleColor = #DBE6FF
PaleEvents = default-rest-duration
GrayColor = #B3B3B3
GrayEvents =
WhiteColor = #FFFFFF
WhiteEvents = default-xp-gain
DefaultColor = #F5E0AD
DefaultEvents =

Mods can integrate with Grail Floating Text as an optional dependency. API v5
adds XP gain claims so a mod can style the next XP stat change it triggers
without producing a duplicate generic XP entry. API v4 adds optional event IDs
and named duration buckets through TryShowEvent. API v3 supports category,
priority, collapse-key, and icon routing through reflection, and API v2 and v3
calls still work.

Built-in icon IDs:

general, system, status, wyrd, reward, combat, warning, critical, debug, rest,
location, one_handed, two_handed, archery, shield, unarmed, magic, crime,
pickpocket, weight, experience, corpse

The painterly source sheet lives under icons/source in the Grailwright source
tree. Runtime icons are the transparent PNG masks in icons and can be replaced
by keeping the same file names.

When per-source controls are enabled, the config gains sections for each source
that sends messages.

Install with Vortex as a BepInEx plugin, or place this folder under
BepInEx/plugins.
