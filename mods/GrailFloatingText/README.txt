Grail Floating Text
Version 1.9.9

Grail Floating Text is a shared BepInEx 5 Mono floating text overlay any
Tainted Grail: The Fall of Avalon mod author can use.

It stacks active messages instead of letting multiple mod messages draw on top
of each other. Callers can send style, category, priority, icon, and collapse-key
intent so status messages update cleanly while reward and event messages still
stack normally. New messages use a short pop-in scale animation, and stacked
messages glide into their new positions when pushed.

When Eyes in the Dark is loaded, built-in Wyrd notifications follow its live
Purple Wyrdness or Native Orange palette through GFT's corresponding color
group. Priority, duration, and icon behavior remain independent.

It also includes a small optional default-game-event layer for useful facts the
game does not already show as ordinary notification text. It can show the actual
time rested after sleep, including short sleeps caused by interruptions, plus
encumbrance, location clear, pickpocket, bounty, throttled block/parry combat
feedback, optional weak spot/sneak attack combat feedback, XP gains, and
vanilla Wyrd state changes. By default, GFT shows one XP entry at a time and
combines compatible gains that arrive while it is visible. Generic XP stays
separate from every mod source, and mod XP merges only through an explicit
source-specific key. KS Wyrd Hunt Addon still owns Wyrd
Scent notifications. By
default, vanilla Safe/Exposed messages are suppressed while Wyrd Hunt Addon is
loaded so Wyrd Scent remains the authoritative Wyrd status line.
Vanilla Wyrdnight and Safe/Exposed messages are also suppressed while loading,
transitioning, or otherwise outside fully initialized visible gameplay. These
state messages are not deferred, so stale loading-screen changes do not appear
after arrival.

Config file:

BepInEx/config/ks.tgfoa.grail-floating-text.cfg

Default settings:

Enabled = true
ConfigSchemaVersion = 24
NotifyModCompatibility = true
Successful schema resets wait for fully visible loaded gameplay, then appear with
the system icon and use the configurable System duration bucket. Integrated
Grailwright mods use the same reset notice. Load-time errors wait for the usable
main menu and use that same System duration.
Schema resets preserve calibrated layout, font, timing, animation, opacity,
icon, color, and per-source display settings. Event assignments, notification
behavior, and diagnostics still receive fresh defaults.
Scale = 1.2
FontSize = 20
FontMode = GameDefault
GameDefault uses the game's active Accessibility FontAsset. Sans and Serif use
the matching game FontAssets directly; ImguiDefault keeps the legacy Arial look.
Startup messages temporarily use a safe fallback until the game font services
are ready.
CenterX = 0.5
BaseCenterY = 0.25
Width = 1040
StackSpacing = 34
MaximumVisibleNotifications = 16
DefaultDurationSeconds = 4
DefaultFadeSeconds = 0.25
Fade-in stays at DefaultFadeSeconds. Fade-out scales with total duration: the
default Medium bucket fades out over 0.25 seconds and System over 0.5 seconds.
VeryShortDurationSeconds = 3
ShortDurationSeconds = 3.5
MediumDurationSeconds = 4
LongDurationSeconds = 4.5
VeryLongDurationSeconds = 5
SystemDurationSeconds = 10
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
IconShadowEnabled = true
IconShadowOpacity = 0.75
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
NotifyBlockedDamage = false
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
ConsolidateXpGains = true
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
Built-in vanilla Wyrd notices request the Purple group directly. Each Color
setting's in-config description also identifies its own default hex value.
Configured color-group names are resolved before literal HTML named colors, so
each Color setting controls its matching group even for names such as Purple.
Floating text isolates Unity IMGUI tint and enabled state while drawing so game
panels and other callbacks cannot darken configured text or icon colors.
Built-in icons use runtime mipmaps and trilinear filtering for stable detail
while scaling, with transparent-edge color dilation to prevent dark fringes.
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

Icon color overrides default to blank and inherit their matching text group:

RedIconColor =
GoldIconColor =
BlueIconColor =
PurpleIconColor =
OrangeIconColor =
PaleIconColor =
GrayIconColor =
WhiteIconColor =
DefaultIconColor =

For example, PurpleIconColor = #FFD0FF keeps Purple-group text at PurpleColor
while tinting its foreground icons separately. Wyrd aliases use the Purple
override. Invalid values safely inherit the text color and log one warning.

Compatibility detection:

GFT checks only exact loaded DLL or setting conflicts it can verify. It reports a
concise System notice without changing either mod. Authors can perform their own
exact check and submit an OnMainMenu System event through the API; log the full
details to BepInEx/LogOutput.log.

When Eyes in the Dark is loaded, GFT flags Wyrd Hunt and Custom Timescale as
incompatible counterparts using this same one-notice convention. It does not
disable, unload, or reconfigure any of the detected plugins.

Mods can integrate with Grail Floating Text as an optional dependency. API v8
adds source-isolated XP batching through TryClaimConsolidatedXpGain. API v7
adds Immediate, OnMainMenu, and OnLoad delivery paths. Deferred messages persist
until their first eligible visible frame, and their duration starts only when
rendered. API v6 adds text-aware XP gain claims so a mod can style and name the
next XP stat change it triggers without producing a duplicate generic XP entry.
API v4 adds optional event IDs and named duration buckets through TryShowEvent.
The System bucket is intended for startup, config-reset, and load-time error
messages. Older calls still work.

Built-in icon IDs:

general, system, status, wyrd, reward, combat, warning, critical, debug, rest,
location, one_handed, two_handed, archery, shield, parry, unarmed, magic, crime,
pickpocket, weight, experience, corpse

The painterly source sheet lives under icons/source in the Grailwright source
tree. Runtime icons are the transparent PNG masks in icons and can be replaced
by keeping the same file names.

When per-source controls are enabled, the config gains sections for each source
that sends messages.

Install with Vortex as a BepInEx plugin, or place this folder under
BepInEx/plugins.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
