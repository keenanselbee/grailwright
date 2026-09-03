# Battlecry Voice Tuner Test Matrix

Version under test: 1.4.3

## Focused release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| BVT-SMOKE-PRESET-01 | On a fresh config, inspect Demonic Voice Preset and its nine governed values. Without restarting, select Minimal, Demonic, and Abyssal in turn; edit one governed value; select Custom directly; reselect a named preset; then save and reload once with a named preset and once after a Custom edit. | Demonic is the default. Every named selection writes all nine governed values and FoA Mod Manager refreshes to show the new live values. Editing any governed value selects Custom without changing the others; selecting Custom directly changes nothing. Named and Custom states persist across reload. | Not run |
