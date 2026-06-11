import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function loadCards() {
  const raw = await readFile(new URL("../data/cards.json", import.meta.url), "utf8");
  return JSON.parse(raw).cards;
}

test("prototype card set has the required Phase 1 shape", async () => {
  const cards = await loadCards();
  const counts = cards.reduce((result, card) => {
    result[card.type] = (result[card.type] || 0) + 1;
    return result;
  }, {});

  assert.equal(cards.length, 29);
  assert.equal(counts.ORIGINAL, 17);
  assert.equal(counts.COMPANION, 5);
  assert.equal(counts.ITEM, 7);
  assert.equal(counts.EVENT || 0, 0);
});

test("every card has editable gameplay fields", async () => {
  const cards = await loadCards();
  const ids = new Set();

  for (const card of cards) {
    assert.equal(typeof card.id, "string");
    assert.equal(typeof card.name, "string");
    assert.equal(typeof card.effectText, "string");
    assert.equal(typeof card.effectId, "string");
    assert.equal(typeof card.rarity, "string");
    assert.equal(typeof card.traitGroup, "string");
    assert.ok(Number.isInteger(card.cost), `${card.name} cost must be an integer`);
    assert.ok(Number.isInteger(card.power), `${card.name} power must be an integer`);
    assert.ok(Number.isInteger(card.appreciation), `${card.name} appreciation must be an integer`);
    assert.ok(["ORIGINAL", "COMPANION", "ITEM", "EVENT"].includes(card.type));
    assert.equal(/dreaded ape/i.test(`${card.id} ${card.name} ${card.artPath}`), false);
    assert.ok(!ids.has(card.id), `Duplicate card id ${card.id}`);
    ids.add(card.id);
  }
});

test("every card has a final-art drop slot", async () => {
  const cards = await loadCards();

  for (const card of cards) {
    assert.equal(card.artKey, card.id, `${card.id} artKey should match its stable card id`);
    assert.equal(card.artPath, `Art/Cards/${card.id}`, `${card.id} artPath should target Unity Resources`);
  }
});
