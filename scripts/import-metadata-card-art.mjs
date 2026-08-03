import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const catalogPath = path.join(root, "backend", "data", "metadata", "appreciators-originals.tokens.json");
const outputDirectory = path.join(root, "unity-client", "Assets", "Resources", "Art", "Cards");

// Artist-supplied mock cards are intentionally absent: those files remain the
// preferred art source. Entries marked proxy use real collection metadata but
// represent an alpha visual stand-in until the matching final illustration lands.
const cardSources = {
  cat_companion: { tokenId: 36, status: "matched" },
  great_white_head: { tokenId: 23, status: "matched" },
  tiger_shark_head: { tokenId: 93, status: "matched" },
  unicorn_head: { tokenId: 32, status: "matched" },
  alpha_kaiju_head: { tokenId: 96, status: "matched" },
  regular_body: { tokenId: 2, status: "proxy: standard body state" },
  no_head_body: { tokenId: 1, status: "matched" },
  decapitated_body: { tokenId: 9, status: "matched" },
  blockchain_background: { tokenId: 5028, status: "proxy: Slate + Space Suit + Astro Helmet" },
  ghost_flame_background: { tokenId: 15, status: "matched: 3D Ghost background" },
  pink_lemonade_background: { tokenId: 12, status: "matched" },
  tropical_background: { tokenId: 1, status: "matched" },
  overcast_background: { tokenId: 3, status: "matched" },
  second_hand_smoke_dawn: { tokenId: 65, status: "matched" },
  second_hand_smoke_seafoam: { tokenId: 42, status: "matched" },
  green_skin: { tokenId: 19, status: "proxy: Mouthwash skin" },
  blue_skin: { tokenId: 4, status: "proxy: Chill skin" },
  purple_skin: { tokenId: 14, status: "proxy: Bruise skin" },
  pink_skin: { tokenId: 9, status: "proxy: Blush skin" },
  yellow_skin: { tokenId: 1, status: "proxy: Nicotine skin" },
  white_skin: { tokenId: 8, status: "proxy: Chalk skin" },
  chaos: { tokenId: 6239, status: "matched" },
  captain_fish_food: { tokenId: 1618, status: "matched" },
  the_original: { tokenId: 2458, status: "proxy: collection 1/1 OG Sid" }
};

const catalog = JSON.parse(await readFile(catalogPath, "utf8"));
const tokensById = new Map(catalog.tokens.map((token) => [Number(token.tokenId), token]));
await mkdir(outputDirectory, { recursive: true });

for (const [cardId, source] of Object.entries(cardSources)) {
  const token = tokensById.get(source.tokenId);
  if (!token?.image) {
    throw new Error(`Missing metadata image for ${cardId} (token ${source.tokenId}).`);
  }

  const destination = path.join(outputDirectory, `${cardId}.png`);
  const temporary = `${destination}.download`;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 20_000);

  try {
    const response = await fetch(token.image, {
      signal: controller.signal,
      headers: { "User-Agent": "Appreciators-TCG-Alpha-Art-Importer/1.0" }
    });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }

    await writeFile(temporary, Buffer.from(await response.arrayBuffer()));
    await rename(temporary, destination);
    console.log(`Imported ${cardId} <- token ${source.tokenId} (${source.status})`);
  } catch (error) {
    await rm(temporary, { force: true });
    throw new Error(`Failed to import ${cardId} from ${token.image}: ${error.message}`);
  } finally {
    clearTimeout(timer);
  }
}

console.log(`Imported ${Object.keys(cardSources).length} metadata-backed card images.`);
