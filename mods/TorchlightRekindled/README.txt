Torchlight Rekindled
====================

Version 0.4.3

Platforms: Windows and Linux.

Torchlight Rekindled is a standalone BepInEx 5 Mono plugin for Tainted Grail:
The Fall of Avalon. It expands the held torch's useful light, gives the visible
flame separate HDR brightness and bloom controls, and adds a spatial looping
fire sound.

Features
--------

- Adds a literal 20-metre range bonus by default, adjustable from 0 to 70
  metres.
- Uses separate live brightness presets for interiors and exteriors while
  retaining every individual tuning control. Bright defaults indoors and
  Vanilla defaults outdoors.
- Scales illumination without flattening or replacing the original flicker.
- Adds synchronized slow and fast variation to the light cast on the world,
  without changing the flame particles or light range.
- Separately scales the HDR colors of the flame, embers, and sparks.
- Controls the HDR headroom that feeds the game's bloom effect.
- Adds a configurable soft warm corona inside the flame for stronger bloom.
- Shapes the corona independently with vertical scale and proportional vertical
  offset controls. Its camera-facing shape follows the animated torch shaft
  through blocking and other animations.
- Raises weak native interior bloom to configurable minimum strength and spread
  throughout interiors, without reducing stronger scene profiles.
- Can optionally limit the interior bloom enhancement to times when the torch
  is equipped.
- Plays the game's own small-fire loop from the torch in the equipped hand.
- Restores the original torch behavior when the master switch is disabled.

Configuration
-------------

Config file:

  BepInEx\config\ks.tgfoa.torchlight-rekindled.cfg

Defaults and ranges:

  Enabled                    true
  InteriorBrightnessPreset  Bright (Vanilla or Bright)
  ExteriorBrightnessPreset  Vanilla (Vanilla or Bright)
  RangeBonusMeters           20.0    (0.0 to 70.0 metres)
  BrightnessMultiplier       1.0     (0.25 to 3.0)
  LightFlickerStrength       1.0     (0.0 to 2.0)
  LightFlickerSpeed          1.0     (0.5 to 2.0)
  FlameBrightnessMultiplier  0.75    (0.25 to 3.0)
  FlameBloomMultiplier       0.75    (0.0 to 3.0)
  FlameHaloStrength          5.0     (0.0 to 10.0)
  FlameHaloSize              0.07    (0.02 to 0.25 metres)
  FlameHaloVerticalScale     2.2     (0.25 to 4.0)
  FlameHaloVerticalOffset    0.45    (-1.0 to 1.0 of scaled height)
  FlameHaloHorizontalOffset -0.12    (-1.0 to 1.0 of width)
  FlameHaloAxisPitchOffsetDegrees 0  (-45.0 to 45.0 degrees)
  FlameHaloAxisYawOffsetDegrees 0    (-45.0 to 45.0 degrees)
  FlameHaloRotationOffsetDegrees -20 (-180.0 to 180.0 degrees)
  FlameHaloBashRotationOffsetDegrees 90 (-180.0 to 180.0 degrees)
  EnhanceInteriorBloom       true
  InteriorBloomOnlyWhileTorchEquipped false
  InteriorBloomThreshold     1.0     (0.0 to 4.0)
  InteriorBloomIntensity     0.25    (0.0 to 1.0)
  InteriorBloomScatter       0.65    (0.0 to 1.0)
  LoopingFireAudio           true
  LoopingFireVolume          1.0     (0.0 to 2.0)

BrightnessMultiplier affects the light cast onto the world. It does not
replace the torch's animation curve, so the original flicker remains.
Displayed 1 produces 5x vanilla illumination and displayed 3 produces 25x.

The interior and exterior brightness presets apply live, non-destructive
balances on top of the individual settings. Vanilla is balanced for normal
interior brightness, while Bright is balanced for darker interiors. Bright is
the default indoors and retains the full configured outputs: 5x world
illumination, 2.25x flame-brightness response, 2.25x flame HDR-bloom response,
and halo strength 5 at the defaults. Vanilla is the default outdoors and halves
those effective outputs to 2.5x, 1.125x, 1.125x, and 2.5. Range, flicker timing,
halo size and shape, audio, and the separate interior bloom controls are
unchanged.

LightFlickerStrength adds irregular slow and fast variation after the original
torch update. Set it to 0 for vanilla flicker only. LightFlickerSpeed controls
the added variation without changing range or flame-particle animation.
Displayed strength 1 uses four-times internal response, while displayed speed 1
uses two-times internal response. Both torch lights share one flicker signal so
their illumination stays synchronized.

The range bonus follows the torch controller's static, dynamic, or curved
range behavior instead of being overwritten by it. Its value is the number of
metres added to the original range.

FlameBrightnessMultiplier affects the visible fire particles. Displayed 1
produces 3x source brightness and displayed 3 produces 9x.
FlameBloomMultiplier affects only HDR color values above display white.
Displayed 0 removes the flame's extra HDR bloom contribution, displayed 1
applies 3x response, and displayed 3 applies 9x. The result still depends on
the active post-processing and bloom settings.

FlameHaloStrength controls an additional soft warm bloom corona inside the
flame. Set it to 0 to remove the corona. FlameHaloSize controls its diameter;
the recommended 0.07 metre size keeps the radial glow inside the fire without
showing solid geometry. The recommended strength 5 produces a pronounced warm
corona, while values up to 10 allow an extreme glow. FlameHaloVerticalScale
changes only its height, with the recommended
2.2 forming a taller column. FlameHaloVerticalOffset moves it by a fraction of
that scaled height; the recommended 0.45 lifts it into the visible flame.
FlameHaloHorizontalOffset moves it sideways by a fraction of its width; the
recommended -0.12 centers it over the torch's burnable portion.
FlameHaloAxisPitchOffsetDegrees and FlameHaloAxisYawOffsetDegrees correct the
tracked direction in torch-local space before it is projected onto the screen,
allowing the elongated glow to follow the burnable portion across animations.
The corona remains camera-facing for visibility. FlameHaloRotationOffsetDegrees
adds the final signed screen-space correction; the recommended -20-degree value
updates live and can be changed for exact visual alignment in-game. During the
brief dual-handed
BlockParry light tap, the legacy-named FlameHaloBashRotationOffsetDegrees adds
an immediate second correction without changing upright or held-block
alignment. Its recommended 90-degree value corrects the light-parry animation's
perpendicular corona, which becomes half-size and smoothly returns to its
configured size over 0.3 seconds afterward. When blocking begins, the corona
fades out over 0.2 seconds and remains hidden through BlockPommel bashes. It
fades back over 0.2 seconds after an ordinary block, or over 0.4 seconds after a
blocking sequence that included a pommel bash.

EnhanceInteriorBloom creates one lightweight HDRP volume in interiors. It
lowers overly restrictive native thresholds and raises weak intensity and
scatter values only to the configured targets. Stronger scene profiles remain
native, including interiors already tuned like exteriors. The controller
leaves bloom quality and resolution unchanged, checks cached scene context once
per second, and disables itself outdoors. Because HDRP bloom is screen-wide,
other bright emissive objects also benefit. Enable
InteriorBloomOnlyWhileTorchEquipped to limit the enhancement to times when the
torch is equipped; it is disabled by default.

The fire loop is emitted from the held torch's flame transform when available,
with the equipped-hand transform as its fallback. It therefore follows the
torch's position rather than playing from the center of the player.
LoopingFireVolume 1 is recommended for a prominent hand-positioned crackle and
now applies four-times internal gain. Use 0 for silence or raise it above 1
when nearby ambience masks the torch.

Recommended Mods & Interior Lighting Setup
------------------------------------------

Light Control is recommended for darker dungeons that make the torch genuinely
necessary:

  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/213

Suggested values under Dungeons:

  Brightness Offset Day    -2
  Brightness Offset Night  -2
  World Intensity Day       2
  World Intensity Night     2

Suggested values under Interiors:

  Brightness Offset Day    -1.5
  Brightness Offset Night  -1.5
  World Intensity Day       1.5
  World Intensity Night     1.5

Light Control adjusts scene lighting and is compatible with Torchlight
Rekindled. It is a different mod from the incompatible Torch Light Control.

First Person Arms Adjuster is also recommended for positioning the rendered
arms, weapons, and held torch without moving the gameplay camera:

  https://www.nexusmods.com/taintedgrailthefallofavalon/mods/263

First Person Arms Adjuster 0.3.8 and newer owns equipped presentation-effect
alignment. It moves the vanilla torch flame and embers; Torchlight Rekindled's
corona position and fire audio follow their corrected flame parent, while the
corona orientation follows the rigid torch model. The
gameplay hand socket, light, physics, and projectile origins remain unchanged.

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
