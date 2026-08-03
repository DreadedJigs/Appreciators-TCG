# Appreciators Official Style Guide Drop

The supplied color and typography reference JPGs are stored in this folder and
the runtime palette is centralized in `UIFactory.cs`.

Preferred filenames:

- `AppreciatorsDisplay.ttf` or `AppreciatorsDisplay.otf` - automatically becomes the runtime UI font.
- `AppreciatorsBody.ttf` or `AppreciatorsBody.otf` - reserved for body copy once supplied.
- `appcolorpalette.jpg` - current official color palette reference.
- `appreciators_font_reference.jpg` - current Aktiv Grotesk usage reference.
- `appreciators_style_guide.png` or `.pdf` - combined typography and color sheet.

Keep original source files alongside exported PNG references. Do not overwrite placeholder art; production UI, card frames, board art, icons, and card illustrations belong under sibling folders inside `Assets/Resources/Art/Official/`.

The reference JPG cannot be used as a runtime font. Drop the licensed Aktiv
Grotesk font export here as `AppreciatorsDisplay.ttf` when it is available; the
existing loader will select it automatically on the next Unity import.
