Killing Blow Mastery Runtime Audio

Files in this folder are loaded by the mod at runtime. Killing Blow Mastery
loads only WAV files that match a supported numbered pool name, such as:

killing_blow1.wav ... killing_blow5.wav
one_handed_blade1.wav ... one_handed_blade5.wav
two_handed_blunt1.wav ... two_handed_blunt5.wav
shield_bash1.wav ... shield_bash5.wav
non_corporeal1.wav ... non_corporeal5.wav
magic_blood1.wav ... magic_blood5.wav
goat.wav for FinisherSoundMode = GoatTest

Bloodless target variants append _dry before .wav after the slot number:

one_handed_blade1_dry.wav ... one_handed_blade5_dry.wav
two_handed_blunt1_dry.wav ... two_handed_blunt5_dry.wav
magic_blood1_dry.wav ... magic_blood5_dry.wav
killing_blow1_dry.wav ... killing_blow5_dry.wav

Fallback order is classified kills -> specific pool only. Broad one_handed,
two_handed, archery, shield, and magic fallback pools are not used in this
release.
Matched non-corporeal enemies use non_corporeal1.wav through
non_corporeal5.wav only, without weapon, magic, or _dry variants.
The unnumbered killing_blow.wav legacy fallback is not used.
The killing_blow pool is used for truly unclassified kills, or when
UseKillingBlowFallbackForClassifiedKills is enabled.

Set FinisherSoundMode = Soulslike to ignore category pools and always play
killing_blow1.wav for every awarded killing blow. Set FinisherSoundMode = Off
to disable reward sounds.

When UseBloodlessSoundVariants is true, skeletons, stone bodies, spirits, Wyrd
variants, and other matched bloodless targets try their matching _dry
pool before the normal pool. This affects sound routing only.

Use tools/audio/Convert-RewardSounds.ps1 to prepare 44.1 kHz 16-bit PCM WAV
files with leading-silence trimming and peak normalization.
Before redistributing replacement sounds publicly, verify source licenses and
credit requirements.
