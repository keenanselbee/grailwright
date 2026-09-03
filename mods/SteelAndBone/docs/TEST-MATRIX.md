# Steel and Bone Test Matrix

Version under test: 4.2.6

## Focused release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| SAB-SMOKE-PRESET-01 | On a fresh config, inspect Difficulty Preset and its three governed-value sections. Without restarting, select Tempered, Hardened, and Crucible in turn; edit one governed value, including Prevent Food Use In Combat; select Custom directly; reselect a named preset; then save and reload once with a named preset and once after a Custom edit. | Hardened is the default. Every named selection writes all 23 governed values and FoA Mod Manager refreshes to show the new live values. Editing any governed value selects Custom without changing the others; selecting Custom directly changes nothing. Selecting another named preset replaces the complete set. Named and Custom states persist across reload, while independent system enable toggles, diagnostics, presentation settings, player-arrow gravity, limits, clamps, and target-family terms remain unchanged. | Not run |
