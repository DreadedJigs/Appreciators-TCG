import crypto from "node:crypto";

const profilesByName = new Map();

export function upsertProfile(payload = {}) {
  const username = String(payload.username || payload.displayName || "Guest").trim().slice(0, 24) || "Guest";
  const accountKey = username.toLowerCase();
  const existing = profilesByName.get(accountKey);
  const now = new Date().toISOString();
  const requestedId = String(payload.playerId || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64);
  const stableId = requestedId || `account_${crypto.createHash("sha256").update(accountKey).digest("hex").slice(0, 16)}`;

  const profile = {
    id: existing?.id || stableId,
    username,
    displayName: username,
    deckIds: Array.isArray(payload.deckIds) ? payload.deckIds.slice(0, 12) : existing?.deckIds || [],
    mockAccount: true,
    createdAt: existing?.createdAt || now,
    updatedAt: now
  };

  profilesByName.set(accountKey, profile);
  return profile;
}
