Steel and Bone
Version 0.9.4-beta

Beta testing note: this build needs in-game testing before it should be treated
as a stable tuning release. Please watch for odd enemy classifications,
unexpected resistance or weakness outcomes, and damage-number readability
issues.

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
Ordinary flesh gets small bleed, poison, slash, and pierce weaknesses only when no more specific family wins first.
Flesh undead resist blood magic, bleed, and poison, while Fire and blunt get mild weaknesses. More specific drowned and infected rules win when detected.
Wyrd enemies resist Wyrdness damage as a Steel and Bone design rule.
Drowned enemies resist blood magic and bleed, with modest Electric and blunt weaknesses as overlays. Vanilla Drowner Fire resistance is preserved.
Infected flesh resists poison and can be Fire-weak when vanilla has not already handled Fire.
Sea creatures resist Cold and can be modestly Electric-weak.
Spirits resist blood, bleed, poison, and ordinary physical damage modestly.
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
Diagnostics = false

Combat feedback is built into Steel and Bone. When DamageNumbersEnabled is true,
the mod draws a World of Warcraft-inspired floating damage number for outgoing
player hits near the hit position using the final damage amount reported by the
game. Neutral hits use the #E3BD02 base color. Resistance numbers shrink and
desaturate toward grey as resistance gets stronger, weakness numbers grow and
warm toward red-orange as the bonus gets stronger, and critical or weakspot hits
get a larger pop. DamageNumberFontMode defaults to GameDefault, which follows
the game's Accessibility font choice. Set it to Sans, Serif, or ImguiDefault to
force one font for Steel and Bone damage numbers.

If another damage-number mod is installed, both mods can draw numbers. Disable
other damage-number overlays such as DamageNumbers.dll or Immersive HUD's damage
text option if duplicate combat text feels too busy.

When Diagnostics is enabled, the log records detected target families, elite-class target flags, metadata or term family evidence, damage tags, generic physical weapon-type hints, no-match reasons, vanilla multiplier amplification, vanilla multiplier skips, elite clamp adjustments, and applied Steel and Bone rules.

Install with Vortex as a BepInEx plugin, or place this folder under BepInEx/plugins.
