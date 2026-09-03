Soul and Service - Summon Overhaul 3.3.0
================================================

Soul and Service makes hero summons responsive, close-following servants while
preserving the game's own combat identity. It also renames and expands the game's
Soul Salvage spell as Soul Rend, giving it a focused necromantic role.

Default behavior
----------------

- Summon AI decisions begin at a deliberate 0.75-second interval at Power 0 and
  improve smoothly to the configured 0.25-second interval at Power 100. A tiny
  deterministic timing offset keeps larger hosts from doing all sensing work on
  the same frame. New summons remain movement-locked for at least 0.10 seconds and until their native
  animation is ready. A bounded recovery also corrects servants that later move
  with an idle or stalled locomotion state.
- Idle summons trot at 4 m, run at 8 m, and use the native safe teleport at 60 m.
- A 1.25x catch-up speed applies only out of combat.
- Before Power 30, idle summons settle beside the hero instead of repeatedly
  turning toward random patrol points. At Power 30, Guard assigns the host loose,
  non-overlapping positions behind and beside the hero, beginning 4.5 m away and
  accepting a 1.25 m settling area around each position. Brief taps and small
  adjustments do not move the formation: following begins after 0.45 seconds of
  sustained movement or 1.5 m of travel. Crossing that boundary shifts an elastic
  formation center only enough to retain a 0.75 m buffer instead of snapping every
  slot onto the hero. Guard and Close Guard preserve their facing until the hero moves
  3 m or holds a stable heading for 0.75 seconds, then turn at no more than 90 degrees
  per second. The host settles 0.35 seconds after the hero stops, and a servant already
  within its settling area can stay put. Positions remain fixed if the hero turns in
  place. One servant at a time
  may occasionally walk near its position, linger, and return. At the Power 30
  formation unlock, idle movement reaches at most about 1.28 m; it narrows to
  0.75 m at Power 100 and remains the same through Power 200. Mastered servants
  also remain still longer between walks. If a Guard or ordinary Hunt slot is
  locally blocked, the shared formation coordinator briefly assigns a nearby reachable,
  size-aware unoccupied slot instead of spinning against the exact point, then retries
  its intended position. Formation rings close empty ranks when servants leave, and held
  or combat-committed servants do not stretch the remaining formation. The same stable
  reservations coordinate Bulwark, Recall, and terrain-directed Hunt movement without
  repeating formation scans per servant. Large and Empowered servants may settle once
  their physical footprint reaches a slot, and sideways orbiting no longer counts as
  progress toward an obstructed center point.
- Guard is the default behavior. It prioritizes recent attackers, enemies
  targeting the hero or another servant, then already-engaged melee threats near
  the hero. It then proactively engages visible faction-hostile enemies inside a
  configurable hero-centered 15 m zone and retains them to 20 m before returning
  to formation. Set Guard Engagement Range to 0 for the old purely reactive behavior.
  Eligible targets may be seen by either the hero or the acting servant.
  Passive crosshair sharing is off; when enabled, its adopted target remains an
  autonomous suggestion governed by the current behavior. Explicit Attack always
  wins. Once behavior control unlocks at Power 50, Guard grants +5% damage dealt
  and reduces damage taken by 5%.
- At 10 Necromantic Power (65 Soul Vigor), hold the remappable Sprint action
  and aim at a nearby hostile to show Attack; tap Interact to order every eligible
  owned summon onto that target. Targeting tolerates small gaps around the visible
  body and brief aim drift. Once given, the order remains authoritative until its
  target becomes invalid or another direct order replaces it; Recall always cancels it.
  New targets acquire within 44 m of the hero. An existing order remains active through
  44.75 m and tolerates a boundary crossing for 0.85 seconds before releasing, so a
  moving enemy cannot make the command flicker at the game's native 45 m limit.
  A stalled order is retained when the target is reachable or directly visible, but
  releases after three spaced checks prove the target unreachable and unseen.
- At 20 Power (133 Soul Vigor), keep Sprint held and aim at an owned summon within the configured
  Targeting Range, then tap Interact to make that servant Hold or Follow. Enable Hold
  Individual Formation Commands to require a deliberate 0.45-second Interact hold
  instead; releasing early cancels without issuing the command. The selector
  follows the visible servant even when its out-of-combat body colliders are disabled. Held servants
  defend within an 8 m leash around their fixed position; an explicit Attack order
  temporarily overrides the hold.
- At 30 Power (206 Soul Vigor), hold Sprint + Take All Items for at least 0.45 seconds
  and release to Hold All while an owned summon is active. If any servant is already
  held, release instead issues Follow All. Once Recall unlocks at Power 70, release
  before 1.5 seconds for the formation command or keep holding to Recall.
  This works without Battlecry Voice Tuner; both mods share the binding safely
  when it is installed.
- At 50 Power (369 Soul Vigor), hold Sprint and Interact for 0.45 seconds
  with at least one servant active and nothing meaningful under the crosshair to
  cycle Guard and Hunt. At 60 Power (463 Soul Vigor), Bulwark joins the
  cycle. No charging prompt appears. Completion briefly shows the selected
  behavior. Keep Sprint held, release only Interact, and hold Interact again to
  cycle immediately. If Power falls below 60 while Bulwark is selected, servants
  safely use Guard until Bulwark unlocks again.
- At 70 Power (567 Soul Vigor), keep holding Sprint + Take All Items for 1.5 seconds
  to Recall the active servant or Recall Host when several are active. The shorter
  formation command is cancelled; every servant drops its hold and explicit target,
  returns to Follow, and takes a distinct navigable place in a loose randomized arc
  around the hero. One servant chooses a randomized position to either side rather
  than directly behind. The host keeps those places until the hero moves 2 m or a
  new command or behavior change releases them. Recall fully exits combat and restores
  each arrival to idle patrol locomotion so humanoid servants do not keep sliding in a
  stale combat animation. Autonomous targets remain suppressed for three seconds so
  Guard or Hunt cannot immediately cancel the disengagement.
- At 200 Power (5,000 Soul Vigor), with no living servants, hold Sprint + Take All
  Items for 1.5 seconds to Raise All. The command checks viable hostile corpses
  within 30 m, raises them nearest-first until Summon Capacity or available Soul
  Vigor runs out, pays each corpse's current Power-scaled cost, and plays the
  summoning effect once at every corpse plus once around the hero.
- Bulwark has two live stances. Holding the remappable Sprint action forms a predictive
  forward Advance wall 4.5 m ahead by default. Releasing Sprint normally
  moves the host immediately into a Close Guard across the sides and rear, leaving
  the forward firing lane open; an optional 0-10 second grace instead retains the
  final Advance facing while the wall continues to follow the moving hero. Advance
  uses a configurable direct movement multiplier from 1x to 3x, defaulting to 2x.
  Advance requests running movement outside the near-slot area, refreshes its moving
  anchors more decisively, and composes its multiplier, displaced-slot catch-up,
  Empower, and Swarm within a 3x total movement ceiling.
  Advance follows deliberate camera intent while moving or stationary, so strafing,
  backpedaling, and small aim corrections do not spin it around. Close Guard instead
  follows Guard's last meaningful movement direction and ignores camera turns.
  Both stances engage nearby hostiles within 4 m by default, retain a chosen target
  within 6 m, and return servants when they move beyond the default 8 m player leash.
  Bulwark commits longer to a valid local enemy and does not spread equal-priority
  servants away from the fight. Explicit Attack and Swarm
  always release the formation. Disable Enable Bulwark Advance to keep the stance in
  Close Guard. Once Bulwark unlocks at Power 60, it reduces damage taken by 15%.
- Hunt assigns distinct, stable perimeter positions beginning 5.5 m from the hero
  and aggressively seeks valid faction-hostile NPCs within 30 m by default when either
  the hero or the acting servant has line of sight, including enemy summons when faction
  rules mark them hostile. Steel and Bone contributes 80% of its active sight increase
  without a cap. Recent attackers and enemies threatening the host take priority,
  and formation movement yields immediately while a hunter pursues its target. Idle
  hunters make staggered local scouting walks without moving inside 5 m; hosts of
  four or more allow at most two scouts at once. Movement, combat, and commands
  cancel those walks. A servant commits briefly to a valid target and changes to
  an equal threat only when the replacement is meaningfully closer; higher-priority
  threats can still take precedence immediately. Equal-priority autonomous choices
  lightly favor enemies with fewer servants already assigned, reducing unnecessary
  crowding without overriding explicit focus-fire orders. All selected enemies remain
  within a 44 m acquisition and 44.75 m retention boundary inside the game's native
  45 m summon leash. Committed targets are checked first, and remaining candidates are
  ranked before the shared sight-ray budget is spent. While Hunt is selected, hold
  the remappable Sprint action and look at reachable terrain at least 5 m away. Tap
  Interact to issue Hunt even though its preview is hidden by default; enable Show
  Directed Hunt Preview to display it. The optional prompt remains available whenever
  the hero owns any servant, even if every servant is busy. Every valid tap confirms
  with the Attack pulse and voice. Idle, uncommitted hunters, including targetless
  servants recovered from stale native combat, attack-move to distinct size-aware
  positions around the point; autonomously fighting servants may retarget under the
  same rules. During the command, each traveling or searching hunter can attack any
  faction-hostile enemy in its own line of sight and normal Hunt awareness, provided
  it can reach the enemy through the connected navigation area. Selected enemies register the
  servant as an immediate combat threat instead of waiting to notice the hero. Hunters
  sweep distinct nearby nav-valid points for four seconds after the first hunter arrives, and return
  to ordinary Hunt if no target is found. Explicit Attack targets, held servants, and
  recalling servants remain protected. If nobody can move or retarget, confirmation still
  plays while current actions continue. A participating hunter leaves the movement group
  as soon as it acquires a target, immediately abandoning the terrain destination.
  Holding Interact for 0.45 seconds still cycles behavior, and a focused Attack,
  Hold, or Follow prompt remains authoritative. Disable Enable Directed Hunt to remove
  the feature. Behavior changes perform one
  immediate acquisition pass. Servants release the game's hero-centered combat
  slots while pursuing NPC targets. At Power 50, Hunt grants +10% damage dealt and
  +10% movement speed while pursuing a target or fighting. Hunt, Swarm, and Empower
  movement remain capped at a combined 1.50x.
- When Battlecry Voice Tuner is installed, every successful Attack, Hold,
  Follow, Recall, Raise All, Guard, Bulwark, or Hunt order plays one gender-matched necromantic command from the matching
  order-specific pool. Each pool avoids its own recent clips; passive and failed
  paths remain silent.
- Disabling Formation Commands releases every held servant immediately.
- "Summon Pass-Through" prevents owned summons from pushing or trapping the hero.
- In combat, confirmed hero projectiles and magic-gauntlet contacts pass through
  owned summons. Outside combat they return to vanilla interception, while bespoke
  scripted ray spells retain their native behavior.
- Persistent Servants is enabled by default and keeps ordinary summons plus each
  raised servant's source identity and progression through saving, loading, and
  restarting. Ordinary summons stay
  on the game's native save path. Raised copies are reconstructed from one
  versioned save snapshot only after the main scene is ready, while their original
  corpses remain the sole native source of loot and scene state. Rest Host Behavior
  defaults to Sustain: rest keeps the host but inflicts severe, lethal Health
  attrition based on host size, actual hours rested, and Necromantic Power. An
  eight-hour rest at Power 0 removes 45% Health from one servant, plus 18% per
  additional servant, capped at 90%; Power 100 removes the penalty. Dismiss
  instead ends every servant's service safely on rest. Summon Capacity adds
  +1/+2/+3 to the native limit at Power 50/100/150; the configured flat bonus
  remains additional.
- The complete Spirituality Summoning tree remains native for ordinary summons,
  fresh raised servants, persistently restored servants, and replacement servants.
  Astral Bonds supplies the base limit before Summon Capacity is added. Summoner's
  Battery and Power Conduit count every living servant. Invocations of Might and
  Mana-Infused Blood arrive through the native summon-spawn event. Feeding Frenzy
  affects the whole living host after a hero kill. Worthy Sacrifice and Hold The
  Line respond to a real servant death. Prepared Rituals still reduces ordinary
  summon-spell mana reservation; it does not discount Soul Vigor reanimation.
  Replacement-only Invocations of Might repair runs only when the hero has learned
  the native talent, composes with raised quality Health, preserves
  current Health percentage, and leaves already-scaled stats untouched.
- Combat death, lethal upkeep or rest attrition, Soul Rend harvest, Blood ritual
  sacrifice, and native active-summon destruction keep the game's death semantics.
  Failed reconstruction, invalid-record recovery, source restoration, plugin
  shutdown, and other administrative cleanup discard the generated copy without
  synthesizing Worthy Sacrifice or Hold The Line.
- Summon idle loops play at 60% volume; combat, hurt, and death sounds are untouched.
- Soul Vigor is an uncapped, save-backed necromancy statistic. Necromantic
  Power follows Blood Magic Expansion's 0-200 mastery curve, reaching 100 at
  1,000 Soul Vigor and 200 at 5,000. Soul Vigor is an integer resource: spending
  it lowers Necromantic Power immediately and can relock commands or Empower
  until enough Vigor is harvested again. Existing servants are never destroyed
  merely because a milestone relocks.
- From Power 40, servants durably earn Soulforged ranks by dealing real
  post-mitigation damage to faction-hostile enemies. Seventeen ranks unlock every
  10 Power through rank XVII at Power 200. Progress is measured against the
  servant's original maximum Health and banks behind locked ranks: rank I needs
  two original Health bars of damage, while rank XVII represents 54 total. Each
  rank adds 1% damage and resistance plus 0.5% visible size. Earned ranks remain
  durable unless deliberately stripped by light Soul Rend. Aim at an otherwise
  uncommanded servant to see `Name [V]` and `HP: 59% | Rank: 89%`; the last value
  is progress toward the next unlocked rank. The diagnostic Soulforged override
  previews any exact rank without changing saved progress.
- Command control grows with Necromantic Power: Attack unlocks at Power 10
  (65 Soul Vigor), individual Hold and Follow at 20 (133), Hold All and Follow All
  at 30 (206), Guard/Hunt behavior control at 50 (369), Bulwark at 60 (463),
  Recall Host at 70 (567), Swarm at 90 (826), and Raise All at 200 (5,000).
  Grail Floating Text can announce each milestone in Necrotic green.
- At Power 90, the Attack prompt and completion text become Swarm. The upgraded
  order lasts five seconds, retains its Attack voice without per-use GFT, and lets
  servants close up to
  1.25x faster and each deals 1.25x damage on its first successful hit against
  the commanded target. Misses do not consume the bonus and repeated orders refresh
  rather than stack it.
- At Power 0/100/200, owned summons deal 0.75x/1.25x/1.50x damage and take
  1.25x/0.75x/0.50x damage before behavior bonuses. Guard adds +5% damage and
  5% mitigation, Bulwark adds 15% mitigation, and Hunt adds +10% damage plus
  +10% pursuit/combat movement. Normal summons receive the same benefits.
- Guard evaluates candidates within 30 m but proactively engages them only inside
  its configurable 15 m hero-centered zone, retaining chosen targets to 20 m. Hunt
  begins at 30 m and inherits 80% of Steel and Bone's active sight increase before
  target eligibility is bounded by the game's native 45 m summon leash; terrain attack-move uses the same
  servant-centered Hunt awareness plus a connected-navigation check. Bulwark uses its stance-specific breach,
  defense, and pursuit zones described above. When Steel and Bone is
  loaded, Guard and Hunt inherit 80% of its active sight increase and autonomous targets
  gain 80% of its aggro-persistence increase.
- Successful Recall Host and automatic too-far catch-up teleports play the same
  green-dark necromancer summoning effect beneath each servant after arrival.
  Native scene-transition relocation remains silent.
- Soul Rend light cast can harvest almost every genuine non-summon corpse created
  during the current session, regardless of faction or reusable summon data.
  Meager/Worthy/Potent/Prime corpses have native randomized integer
  values of 2-4/7-11/12-18/24-36 Soul Vigor. Rolls favor the center, corpse
  quality nudges the result within its tier, mastery adds only a subtle positive
  bonus, and the current balance value scales the final harvest award. Ordinary
  structurally safe bodies become loot-preserving simplified remains. Unique,
  scripted, merchant, guard, companion, and otherwise protected bodies remain
  intact but become soul-spent, so they cannot pay twice or be reanimated.
  Corpses restored from an earlier session retain the conservative structural
  eligibility rules, and summon corpses remain excluded to prevent Vigor farming.
- Genuine saved scene-authored miniboss and boss corpses instead hold six equal,
  deterministic Soul Vigor portions. Miniboss pools total 30/30/36/42/48/54/60
  from native tier 1-7; boss pools total 36/42/48/54/60/66/72. Runtime-spawned,
  summon, respawn-controller, temporary-death, kill-protected, and reward-free
  copies cannot create a pool. With Soul Rend equipped, ordinary Search text is
  augmented with only the remaining number, such as `54 Soul Vigor`. Holding a
  heavy cast replaces that line with the exact reanimation price or a stable
  unbindable-spirit message. Each light cast removes and awards one portion.
  The final drain converts a structurally safe repetitive body into the same
  one-copy loot-preserving simplified remains used by ordinary harvest; protected
  unique, story, presence, dialogue, and merchant bodies remain intact at
  `0 Soul Vigor` and cannot be bound.
- Every summon costs Soul Vigor. Before balance-preset scaling, Power-100
  tier-one through tier-six summon spells cost 3/6/9/12/15/18; Power 0 doubles
  those costs and Power 200 halves them, rounded up, with smooth scaling between.
  Each spell shows its exact effective cost.
  Corpse reanimation uses the same Power curve against that corpse's stable,
  quality-scaled soul value. The heavy-cast interaction previews its exact cost.
  A heavy cast that strikes ground or irrelevant surface clutter selects the nearest
  eligible corpse within 0.4 m when no direct corpse, servant, or living target is under
  the crosshair. Surface-side checks prevent selection through walls or floors.
  Spending can relock abilities; a Swarm order already in progress may finish.
- Light Soul Rend dismantles an owned servant safely in layers. The first cast on
  an Empowered servant removes Empowerment and returns 75% of its exact recorded
  Soul Vigor cost. Later casts remove two real Soulforged ranks at a time, and only
  an unempowered rank-0 servant is unbound by the next cast. Every unique stripped
  rank releases 3% of the servant's original refundable Mana and base Soul Vigor,
  paid at current Health with a 50% minimum efficiency. Re-earned ranks cannot pay
  twice; final unbinding returns the remaining pool under the normal Health rules.
- With Grail Floating Text, each completed ritual appears as a short Necrotic
  result. Empower and rank removal use reliable high-priority per-servant Status
  lines, show `Base` below rank I, and omit zero-value resource segments. Corpse
  harvests show `+X Soul Vigor`; servant unbinding shows
  `+X Mana | +Y Soul Vigor`, or Mana alone when ordinary-summon Vigor is capped.
  GFT has no separate Mana-restoration event, so the combined line duplicates none.
  Successful summons and reanimations report the servant name and exact Vigor
  spent; unaffordable attempts show `Requires X Soul Vigor`.
- Successful corpse harvests and summon sacrifices each start an independent one
  of forty authored FMOD ritual sounds, so rapidly unbinding several servants
  keeps one overlapping sound per completed cast. Meager, Worthy, Potent, and
  Prime targets use the low, medium, high, and max banks. The default 0.85 volume
  preserves their authored tier loudness, avoids the previous two clips per tier,
  varies pitch by up to 0.20 semitones, and adds two restrained ethereal echoes.
  Runtime female and male targets default to +3 and -3 semitones. Recognized
  female and male monsters add -1 and -3 more, producing final defaults of +2
  and -6. Gender-unknown monsters use the configurable -6-semitone fallback;
  other unknown targets retain normal pitch. Failed actions and living-target
  casts do not play this bank. Raised-servant light
  harvests complete reliably even when the native spell kills the underlying
  location directly.
- Against an eligible living hostile, Soul Rend's light cast deals a normal
  hero-attributed generic magical hit worth 50%/100%/200% of a comparable tier-one
  light offensive spell at Necromantic Power 0/100/200. Compatible mods can identify
  that exact packet as Necrotic without treating Soul Claim as ordinary damage. It
  grants no Soul Vigor. Each hit the enemy survives raises that enemy's Soul
  Claim threshold by 2% for as long as it remains present, up to five hits and
  a total +10%.
- Soul Rend's heavy cast costs 30 base mana. Corpse quality controls binding
  resistance and maximum health; repeated casts make deterministic, save-backed
  progress until the soul yields. Heavy casts never grant Soul Vigor.
- Against an eligible living hostile, heavy cast deterministically uses Soul Claim
  when current Health is at or below the final threshold. Base thresholds are
  5%/10%/15%/20%/25%/30% at Power 25/50/75/100/150/200, with smooth values
  between them. Meager/Worthy/Potent/Prime souls resist by 0%/3%/6%/9%, while
  every surviving light hit adds 2% up to +10%. The final threshold is clamped
  to 1-40%. Success deals one native hero-attributed killing hit, then raises the
  corpse through the same protected lifecycle and normal starting-Health roll.
- Successful corpse harvests advance authoritative save-backed Meager, Worthy,
  Potent, and Prime counts. Deeds of Avalon can show one total or the default four
  Necrotic quality-icon rows. Old Souls Bound and binding-count data is not imported.
- Before balance-preset scaling, raised servants begin at a randomized 40-60%
  health at Power 0; the range rises and narrows until Power 200 guarantees 100%.
- Every active ordinary or reanimated servant shares lethal necromantic upkeep,
  scaled by the current balance-preset value.
  At full Health, a three-servant host lasts about 3 minutes at Power 0,
  20 minutes at Power 50, and 50 minutes at Power 90 before upkeep alone kills it.
  One servant receives half that drain and seven or more receive twice that
  drain, with intermediate host sizes scaling smoothly. Upkeep continues in
  combat, can deliver the killing blow without a floating-text warning, and ends
  only at Power 100 (1,000 Soul Vigor).
  The rest preview shows only nonzero Health loss that applies to the current
  host, preserves the native Wyrdnight warning, and composes cleanly with
  Glorious UI's Sensible Rest Menu.
- Selected scene-authored minibosses can be reanimated without a Power gate when
  their exact non-scalable NPC template is allowlisted and the individual corpse
  is repetitive, nonunique, unprotected, full, saved, and backed by one durable
  LocationTemplate. The initial allowlist covers Foredweller Weaver and Weaver
  Mage, Blood Abomination, Rootambusher, Forlorn Ogre, and Sleepwalker templates;
  an encounter using one of those templates is still rejected if any typed safety
  condition fails. Bosses, scalable Drowned Knights, Tibby, and the Lancelot-crypt
  Eldritch Reaver remain unbindable.
- A miniboss starts with half native maximum Health, but that reduced bar begins
  full, and deals 60% damage before normal Power, behavior, and Soulforged scaling.
  It occupies two Summon Capacity slots, only one may serve at once, receives half
  normal Soul Rend healing, and cannot be Claimed, Raised All, Empowered, drained
  by Blood Magic, harvested for a servant refund, or converted into duplicate loot.
  Its personal active and rest upkeep is twice the host's ordinary rate through
  Power 90, then fades smoothly from that Power-90 value to zero only at Power 200.
  Other servants retain their ordinary upkeep, and the miniboss counts as one body
  when host-size pressure is calculated.
- Miniboss binding uses 340 base resistance, twice Prime, with a fresh deterministic
  service-cycle attempt ledger. Reanimation costs 120 base Soul Vigor through the
  normal Power curve, then rises by 10% after each completed service: at Power 100
  the six prices are 120/132/144/156/168/180. The pool is not spent when service
  begins. Combat death, upkeep, rest, dismissal, execution, or light unbinding
  consumes one portion without awarding it; save/load, scene travel, Recall, and
  failed initialization do not.
- Heavy Soul Rend restores 20%/35%/50% maximum Health at Power 0/100/200.
  Below 95% Health, restoration is its only service for that cast. At 95% or
  higher it generously restores the remaining Health and, at 100 Necromantic
  Power (1,000 Soul Vigor), can also Empower a servant that is not already Empowered.
  Empower costs twice the servant's stable base Soul Vigor value through the
  current Power-scaled cost curve. The interaction shows `Empower: X Soul Vigor`
  or `Requires X Soul Vigor`, and payment occurs only when Empower succeeds.
  The exact paid Vigor is recorded separately. Light Soul Rend can later sever
  Empowerment for a 75% refund before any Soulforged ranks or the servant itself
  can be removed.
  One lower-biased 1.2x-1.5x roll still controls outgoing damage and
  incoming-damage resistance. Visible Empower growth maps smoothly across a
  1.05x-1.20x range; Soulforged adds 0.5% per rank and the combined body scale
  never exceeds 1.30x.
  Reanimation VFX brightness scales by that exact combat roll, then by the
  servant's Soulforged rank, capped at 20.0. The completion text reports the roll
  and Vigor spent; movement and locomotion compensate
  for the larger stride, and active Empowerment cannot stack or reroll. After
  light Soul Rend severs it, the servant can undergo a new paid ritual. Heavy casting never
  sacrifices the targeted servant when Empower is locked or already applied.
  While the heavy cast is held over a relevant servant, actionable interaction
  text is `Restore Servant` or the exact Empower cost. A fully restored servant
  relies on Dishonored Dynamic Crosshair's desaturated heal reticle without text.
- Raised copies use the game's native hero-summon faction and targeting behavior,
  including autonomous enemy aggression. Ordinary hostile scene enemies are
  eligible when their reusable summon data resolves safely. Named or unique NPCs,
  quest actors, scripted deaths, merchants, guards, companions, bosses, unallowlisted minibosses,
  friendly corpses, and unresolved templates remain protected. The original corpse is
  hidden during service; death or light-cast harvest leaves its loot-bearing native
  simplified remains at the servant's last safe position, with the source position
  as fallback. Persistent scene changes retain that protected relationship; if the
  source scene is temporarily unavailable when service ends, the original corpse
  safely restores the next time that scene loads. Failed initialization restores
  the source instead.
  An eligible miniboss uses a stricter canonical lifecycle: its original full corpse
  and native Search inventory become the only saved inventory owner. Before it moves
  hidden into Gameplay, the same Location is promoted to the game's native saved
  runtime initializer, preserving any authored prefab override, and its authored scene
  ID is retired. When service ends, it returns to the current scene at the servant's
  last safe position and remains there across saves until it is bound or drained again.
  Deferred recovery keeps it hidden until a main scene is ready and the runtime source
  identity validates. The reward-free combat copy never owns Search loot, and the scene
  spawner cannot recreate the original ID. When the sixth portion is consumed, that
  canonical source alone becomes normal simplified remains with discard-on-empty
  cleanup; a failed conversion restores the last portion and keeps the full corpse for
  retry.
  Every raised servant carries a configurable body-bound Reanimation VFX treatment;
  ordinary summons gain it when they earn rank I.
  The persistent effect uses only the native electricity and its integrated smoke;
  the former rune, standalone-smoke, and separate raise-spark
  system has been removed. Its darker default three-tone necromantic palette uses #179B43
  electrical arcs, a #78C98F corpse-green core, and deep #123F2D smoke, with each
  color independently configurable. Particle Amount defaults to 75%, Base Brightness to
  5.0, Full Potential Brightness to 20.0, Electricity Opacity to 1.0, Smoke Opacity
  to 0.5, and Scale to 1.0. Rank and
  Empowerment blend every layer toward a saturated #00E676 emerald by default. Use
  Custom Full Potential Color is disabled by default; enabling it replaces the emerald
  endpoint with Full Potential Color, which remains white by default. Soulforged and
  Empower size multipliers combine under a 1.30x visible ceiling. Brightness interpolates between its base
  and full-potential endpoints without changing either independent opacity; both accept
  values through 20.0.
  The native orange-spark
  emitter, audio, and point light are suppressed. The default dynamic budget reduces
  electricity and integrated-smoke density as the active reanimated host grows while preserving
  the full presentation for small hosts. It retains its
  native NPC portrait when valid;
  otherwise the vanilla skeleton-summon portrait is used. Successful raises also use
  the Forgotten Cemetery necromancer's native skeleton-summon effect.
- Raised servants are true native hero summons, so normal summon faction, limits,
  targeting, collision, persistence, and Soul and Service improvements apply. They
  use their randomized native soul value, invested Soul Vigor, and current Health
  for light-salvage returns.
- Blood Magic Expansion leaves drained corpses intact. Reanimating one applies a
  randomized 20-30% current-Health penalty whose center improves with Blood Power.
  Blood/Life Transfusion can also ritualize an owned living flesh servant. The
  normal blood reticle remains visible but desaturated while combat blocks the
  ritual; outside combat the channel holds it in place, drains real Health, and
  executes it at 20% Health or below. Raised servants complete only their source's
  remaining one-time rewards and play a native blood burst on completion: Meager
  and Worthy servants use the lighter effect, while Potent and Prime servants use
  the stronger effect. They remain available for light Soul Rend after execution.
  Ordinary spell summons provide healing only, with no XP or Blood
  Essence; bloodless servants are invalid but retain desaturated crosshair
  feedback. Light Abhartach's Calling can instead sacrifice only the eligible
  owned flesh servant under the crosshair, even in combat. The servant becomes
  a real native corpse for Abhartach's normal explosion and quality scaling;
  service ends with no XP, Blood Essence, Soul Vigor, Mana refund, or duplicate
  loot. Heavy Abhartach remains corpse-only.
- Dishonored Dynamic Crosshair can show Necrotic-green Meager/Worthy/Potent/Prime
  reticles over eligible corpses and active owned summons while Soul Rend is
  equipped. Heavy servant service uses the dedicated heal or empower reticle,
  and commands retain their distinct Attack, Hold, Follow, and Behavior icons.

Soul Rend Inner Light
---------------------

Each raised Soul Rend hand emits a no-shadow necromantic-green light. Its
brightness uses the same gentle smoothstep growth as Blood Magic through
Necromantic Power 0-100, then grows linearly to Power 200; casting triples that
hand's brightness after 0.3 seconds before it fades back. Soul Rend defaults to a
restrained 0.8 intensity multiplier. The base intensity, Soul Rend multiplier,
interior multiplier, three power brightness and range milestones, and fade time
are configurable. Lights update after animation, include First Person Arms
Adjuster's current visual offset when available, and explicitly disable HDRP
shadows and volumetric contribution. Versatile Weapons suppression hides the
corresponding light while an opposite-hand weapon is being used with both hands.
World transitions clear stale hand anchors and retry after the rebuilt hero body
and equipment hands are ready.

Custom audio
------------

Soul Rend audio is stored under the installed mod's audio folder. Each quality
tier supports files numbered 0 through 9. Successful light and heavy Soul Rend
contacts also use their own four-clip impact pools; invalid and unaffordable casts
remain silent. Impact clips avoid immediate repeats, use the same distance fade,
default to 0.8 volume, and allow up to four concurrent voices:

  soul_salvage_low_0.wav through soul_salvage_low_9.wav
  soul_salvage_medium_0.wav through soul_salvage_medium_9.wav
  soul_salvage_high_0.wav through soul_salvage_high_9.wav
  soul_salvage_max_0.wav through soul_salvage_max_9.wav
  soul_salvage_impactlight_0.wav through soul_salvage_impactlight_3.wav
  soul_salvage_impactheavy_0.wav through soul_salvage_impactheavy_3.wav

Missing files are skipped. If an entire tier is absent, the nearest available
tier is used. Replace files with valid WAV audio while retaining their names.
All ritual and impact clips use the game's SFX mixer category, so both the
Master and SFX volume controls and mute states apply.

Balance presets
---------------

Balance Preset defaults to Grave Pact and changes only the Soul Vigor economy,
servant sustain, newly raised starting Health, and Soul Claim threshold. Progression
milestones, capacity, damage scaling, corpse eligibility, persistence, commands,
AI, safety rules, and investment refunds are unchanged.

                          Soul        Grave
Effective value           Famine      Pact       Dominion
Soul Vigor rewards        x1.00       x1.50      x2.25
Soul Vigor costs          x1.00       x0.75      x0.50
Active and rest upkeep    x1.00       x0.60      x0.25
Raised starting Health    x1.00       x1.35      x2.00
Soul Claim threshold      -5%         +0%        +5%

Starting Health is capped at full Health. Claim adds the preset value to its
Power-, hit-, and quality-based threshold before the final 1-40% clamp. Harvest
rewards round to the nearest whole Vigor; costs round up and remain at least one.
Greater-soul pools still lose one native portion per drain, and summon or servant
investment refunds are never multiplied.

The five bounded controls appear directly below Balance Preset. Choosing Soul
Famine, Grave Pact, or Dominion immediately writes that preset's complete values.
Changing any one of those values switches Balance Preset to Custom and preserves
the resulting mix. FoA Mod Manager refreshes the displayed values after either
change. Preset changes never reroll an existing servant, heal the
current host, or retroactively change a past payment or refund.

Configuration
-------------

Config file: BepInEx/config/ks.tgfoa.soul-and-service.cfg

FoA Mod Manager presents the everyday decisions first: Soul Rend, Host and
Persistence, Commands and Targeting, Summon Behaviors, Following, and Collision.
Presentation sections for Reanimation VFX, the Soul Rend Hand Light, and Audio
follow them. Advanced contains AI timing, spawn recovery, and the safe Invocations
of Might repair. Diagnostics and Import Previous Settings remain the final tabs.

Most players need only choose a balance preset, decide whether servants persist
through saves and rest, select the command modifier, tune Guard engagement and formation distances, choose
collision behavior, and balance ritual, impact, and summon-idle audio. Specialist
controls remain available for Directed Hunt, Bulwark, body-specific ritual pitch,
hand-light growth, reanimation colors and particles, and diagnostic overrides.
Import Previous Settings safely restores compatible customized values after a
future config reset.

Distance Fade Strength (`SoulSalvageAudioRangeVolume` in the raw config) defaults
to 1.0. It keeps ritual and impact audio
two-dimensional while fading from 100% volume at the target to 10% at 30 meters or
farther. Set it to 0 to disable distance fading. Missing position data safely uses
full volume.

IMPORTANT FOR TESTING: enable Diagnostics -> Override Soul Vigor and set Soul Vigor
Override Value to 5,000 to immediately unlock and exercise every progression-gated
feature, behavior, and command. While enabled, the test value drives Necromantic Power,
summon and Soul Rend scaling, the public API, and Deeds display without changing
the character's saved Soul Vigor. Disable it to return immediately to saved
progression. Useful checkpoints are 0, 250, 1,000, 2,000, 3,000, 4,000, and 5,000.
The override defaults to 5,000, accepts values through 10,000, and keeps Power capped
at 200 above 5,000. Override Soulforged Rank can separately force every current and
future servant to an exact effective rank without changing real rank or damage
progress.

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
Recall Host, Raise All, Guard, Bulwark, and Hunt orders request one spoken command through
its public API, with a dedicated pool for each order. While owned summons are active
at 30 or more Necromantic Power, Soul and Service owns Sprint + Take All Items
for formation, Recall Host, and eligible Raise All commands. Battlecry Voice Tuner's separate
battlecry hotkey remains available. It stays authoritative
over gender selection, pitch, volume, cooldown, repeat avoidance, and smart reverb;
Soul and Service continues normally when it is absent or declines playback.

Eyes in the Dark 1.3.2 or newer is optional. Each successful ordinary corpse
harvest reports its normalized quality through Eyes' soft API. Eyes alone decides
whether the Hero is exposed during an active Wyrdnight and applies its configured
quality-scaled threat. Failed or repeated harvests, greater-soul portions, living
servant unbinding, and remains already executed through Blood Magic Expansion add
no corpse-harvest threat. Soul and Service continues normally when Eyes is absent
or its API is unavailable.

Dishonored Dynamic Crosshair is optional. Attack, individual Hold, and individual
Follow confirmations last 0.675 seconds. Hold All, Follow All, Recall Host, Raise All, and
Behavior confirmations last 1.35 seconds in both native interaction text and their
matching Dishonored icon pulse. Behavior changes also show Behavior: Guard, Behavior:
Bulwark, or Behavior: Hunt through Grail Floating Text when it is installed.

Versatile Weapons is optional. Soul Rend retained in a hand hidden by an active
two-handed grip is not treated as currently equipped: its targeting reticle and
hover state clear until that hand becomes available again. The spell remains in
its equipment slot and returns normally when the weapon switches back to one hand.

Raised servants are allied copies that grant no XP or scripted death reward. With
Persistent Servants enabled, the copy's Health, Empowerment, Soulforged progress,
investment, staged-recovery ledger, and protected source relationship survive saving, loading, and
restarting without adding the generated copy to the game's native save graph.
Incomplete, invalid, and persistence-disabled recovery restores the source safely
and refunds committed Soul Vigor. The snapshot is replaced only when every hidden
source has either a reconstruction record or a restoration-only record. Heavy cast
still accepts only ordinary hostiles whose summon data can be
resolved to one canonical reusable template; ordinary repetitive scene enemies are
eligible while authored unique and persistent NPC identities remain protected.
Fresh bodies rejected by those binding rules can still be harvested for Soul
Vigor. Heavy Soul Rend reports a resistant soul, while a previously harvested
intact body reports that no soul remains to bind.
Allowlisted minibosses additionally preserve one full canonical source through a
native saved runtime initializer and validate its exact source, spawn-template, and pool identity
before a saved servant can be reconstructed. Malformed or mismatched records restore
the source and never reach native spawning.

Troubleshooting
---------------

Enable the Diagnostics setting in the Diagnostics section and inspect the newest
BepInEx LogOutput.log. Raised persistence reports each snapshot write and load,
every rejected record with its reason, scheduled reconstruction, completed
rehydration, immediate source restoration, and deferred recovery totals. Pressing
Interact while aiming at an owned summon records the configured command modifier,
the game's Sprint-held state, focus validity, hold mode, and rejection reason;
Attack attempts report the same input state, target validity, outcome, and number
of servants commanded. These messages are emitted once per save/load boundary or
explicit input attempt rather than every frame. With Grail Floating Text installed, leave Show Grail
Floating Text Diagnostics enabled to see Pale System diagnostics for Soul Rend
targeting, binding details, and servant lifecycle. GFT groups those separately
from ordinary capacity, Soul Vigor, reanimation, reward, and command feedback.
Disabling GFT's Show Mod Diagnostic Messages hides only the diagnostic channel.
Do not install this DLL alongside Avalon Summons, Better Summon, or the temporary
pass-through test DLL.
