Steel and Bone
Version 1.0.6

Steel and Bone is a BepInEx 5 Mono knowledge-based difficulty plugin for Tainted Grail: The Fall of Avalon.

It expands the game's existing weakness and resistance feel with a small post-vanilla damage-rule layer. The mod does not rewrite enemies, armor, AI, or encounters. Vanilla damage subtype multipliers still run first, and Steel and Bone can skip its own subtype overlay when vanilla already has a non-neutral rule for that same subtype.

Steel and Bone can also amplify vanilla enemy weaknesses and resistances. Tempered keeps vanilla multipliers unchanged by default. Hardened and Crucible push vanilla-authored weaknesses and resistances farther from neutral while keeping true vanilla immunities intact and clamping extreme values.

When the game sends a generic physical hit, Steel and Bone can also infer a physical lane from the weapon identity exposed by Tainted Grail itself: swords and axes count as slashing, daggers, polearms, and ranged weapons count as piercing, and blunt weapons count as bludgeoning.

Target classification now prefers runtime/template metadata when it is reachable. Signals such as HitBones, HitStone, WyrdnessBound, SarrasCreature, ReefboundBody, Ghost, Scourge, Zombie, Bloody, Human, Humanoid, Bandit, and Cultist classify enemies before broad display-name terms are used as fallback. Elite, MiniBoss, Boss, and Type:Elite metadata can also flag elite-class targets for Steel and Bone's custom-rule clamp.

Current matchups:

Bone undead resist blood magic, bleed, slashing, and piercing. Blunt gets a small Steel and Bone weakness only when vanilla has not already handled the subtype.
Constructs resist blood magic, bleed, poison, slashing, and piercing. Blunt gets a modest weakness.
Bone undead and constructs mildly resist untyped physical hits so GenericPhysical never becomes the best answer by accident.
Armored humanoids resist slash and untyped physical damage, while blunt gets a modest weakness.
Ordinary flesh gets small blood magic, bleed, poison, slash, and pierce weaknesses only when no more specific family wins first.
Flesh undead resist blood magic, bleed, poison, and pierce, while Fire and blunt get mild weaknesses. Slash remains neutral. More specific drowned and infected rules win when detected.
Wyrd enemies resist Wyrdness damage as a Steel and Bone design rule.
Drowned enemies resist blood magic, bleed, and pierce, with modest Electric and blunt weaknesses as overlays. Slash remains neutral, and vanilla Drowner Fire resistance is preserved.
Infected flesh resists poison, can be Fire-weak when vanilla has not already handled Fire, and shares ordinary flesh's small slash and pierce weaknesses while remaining neutral to blunt.
Sea creatures resist Cold, can be modestly Electric-weak, and share ordinary flesh's small slash and pierce weaknesses.
Spirits resist blood, bleed, poison, and ordinary physical damage modestly, while Wyrdness is a strong answer.
Flora resists poison, bleed, and piercing, while Fire and slash are better answers.
Elite-class targets reduce custom Steel and Bone weakness bonuses and floor custom Steel and Bone resistances so correct matchups still matter without deleting major fights.

Rules are not gated behind Crucible. Presets only scale how strongly every Steel and Bone rule pulls away from neutral:

Tempered = lighter, closer to vanilla.
Hardened = default rule strength.
Crucible = stronger and more punishing, but no exclusive Crucible-only matchups.

Default settings:

Enabled = true
Preset = Hardened
RespectVanillaMultipliers = true
AmplifyVanillaMultipliers = true
TemperedVanillaAmplification = 0
HardenedVanillaAmplification = 0.35
CrucibleVanillaAmplification = 0.7
EliteRuleClampsEnabled = true
EliteWeaknessBonusReduction = 0.1
EliteMinimumResistanceMultiplier = 0.2
DamageNumbersEnabled = true
DamageNumberBaseColor = #E3BD02
DamageNumberFontSize = 34
DamageNumberFontMode = GameDefault
DamageNumberHorizontalDrift = 1
DamageNumberVerticalDrift = 1
DamageOverTimeNumberHeightMultiplier = 1.25
DamageNumberSizeContrast = 1
DamageNumberColorContrast = 1
Diagnostics = false

Combat feedback is built into Steel and Bone. When DamageNumbersEnabled is true,
the mod draws a World of Warcraft-inspired floating damage number for outgoing
player hits near the hit position using the final damage amount reported by the
game. Neutral hits use the #E3BD02 base color. Resistance numbers shrink and
desaturate toward grey as resistance gets stronger, weakness numbers grow and
warm toward red-orange as the bonus gets stronger, and critical or weakspot hits
get a larger pop. DamageNumberFontMode defaults to GameDefault, which follows
the game's active Accessibility FontAsset. Set it to Sans or Serif to use that
game FontAsset directly, or ImguiDefault to keep the legacy Arial look.

DamageNumberHorizontalDrift and DamageNumberVerticalDrift independently scale
the left/right and upward motion. Use 0 to disable an axis, 1 for the default
motion, or a value up to 3 for a more exaggerated launch. Setting both to 0
keeps the size pop and fade while the number remains near its hit position.
Criticals still move less sideways and rise higher than normal hits at matching
drift values.

DamageOverTimeNumberHeightMultiplier controls the initial world-space height
of Bleed, Poison, Burn, and Breath status-tick numbers. Its default of 1.25
starts those numbers 25% higher than ordinary hit numbers. Use 1 for the old
shared baseline, or adjust it from 0 to 3.

DamageNumberSizeContrast independently controls resistance shrinking and
weakness growth. DamageNumberColorContrast independently controls resistance
grey and weakness red-orange tinting. For either setting, use 0 for no
resistance/weakness contrast, 1 for the default look, or a value up to 3 for a
more dramatic difference. Critical and weak-spot size pop remain independent,
and IMMUNE retains its dedicated text and color.

If another damage-number mod is installed, both mods can draw numbers. Disable
other damage-number overlays such as DamageNumbers.dll or Immersive HUD's damage
text option if duplicate combat text feels too busy.

When Diagnostics is enabled, the log records detected target families, elite-class target flags, metadata or term family evidence, damage tags, generic physical weapon-type hints, no-match reasons, vanilla multiplier amplification, vanilla multiplier skips, elite clamp adjustments, and applied Steel and Bone rules.

Install with Vortex as a BepInEx plugin, or place this folder under BepInEx/plugins.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
