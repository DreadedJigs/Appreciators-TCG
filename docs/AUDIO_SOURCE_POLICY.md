# Audio source policy

## Active release audio

The active battle and Appreciation Ritual sound effects use a curated Pixabay
set documented in `PIXABAY_SFX_ATTRIBUTION.md`. Pixabay permits use and
modification inside games under its Content License, but the source files must
not be redistributed as a standalone sound library.

The former Kenney and GameSounds clips remain in the repository as legacy
fallbacks and alpha history. Live gameplay no longer maps to those clips.

## Clash Royale reference archive

`Henrylq/Clash-Royale-SFX` was reviewed as an interaction-audio reference only.
Its README says the files were extracted from the Clash Royale APK, and the
repository does not provide a reuse license. No files from that repository are
included in Appreciators TCG.

Do not add extracted third-party game audio to a distributable build. Replace
the prototype mappings with commissioned or explicitly licensed clips before a
commercial release.

## Runtime mappings

- Card selection and invalid action
- Card placement, attack, impact, and defeat
- Resource gain and spend
- Shield, rally, and end turn
- Ritual start and neutral seal break
- Standard and rare card reveal
- Duplicate conversion and ritual summary

Active runtime clips live in `unity-client/Assets/Resources/Audio/Battle/Pixabay/`.
