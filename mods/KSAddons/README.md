# KS Addons

KS Addons is the companion-addon area inside Grailwright. It houses small
targeted plugins for other Tainted Grail: The Fall of Avalon mods.

Current addon folders:

- `KSBetterMovementAddon`
- `KSTGVolumetricFixAddon`
- `KSWyrdSightAddon`
- `KSTGAllLightsCastShadowsAddon`
- `KSTGContactShadowsAddon`
- `KSTGGlobalIlluminationAddon`
- `KSPersistentCorpsesAddon`

Original mod targets:

- Better Movement Addon 0.1.6: for Better Movement 1.3.0. It adds
  terrain-aware positional slide audio, including continuing downhill slides,
  live surface transitions, and edited terrain Foley.
- Better Volumetric Fog Addon 0.1.2: for Better Volumetric Fog 1.0.2-mono,
  which loads in BepInEx as plugin version 1.0.0. It replaces recurring global
  Fog discovery with an event-fed cache and uses Low quality only in interiors
  by default, restoring vanilla volumetrics elsewhere.
- Wyrd Sight Addon 1.2.6: for Wyrd Sight, which loads in BepInEx as plugin
  version 1.2.0. Its pulse also reveals untaken quest givers with Balanced
  story-lock filtering and an integrated, event-driven golden outline by default.
- All Lights Cast Shadows Addon 1.2.7: for All Lights Cast Shadows Mono file
  1.0.0-mono, which loads in BepInEx as plugin version 1.2.0. It restores
  shadow state, protects bonfire lighting, limits atlas pressure, and can
  temporarily lower shadow cost during outdoor combat.
- Contact Shadows Addon 0.1.3: for Contact Shadows 1.0.0-mono. It enables the
  effect only in interiors by default, keeps up to four stable nearby point or
  spot lights active, and exactly restores touched light, camera, and volume
  state.
- Global Illumination Addon 0.1.7: for Global Illumination 1.0.0. It
  starts new scenes at Performance by default, raises quality after sustained
  target frame rate, and uses separate interior and exterior Adaptive presets
  as recovery limits while preserving the parent's full-quality indoor look.
- Persistent Corpses Addon 1.1.0: for Persistent Corpses 1.0.0. It conceals
  restored corpse renderers while ragdoll physics settles, then reveals the
  bodies lying down instead of visibly dropping from a standing pose. Long
  bonfire rests also simplify loaded corpses through the game's loot-preserving
  replacement system.

The imported source versions may be newer than the original suite description
line when an addon has received a follow-up update.
