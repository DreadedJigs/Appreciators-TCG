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

  assert.equal(cards.length, 30);
  assert.equal(counts.ORIGINAL, 12);
  assert.equal(counts.COMPANION, 6);
  assert.equal(counts.TRAIT, 6);
  assert.equal(counts.BACKGROUND, 6);
});

test("every card has editable gameplay fields", async () => {
  const cards = await loadCards();
  const ids = new Set();

  for (const card of cards) {
    assert.equal(typeof card.id, "string");
    assert.equal(typeof card.name, "string");
    assert.equal(typeof card.effectText, "string");
    assert.equal(typeof card.effectId, "string");
    assert.ok(Number.isInteger(card.cost), `${card.name} cost must be an integer`);
    assert.ok(Number.isInteger(card.power), `${card.name} power must be an integer`);
    assert.ok(["ORIGINAL", "COMPANION", "TRAIT", "BACKGROUND"].includes(card.type));
    assert.ok(!ids.has(card.id), `Duplicate card id ${card.id}`);
    ids.add(card.id);
  }
});
