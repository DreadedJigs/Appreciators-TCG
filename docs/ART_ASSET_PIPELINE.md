# Art Asset Pipeline

Phase 1 ships with runtime placeholders. Official card art can be added without changing gameplay code.

## Card Art Drop Folder

Place final card art PNG files here:

```text
unity-client/Assets/Resources/Art/Cards/
```

Each file should use the stable card id:

```text
ghost_companion.png
beer_helmet.png
the_original.png
```

Unity card data points to these files through each card's `artPath` field, for example:

```json
{
  "id": "ghost_companion",
  "artKey": "ghost_companion",
  "artPath": "Art/Cards/ghost_companion"
}
```

Do not include the `.png` extension in `artPath`; Unity `Resources.Load` expects the path without the extension.

## Recommended Card Art Spec

- Format: PNG
- Suggested source size: 1024 x 1408 px
- Safe important content area: center 80% width and center 78% height
- Background: opaque preferred
- File names: lowercase `snake_case`, matching `docs/ART_ASSET_MANIFEST.csv`
- One image per card

The runtime UI preserves aspect ratio and falls back to placeholder art if a card image is missing.

## Placeholder Art

Runtime placeholders live here:

```text
unity-client/Assets/Resources/Art/Placeholder/
```

These are intentionally temporary. Keep them in the repo so every card renders in editor, WebGL, and tests before final art is delivered.

## Import Notes

Unity can load these PNGs as `Texture2D` from `Resources`, so artists can drop files in and the game will work after Unity reimports. For final production polish, imported textures can be tuned later for compression and platform-specific size.

## Asset Replacement Checklist

1. Add PNGs to `unity-client/Assets/Resources/Art/Cards/`.
2. Confirm every file name matches `docs/ART_ASSET_MANIFEST.csv`.
3. Open Unity and let assets import.
4. Open `Assets/Scenes/Main.unity`.
5. Press Play and check Collection, Deck Builder, and Match hand cards.
6. Run EditMode tests.
7. Build WebGL.
