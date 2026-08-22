# Blood Magic Expansion

BepInEx 5 Mono plugin for Tainted Grail: The Fall of Avalon.

Blood Magic Expansion turns Blood Transfusion, Life Transfusion, and
Abhartach's Calling into a focused blood-magic progression loop. Drain valid
corpses for XP, healing, and permanent Blood Essence; feed on living enemies
for capped XP ticks; and grow that player-facing essence statistic from the
number and quality of completed corpse rituals.

The idea started while I was playing the prologue with Blood Transfusion and
draining corpses as if I were drawing power from them to fuel darker magic.
Later I wondered if that little bit of roleplay could become a real feature. It
turned out to be much harder than "how hard could it be?", but the mod is now
meant to feel like a fairly complete blood-magic expansion rather than a simple
spell tweak.

This is not a broad magic overhaul. It is a blood-magic expansion built around
corpse rituals, live draining, Blood/Life spell growth, Abhartach corpse
effects, and readable cross-mod corpse quality data.

## Identity

```text
Name: Blood Magic Expansion
DLL: BloodMagicExpansion.dll
GUID: ks.tgfoa.blood-magic-expansion
Config: BepInEx\config\ks.tgfoa.blood-magic-expansion.cfg
Plugin folder: BepInEx\plugins\BloodMagicExpansion
API: BloodMagicExpansion.BloodMagicApi v9
Version: 2.9.4
Platforms: Windows and Linux through Proton.
```

This is a clean technical rename from Blood Mage. Do not deploy the older
BloodMage.dll beside BloodMagicExpansion.dll.

## Default Loop

```text
Kill a blood-plausible enemy
Drain the corpse with Blood/Life Transfusion
Gain corpse XP and immediate healing
Replace the drained body through the game's native corpse cleanup
Higher-quality corpses improve healing and Abhartach effects
Completed corpse rituals build the player-facing Blood Essence statistic
An internal power curve unlocks Blood/Life and Abhartach bonuses from zero
Ready a blood spell with raised hands to cast a scalable red inner player light
Feed on living enemies for capped XP ticks during combat
Hear a short randomized FMOD ritual sound on successful corpse drains
```

Corpse XP comes from the enemy's vanilla effective kill XP, including the
lower-level enemy XP falloff. Corpse quality can scale healing and Abhartach
effects, but character XP is not multiplied again.

`SimplifyDrainedCorpses = true` is enabled by default. After a corpse ritual
fully completes, the game replaces a body that still has visible loot with its
lightweight loot pile and removes an empty body. Unsupported scripted corpses
remain intact. Disable this setting to leave every successfully drained body in
place as a spent corpse.

## Presets

Preset is the main user-facing control. It affects corpse ritual timing,
corpse XP and healing baseline, live-drain XP rhythm, Blood/Life spell tuning,
and Abhartach tuning.

```text
Preset       | Role              | Corpse ritual | Corpse XP | Base heal
Blood Rite   | quick restraint   | 1.0s          | 30%       | 15%
Desecration  | balanced default  | 1.5s          | 40%       | 20%
Soul Feast   | slow high payout  | 2.0s          | 50%       | 25%
Custom       | user tuned        | config        | config    | config
```

Single-held casting pays half of the effective corpse XP and healing by
default. Dual-held Blood Transfusion or Life Transfusion pays the full amount.

Live drain timing is reward pacing, not attack speed:

```text
Preset       | Live XP tick  | Target cap | Tap/projectile | Held channel
Blood Rite   | 4% / 1.0s     | 20%        | 1.03x          | 1.00x
Desecration  | 8% / 1.5s     | 35%        | 1.06x          | 1.01x
Soul Feast   | 12% / 2.0s    | 50%        | 1.12x          | 1.02x
```

Blood Power supplies permanent spell growth. At Blood Power 0, preset spell
tuning contributes no bonus; at Blood Power 100, it reaches the intended full
preset and curve balance. Power 100-200 linearly continues each fully unlocked
bonus portion until Power 200 doubles its Power 100 contribution.
Held channel speed remains intentionally modest so held drains do not multiply
damage too aggressively.

Projectile travel, damage radius, Bleed buildup, and tap casting speed use
independent growth curves. Their default curve bonuses at full normal mastery
are +56%, +34%, +56%, and +21%, respectively, before the selected preset base
and the normal Power unlock are combined. Homing search, held range, held
channel speed, and all Abhartach Power curves retain their established values.

Successful player-sourced Bleed procs from Blood Transfusion, Life Transfusion,
and Abhartach's Calling keep their active effects longer as Blood Power grows.
Native duration is preserved at Power 0, reaches 1.5x at Power 100, and reaches
2x at Power 200. A successful new proc refreshes rather than adds duration, and
the latest completed proc owns the refreshed window; an ordinary weapon or
enemy proc therefore returns it to native duration. Partial pre-proc buildup
decay, Bleed tick strength, and tick frequency are unchanged.

Only held-channel damage earns live-drain XP; tap projectiles never qualify,
including when one lands during another active hold. Confirmed held-channel
Blood/Life Transfusion healing is multiplied by 2x by default without changing
tap projectile healing:

```text
HeldHealingMultiplier = 2
```

The light- and heavy-cast tooltips for Blood Transfusion, Life Transfusion, and
Abhartach's Calling summarize their active BME behavior. They adapt to the
reward, hand-requirement, tuning, and corpse-quality settings while leaving the
game's normal cost and calculated-effect fields intact.

## Blood Essence Progression

Every successfully completed corpse ritual awards an integer amount from the
corpse's existing quality tier:

```text
Meager = 1 Blood Essence
Worthy = 3 Blood Essence
Potent = 5 Blood Essence
Prime  = 10 Blood Essence

When total essence is at most 1,000:
x = clamp(total essence / 1,000, 0, 1)
Power = 10x^3 - 70x^2 + 160x

Above 1,000 Essence:
y = clamp((total essence - 1,000) / 4,000, 0, 1)
Power = 100 + 100y
```

The established mastery curve retains its moderate early advantage and grants
Blood Power 100 with the intended full scaling at 1,000 Essence. Overmastery is
then deliberately linear: every additional 40 Essence grants one Power until
the hard cap of 200 at 5,000 Essence. That final Power stretch doubles each
fully unlocked gameplay bonus portion.
Blood Essence itself is never capped and successful rituals always add more,
even after Power 200. Blood Essence is stored per character in the game save's
GameplayMemory. Deeds of Avalon is optional and is not the progression authority.
Existing Deeds tier counts are imported once when a character has no Blood
Magic Expansion progression state yet.

Blood Essence is the uncapped earned progression statistic. Blood Power is its
derived 0-200 rating, shown by supported Deeds versions and used to scale spells
and the inner light.

## Configuration

Start the game once to generate:

```text
BepInEx\config\ks.tgfoa.blood-magic-expansion.cfg
```

The optional vanilla Bleed graph preload is enabled by default:

```text
PreloadBleedSkillGraphs = true
```

It loads the heavy dependency and parent graph on consecutive gameplay-loading
frames and retains the parent for the active gameplay domain. Disable this
setting if a future game update removes the first Bleed application hitch. The
preloader is isolated from BME's spell tuning and uses no Harmony patches.

Version 2.0.0 and newer use a clean GUID and config path. There is no old config
migration. The old `ks.tgfoa.blood-mage.cfg` file is ignored.

The current config uses ConfigSchemaVersion 21 because the obsolete GrowthSource
and SpiritualityStatTerms settings were removed. Blood Essence is now the sole
spell-growth progression source, with Blood Power retained as its derived
0-200 gameplay rating. If the schema marker is missing or outdated, the old
config is backed up beside the active file and fresh defaults are generated.
Compatible customized settings remain eligible for conservative recovery.

Diagnostics can temporarily test a specific effective Essence value without
overwriting the saved progression:

```text
OverrideBloodEssence = false
BloodEssenceOverrideValue = 1000
```

## Grail Floating Text

When Grail Floating Text is installed, corpse-leech and live-drain character XP
use red source-specific messages. GFT 2.4.4 or newer uses the matching Meager,
Worthy, Potent, or Prime corpse icon for each completed ritual, while live-drain
ticks use the magic icon. GFT 2.1.2 or newer
shows each completed corpse's XP and saved Essence together and cancels that
claim if XP cannot be awarded. Corpse rewards are never consolidated, so every
line reports the exact XP and Essence from one ritual. Older GFT versions safely
fall back to its generic XP line for corpse rituals. GFT 1.9.0 or newer still
    consolidates queued live-drain ticks. GFT 2.4.6 or newer presents visible corpse
    ritual, Blood/Life Transfusion, and Abhartach healing through its configurable
    Red group with the Blood Magic icon. Those blood-healing batches remain separate
    from ordinary green healing. Correlated held-channel healing ticks still stay
    quiet by default while leaving potions and unrelated immediate healing
    notifications unchanged. Blood Magic XP
never merges with another source. The normal character XP stat path,
multipliers, and level-up behavior are unchanged.

ShowGrailFloatingTextDiagnostics defaults to true. It remains inactive unless
the matching LogRejectedCorpses, LogCorpseQuality, or
LogBloodSpellInnerLight option and shows only collapsed corpse-resolution,
quality-change, and inner-light visibility summaries. Full evidence remains in
the BepInEx log.

Completed corpse notifications include both rewards on one line:

```text
+40 XP | +5 Blood Essence
```

```text
ClaimGrailFloatingTextCorpseXP = true
ClaimGrailFloatingTextLiveDrainXP = true
SuppressGrailFloatingTextLiveDrainHealing = true
```

## Deeds of Avalon

When Deeds of Avalon 1.6.7 or newer and Grail Floating Text are installed,
Deeds displays `Blood Essence: X (Y)` immediately above Corpses
Drained. The progression row remains visible at zero, and the corpse counts stay
separated into Meager, Worthy, Potent, and Prime tiers; X is integer Blood
Essence and Y is Blood Power. Interrupted, rejected,
rolled-back, or incomplete rituals do not count. Blood Magic Expansion stores
the tier ledger with the progression and periodically sends Deeds 1.6.7+ an
absolute snapshot so a temporarily unavailable callback cannot drift it.
Progression still works normally without
either display mod. BloodMagicApi v9 also lets integrations read both values,
and Deeds can consolidate new and previously recorded supported spell kills
under the Blood Magic type without rewriting save-backed counters.

## Blood Spell Inner Light

Equipping Blood Transfusion, Life Transfusion, or Abhartach's Calling and
raising a magic hand casts a red no-shadow point light from that hand's
animated hand marker, with its wrist used only as a compatibility fallback.
Each hand is independent: lowering or changing one hand
disables only its light, while dual blood spells can illuminate from both
hands. Casting temporarily triples only the casting hand's brightness 0.3
seconds after cast start, then fades it back down when the cast performs, ends,
or cancels.
Changing that same hand from a blood spell to non-blood equipment turns its
light off immediately. Switching only the opposite weapon, or replacing one
supported blood spell with another, retains the configured fade behavior.
The hand lights are separate from the vanilla HeroLight, so No Player Light can
stay installed while BME provides only the blood-spell glow. The configured
brightness is scaled internally for the game's HDRP renderer so small config
values remain human-friendly. Full interior scenes can apply an additional
multiplier; its default of 1.0 preserves the configured brightness.
Blood Power smoothly grows brightness from a faint 0.2x multiplier at Power 0
to 2x at Power 100, then linearly reaches 3x at Power 200. Default range grows
more modestly: 1.5 meters at Power 0, 3 meters at Power 100, and 4.5 meters at
Power 200.
Blood Transfusion defaults to 0.8x the shared base, Life Transfusion to 1.0x,
and Abhartach's Calling to 1.2x. Their default pre-cast brightness values are
0.08, 0.10, and 0.12 at Power 0; 0.8, 1.0, and 1.2 at Power 100; and 1.2, 1.5,
and 1.8 at Power 200.

```text
Enabled = true
Intensity = 0.5
BloodTransfusionIntensityMultiplier = 0.8
LifeTransfusionIntensityMultiplier = 1.0
AbhartachCallingIntensityMultiplier = 1.2
InteriorIntensityMultiplier = 1.0
MinimumPowerBrightnessMultiplier = 0.2
MasteryBrightnessMultiplier = 2.0
MaximumPowerBrightnessMultiplier = 3.0
MinimumPowerRange = 1.5
MasteryRange = 3.0
MaximumPowerRange = 4.5
FadeSeconds = 0.12
LogBloodSpellInnerLight = false
```

Lower the shared intensity, a spell multiplier, or any milestone value for a
subtler effect. Raise InteriorIntensityMultiplier to strengthen the lights only
in full interiors.
Set Enabled to false, or Intensity to zero, for no visual light. Diagnostics
are limited and can be disabled after confirming readiness and visibility.

## Corpse Leech Audio

Successful corpse rituals can play a short quality-matched WAV through FMOD.
The default package checks five files for each quality tier under the plugin
audio folder:

```text
BepInEx\plugins\BloodMagicExpansion\audio\corpse_leech_low_1.wav    through corpse_leech_low_5.wav
BepInEx\plugins\BloodMagicExpansion\audio\corpse_leech_medium_1.wav through corpse_leech_medium_5.wav
BepInEx\plugins\BloodMagicExpansion\audio\corpse_leech_high_1.wav   through corpse_leech_high_5.wav
BepInEx\plugins\BloodMagicExpansion\audio\corpse_leech_max_1.wav    through corpse_leech_max_5.wav
```

The sounds are cached as FMOD samples after first use. Each successful corpse
leech chooses low, medium, high, or max from the drained corpse's quality, then
picks one loaded file from that tier at random and applies the global corpse
leech volume. By default, it avoids the last two sounds played in the same
quality tier and applies a subtle per-play FMOD pitch variation so repeated
rituals feel less identical.

```text
Quality <=0.25 -> low    / Meager
Quality <=0.50 -> medium / Worthy
Quality <=0.75 -> high   / Potent
Quality >0.75  -> max    / Prime
```

```text
PlayCorpseLeechSound = true
CorpseLeechSoundVolume = 0.85
CorpseLeechSoundRangeVolume = 1.0
AvoidRecentCorpseLeechRepeats = true
RecentCorpseLeechSoundMemory = 2
CorpseLeechRandomPitchSemitones = 0.20
```

CorpseLeechSoundRangeVolume controls a simple distance fade while keeping ritual
audio two-dimensional. At the default 1.0, volume falls from 100% at the corpse
to 10% at 30 meters or farther. Set it to 0 to disable distance fading. Missing
position data safely uses full volume.

## Corpse Quality

Blood Magic Expansion reports focused corpse quality from 0 to 1. Native enemy
Tier tags set intrinsic quality for nearly the entire roster, with distinct
anchors for Tiers 0 through 7. This preserves the four shared presentation
buckets while allowing nearby early-game enemies to separate meaningfully.

For an untagged enemy, base kill XP and maximum health provide a shared fallback
against fixed references of 700 XP and 3400 health. These references are part of
the shared quality policy rather than user settings, keeping Steel and Bone and
Blood Magic Expansion in agreement.

Elite enemies gain 0.10 quality, MiniBosses gain 0.175, and Bosses begin at a
minimum of 0.875 Prime quality. Relative enemy level then moves quality by 0.025
per level above or below the hero, capped at 0.075 in either direction. Level can
distinguish nearby threats without eventually making every old corpse Meager or
every distant enemy Prime. Focused spent, bloodless, and otherwise blocked
registered corpses retain their tier while available to the targeting API, so
integrations can separate quality from usability.

```text
Native enemy tier | Quality
Tier 0            | 0.050
Tier 1            | 0.125
Tier 2            | 0.230
Tier 3            | 0.425
Tier 4            | 0.625
Tier 5            | 0.800
Tier 6            | 0.900
Tier 7            | 1.000
```

By default, corpse quality scales Blood/Life corpse healing plus Abhartach
explosion damage and Bleed buildup from 0.5x to 1.5x. Abhartach explosion
radius uses a narrower 0.85x-to-1.15x band, while held healing uses
0.75x-to-1.25x. Character XP is not multiplied again.
Character XP already comes from vanilla effective kill XP. Corpse quality does
not include difficulty XP multipliers and does not multiply XP a second time.

When Dishonored Dynamic Crosshair 3.0.3 or newer is installed, the blood-magic
corpse reticle can select a Meager, Worthy, Potent, or Prime sprite and scale
visually from the same quality. Reticle presentation is visual-only and
defaults to 1.0x to 2.0x Magic reticle size with a low-quality dead zone.
Quality does not change the usable-corpse blood-red color. Corpses restored
from save data are reconnected to their owning game location after restoration
and remain connected for the current play session.
They use the default-size base-color corpse icon and remain non-drainable.

## Abhartach's Calling

Abhartach's Calling gets separate corpse effect tuning:

```text
Preset       | Damage | Radius | Bleed | Held heal | Search
Blood Rite   | 1.00x  | 1.05x  | 1.05x | 1.10x     | 1.00x
Desecration  | 1.05x  | 1.10x  | 1.12x | 1.20x     | 1.05x
Soul Feast   | 1.12x  | 1.20x  | 1.20x | 1.35x     | 1.10x
```

Blood Power can unlock the configured +40% explosion damage, +35% explosion
radius, +40% bleed buildup, +40% held corpse healing, and +35% corpse search
growth at Power 100. Power 200 doubles each fully unlocked bonus portion.
Corpse search is capped at 2x by default.

## Bloodless Filter

`RequireBloodPlausible = true` rejects corpses and live targets whose names,
templates, or type text match the blacklist. Default blocked examples include
stone, crystal, spirits, ghosts, skeletons, constructs, Wyrdspawn,
Wyrdspirit, Wyrd Slime, Wyrdness, and similar enemies. Wights are intentionally
not blacklisted by default. Whitelist terms win over blacklist terms.

## Requirements

```text
Tainted Grail: The Fall of Avalon Patch 1.25
Steam beta branch: mono
Mono branch build: 24270691
BepInEx 5 Mono
```

This plugin uses the game's managed Mono assemblies. It is not compatible with
the public IL2CPP branch or an IL2CPP BepInEx installation.

## Compatibility

Dishonored Dynamic Crosshair 3.0.3+ can use
`BloodMagicExpansion.BloodMagicApi` v9 for tiered focused-corpse reticle feedback,
supported damage-source classification, and read-only effective Blood Essence
and Blood Power values.

First Person Arms Adjuster 0.3.5+ is an optional soft integration. When present,
each unparented world-space blood light follows FPA's presentation-only visual
offset so the light remains centered on the rendered hand after arm adjustment.

Versatile Weapons 0.1.3+ is an optional soft integration. When the opposite
weapon takes a two-handed grip, an equipped blood spell in the hidden hand is
immediately removed from inner-light, readiness, cast-boost, and held-cast
tracking. In Blood Magic Expansion 2.7.2+, that hidden spell also stops counting
as relevant for API consumers such as Dishonored Dynamic Crosshair, so its
corpse reticle is not shown for an unavailable spell. Normal spell presentation
and API relevance resume after the hand becomes active.

Eyes in the Dark 1.3.2+ is an optional soft integration. Each successfully
completed corpse ritual reports its 0-to-1 quality to Eyes. During an exposed
outdoor Wyrdnight, Eyes converts that quality to 4-to-12 Wyrd Threat with its
default tuning; daytime, indoor, protected, interrupted, and failed rituals add
no threat.

Avoid running older Blood Mage or other Blood/Life Transfusion reward mods at
the same time unless you intentionally want overlapping XP systems.

## Performance

Corpse rituals use lightweight held-state checks and camera raycasts only while
Blood Transfusion, Life Transfusion, or Abhartach corpse feedback is active.
The red inner lights are two cached no-shadow point lights, each enabled only
while its hand has a matching blood spell equipped and raised. Their active
positions require one cached optional-integration call and at most two transform
assignments per frame; reflection is resolved only once. Live draining
and Abhartach tuning are event-driven from real damage, status, and spell
events. Save-bounded Blood Essence is read from the current GameplayMemory and
synced to the optional Deeds integration at most once per second. Noisy diagnostics default to off; startup
and patch-warning logs remain enabled.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
