import test from "node:test";
import assert from "node:assert/strict";
import { createApp } from "../src/createApp.js";
import { getOriginalsTokenMetadata, getOriginalsTraitCatalog } from "../src/originalsMetadataRepository.js";

function listen(app) {
  return new Promise((resolve) => {
    const server = app.listen(0, () => resolve(server));
  });
}

async function request(server, path) {
  const address = server.address();
  const response = await fetch(`http://127.0.0.1:${address.port}${path}`);
  return { response, body: await response.json() };
}

test("on-chain catalog contains the complete filtered Originals snapshot", () => {
  const catalog = getOriginalsTraitCatalog();
  assert.equal(catalog.collection.chainId, 33139);
  assert.equal(catalog.collection.contractAddress, "0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd");
  assert.equal(catalog.collection.totalSupply, 6666);
  assert.equal(catalog.summary.importedTokenCount, 6666);
  assert.equal(catalog.summary.excludedTokenCount, 0);
  assert.equal(catalog.approvedGameplayTraits.length, 28);
  assert.equal(/dreaded ape/i.test(JSON.stringify(catalog)), false);

  const devilDog = catalog.approvedGameplayTraits.find((entry) => entry.gameplayId === "devil_dog_companion");
  assert.equal(devilDog.status, "matched");
  assert.equal(devilDog.tokenCount, 76);
});

test("token metadata lookup returns normalized on-chain attributes", () => {
  const token = getOriginalsTokenMetadata(1);
  assert.equal(token.tokenId, 1);
  assert.match(token.metadataUrl, /\/1\.json$/);
  assert.equal(token.attributes.some((attribute) => attribute.traitType === "Background" && attribute.value === "Tropical"), true);
  assert.equal(/dreaded ape/i.test(JSON.stringify(token)), false);
});

test("Originals metadata API exposes catalog and rejects unknown token IDs", async () => {
  const server = await listen(createApp());
  try {
    const traits = await request(server, "/api/nft/originals/traits");
    assert.equal(traits.response.status, 200);
    assert.equal(traits.body.collection.symbol, "OG");

    const token = await request(server, "/api/nft/originals/token/6666");
    assert.equal(token.response.status, 200);
    assert.equal(token.body.token.tokenId, 6666);

    const missing = await request(server, "/api/nft/originals/token/6667");
    assert.equal(missing.response.status, 404);
    assert.equal(missing.body.errorCode, "ORIGINAL_TOKEN_NOT_FOUND");
  } finally {
    server.close();
  }
});
