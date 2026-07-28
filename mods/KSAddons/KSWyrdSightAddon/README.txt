Wyrd Sight Addon
Version 1.0.3

Companion addon for Wyrd Sight 1.1.1.

This addon changes Wyrd Sight's own Highlight Key from a toggle into a pulse:
press the configured Wyrd Sight Highlight Key once, Wyrd Sight turns on briefly,
then the addon turns off only the pulse it started. Wyrd Sight's normal fade
handles the fade-out.

The addon does not edit or enforce Wyrd Sight's visual settings. For the intended
clean pulse look, Wyrd Sight works best with:

Enable Wyrd Sight Particles = false
Enable Glow Shape = true

Safety behavior:
- If Wyrd Sight was already on from outside the addon, the Highlight Key will not
  turn it off. The addon only turns off pulses it started.
- Pressing the Highlight Key during an addon-owned pulse extends the pulse timer.
- The addon suppresses Wyrd Sight's original Highlight Key toggle while enabled.
- If Wyrd Sight changes its private input/toggle methods in a future update, the
  addon logs a warning and lets Wyrd Sight's original input handling continue.

Config file: BepInEx/config/ks.tgfoa.wyrd-sight-addon.cfg

Defaults:

ConfigSchemaVersion = 2
Enabled = true
PulseDurationSeconds = 3
PulseStateCheckIntervalSeconds = 0.25
OffRetryDelaySeconds = 0.25
MaximumOffAttempts = 3
Diagnostics = false

Pulse timing lives in the addon's own config, not Wyrd Sight's config. Lower
PulseDurationSeconds for a shorter flash, or raise it for a longer scan. The
state-check and off-retry timing defaults are conservative; change them only if
the parent mod needs slower or faster pulse ownership handling.

Version 1.0.3 appears as Wyrd Sight Addon in BepInEx and Configuration
Manager while keeping the existing ks.tgfoa.wyrd-sight-addon.cfg config path.
It still uses ConfigSchemaVersion 2. Older configs are backed up and a fresh
default config is regenerated when the schema changes.

Requires BepInEx 5 Mono and Wyrd Sight 1.1.1.
