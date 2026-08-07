Torchlight Rekindled
====================

Version 0.1.7

Platforms: Windows and Linux through Proton.

Torchlight Rekindled is a standalone BepInEx 5 Mono plugin for Tainted Grail:
The Fall of Avalon. It expands the held torch's useful light, gives the visible
flame separate HDR brightness and bloom controls, and adds a spatial looping
fire sound.

Features
--------

- Uses a compact amplified range control: displayed 3 adds 20 metres and
  displayed 10 adds about 66.7 metres.
- Scales illumination without flattening or replacing the original flicker.
- Adds synchronized slow and fast variation to the light cast on the world,
  without changing the flame particles or light range.
- Separately scales the HDR colors of the flame, embers, and sparks.
- Controls the HDR headroom that feeds the game's bloom effect.
- Adds a configurable soft warm corona inside the flame for stronger bloom.
- Plays the game's own small-fire loop from the torch in the equipped hand.
- Restores the original torch behavior when the master switch is disabled.

Configuration
-------------

Config file:

  BepInEx\config\ks.tgfoa.torchlight-rekindled.cfg

Defaults and ranges:

  Enabled                    true
  RangeBonusMeters           3.0     (0.0 to 10.0)
  BrightnessMultiplier       1.0     (0.25 to 3.0)
  LightFlickerStrength       1.0     (0.0 to 2.0)
  LightFlickerSpeed          1.0     (0.5 to 2.0)
  FlameBrightnessMultiplier  0.75    (0.25 to 3.0)
  FlameBloomMultiplier       0.75    (0.0 to 3.0)
  FlameHaloStrength          1.0     (0.0 to 3.0)
  FlameHaloSize              0.08    (0.02 to 0.25 metres)
  LoopingFireAudio           true
  LoopingFireVolume          1.0     (0.0 to 2.0)

BrightnessMultiplier affects the light cast onto the world. It does not
replace the torch's animation curve, so the original flicker remains.
Displayed 1 produces 5x vanilla illumination and displayed 3 produces 25x.

LightFlickerStrength adds irregular slow and fast variation after the original
torch update. Set it to 0 for vanilla flicker only. LightFlickerSpeed controls
the added variation without changing range or flame-particle animation. Both
torch lights share one flicker signal so their illumination stays synchronized.

The range bonus follows the torch controller's static, dynamic, or curved
range behavior instead of being overwritten by it. Displayed 3 adds 20 metres,
displayed 5 adds about 33.3 metres, and displayed 10 adds about 66.7 metres.

FlameBrightnessMultiplier affects the visible fire particles. Displayed 1
produces 3x source brightness and displayed 3 produces 9x.
FlameBloomMultiplier affects only HDR color values above display white.
Displayed 0 removes the flame's extra HDR bloom contribution, displayed 1
applies 3x response, and displayed 3 applies 9x. The result still depends on
the active post-processing and bloom settings.

FlameHaloStrength controls an additional soft warm bloom corona inside the
flame. Set it to 0 to remove the corona. FlameHaloSize controls its diameter;
the recommended 0.08 metre size keeps the radial glow inside the fire without
showing solid geometry.

The fire loop is emitted from the held torch's flame transform when available,
with the equipped-hand transform as its fallback. It therefore follows the
torch's position rather than playing from the center of the player.
LoopingFireVolume 1 is recommended for an audible hand-positioned crackle.
Use 0 for silence or raise it above 1 when nearby ambience masks the torch.

Recommended Companion Setup
---------------------------

Light Control is recommended for stronger dungeon contrast around the torch:

  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/213

Suggested Light Control values under 06 Dungeons:

  Brightness Offset Day    -2
  Brightness Offset Night  -2
  World Intensity Day       2
  World Intensity Night     2

Suggested Light Control values under 07 Interiors:

  Brightness Offset Day    -1.5
  Brightness Offset Night  -1.5
  World Intensity Day       1.5
  World Intensity Night     1.5

Light Control adjusts scene lighting and is compatible with Torchlight
Rekindled. It is a different mod from the incompatible Torch Light Control.

First Person Arms Adjuster is also recommended for positioning the rendered
arms, weapons, and held torch without moving the gameplay camera:

  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/263

Compatibility
-------------

Do not use Torchlight Rekindled with Torch Light Control. Both plugins modify
the held torch's light values, and Torchlight Rekindled declares it as an
incompatible plugin to prevent compounded changes.

No Player Light is compatible. It controls the separate hidden HeroLight and
does not modify the held torch.

Install Shape
-------------

Vortex mod folder payload:

  TorchlightRekindled\TorchlightRekindled.dll
  TorchlightRekindled\README.txt
  TorchlightRekindled\CHANGELOG.txt

When installed as a BepInEx plugin mod in Vortex, this payload is placed under:

  BepInEx\plugins\TorchlightRekindled\

Plugin GUID:

  ks.tgfoa.torchlight-rekindled

Troubleshooting
---------------

If the torch is unchanged, disable Torch Light Control and check the BepInEx
log for Torchlight Rekindled startup errors.

If the flame becomes uncomfortable to view, lower FlameBloomMultiplier first,
then lower FlameBrightnessMultiplier if needed.
