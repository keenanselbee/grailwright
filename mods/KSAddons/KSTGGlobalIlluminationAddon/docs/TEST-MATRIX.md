# KS Global Illumination Addon Test Matrix

## Release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| GI-01 | Start a previously unseen playable interior with Adaptive mode, StartAtPerformance true, and InteriorPreset Full. | Log reports Interior and Performance; after sustained qualifying FPS, quality can rise through Balanced to Full. | Not run |
| GI-02 | Move to a previously unseen open-world scene with StartAtPerformance true and ExteriorPreset Balanced. | The context changes to Exterior, starts at Performance, and never rises above Balanced. | Not run |
| GI-03 | Reach Balanced or Full, then sustain fewer than 54 FPS for at least four seconds after warmup. | The controller steps down exactly one tier and waits through its cooldown. | Not run |
| GI-04 | Start at Performance and sustain at least 59 FPS for 30 seconds after cooldown. | The controller steps up exactly one tier, never above Full indoors or Balanced outdoors. | Not run |
| GI-05 | Pause, open a loading transition, or unfocus the game. | Sampling does not advance and no quality decision is made from those frames. | Not run |
| GI-06 | Set Mode to Full, Balanced, and Performance in turn. | Each fixed mode applies globally and does not make adaptive tier changes. | Not run |
| GI-07 | Disable the addon, then re-enable it. | The parent profile is restored while disabled; contextual control resumes cleanly when enabled. | Not run |
| GI-08 | Edit and reload the parent JSON while Diagnostics is enabled. | The changed parent values become the new Full profile and are restored on addon disable. | Not run |
| GI-09 | Run with All Lights Cast Shadows installed. | The addon makes no shadow config, budget, resolution, or light-state changes. | Not run |
| GI-10 | Toggle the parent GI mod off and on with Grail Floating Text installed, then repeat with ShowToggleNotifications false. | One `Global Illumination: Disabled/Enabled` System notification confirms each actual state change while notifications are enabled, and none appears while disabled. | Not run |
| GI-11 | Force an adaptive downgrade and recovery with Diagnostics false, then with Diagnostics true and ShowGrailFloatingTextDiagnostics true, then with only the GFT setting false. | Tier notifications appear only while both diagnostic settings are enabled; detailed logs continue with Diagnostics true after the subordinate GFT setting is disabled. | Not run |
| GI-12 | Set InteriorPreset to Balanced and ExteriorPreset to Performance while Adaptive and StartAtPerformance are active. | New scenes start at Performance; the interior can recover only to Balanced and the exterior remains at Performance. | Not run |
| GI-13 | Select each fixed Mode while changing the Adaptive presets. | Full, Balanced, and Performance fixed modes ignore both contextual preset settings. | Not run |
| GI-14 | Disable StartAtPerformance, enter an unseen scene, then re-enable it and return to a scene with a remembered tier. | The unseen scene starts at its contextual preset while disabled; remembered scenes resume their last successful tier after the setting is re-enabled. | Not run |
