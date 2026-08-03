# Runtime Card Art

Drop final square PNG illustrations here using the exact card id as the filename,
for example `regular_body.png`.

- Recommended source: 2048 x 2048 PNG, sRGB
- Keep important faces/details inside the center 80 percent safe area
- Do not bake rules text, cost, power, or Appreciation into the illustration
- Unity renders card name, stats, rarity, lane, and approved effect data separately

Portrait mock-card sheets are supported as alpha references. The runtime detects
their tall aspect ratio and extracts the illustration window. Run
`scripts/generate-card-faces.ps1` after replacing art; it creates the complete
2:3 production face with a centered safe crop and no live UI overlays.
