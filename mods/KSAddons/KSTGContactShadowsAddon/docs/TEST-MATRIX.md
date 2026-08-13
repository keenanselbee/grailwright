# KS Contact Shadows Addon Test Matrix

## Release smoke

| ID | Check | Expected result | Status |
| --- | --- | --- | --- |
| CONTACT-01 | Load an interior with Contact Shadows 1.0.0-mono and the addon enabled. | Version 0.1.3 loads without an exception and Diagnostics reports an Interior context. | Not run |
| CONTACT-02 | Inspect an interior containing more than four nearby lights with Diagnostics enabled. | Up to four active point or spot lights are selected; inactive, disabled, directional, and area lights are ignored. | Not run |
| CONTACT-03 | Slowly turn between similarly influential interior lights after all four slots are filled. | Each valid light remains selected until its hold time expires, and a challenger replaces only the weakest selection after exceeding the configured advantage. | Not run |
| CONTACT-04 | Revisit the Horns of the South fortress gate and another large castle interior. | Contact-shadow flicker is materially reduced compared with the parent mod alone, without obvious loss of nearby grounding. | Not run |
| CONTACT-05 | Move more than 15 meters from one or more selected lights. | Each ineligible light's exact original HDRP contact-shadow values return and other valid nearby lights may take over. | Not run |
| CONTACT-06 | Walk from an interior into the open world with InteriorsOnly true. | All selected lights, touched camera frame settings, and the parent global volume are restored or removed immediately. | Not run |
| CONTACT-07 | Return indoors. | Contact shadows resume after the playable interior initializes, with up to four fresh stable selections. | Not run |
| CONTACT-08 | Toggle the parent mod off and on with its hotkey and Grail Floating Text installed, then repeat with ShowToggleNotifications false. | All state restores while off and stable management resumes while on; one `Contact Shadows: Disabled/Enabled (interiors only)` System notification confirms each actual change while notifications are enabled, and none appears while disabled. | Not run |
| CONTACT-09 | Toggle the addon Enabled setting off and on. | Off restores the parent mod's normal behavior; on retakes stable control without leaving stale light or camera state. | Not run |
| CONTACT-10 | Set InteriorsOnly false and visit outdoor daylight and nighttime scenes. | The same configured point/spot light budget works outdoors without enabling the directional sun. | Not run |
| CONTACT-11 | Compare SampleCount 16 and 8 in the same dense interior. | Sixteen is cleaner; eight lowers the effect's cost without changing selection or restoration behavior. | Not run |
| CONTACT-12 | Edit TGContactShadows.json while playing. | The parent reloads its file, the addon reapplies only its runtime visual values, and the JSON remains unchanged on disk. | Not run |
| CONTACT-13 | Run with KS All Lights Cast Shadows Addon and KS Global Illumination Addon. | Each addon controls only its own effect; no exceptions, stuck shadows, or config-file edits occur. | Not run |
| CONTACT-14 | Disable or unload the addon while several lights are selected. | Every selected light and captured camera setting returns to its exact pre-addon values, and the parent can resume normally. | Not run |
