# KS All Lights Cast Shadows Addon Test Matrix

## Release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| SHADOW-01 | Load a save with All Lights Cast Shadows and this addon enabled. | Version 1.2.8 loads without an exception and reports atlas and combat-aware protection active. | Not run |
| SHADOW-02 | Toggle the parent shadow mod off and on with Grail Floating Text installed, first with ShowToggleNotifications true and then false. | One `All Lights Cast Shadows: Disabled/Enabled` System notification confirms each actual change while true; no toggle notification appears while false. | Not run |
| SHADOW-03 | Visit an exterior with several point and spot lights, then inspect the log with Diagnostics and ShowGrailFloatingTextDiagnostics true; repeat with only the GFT setting false. | The scan always logs active, point, spot, estimated map, constrained, tracked, restored, and cap values; GFT shows one collapsed summary per changed count set only while both settings are enabled. | Not run |
| SHADOW-04 | Compare the same atlas-heavy view with ProtectShadowAtlas true and false. | The guard reduces promoted-light shadow resolution while enabled; disabling it immediately restores original HDRP resolution state. | Not run |
| SHADOW-05 | Move far enough that a constrained light leaves the parent budget, then return. | The light's original resolution override, tier, and override mode are restored when inactive and captured cleanly if promoted again. | Not run |
| SHADOW-06 | Use a light with an explicit resolution below PromotedShadowResolution. | The addon does not raise the light's authored lower resolution. | Not run |
| SHADOW-07 | Check a protected bonfire or campfire. | The fire illumination remains protected from self-shadowing as in version 1.2.0. | Not run |
| SHADOW-08 | Play with No Player Light installed. | No HeroLight-specific warning or compatibility behavior is required; other promoted point and spot lights remain protected. | Not run |
| SHADOW-09 | Enter outdoor combat with the default combat settings. | The active atlas cap becomes 128 without changing the parent's budget or distance. | Not run |
| SHADOW-10 | End combat and wait five seconds. | A parent rescan restores the normal 256 atlas cap without rapid toggling during the delay. | Not run |
| SHADOW-11 | Fight inside an interior with OutdoorCombatOnly true, then false. | Combat overrides stay inactive while true and engage after the setting is disabled. | Not run |
| SHADOW-12 | Enable the combat budget and distance limits with parent values above and below their configured limits. | Each scan uses the lower value, never raises a stricter parent value, and restores the live parent config immediately afterward. | Not run |
| SHADOW-13 | Repeat combat entry and exit with Diagnostics enabled, first with ShowGrailFloatingTextDiagnostics true and then false. | Logs always report combat activation, restoration, and the exact enabled override set; collapsed GFT System notices appear only while both settings are enabled. | Not run |
| SHADOW-14 | With Diagnostics enabled, remain on the main menu and pass through a loading or teleport transition before entering gameplay. | Atlas summaries remain available in the BepInEx log, no atlas GFT summary appears outside active gameplay, and one collapsed summary appears after the gameplay scene is fully initialized. | Not run |
