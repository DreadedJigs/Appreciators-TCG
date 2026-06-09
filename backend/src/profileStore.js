const profilesByName = new Map();

export function upsertProfile(payload = {}) {
  const username = String(payload.username || payload.displayName || "Guest").trim().slice(0, 24) || "Guest";
  const existing = profilesByName.get(username);
  const now = new Date().toISOString();

  const profile = {
    id: existing?.id || `mock_${username.toLowerCase().replace(/[^a-z0-9]+/g, "_")}_${Date.now()}`,
    username,
    displayName: username,
    deckIds: Array.isArray(payload.deckIds) ? payload.deckIds.slice(0, 12) : existing?.deckIds || [],
    mockAccount: true,
    createdAt: existing?.createdAt || now,
    updatedAt: now
  };

  profilesByName.set(username, profile);
  return profile;
}
