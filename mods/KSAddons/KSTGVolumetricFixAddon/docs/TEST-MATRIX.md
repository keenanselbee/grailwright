# KS Better Volumetric Fog Addon Test Matrix

## Release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| VFOG-01 | Start the Mono game with Better Volumetric Fog 1.0.2-mono and this addon installed. | Version 0.1.2 loads without an exception and reports Low, interior-only, optimized discovery defaults. | Not run |
| VFOG-02 | Enter the prison or another interior with the defaults. | The parent applies Manual fog control with 30 percent screen resolution and 32 slices. | Not run |
| VFOG-03 | Leave the interior for an open-world exterior. | The exact game-authored Fog values and override states return once and remain stable outdoors. | Not run |
| VFOG-04 | Travel through a loading screen between an interior and exterior. | Enhanced values are restored during loading and Low is applied only after a reliable interior context becomes active. | Not run |
| VFOG-05 | Stand near the Cunacht player-home alchemy station and watch the Steam frametime graph for at least one minute. | The recurring two- or ten-second spikes attributed to global Fog discovery are absent or materially reduced. | Not run |
| VFOG-06 | Repeat the same view with only Better Volumetric Fog 1.0.2-mono enabled, then with the addon enabled. | The addon retains the interior visual cleanup at Low while producing smoother frametimes than the parent alone. | Not run |
| VFOG-07 | Register or activate a new HDRP Volume after the cache has been seeded. | Its Fog component joins the cached snapshot and requests the next parent update immediately without a new all-resources scan. | Not run |
| VFOG-08 | Change the parent TGVolumetricFix.json Quality to Medium while standing indoors. | The parent reloads normally, the addon still applies its configured Low in memory, and the JSON remains Medium on disk. | Not run |
| VFOG-09 | Toggle Better Volumetric Fog off and on through its parent hotkey in an interior and exterior with Grail Floating Text installed, then repeat with ShowToggleNotifications false. | Off restores exact game-authored values; on reapplies Low only indoors; one `Better Volumetric Fog: Disabled/Enabled (interiors only)` System notification confirms each actual change while notifications are enabled, and none appears while disabled. | Not run |
| VFOG-10 | Set Enabled to false. | Better Volumetric Fog returns to its own configured global behavior without requiring a restart. | Not run |
| VFOG-11 | Set InteriorsOnly to false and select Medium. | Medium applies in both interiors and exteriors while the event-fed Fog cache remains active. | Not run |
| VFOG-12 | Set OptimizeFogDiscovery to false with Diagnostics enabled. | Contextual behavior remains active and the log confirms parent applications while its original Fog search is used. | Not run |
| VFOG-13 | Open FoA Mod Manager. | The final Import Previous Settings tab is present and safely reports no compatible backup on a fresh install. | Not run |
