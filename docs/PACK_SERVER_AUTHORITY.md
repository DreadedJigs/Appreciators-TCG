# Pack Server Authority

Production Unity builds do not roll or award final pack rewards. Unity sends a stable `playerId`, an idempotent `requestId`, and `packId` to Render. All Appreciation Ritual openings resolve as Neutral.

## Routes

- `GET /api/packs/catalog` returns pack/card definitions and all published mystery odds profiles.
- `GET /api/packs/odds/:packId` returns authoritative per-slot odds and guarantee disclosures.
- `GET /api/packs/inventory?playerId=...` returns authoritative pack, card, and shard inventory.
- `POST /api/packs/open` validates ownership, consumes one pack, generates five rewards, applies duplicates/shards, and returns an HMAC-signed reward envelope.
- `POST /api/packs/purchase` spends the published shard price and adds one pack.
- `POST /api/economy/match-win` awards 69 shards once per player and unique match ID.
- `GET /api/economy/boss-pool` returns the shared 2,000-shard Boss Vault status.
- `POST /api/economy/boss-contribute` atomically contributes player shards to the shared vault.
- `POST /api/packs/grant-test-pack` grants free alpha test packs only in development or admin-authorized staging.
- `POST /api/packs/reset-test-inventory` resets one alpha player's pack inventory only in development or admin-authorized staging.
- `POST /api/packs/simulate` runs restricted backend-only odds simulations without awarding inventory.

Example open request:

```json
{
  "requestId": "pack_unique_retry_token",
  "playerId": "player_stable_id",
  "packId": "starter_appreciation_pack",
  "attunement": "Neutral"
}
```

The response includes `version`, `algorithm`, `keyId`, `payloadBase64`, `signature`, `reward`, and the updated authoritative `inventory`. Reusing the same `requestId` returns the same signed reward without consuming another pack.

All card lanes roll naturally. Standard Mystery odds are Common 50%, Uncommon 30%, Rare 15%, Epic 4%, Legendary 1%. Starter Mystery odds are Rare 85%, Epic 13%, Legendary 2%. Guaranteed pack profiles enforce Uncommon+, Rare+, Mythic/Epic+, or Legendary mystery results and publish their exact odds before purchase.

## Render Configuration

`render.yaml` generates `PACK_REWARD_SIGNING_SECRET`, disables public test grants, and enables JSON persistence at `/tmp/appreciators-pack-inventory.json` for alpha testing. `/tmp` is ephemeral across service replacement or restart, so production inventory must move to a transactional database before packs represent purchases, prizes, or durable player property.

The Unity client checks that the response is a complete signed envelope before revealing it. It does not contain the HMAC secret; cryptographic verification and replay enforcement stay server-side.

Pack opens and reads are rate limited. Production test routes require both `PACK_TEST_GRANTS_ENABLED=true` and a matching `x-pack-test-key`; the committed Render Blueprint leaves test routes disabled.

Alpha identity is still a locally generated `playerId`. Add authenticated accounts and bind inventory to the authenticated subject before enabling valuable rewards or paid packs.
