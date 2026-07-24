Steel and Bone
Version 0.2.0

Steel and Bone is a BepInEx 5 Mono proof-of-concept resistance plugin for Tainted Grail: The Fall of Avalon.

This version adds a small set of thematic enemy resistances:

Skeleton, bone, and animated-armor enemies resist blood magic and bleed.
Skeleton, bone, and animated-armor enemies resist slashing and piercing damage.
Golem, stone, and construct enemies resist blood magic and poison.
Wyrdspawn, Wyrd spirits, Wyrd slime, and Wyrdness enemies resist Wyrdness damage.

The mod patches the game's per-target damage modifier path and only runs when damage is being processed. It does not scan enemies.
Rules are evaluated together, and the strongest matching resistance wins.

Default settings:

Enabled = true
Preset = Forsaken
ResistanceTextEnabled = true

Presets:

Bloodied reduces the harsh thematic counters to 45% damage and skeleton slash/pierce to 75% damage.
Forsaken reduces the harsh thematic counters to 25% damage and skeleton slash/pierce to 55% damage.
Nightmare reduces the harsh thematic counters to 0% damage and skeleton slash/pierce to 35% damage.

The compact resistance text uses the same general overlay style and position as Killing Blow Mastery. Resisted hits always show Resistant, with the color shifting from orange-yellow toward red as resistance becomes stronger. True zero-damage counters still show Resistant, but in the deepest red.

Install with Vortex as a BepInEx plugin, or place this folder under BepInEx/plugins.
