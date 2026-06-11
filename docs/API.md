# Appreciators TCG Backend API

Base URL locally:

```text
http://localhost:3001
```

## GET /health

Returns service health and phase metadata.

## POST /api/profile

Creates or updates a mock player profile.

Example body:

```json
{
  "username": "Player",
  "deckIds": ["regular_body"]
}
```

## GET /api/cards

Returns the Phase 1 prototype card set as JSON.

## GET /api/assets/manifest

Returns the expected final-art file names and Unity `Resources` paths for all prototype cards.

## POST /api/matchmaking/casual

Returns a mock AI opponent assignment.

Example body:

```json
{
  "username": "Player"
}
```

## POST /api/wallet/verify

Returns a mock Phase 4 wallet verification response.

## POST /api/nft/sync

Returns a mock Phase 4 NFT ownership sync response.
