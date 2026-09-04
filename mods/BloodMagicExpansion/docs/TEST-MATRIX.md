# Blood Magic Expansion Test Matrix

Version under test: 3.2.8

## Focused release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| BME-SMOKE-PRESET-01 | On a fresh config, inspect Preset and its five ritual-economy values. Without restarting, select Blood Rite, Desecration, and Exsanguination in turn; confirm Blood/Life and Abhartach strength settings do not change; edit one governed value; select Custom directly; reselect a named preset; then save and reload once with a named preset and once after a Custom edit. | Desecration is the default. Every named selection writes only corpse XP, ritual time, live-drain tick time, live-drain XP per tick, and the per-target cap; FoA Mod Manager refreshes to show those live values. Blood Essence remains the sole spell-strength progression. Editing any governed value selects Custom without changing the others; selecting Custom directly changes nothing. Named and Custom states persist across reload. | Not run |
| BME-SMOKE-CAST-01 | Equip Blood Transfusion and Life Transfusion in both hand orders. Hold both casts, then release together, main hand first, and offhand first; immediately tap-cast and hold-cast again with each hand after every release. Repeat once with the same Transfusion spell in both hands, including several quick held-to-tap transitions. | Each released hand leaves MagicHeavyLoop through MagicHeavyEnd, every performed tap cast leaves MagicLightInitial for Idle or its normal light sequence, and both hands can cast again without changing weapons. The other hand remains independent; cast-speed bonuses remain active, and damage, healing, costs, corpse rewards, and live-drain rewards occur only once. | Not run |
