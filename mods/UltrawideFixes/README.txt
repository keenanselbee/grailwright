Ultrawide Fixes
===============

Version 1.0.7

Ultrawide Fixes is a standalone BepInEx 5 Mono plugin for Tainted Grail: The
Fall of Avalon.

Short Description
-----------------

Fixes title-screen and loading-screen presentation issues on ultrawide displays.

Behavior
--------

The game title scene uses a 1920x1080 title video and explicit side-bar UI
objects. This plugin:

  1. Resizes the title video RawImage to the configured ultrawide aspect.
  2. Sizes the title video parent RectTransforms so old 16:9 containers do not
     leave edge bars.
  3. Blends between a vertical crop and a horizontal stretch through RawImage UVs.
  4. Biases the crop window upward so the upper part of the video is preserved.
  5. Disables title-screen black bar objects.

It also patches loading screens by targeting the serialized VLoadingScreenUI
background and blurred background Image fields, then resizing those images to
cover ultrawide displays after the real artwork sprite is assigned. The loading
bar, loading text, and loading wheel views are not resized or moved.

By default, the plugin fills the current screen aspect. If FillCurrentScreen is
disabled, the fixed target aspect is 2.333333, which is 21:9.

Config
------

The config is generated after the game starts once:

  BepInEx\config\ks.tgfoa.ultrawide-fixes.cfg

Version 1.0.7 uses ConfigSchemaVersion 1. Older configs are backed up and a
fresh config is generated once so defaults apply cleanly. Display aspect,
crop/stretch, crop focus, and title-rendering compatibility calibration survive
schema resets.

Useful config entries:

  Enabled = true
  PatchTitleVideo = true
  HideTitleBlackBars = true
  PatchLoadingBackground = true
  PatchLoadingBlurBackground = true
  FillCurrentScreen = true
  UseScreenAspect = false
  TargetAspect = 2.333333
  MinimumScreenAspect = 1.8
  CropVideoUv = true
  StretchBlend = 0.1
  VerticalCropFocus = 0.5
  ResizeRawImageRect = true
  ResizeVideoParents = true
  LoadingStretchBlend = 0.2
  LoadingVerticalCropFocus = 0.5
  VerboseLogging = false

StretchBlend controls the title video compromise:

  0.0 = pure crop, no stretch
  0.1 = very light stretch with more crop
  1.0 = full stretch, no crop

LoadingStretchBlend controls loading-screen paintings:

  0.0 = pure crop, preserves painting proportions
  0.1 = very light stretch with mostly crop
  0.2 = light stretch with less vertical crop
  1.0 = full stretch to the display aspect

Install Shape
-------------

Vortex mod folder payload:

  UltrawideFixes\UltrawideFixes.dll

When installed as a BepInEx plugin mod in Vortex, this payload is placed under:

  BepInEx\plugins\UltrawideFixes\UltrawideFixes.dll

Plugin GUID:

  ks.tgfoa.ultrawide-fixes

Notes
-----

This does not edit bundles, video files, resources.assets, sprites, or game
files. The title video and loading paintings are native assets, so the plugin
cannot reveal real extra side artwork. It presents them with configurable
crop/stretch behavior at runtime instead.

PREVIOUS SETTINGS
-----------------

FoA Mod Manager always shows a final Import Previous Settings tab with the
current and available backup schemas. Its one-shot action restores compatible
customized settings, then automatically turns back off. Restart the game after importing.
