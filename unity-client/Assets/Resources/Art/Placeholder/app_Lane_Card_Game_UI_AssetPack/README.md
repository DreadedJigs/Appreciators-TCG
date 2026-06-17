# Dreaded Lane Card Game UI Asset Pack

This folder is a prototype asset pack sliced from the generated lane-style card game UI mockup.

## What is included

- `00_full_mockup/` — full UI target screenshot.
- `01_cards/full_cards_rect/` — lossless rectangular crops of visible cards.
- `01_cards/full_cards_alpha_approx/` — approximate transparent/rounded crops for quick in-engine use.
- `02_card_art/portraits/` — character portrait crops from card art windows.
- `03_hud/` — player and opponent HUD panels, nameplates, health, and resource bars.
- `04_ui/` — buttons, card backs, deck/objective panels, resource widgets.
- `05_board/background_crops/` — board/backdrop crops from the mockup.
- `07_style_references/` — NFT style references used to keep the build on-brand.
- `08_metadata/asset_manifest.json` — asset list with dimensions and source crop coordinates.
- `08_metadata/palette.json` — core color palette.
- `08_metadata/codex_import_prompt.txt` — prompt to paste into Codex with the asset folder.
- `06_spritesheets/` — generated spritesheet and atlas data.

## Important production note

These assets are sliced from a rendered mockup, so some background pixels may remain around sprites. They are suitable for Codex prototyping, Unity UI layout, and gameplay testing. For final production, redraw/export each component from vector or layered source files.

## Recommended Unity import settings

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single` for individual PNG files, `Multiple` for the spritesheet
- Mesh Type: `Full Rect`
- Filter Mode: `Point` or `Bilinear` depending on whether you want crisper flat art or smoother UI scaling
- Compression: `None` during prototyping, then optimize later

## Fast build workflow

1. Drop this folder into `Assets/Art/DreadedLaneCardGame/`.
2. Use `00_full_mockup/ui_mockup_fullscreen.png` as the UI reference.
3. Build prefabs from `01_cards`, `03_hud`, and `04_ui`.
4. Use `08_metadata/codex_import_prompt.txt` as the Codex task prompt.
