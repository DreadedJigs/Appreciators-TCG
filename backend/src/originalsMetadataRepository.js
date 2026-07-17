import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const traitsPath = resolve(__dirname, "../data/metadata/appreciators-originals.traits.json");
const tokensPath = resolve(__dirname, "../data/metadata/appreciators-originals.tokens.json");

let traitsCatalog;
let tokensById;

export function getOriginalsTraitCatalog() {
  if (!traitsCatalog) {
    traitsCatalog = readJson(traitsPath, "Appreciators Originals trait catalog");
  }
  return traitsCatalog;
}

export function getOriginalsTokenMetadata(tokenId) {
  const parsedTokenId = Number(tokenId);
  if (!Number.isInteger(parsedTokenId) || parsedTokenId < 1) {
    throw requestError("Token ID must be a positive integer.", 400, "INVALID_ORIGINAL_TOKEN_ID");
  }

  ensureTokenIndex();
  const token = tokensById.get(parsedTokenId);
  if (!token) {
    throw requestError(`Appreciators Original #${parsedTokenId} was not found in the imported catalog.`, 404, "ORIGINAL_TOKEN_NOT_FOUND");
  }
  return token;
}

function ensureTokenIndex() {
  if (tokensById) {
    return;
  }

  const document = readJson(tokensPath, "Appreciators Originals token catalog");
  if (!Array.isArray(document.tokens)) {
    throw new Error("Appreciators Originals token catalog is missing its tokens array.");
  }
  tokensById = new Map(document.tokens.map((token) => [token.tokenId, token]));
}

function readJson(path, label) {
  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    throw new Error(`${label} could not be loaded from ${path}: ${error.message}`);
  }
}

function requestError(message, statusCode, errorCode) {
  return Object.assign(new Error(message), { statusCode, errorCode });
}
