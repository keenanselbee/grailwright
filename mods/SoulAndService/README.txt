Soul and Service - Summon Overhaul 1.0.6
================================================

Soul and Service makes hero summons responsive, close-following servants while
preserving the game's own combat identity. It also renames and expands the game's
Soul Salvage spell as Soul Rend, giving it a focused necromantic role.

Default behavior
----------------

- Summon AI decisions begin at a deliberate 0.75-second interval at Power 0 and
  improve smoothly to the configured 0.25-second interval at Power 100. New
  summons recover 0.10 seconds after spawning.
- Idle summons trot at 4 m, run at 8 m, and use the native safe teleport at 35 m.
- A 1.25x catch-up speed applies only out of combat.
- Before Power 30, idle summons settle beside the hero instead of repeatedly
  turning toward random patrol points. At Power 30, Guard assigns the host loose,
  non-overlapping positions behind and beside the hero, beginning 4.5 m away and
  accepting a 1 m settling area around each position. When the hero stops, those
  positions remain fixed even if the hero turns in place. One servant at a time
  may occasionally walk near its position, linger, and return. At the Power 30
  formation unlock, idle movement reaches at most about 1.28 m; it narrows to
  0.75 m at Power 100 and remains the same through Power 200. Mastered servants
  also remain still longer between walks.
- Guard is the default behavior. It prioritizes recent attackers, enemies
  targeting the hero or another servant, then already-engaged melee threats near
  the hero. Defensive targets may be seen by either the hero or the acting servant.
  It never pulls an uninvolved hostile merely because another servant is fighting.
  Passive crosshair sharing is off and explicit Attack always wins.
- At 10 Necromantic Power (about 65 Soul Vigor), hold the remappable Sprint action and aim at a nearby hostile
  to show Attack; tap Interact to order every eligible owned summon onto that target.
- At 20 Power (about 133 Soul Vigor), keep Sprint held and aim at an owned summon within the configured
  Targeting Range, then tap Interact to make that servant Hold or Follow. The selector
  follows the visible servant even when its out-of-combat body colliders are disabled. Held servants
  defend within an 8 m leash around their fixed position; an explicit Attack order
  temporarily overrides the hold.
- At 30 Power (about 206 Soul Vigor), hold Take All Items for at least 0.45 seconds
  and release before two seconds to Hold All while an owned summon is active. If
  any servant is already held, release instead issues Follow All.
  This works without Battlecry Voice Tuner; both mods share the binding safely
  when it is installed.
- At 50 Power (about 369 Soul Vigor), hold Sprint and Interact for 0.45 seconds
  with at least one servant active and nothing meaningful under the crosshair to
  cycle Guard, Bulwark, and Hunt. No charging prompt appears. Completion briefly
  shows Behavior: Guard, Behavior: Bulwark, or Behavior: Hunt. Keep Sprint held,
  release only Interact, and hold Interact again to cycle immediately.
- At 70 Power (about 567 Soul Vigor), keep holding Take All Items for two seconds
  to Recall the active servant or Recall Host when several are active. The shorter
  formation command is cancelled; every servant drops its hold and explicit target,
  returns to Follow, and takes a distinct navigable place in a loose randomized arc
  around the hero. One servant chooses a randomized position to either side rather
  than directly behind. The host keeps those places until the hero moves 2 m or a
  new command or behavior change releases them. Autonomous targets remain suppressed
  for three seconds so Guard or Hunt cannot immediately cancel the disengagement.
- Bulwark assigns servants stable positions in a disciplined forward shield. Its
  first four positions begin 3.5 m from the hero with a 0.5 m settling area, and
  small stationary facing changes do not continually rotate the line. Servants
  counterattack only recent attackers or enemies threatening the host within 6 m,
  never chase an autonomous threat beyond 8 m, and skip native target acquisition
  when no such threat exists. Explicit Attack temporarily releases them.
- Hunt assigns distinct, stable perimeter positions beginning 5.5 m from the hero
  and attacks valid faction-hostile NPCs it can see from its own position. Idle
  hunters make staggered local scouting walks without moving inside 5 m; hosts of
  four or more allow at most two scouts at once. Movement, combat, and commands
  cancel those walks. A servant commits briefly to a valid target and changes to
  an equal threat only when the replacement is meaningfully closer; higher-priority
  threats can still take precedence immediately. Behavior changes perform one
  immediate acquisition pass. Servants release the game's hero-centered combat
  slots while pursuing NPC targets.
- When Battlecry Voice Tuner is installed, every successful Attack, Hold,
  Follow, Guard, Bulwark, or Hunt order plays one gender-matched necromantic command from the matching
  order-specific pool. Each pool avoids its own recent clips; passive and failed
  paths remain silent.
- Disabling Formation Commands releases every held servant immediately.
- "Summon Pass-Through" prevents owned summons from pushing or trapping the hero.
- In combat, confirmed hero projectiles and magic-gauntlet contacts pass through
  owned summons. Outside combat they return to vanilla interception, while bespoke
  scripted ray spells retain their native behavior.
- Rest dismisses ordinary and reanimated servants by default. Persistent Servants
  can keep the active host through rest. Command Capacity adds +1/+2/+3 to the
  native summon limit at Power 50/100/150; the configured flat bonus is additional.
- Replacement summons recover missing Invocation of Might scaling only when the
  outgoing summon proves that the native effect is active.
- Summon idle loops play at 60% volume; combat, hurt, and death sounds are untouched.
- Soul Vigor is an uncapped, save-backed necromancy statistic. Necromantic
  Power follows Blood Magic Expansion's 0-200 mastery curve, reaching 100 at
  1,000 Soul Vigor and 200 at 5,000.
- Command control grows with Necromantic Power: Attack unlocks at Power 10
  (about 65 Soul Vigor), individual Hold and Follow at 20 (about 133), Hold All
  and Follow All at 30 (about 206), behavior control at 50 (about 369), Recall Host
  at 70 (about 567), and Swarm at 90 (about 826). Grail Floating Text can announce
  each milestone in Necrotic green.
- At Power 90, the Attack prompt and completion text become Swarm. The upgraded
  order lasts five seconds, retains its Attack voice without per-use GFT, and lets
  servants close up to
  1.25x faster and each deals 1.25x damage on its first successful hit against
  the commanded target. Misses do not consume the bonus and repeated orders refresh
  rather than stack it.
- At Power 0/100/200, owned summons deal 0.75x/1.25x/1.50x damage and take
  1.25x/0.75x/0.50x damage. Normal summons receive the same benefits.
- Guard and Hunt evaluate eligible enemies within 30 m by default, while Bulwark
  defends a 6 m zone and retains a short 8 m combat leash. When Steel and Bone is
  loaded, they inherit 80% of its active sight and aggro-persistence increase,
  with a short lost-target grace and the native 45 m command tether retained.
- Successful Recall Host and automatic too-far catch-up teleports play the same
  green-dark necromancer summoning effect beneath each servant after arrival.
- Soul Rend light cast turns an eligible hostile corpse into native simplified
  remains and grants 2/6/10/20 Soul Vigor for Meager/Worthy/Potent/Prime quality.
  On a summon, it restores 50% of invested mana at full health, with current
  health scaling the return. Raised corpses grant their quality reward; ordinary
  summons grant 1 Soul Vigor, capped at five awards per rolling 60 seconds.
- With Grail Floating Text, each completed ritual appears as a short Necrotic
  reward. Corpse harvests show `+X Soul Vigor`; servant unbinding shows
  `+X Mana | +Y Soul Vigor`, or Mana alone when ordinary-summon Vigor is capped.
  GFT has no separate Mana-restoration event, so the combined line duplicates none.
- Successful corpse harvests and summon sacrifices play one of forty authored
  FMOD ritual sounds. Meager, Worthy, Potent, and Prime targets use the low,
  medium, high, and max banks. The default 0.85 volume preserves their authored
  tier loudness, avoids the previous two clips per tier, and varies pitch by up
  to 0.20 semitones. Failed actions and living-target casts do not play this bank.
  Raised-servant light harvests complete reliably even when the native spell kills
  the underlying location directly, and play exactly one sound only after
  loot-bearing remains are created.
- Against an eligible living hostile, Soul Rend's light cast deals a normal
  hero-attributed generic magical hit worth 50%/100%/200% of a comparable tier-one
  light offensive spell at Necromantic Power 0/100/200. Compatible mods can identify
  that exact packet as Necrotic without treating Soul Claim as ordinary damage. It
  grants no Soul Vigor. Surviving repeated hits build an internal claim-strength
  bonus for eight seconds, refreshing up to three stacks.
- Soul Rend's heavy cast costs 30 base mana. Corpse quality controls binding
  resistance and maximum health; repeated casts make deterministic, save-backed
  progress until the soul yields. Heavy casts never grant Soul Vigor.
- Against an eligible living hostile at or below 40% Health, heavy cast attempts
  Soul Claim. Chance rises as Health falls and Necromantic Power rises; each
  consumed internal claim-strength stack adds 10% relative chance. Meager/Worthy/Potent/Prime
  quality applies 1.00x/0.85x/0.65x/0.45x chance, and the final chance is capped
  at 35%. Failure leaves the enemy alive. Success deals one native hero-attributed
  killing hit, then raises the corpse through the same protected lifecycle.
- Successful corpse harvests advance authoritative save-backed Meager, Worthy,
  Potent, and Prime counts. Deeds of Avalon can show one total or the default four
  Necrotic quality-icon rows. Old Souls Bound and binding-count data is not imported.
- Raised servants begin at a randomized 40-60% health at Power 0; the range
  rises and narrows until Power 200 guarantees 100%.
- Every active ordinary or reanimated servant shares lethal necromantic upkeep.
  One servant loses 2% maximum health per minute at 0 Necromantic Power; each
  additional servant adds 1% to every servant, capped at 8% for seven or more.
  Power reduces the drain linearly until it reaches zero at 100 (1,000 Soul Vigor). Upkeep continues in
  combat and can deliver the killing blow without a floating-text warning.
- Heavy Soul Rend restores 20%/35%/50% maximum Health at Power 0/100/200.
  Below 95% Health, restoration is its only service for that cast. At 95% or
  higher it generously restores the remaining Health and, at 100 Necromantic
  Power (1,000 Soul Vigor), can also Empower the servant once for its lifetime.
  One lower-biased 1.2x-1.5x roll controls visible size, outgoing
  damage, and incoming-damage resistance. The existing reanimation VFX plays,
  the completion text reports the exact roll, movement and locomotion compensate
  for the larger stride, and Empower cannot stack or reroll. Heavy casting never
  sacrifices the targeted servant when Empower is locked or already applied.
- Raised copies use the game's native hero-summon faction and targeting behavior,
  including autonomous enemy aggression. Authored scene NPCs, bosses, minibosses,
  friendly corpses, and unresolved templates are rejected. The original corpse is
  hidden during service; death or light-cast harvest leaves its loot-bearing native
  simplified remains at the servant's last safe position, with the source position
  as fallback. Unload, shutdown, and failed initialization restore the source instead.
  Raised servants retain their native NPC portrait
  when valid; otherwise the vanilla skeleton-summon portrait is used. Successful raises
  also use the Forgotten Cemetery necromancer's native skeleton-summon effect.
- Raised servants are true native hero summons, so normal summon faction, limits,
  targeting, collision, persistence, and Soul and Service improvements apply. They
  use the binding cost, corpse quality, and current health for
  light-salvage returns, capped at 75% of binding cost to prevent a recursive loop.
- Dishonored Dynamic Crosshair can show Necrotic-green Meager/Worthy/Potent/Prime
  reticles over eligible corpses and active owned summons while Soul Rend is
  equipped, plus distinct Attack, Hold, and Follow icons and command pulses.

Custom audio
------------

Soul Rend audio is stored under the installed mod's audio folder. Each quality
tier supports files numbered 0 through 9:

  soul_salvage_low_0.wav through soul_salvage_low_9.wav
  soul_salvage_medium_0.wav through soul_salvage_medium_9.wav
  soul_salvage_high_0.wav through soul_salvage_high_9.wav
  soul_salvage_max_0.wav through soul_salvage_max_9.wav

Missing files are skipped. If an entire tier is absent, the nearest available
tier is used. Replace files with valid WAV audio while retaining their names.

Configuration
-------------

Config file: BepInEx/config/ks.tgfoa.soul-and-service.cfg

All timings, distances, target range, full-mastery AI interval, command modifier, Attack prompt, formation commands, pass-through behavior,
summon-limit bonus, idle volume, Soul Rend mana-return percentage,
living-target Soul Rend, persistent servants, ritual audio volume, repeat
avoidance, and pitch variation
can be configured. Settings are also visible in FoA Mod Manager. The final Import
Previous Settings tab safely imports compatible customized values after a future
config reset.

SoulSalvageAudioRangeVolume defaults to 1.0. It keeps ritual audio two-dimensional
while fading from 100% volume at the harvested corpse or summon to 10% at 30 meters
or farther. Set it to 0 to disable distance fading. Missing position data safely
uses full volume.

For temporary balance testing, Diagnostics includes Override Soul Vigor and Soul
Vigor Override Value. While enabled, the test value drives Necromantic Power,
summon and Soul Rend scaling, the public API, and Deeds display without changing
the character's saved Soul Vigor. Disable it to return immediately to saved
progression. Useful checkpoints are 0, 250, 1,000, 2,000, 3,000, 4,000, and 5,000.

Compatibility
-------------

This mod intentionally replaces Avalon Summons, Better Summon, and the temporary
Summon Pass-Through test plugin. Remove or disable those plugins before loading.

Steel and Bone is compatible. Soul and Service does not add a late flat damage
multiplier, so summon attacks continue through the game's normal damage types and
Steel and Bone's material rules. Soul Rend remains a hero-attributed Generic Magical
hit in the native pipeline, while Steel and Bone recognizes its exact packet as
Necrotic: living flesh, spirits, flora, and fungus are vulnerable, while corrupted,
undead, skeletal, and constructed bodies increasingly resist it. Soul Claim remains
a protected execution outside ordinary Necrotic resistance rules.

Battlecry Voice Tuner is optional. Successful explicit Attack, Hold, Follow,
Recall Host, Guard, Bulwark, and Hunt orders request one spoken command through
its public API; Recall Host uses the Follow pool. While owned summons are active
at 30 or more Necromantic Power, Soul and Service owns the Take All Items hold
action for formation and Recall Host commands. Battlecry Voice Tuner's separate
battlecry hotkey remains available. It stays authoritative
over gender selection, pitch, volume, cooldown, repeat avoidance, and smart reverb;
Soul and Service continues normally when it is absent or declines playback.

Dishonored Dynamic Crosshair is optional. Attack, individual Hold, and individual
Follow confirmations last 0.675 seconds. Hold All, Follow All, Recall Host, and
Behavior confirmations last 1.35 seconds in both native interaction text and their
matching Dishonored icon pulse. Behavior changes also show Behavior: Guard, Behavior:
Bulwark, or Behavior: Hunt through Grail Floating Text when it is installed.

Raised servants are runtime copies, grant no XP or scripted death reward, and are
not saved. Heavy cast accepts only runtime-spawned ordinary hostiles, excluding the
authored scene locations used by persistent NPCs. The feature should still be
treated as an initial-release feature and tested on expendable generic enemies
before long play sessions.

Troubleshooting
---------------

Enable the Diagnostics setting in the Diagnostics section and inspect the newest
BepInEx LogOutput.log. With Grail Floating Text installed, leave Show Grail
Floating Text Diagnostics enabled to see concise Soul Rend heavy-cast results
and source-corpse restoration in-game. Disable it to keep detailed logging only.
Do not install this DLL alongside Avalon Summons, Better Summon, or the temporary
pass-through test DLL.
