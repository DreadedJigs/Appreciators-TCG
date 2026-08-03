import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const ROOT_DIR = resolve(SCRIPT_DIR, "..");
const CONTRACT_ADDRESS = "0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd";
const CHAIN_ID = 33139;
const COLLECTION_NAME = "Appreciators Originals";
const COLLECTION_SYMBOL = "OG";
const TOTAL_SUPPLY = 6666;
const METADATA_BASE_URL = "https://originals-metadata.s3.us-east-2.amazonaws.com/metadata";
const IMAGE_BASE_URL = "https://originals-metadata.s3.us-east-2.amazonaws.com/images";
const EXPLORER_URL = `https://apescan.io/address/${CONTRACT_ADDRESS}`;
const EXCLUDED_TERM = "dreaded ape";
const CONCURRENCY = Math.max(1, Math.min(64, Number(process.env.METADATA_CONCURRENCY || 24)));
const RETRIES = Math.max(1, Math.min(8, Number(process.env.METADATA_RETRIES || 4)));

const BACKEND_METADATA_DIR = resolve(ROOT_DIR, "backend/data/metadata");
const UNITY_METADATA_DIR = resolve(ROOT_DIR, "unity-client/Assets/Resources/Metadata");
const CONTRACT_OUTPUT = resolve(BACKEND_METADATA_DIR, "appreciators-originals.contract.json");
const TOKENS_OUTPUT = resolve(BACKEND_METADATA_DIR, "appreciators-originals.tokens.json");
const TRAITS_OUTPUT = resolve(BACKEND_METADATA_DIR, "appreciators-originals.traits.json");
const UNITY_OUTPUT = resolve(UNITY_METADATA_DIR, "appreciators-originals-traits.json");
const AUDIT_OUTPUT = resolve(ROOT_DIR, "docs/APPRECIATORS_ORIGINALS_METADATA_AUDIT.md");

const APPROVED_TRAITS = [
  rule("ghost_companion", "Ghost Companion", "Companion", ["Companion"], ["Ghost Companion", "Ghost"]),
  rule("pigeon_companion", "Pigeon Companion", "Companion", ["Companion"], ["Pigeon Companion", "Pigeon"]),
  rule("cat_companion", "Cat Companion", "Companion", ["Companion"], ["Cat Companion", "Cat"]),
  rule("devil_dog_companion", "Devil Dog Companion", "Companion", ["Companion"], ["Devil Dog Companion", "Devil Dog", "DevilDog"]),
  rule("snake_companion", "Snake Companion", "Companion", ["Companion"], ["Snake Companion", "Snake"]),
  rule("great_white_head", "Great White Head", "Head", ["Head"], ["Great White"]),
  rule("tiger_shark_head", "Tiger Shark Head", "Head", ["Head"], ["Tiger Shark"]),
  rule("unicorn_head", "Unicorn Head", "Head", ["Head"], ["Unicorn"]),
  rule("alpha_kaiju_head", "Alpha Kaiju Head", "Head", ["Head"], ["Alpha Kaiju"]),
  rule("regular_body", "Regular Body", "Body", ["Body State"], ["Regular Body", "Regular"]),
  rule("no_head_body", "No Head Body", "Body", ["Body State"], ["No Head", "Headless"]),
  rule("decapitated_body", "Decapitated Body", "Body", ["Body State"], ["Decapitated"]),
  rule("blockchain_background", "Blockchain Background", "Background", ["Background"], ["Blockchain"]),
  rule("ghost_flame_background", "Ghost Flame Background", "Background", ["Background"], ["Ghost Flame"]),
  rule("pink_lemonade_background", "Pink Lemonade Background", "Background", ["Background"], ["Pink Lemonade"]),
  rule("tropical_background", "Tropical Background", "Background", ["Background"], ["Tropical"]),
  rule("overcast_background", "Overcast Background", "Background", ["Background"], ["Overcast"]),
  rule("second_hand_smoke_dawn", "Second Hand Smoke Dawn", "Background", ["Background"], ["Second Hand Smoke Dawn"]),
  rule("second_hand_smoke_seafoam", "Second Hand Smoke Seafoam", "Background", ["Background"], ["Second Hand Smoke Seafoam"]),
  rule("green_skin", "Green Skin", "Skin Tone", ["Skin", "GRP Color"], ["Green"]),
  rule("blue_skin", "Blue Skin", "Skin Tone", ["Skin", "GRP Color"], ["Blue"]),
  rule("purple_skin", "Purple Skin", "Skin Tone", ["Skin", "GRP Color"], ["Purple"]),
  rule("pink_skin", "Pink Skin", "Skin Tone", ["Skin", "GRP Color"], ["Pink"]),
  rule("yellow_skin", "Yellow Skin", "Skin Tone", ["Skin", "GRP Color"], ["Yellow"]),
  rule("white_skin", "White Skin", "Skin Tone", ["Skin", "GRP Color"], ["White"]),
  rule("chaos", "CHAOS", "Special 1/1", ["Name", "Special", "1/1"], ["CHAOS"], true),
  rule("captain_fish_food", "CAPTAIN FISH FOOD", "Special 1/1", ["Name", "Special", "1/1"], ["CAPTAIN FISH FOOD"], true),
  rule("the_original", "THE ORIGINAL", "Special 1/1", ["Name", "Special", "1/1"], ["THE ORIGINAL"], true)
];

async function main() {
  const limit = parseLimit(process.argv.slice(2));
  const tokenIds = Array.from({ length: limit }, (_, index) => index + 1);
  console.log(`Importing ${tokenIds.length} ${COLLECTION_NAME} metadata records with concurrency ${CONCURRENCY}...`);

  const rawTokens = await mapConcurrent(tokenIds, CONCURRENCY, fetchTokenMetadata);
  const failed = rawTokens.filter((entry) => entry.error);
  if (failed.length > 0) {
    const failedIds = failed.map((entry) => entry.tokenId).join(", ");
    throw new Error(`Metadata import failed for ${failed.length} token(s): ${failedIds}`);
  }

  const normalizedTokens = rawTokens.map(normalizeTokenMetadata);
  const excludedTokens = normalizedTokens.filter(containsForbiddenTerm);
  const tokens = normalizedTokens.filter((token) => !containsForbiddenTerm(token));
  const generatedAt = new Date().toISOString();
  const collection = collectionSummary();
  const traitTypes = aggregateTraits(tokens);
  const approvedGameplayTraits = mapApprovedGameplayTraits(tokens);
  const gameplayCardAudit = await auditGameplayCardDefinitions(approvedGameplayTraits);

  const contractDocument = {
    version: 1,
    generatedAt,
    collection,
    importPolicy: {
      metadataOnly: true,
      imagesDownloaded: false,
      excludedTerms: ["Dreaded Ape"],
      gameplayTraitsRestrictedToApprovedList: true
    }
  };
  const tokensDocument = {
    version: 1,
    generatedAt,
    collection: compactCollection(collection),
    tokenCount: tokens.length,
    excludedTokenCount: excludedTokens.length,
    tokens
  };
  const traitsDocument = {
    version: 1,
    generatedAt,
    collection,
    summary: {
      requestedTokenCount: limit,
      importedTokenCount: tokens.length,
      excludedTokenCount: excludedTokens.length,
      traitTypeCount: traitTypes.length,
      uniqueTraitValueCount: traitTypes.reduce((sum, trait) => sum + trait.uniqueValueCount, 0)
    },
    approvedGameplayTraits,
    traitTypes
  };
  const unityDocument = {
    version: 1,
    generatedAt,
    chainId: CHAIN_ID,
    contractAddress: CONTRACT_ADDRESS,
    collectionName: COLLECTION_NAME,
    symbol: COLLECTION_SYMBOL,
    totalSupply: TOTAL_SUPPLY,
    importedTokenCount: tokens.length,
    excludedTokenCount: excludedTokens.length,
    approvedGameplayTraits,
    traitTypes
  };

  await Promise.all([
    writeJson(CONTRACT_OUTPUT, contractDocument),
    writeJson(TOKENS_OUTPUT, tokensDocument),
    writeJson(TRAITS_OUTPUT, traitsDocument),
    writeJson(UNITY_OUTPUT, unityDocument),
    writeMarkdownAudit(AUDIT_OUTPUT, traitsDocument, gameplayCardAudit)
  ]);

  console.log(`Imported ${tokens.length} tokens; excluded ${excludedTokens.length}.`);
  console.log(`Found ${traitTypes.length} trait types and ${traitsDocument.summary.uniqueTraitValueCount} unique values.`);
  console.log(`Approved gameplay matches: ${approvedGameplayTraits.filter((entry) => entry.status === "matched").length}/${APPROVED_TRAITS.length}.`);
  console.log(`Wrote ${TRAITS_OUTPUT}`);
  console.log(`Wrote ${UNITY_OUTPUT}`);
}

function rule(gameplayId, displayName, gameplayGroup, sourceTraitTypes, aliases, searchTokenName = false) {
  return { gameplayId, displayName, gameplayGroup, sourceTraitTypes, aliases, searchTokenName };
}

function parseLimit(args) {
  const argument = args.find((value) => value.startsWith("--limit="));
  if (!argument) {
    return TOTAL_SUPPLY;
  }

  const value = Number(argument.split("=")[1]);
  if (!Number.isInteger(value) || value < 1 || value > TOTAL_SUPPLY) {
    throw new Error(`--limit must be between 1 and ${TOTAL_SUPPLY}.`);
  }
  return value;
}

async function fetchTokenMetadata(tokenId) {
  const metadataUrl = `${METADATA_BASE_URL}/${tokenId}.json`;
  let lastError;
  for (let attempt = 1; attempt <= RETRIES; attempt += 1) {
    try {
      const response = await fetch(metadataUrl, {
        headers: { Accept: "application/json", "User-Agent": "Appreciators-TCG-Metadata-Importer/1.0" },
        signal: AbortSignal.timeout(20_000)
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      return { tokenId, metadataUrl, metadata: await response.json() };
    } catch (error) {
      lastError = error;
      if (attempt < RETRIES) {
        await delay(150 * attempt * attempt);
      }
    }
  }

  return { tokenId, metadataUrl, error: lastError?.message || "Unknown metadata error" };
}

function normalizeTokenMetadata(entry) {
  const metadata = entry.metadata || {};
  const attributes = Array.isArray(metadata.attributes)
    ? metadata.attributes
        .map((attribute) => ({
          traitType: cleanText(attribute?.trait_type ?? attribute?.traitType),
          value: cleanText(attribute?.value)
        }))
        .filter((attribute) => attribute.traitType && attribute.value)
    : [];

  return {
    tokenId: entry.tokenId,
    name: cleanText(metadata.name) || `${COLLECTION_NAME} #${entry.tokenId}`,
    description: cleanText(metadata.description),
    image: cleanText(metadata.image) || `${IMAGE_BASE_URL}/${entry.tokenId}.png`,
    externalUrl: cleanText(metadata.external_url ?? metadata.externalUrl),
    metadataUrl: entry.metadataUrl,
    attributes
  };
}

function aggregateTraits(tokens) {
  const traitMap = new Map();
  for (const token of tokens) {
    for (const attribute of token.attributes) {
      if (!traitMap.has(attribute.traitType)) {
        traitMap.set(attribute.traitType, new Map());
      }
      const values = traitMap.get(attribute.traitType);
      const current = values.get(attribute.value) || { value: attribute.value, count: 0, sampleTokenIds: [] };
      current.count += 1;
      if (current.sampleTokenIds.length < 10) {
        current.sampleTokenIds.push(token.tokenId);
      }
      values.set(attribute.value, current);
    }
  }

  return [...traitMap.entries()]
    .map(([traitType, values]) => {
      const sortedValues = [...values.values()].sort((a, b) => b.count - a.count || a.value.localeCompare(b.value));
      return {
        traitType,
        tokenCount: sortedValues.reduce((sum, value) => sum + value.count, 0),
        uniqueValueCount: sortedValues.length,
        values: sortedValues
      };
    })
    .sort((a, b) => b.tokenCount - a.tokenCount || a.traitType.localeCompare(b.traitType));
}

function mapApprovedGameplayTraits(tokens) {
  return APPROVED_TRAITS.map((approved) => {
    const matches = [];
    const matchedValues = new Map();
    for (const token of tokens) {
      let matched = false;
      for (const attribute of token.attributes) {
        if (!approved.sourceTraitTypes.some((type) => equalsIgnoreCase(type, attribute.traitType))) {
          continue;
        }
        if (!approved.aliases.some((alias) => includesWords(attribute.value, alias))) {
          continue;
        }
        matched = true;
        const key = `${attribute.traitType}:${attribute.value}`;
        const existing = matchedValues.get(key) || {
          traitType: attribute.traitType,
          value: attribute.value,
          tokenCount: 0
        };
        existing.tokenCount += 1;
        matchedValues.set(key, existing);
      }

      if (approved.searchTokenName && approved.aliases.some((alias) => includesWords(token.name, alias))) {
        matched = true;
        const key = `Name:${token.name}`;
        const existing = matchedValues.get(key) || { traitType: "Name", value: token.name, tokenCount: 0 };
        existing.tokenCount += 1;
        matchedValues.set(key, existing);
      }
      if (matched) {
        matches.push(token.tokenId);
      }
    }

    return {
      gameplayId: approved.gameplayId,
      displayName: approved.displayName,
      gameplayGroup: approved.gameplayGroup,
      status: matches.length > 0 ? "matched" : "not_present_in_originals_metadata",
      sourceTraitTypes: approved.sourceTraitTypes,
      aliases: approved.aliases,
      tokenCount: matches.length,
      sampleTokenIds: matches.slice(0, 20),
      matchedValues: [...matchedValues.values()].sort((a, b) => b.tokenCount - a.tokenCount || a.value.localeCompare(b.value))
    };
  });
}

async function auditGameplayCardDefinitions(approvedGameplayTraits) {
  const cardsPath = resolve(ROOT_DIR, "backend/data/cards.json");
  const parsed = JSON.parse(await readFile(cardsPath, "utf8"));
  const cardIds = new Set((parsed.cards || []).map((card) => card.id));
  const approvedIds = new Set(approvedGameplayTraits.map((entry) => entry.gameplayId));
  return {
    missingApprovedCardIds: [...approvedIds].filter((id) => !cardIds.has(id)),
    unapprovedCardIds: [...cardIds].filter((id) => !approvedIds.has(id)),
    cardCount: cardIds.size
  };
}

function containsForbiddenTerm(value) {
  return JSON.stringify(value).toLowerCase().includes(EXCLUDED_TERM);
}

function includesWords(value, alias) {
  const normalizedValue = normalizeForMatch(value);
  const normalizedAlias = normalizeForMatch(alias);
  return normalizedValue === normalizedAlias || normalizedValue.includes(normalizedAlias);
}

function equalsIgnoreCase(left, right) {
  return normalizeForMatch(left) === normalizeForMatch(right);
}

function normalizeForMatch(value) {
  return cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
}

function cleanText(value) {
  return typeof value === "string" ? value.trim() : value == null ? "" : String(value).trim();
}

function collectionSummary() {
  return {
    name: COLLECTION_NAME,
    symbol: COLLECTION_SYMBOL,
    chain: "ApeChain Mainnet",
    chainId: CHAIN_ID,
    contractAddress: CONTRACT_ADDRESS,
    standard: "ERC-721",
    totalSupply: TOTAL_SUPPLY,
    tokenIdStart: 1,
    tokenIdEnd: TOTAL_SUPPLY,
    explorerUrl: EXPLORER_URL,
    metadataBaseUrl: METADATA_BASE_URL,
    imageBaseUrl: IMAGE_BASE_URL
  };
}

function compactCollection(collection) {
  return {
    name: collection.name,
    symbol: collection.symbol,
    chainId: collection.chainId,
    contractAddress: collection.contractAddress,
    totalSupply: collection.totalSupply
  };
}

async function writeJson(path, value) {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function writeMarkdownAudit(path, catalog, cardAudit) {
  const lines = [
    "# Appreciators Originals Metadata Audit",
    "",
    `Generated: ${catalog.generatedAt}`,
    "",
    "## Contract",
    "",
    `- Collection: ${COLLECTION_NAME} (${COLLECTION_SYMBOL})`,
    `- ApeChain mainnet chain ID: ${CHAIN_ID}`,
    `- Contract: [${CONTRACT_ADDRESS}](${EXPLORER_URL})`,
    `- Standard: ERC-721`,
    `- Total supply: ${TOTAL_SUPPLY.toLocaleString()}`,
    `- Imported tokens: ${catalog.summary.importedTokenCount.toLocaleString()}`,
    `- Excluded by policy: ${catalog.summary.excludedTokenCount.toLocaleString()}`,
    "- Images are referenced by URL and are not copied into the repository.",
    "",
    "## Import Policy",
    "",
    "Only the gameplay traits explicitly approved for Appreciators TCG are mapped to gameplay card IDs. Other on-chain attributes remain catalog metadata and do not automatically become mechanics. Any token or metadata containing `Dreaded Ape` is excluded case-insensitively.",
    "",
    "## Approved Gameplay Mapping",
    "",
    "| Gameplay card | Group | Status | Matching tokens | On-chain values |",
    "|---|---|---:|---:|---|"
  ];

  for (const entry of catalog.approvedGameplayTraits) {
    const values = entry.matchedValues.length > 0
      ? entry.matchedValues.map((value) => `${value.traitType}: ${value.value} (${value.tokenCount})`).join("; ")
      : "None";
    lines.push(`| ${entry.displayName} | ${entry.gameplayGroup} | ${entry.status} | ${entry.tokenCount} | ${values} |`);
  }

  lines.push(
    "",
    "## Catalog Summary",
    "",
    `- Trait types: ${catalog.summary.traitTypeCount}`,
    `- Unique trait values: ${catalog.summary.uniqueTraitValueCount}`,
    `- Gameplay definitions audited: ${cardAudit.cardCount}`,
    `- Missing approved gameplay definitions: ${cardAudit.missingApprovedCardIds.length ? cardAudit.missingApprovedCardIds.join(", ") : "None"}`,
    `- Unapproved gameplay definitions: ${cardAudit.unapprovedCardIds.length ? cardAudit.unapprovedCardIds.join(", ") : "None"}`,
    "",
    "## Refresh",
    "",
    "From the repository root:",
    "",
    "```powershell",
    "node scripts/import-apechain-originals.mjs",
    "```",
    "",
    "This metadata snapshot is suitable for local ownership previews and future wallet sync. Production ownership verification must query ApeChain or a trusted indexer server-side rather than trusting client data."
  );

  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, `${lines.join("\n")}\n`, "utf8");
}

async function mapConcurrent(items, concurrency, worker) {
  const results = new Array(items.length);
  let nextIndex = 0;
  let completed = 0;
  const runners = Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    while (true) {
      const index = nextIndex;
      nextIndex += 1;
      if (index >= items.length) {
        return;
      }
      results[index] = await worker(items[index]);
      completed += 1;
      if (completed % 500 === 0 || completed === items.length) {
        console.log(`Fetched ${completed}/${items.length}`);
      }
    }
  });
  await Promise.all(runners);
  return results;
}

function delay(milliseconds) {
  return new Promise((resolvePromise) => setTimeout(resolvePromise, milliseconds));
}

main().catch((error) => {
  console.error(error.stack || error.message || error);
  process.exitCode = 1;
});
