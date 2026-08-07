# Blood Magic Expansion

BepInEx 5 Mono plugin for Tainted Grail: The Fall of Avalon.

Blood Magic Expansion turns Blood Transfusion, Life Transfusion, and
Abhartach's Calling into a focused blood-magic progression loop. Drain valid
corpses for XP and healing, feed on living enemies for capped XP ticks, and
grow through preset identity, Spirituality, and corpse quality.

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
API: BloodMagicExpansion.BloodMagicApi v4
Version: 2.4.6
Platforms: Windows and Linux through Proton.
```

This is a clean technical rename from Blood Mage. Do not deploy the older
BloodMage.dll beside BloodMagicExpansion.dll.

## Default Loop

```text
Kill a blood-plausible enemy
Drain the corpse with Blood/Life Transfusion
Gain corpse XP and immediate healing
Higher-quality corpses improve healing and Abhartach effects
Preset + Spirituality improve Blood/Life spell behavior
Ready a blood spell with raised hands to cast a scalable red inner player light
Feed on living enemies for capped XP ticks during combat
Hear a short randomized FMOD ritual sound on successful corpse drains
```

Corpse XP comes from the enemy's vanilla effective kill XP, including the
lower-level enemy XP falloff. Corpse quality can scale healing and Abhartach
effects, but character XP is not multiplied again.

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

Spirituality supplies most late-game spell growth. Held channel speed remains
intentionally modest so held drains do not multiply damage too aggressively.

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

Version 2.4.5 and newer enforce ConfigSchemaVersion 11 because the default
Blood Spell Inner Light intensity changed from 1.0 to 0.5. If the schema marker is
missing or outdated, the old config is backed up beside the active file and
fresh defaults are generated. Carefully tuned inner-light and corpse-leech
audio values, blood whitelist terms, and exact spell template GUID overrides
are then restored by exact current setting name with numeric values clamped to
their current supported ranges.

## Grail Floating Text

When Grail Floating Text is installed, corpse-leech and live-drain character XP
use red source-specific messages with corpse and magic icons. GFT 1.9.0 or newer
consolidates queued live-drain ticks together and corpse rewards only within the
same Meager, Worthy, Potent, or Prime tier. Blood Magic XP never merges with
generic XP or another mod. Older GFT versions keep the styled gains separate.
The normal character XP stat path, multipliers, and level-up behavior are unchanged.

```text
ClaimGrailFloatingTextCorpseXP = true
ClaimGrailFloatingTextLiveDrainXP = true
```

## Deeds of Avalon

When Deeds of Avalon is installed, Blood Magic Expansion reports a statistic
only after a corpse ritual completes successfully. The display wording is
Corpses Drained, separated into Meager, Worthy, Potent, and Prime tiers.
Interrupted, rejected, or incomplete rituals do not count.

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
Blood Transfusion defaults to 0.8x the shared base, Life Transfusion to 1.0x,
and Abhartach's Calling to 1.2x. With the default base of 0.5, their normal
pre-cast brightness values are therefore 0.4, 0.5, and 0.6 respectively.

```text
Enabled = true
Intensity = 0.5
BloodTransfusionIntensityMultiplier = 0.8
LifeTransfusionIntensityMultiplier = 1.0
AbhartachCallingIntensityMultiplier = 1.2
InteriorIntensityMultiplier = 1.0
Range = 5.0
FadeSeconds = 0.12
LogBloodSpellInnerLight = true
```

Lower the shared intensity, a spell multiplier, or range for a subtler effect. Raise
InteriorIntensityMultiplier to strengthen the lights only in full interiors.
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
AvoidRecentCorpseLeechRepeats = true
RecentCorpseLeechSoundMemory = 2
CorpseLeechRandomPitchSemitones = 0.20
```

## Corpse Quality

Blood Magic Expansion reports focused corpse quality from 0 to 1. When both
signals are known, quality is a 50/50 blend of base kill XP and enemy max
health. If only one signal is available, that signal is used by itself.

```text
ReferenceKillXP         = 300
ReferenceMaxHealth      = 600
MinimumEffectMultiplier = 0.5
MaximumEffectMultiplier = 1.5
```

By default, corpse quality scales Blood/Life corpse healing and Abhartach
corpse effects from 0.5x to 1.5x. Character XP is not multiplied again.
Character XP already comes from vanilla effective kill XP. The 50/50 blend
keeps large, lower-XP bodies from outranking similarly valuable monsters from
HP alone while still letting enemy bulk matter.

When Dishonored Dynamic Crosshair is installed, the blood-magic corpse reticle
can scale visually from the same quality. Reticle size is visual-only and
defaults to 1.0x to 2.0x Magic reticle size with a low-quality dead zone.
Quality changes scale only; valid usable corpses stay the same blood-red
color. Corpses restored from save data are reconnected to their owning game
location after restoration and remain connected for the current play session.
They use the default-size base-color corpse icon and remain non-drainable.

```text
Band / example                       | Quality | Effect  | Reticle
Bloodless Wyrd variants              | --      | blocked | 1.00x default
Harmless animal, chicken/cow         | 0.04    | 0.54x   | 1.00x
Weak passive animal, deer            | 0.08    | 0.58x   | 1.00x
Common animal threat, wolf           | 0.11    | 0.61x   | 1.00x
Tutorial weak enemy                  | 0.16    | 0.66x   | 1.00x
Weak human, outcast/outlaw           | 0.23    | 0.73x   | 1.00x
Flamegobbler                         | 0.29    | 0.79x   | 1.02x
T1 monster, Redcap/CorpseEater       | 0.38    | 0.88x   | 1.07x
T2 human/deranged/highwayman         | 0.45    | 0.95x   | 1.12x
T2 monster, LostKnight/Bullrat       | 0.52    | 1.02x   | 1.19x
Plain bear / high-HP animal          | 0.55    | 1.05x   | 1.23x
Grindylow                            | 0.55    | 1.05x   | 1.23x
T3 common/Cuanacht                   | 0.63    | 1.13x   | 1.33x
T4 human/Dal Riata                   | 0.68    | 1.18x   | 1.40x
T3 heavy, Ogre/Syldren               | 0.75    | 1.25x   | 1.51x
Wight / strong T4                    | 0.75    | 1.25x   | 1.51x
T4 heavy, Nuckelavee/Barnaclator     | 0.85    | 1.35x   | 1.69x
T5 elite, Beholder/IceWeaver         | 0.90    | 1.40x   | 1.79x
T5 strong, Archivist/Tidewraith      | 0.92    | 1.42x   | 1.83x
T6 giant/ancient                     | 0.97    | 1.47x   | 1.93x
T6 boss-tier                         | 1.00    | 1.50x   | 2.00x
```

## Abhartach's Calling

Abhartach's Calling gets separate corpse effect tuning:

```text
Preset       | Damage | Radius | Bleed | Held heal | Search
Blood Rite   | 1.00x  | 1.05x  | 1.05x | 1.10x     | 1.00x
Desecration  | 1.05x  | 1.10x  | 1.12x | 1.20x     | 1.05x
Soul Feast   | 1.12x  | 1.20x  | 1.20x | 1.35x     | 1.10x
```

Spirituality can add up to +40% explosion damage, +35% explosion radius, +40%
bleed buildup, +40% held corpse healing, and +35% corpse search range at 50
Spirituality. Corpse search is capped at 1.5x by default.

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

Dishonored Dynamic Crosshair 2.8.1+ can use
`BloodMagicExpansion.BloodMagicApi` v4 for focused corpse reticle feedback.

First Person Arms Adjuster 0.3.5+ is an optional soft integration. When present,
each unparented world-space blood light follows FPA's presentation-only visual
offset so the light remains centered on the rendered hand after arm adjustment.

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
events. Spirituality is cached
briefly before spell tuning reads it. Noisy diagnostics default to off; startup
and patch-warning logs remain enabled.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
