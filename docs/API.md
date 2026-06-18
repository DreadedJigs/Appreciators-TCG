# Appreciators TCG Backend API

Base URL locally:

```text
http://localhost:3001
```

Live prototype backend:

```text
https://appreciators-tcg-backend.onrender.com
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

## POST /api/matchmaking/invite

Creates a private invite room for 1v1 play.

Example body:

```json
{
  "username": "Host",
  "deckIds": ["regular_body", "beer_helmet"]
}
```

## GET /api/matchmaking/invite/:inviteCode

Returns the current invite room status.

## GET /api/matchmaking/invite/new

Creates a private invite room with query-string inputs for WebGL-friendly links.

Example:

```text
/api/matchmaking/invite/new?username=Host&deckIds=regular_body,beer_helmet
```

## GET /api/matchmaking/invite-lobby/announce

Marks a player as available for direct 1v1 challenges and returns the current lobby view.

Example:

```text
/api/matchmaking/invite-lobby/announce?username=Host&playerId=local-player-id&deckIds=regular_body,beer_helmet
```

## GET /api/matchmaking/invite-lobby

Returns available players and incoming direct challenges for the requesting player.

Example:

```text
/api/matchmaking/invite-lobby?username=Host&playerId=local-player-id
```

## GET /api/matchmaking/invite-lobby/challenge

Creates a private invite room targeted at an available player. The challenged player can accept it from their 1v1 menu.

Example:

```text
/api/matchmaking/invite-lobby/challenge?username=Host&playerId=host-id&targetPlayerId=guest-id&deckIds=regular_body
```

## POST /api/matchmaking/invite/:inviteCode/join

Joins a private invite room as the second player.

Example body:

```json
{
  "username": "Guest",
  "deckIds": ["ghost_companion"]
}
```

## GET /api/matchmaking/invite/:inviteCode/join-link

Joins a room with query-string inputs for QR codes and mobile browser links.

Example:

```text
/api/matchmaking/invite/ABC123/join-link?username=Guest&deckIds=ghost_companion
```

## POST /api/matchmaking/invite/:inviteCode/reconnect

Reconnects an existing host or guest after refresh or mobile tab resume.

Example body:

```json
{
  "playerId": "player-id-from-create-or-join-response"
}
```

## GET /api/matchmaking/invite/:inviteCode/reconnect-link

Reconnects through query-string inputs. This is useful for WebGL/mobile fallback flows.

Example:

```text
/api/matchmaking/invite/ABC123/reconnect-link?username=Guest&role=guest
```

## POST /api/matchmaking/invite/:inviteCode/start

Starts a ready invite room. The host must start the room.

Example body:

```json
{
  "username": "Host",
  "playerId": "host-player-id-from-create-response"
}
```

## GET /api/matchmaking/invite/:inviteCode/start-link

Starts a ready invite room with query-string inputs.

## GET /api/matchmaking/invite/:inviteCode/state

Returns the current public match state, including turn, energy, lane cards, lane power, and result.

## GET /api/matchmaking/invite/:inviteCode/actions

Returns synced actions after an optional `after` sequence number.

Example:

```text
/api/matchmaking/invite/ABC123/actions?after=4
```

## GET /api/matchmaking/invite/:inviteCode/action

Submits a WebGL-friendly match action through query-string inputs.

Examples:

```text
/api/matchmaking/invite/ABC123/action?playerId=player-id&actionId=a1&type=play-card&cardId=regular_body&lane=Art&turn=1
/api/matchmaking/invite/ABC123/action?playerId=player-id&actionId=a2&type=end-turn&turn=1
```

The server rejects stale turns, duplicate end-turn attempts, invalid lanes, and actions from non-participants.

## POST /api/wallet/verify

Returns a mock Phase 4 wallet verification response.

## POST /api/nft/sync

Returns a mock Phase 4 NFT ownership sync response.

## Invite Room Runtime Storage

Invite rooms are in memory by default for local tests. On Render, `render.yaml` enables a small JSON runtime store at `/tmp/appreciators-invite-rooms.json` so invite state can survive ordinary process-level reloads when the filesystem is still available.

For durable production multiplayer, move invite rooms to a database, Render Key Value, or attach a paid Render persistent disk and set `INVITE_ROOM_STORE_PATH` to a file under that disk mount.
