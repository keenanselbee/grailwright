Grail Floating Text
Version 2.5.6

Platforms: Windows and Linux through Proton.

Grail Floating Text is a shared BepInEx 5 Mono floating text overlay any
Tainted Grail: The Fall of Avalon mod author can use.

It stacks active messages instead of letting multiple mod messages draw on top
of each other. Callers can send style, category, priority, icon, and collapse-key
intent so status messages update cleanly while reward and event messages still
stack normally. New messages use a short pop-in scale animation, and stacked
messages glide into their new positions when pushed. QuickWheelPanelApi v15 provides a
right-anchored, two-column quick-wheel panel surface with GFT fonts, colors,
built-in and source-provided icons, and a compact resource strip for
integrations such as Deeds of Avalon.

When Eyes in the Dark's Atmospheric or Detailed notifications are active, it
claims the night-transition event so GFT does not add redundant Wyrdnight
falls/fades text. GFT resumes its built-in message automatically when Eyes no
longer owns that event. All Wyrd notifications follow Eyes' live Purple
Wyrdness or Native Orange palette through GFT's corresponding color group.

It also includes a small optional default-game-event layer for useful facts the
game does not already show as ordinary notification text. It can show the actual
time rested as the sleep transition ends, including short sleeps caused by interruptions, plus
encumbrance and near-capacity warnings, newly available progression points,
location clear, pickpocket, bounty totals, throttled block/parry combat
feedback, optional weak spot/sneak attack combat feedback, hero healing, XP
gains, food and potion use, Potion Poisoning activation, and vanilla Wyrd state
changes. Food and potion lines show only the consumed item's name by default,
using the built-in food and potion icons with gold and blue presentation.
When Steel and Bone prevents food use during combat, GFT suppresses its
consumption line so only Steel and Bone's brief restriction message appears.
Potion Poisoning is always a separate red line with the potion icon and appears
only when poisoning activates; GFT never reports overdrink buildup progress.
GFT suppresses the matching native potion-use or poisoning announcement only
after its replacement is accepted, so disabled or rejected events retain the
native fallback. This supports vanilla potions and Steel and Bone's native
overdrink adjustments without depending on Steel and Bone's potion data.
Built-in healing uses the configurable
    Green group and the heart-and-spark Healing icon. Blood Magic Expansion 2.8.1
    can mark its healing at the exact mutation, routing visible blood healing through
    the configurable Red group with the Blood Magic icon. Presentation-aware healing
    batches stay separate, so blood healing cannot merge into ordinary green healing.
    Immediate healing is shown by
default, while periodic regeneration and timed healing effects are excluded
unless NotifyHealingOverTime is enabled. Integrations can also claim the
default Healed event only around healing they present or intentionally keep
    quiet; Blood Magic Expansion also uses this for its frequent held-channel healing
    ticks without hiding other immediate healing. By default, GFT shows one XP entry at a time and
combines compatible gains that arrive while it is visible. Generic XP stays
separate from every mod source, and mod XP merges only through an explicit
source-specific key. Healing follows the same visible-entry rule: rapid gains
received while one Healed entry is visible combine into one queued follow-up
without restarting the current timer. KS Wyrd Hunt Addon still owns Wyrd
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
ConfigSchemaVersion = 27
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
NotifyNearEncumbranceLimit = true
EncumbranceWarningPercent = 90
NotifyProgressionPointsGained = true
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
NotifyHealing = true
NotifyHealingOverTime = false
ConsolidateHealing = true
HealingMinimumAmount = 1
HealingTextFormat = Healed {health}
HealingDurationBucket = Short
NotifyFoodConsumed = true
NotifyPotionConsumed = true
IncludeConsumableDescription = false
NotifyPotionOverdrinkTrigger = true
SuppressVanillaPotionNotifications = true

VanillaWyrdEventsEnabled = true
NotifyWyrdNightChange = true
NotifyWyrdSafetyChange = true
SuppressWyrdSafetyWhenWyrdHuntAddonLoaded = true
NotifyWyrdSoulFragmentCollected = true
NotifyWyrdSkillToggle = false
VanillaWyrdEventCooldownSeconds = 0.75
RedColor = #FF3D2E
RedEvents = killing-blow; blood-magic-corpse-xp; default-unforgivable-crime; default-combat-weakspot; default-combat-sneak-attack; default-potion-poisoning
GoldColor = #FFC03A
GoldEvents = default-location-cleared; default-pickpocket-success; default-bounty-cleared; vanilla-wyrd-fragment; default-food-consumed
BlueColor = #9EE0FF
BlueEvents = default-burden-lifted; default-potion-consumed
GreenColor = #8FD36B
GreenEvents =
PurpleColor = #C294FF
PurpleEvents = wyrd-hunt-status; vanilla-wyrd-night; vanilla-wyrd-safety; vanilla-wyrd-skill
PinkColor = #E06AAE
PinkEvents =
Built-in healing notices request the Green group directly, and built-in vanilla
Wyrd notices request the Purple group directly. Each Color
setting's in-config description also identifies its own default hex value.
Configured color-group names are resolved before literal HTML named colors, so
each Color setting controls its matching group even for names such as Purple.
Floating text isolates Unity IMGUI tint and enabled state while drawing so game
panels and other callbacks cannot darken configured text or icon colors.
Built-in icons use runtime mipmaps and trilinear filtering for stable detail
while scaling, with transparent-edge color dilation to prevent dark fringes.
OrangeColor = #FF9A35
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
GreenIconColor =
PurpleIconColor =
PinkIconColor =
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
concise orange Warning-styled System notice without changing either mod. These
notices use High priority, the warning icon, the System duration, and OnMainMenu
delivery. NotificationApi v13 lets any loaded mod submit this standardized notice
with concise player text and exact diagnostic evidence. GFT writes that evidence
to BepInEx/LogOutput.log.

When BepInEx rejects a plugin before its code can run because of a declared hard
incompatibility, GFT reads the verified loader error directly. It identifies the
rejected and conflicting plugins, tells the player to remove or disable one and
restart, and keeps the original BepInEx error in the log. This covers Soul and
Service conflicts with Avalon Summons or Better Summon without requiring Soul and
Service to start or hard-coding those pairs into GFT.

When Eyes in the Dark is loaded, GFT flags Wyrd Hunt and Custom Timescale as
incompatible counterparts using this same one-notice convention. It does not
disable, unload, or reconfigure any of the detected plugins.

Mods can integrate with Grail Floating Text as an optional dependency. The
installed docs/API.md contains a copy-ready reflection resolver, exact overload
signatures, capability names, invocation examples, and the complete icon list.
NotificationApi v13 adds TryShowCompatibilityNotice for standardized, deferred
main-menu compatibility warnings. API v12 adds source-scoped presentation claims for restyling one
exact built-in event without bypassing its user settings. API v11 adds scoped
built-in event ownership for integrations that provide or suppress a replacement.
NotificationApi v10 adds cancellable XP claims so a producer can remove its
reserved line when the matching XP mutation fails. QuickWheelPanelApi provides
persistent two-column quick-wheel panels with tooltip-aware opacity. Version 15
uses native TextMesh Pro SDF outlines and underlay shadows for supported fonts,
with caller-controlled column spacing, approximate outline and shadow reach up to 16, and strength up to 8. One
cached material-backed glyph mesh normally replaces copied effect geometry;
unsupported non-SDF fonts use a bounded six-copy fallback. It retains version
8's compact three-item resource strip, source-provided icons, caller-controlled
text effects, shared header/subheader colors, a caller-controlled outline
strength multiplier for semantic White text, adjustable SDF underlay softness,
and two caller-controlled charcoal column backplates rendered as simple UI
quads. Callers can set each column's reference width. Each quad receives its own
small procedural texture, so their mottling and irregular silhouette differ.
Guaranteed transparent outer gutters and broader feathering remove rectangular
edge cutoffs, while multi-scale grain and fine fibers give them a higher-quality
tooltip-like finish without loading or copying a game texture. A subtle internal
corner blend reduces sharp points without flattening the irregular silhouette.
The backplates use black alpha masks, preserving contrast against bright scenery
without lifting black scenes toward gray. Callers can mark leading left-column
rows as one continuous summary, followed by a slight fixed break before ordinary
statistics. GFT repeats layout only when providers republish panel content or
settings; opacity fades remain frame-smooth.
The multiplier does not affect icons, Pale text, or other color styles. API v8
adds source-isolated XP batching through TryClaimConsolidatedXpGain. API v7
adds Immediate, OnMainMenu, and OnLoad delivery paths. Deferred messages persist
until their first eligible visible frame, and their duration starts only when
rendered. API v6 adds text-aware XP gain claims so a mod can style and name the
next XP stat change it triggers without producing a duplicate generic XP entry.
API v4 adds optional event IDs and named duration buckets through TryShowEvent.
The System bucket is intended for startup, compatibility, config-reset, and load-time error
messages. Older calls still work.

Built-in icon IDs:

general, system, status, wyrd, reward, gold_earned_very_low, gold_earned_low,
gold_earned_medium, gold_earned_high, gold_earned_very_high, combat, warning, critical, debug, rest,
location, one_handed, one_handed_sword, one_handed_axe, one_handed_blunt,
one_handed_dagger, one_handed_spear, two_handed,
two_handed_sword, two_handed_axe, two_handed_blunt, two_handed_spear,
archery, shield, parry, unarmed, magic, crime,
magic_blood, magic_fire, magic_cold, magic_poison, magic_electric,
magic_pure, magic_wet, pickpocket, lock, craft, food, potion, healing, fish, recipe,
weight, experience, corpse_meager, corpse_worthy, corpse_potent, corpse_prime,
summon, skull

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
