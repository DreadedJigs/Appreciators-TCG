# Card Art Provenance

The alpha uses the active 24-card non-Companion gameplay catalog. Art sources do not add
traits or change rules.

## Official front and back

The active front layout uses the supplied Pathetic Kid production template at
`Assets/Resources/Art/Official/CardTemplate/templates/full_card_template_blank.png`.
The artist's frame owns the card geometry. `scripts/generate-card-faces.ps1`
composes each active card into a single 2048 x 3072 production texture containing
its clean lane plaques, metadata illustration, name, cost, and approved ability.
Unity displays that one baked texture; it does not layer live lane values, names,
costs, or rules over an older card face. Generated faces live under
`Assets/Resources/Art/Official/GeneratedCards`.

The source component layers remain beside the template for later art-pipeline
work. Earlier card-front approximations and placeholder portrait-pack designs are
not used when a generated face exists. Unity import settings preserve the exact
2:3 non-power-of-two ratio instead of rescaling faces to a clipping-prone size.

The official `app_card_reverse` asset remains the only runtime card back.

## Metadata art

Run `node scripts/import-metadata-card-art.mjs` to refresh metadata illustrations
from the cached Appreciators Originals metadata catalog. Exact trait matches are
preferred. Alpha-only proxies are explicitly labeled in the import script:

- `regular_body`: token 2, standard body-state proxy
- `blockchain_background`: token 5028, Slate/Space Suit/Astro Helmet proxy
- Skin cards: Mouthwash, Chill, Bruise, Blush, Nicotine, and Chalk color proxies
- `the_original`: token 2458, collection 1/1 OG Sid proxy

Replace any proxy by dropping a square PNG at:

`unity-client/Assets/Resources/Art/Cards/<card-id>.png`

No gameplay code or card data needs to change when final art arrives.

After replacing illustrations or changing card text/stats, regenerate the faces:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/generate-card-faces.ps1
```

Companion metadata mappings and source images are retained only as provenance for
future design decisions. Companion cards and the Fish token are excluded from the
active catalog, starter decks, AI decks, collection, pack rewards, and battle flow.
