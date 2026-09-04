# Online, account, and wallet release checklist

The production API now has three server-owned boundaries:

- `/api/auth/*` uses scrypt password hashes and opaque, hashed, revocable sessions. Access tokens are memory-only in the Unity client; they are not stored in PlayerPrefs, browser storage, or source control.
- `/api/cloud-save` only accepts a versioned, whitelisted snapshot: preferences, tutorial progress, decks, and the selected boss asset. The client syncs a changed snapshot after a short debounce. Economy balances, inventory, match results, and boss eligibility are excluded from client writes. Concurrent writes are rejected rather than silently overwriting another device.
- `/api/online-matches/*` is server-owned. It validates match membership, active side, phase order, action idempotency, and optimistic match revisions. The existing invite flow is also identity-bound in production: room, state, and action reads are participant-only, and a submitted `playerId` cannot impersonate another account.

## Render configuration

Before deploying this release, create a Render persistent disk mounted at `/var/data`, then copy the production values from [`backend/.env.example`](../backend/.env.example) into Render's Environment page. `APP_DATA_DIR` must point to that mount and `APP_ALLOW_FILE_PERSISTENCE` must be `true`.

The `/health` endpoint reports `degraded` until the persistence boundary is configured. Do not launch secure accounts, economy, or online play while that endpoint is degraded.

Set a unique `PACK_REWARD_SIGNING_SECRET` in Render. Never reuse a development secret and never commit it.

## Wallet verification

Production wallet links require a one-time EIP-191 signature bound to the signed-in game account, wallet address, nonce, and five-minute expiry. The server then verifies the configured RPC chain ID, Originals contract bytecode, ERC-721 `balanceOf`, and `ownerOf` for each supported 1-of-1 token. Only an on-chain `ownerOf` match can unlock Boss Mode.

Set a reliable authenticated ApeChain RPC endpoint in `APECHAIN_RPC_URL` before public scale; the public endpoint in `render.yaml` is a bootstrap configuration only. If the RPC is unavailable, the server records the signature result but does **not** grant ownership or Boss Mode. Mock wallet, mint, legacy casual, and legacy player-ID routes return `410` in production.

## Operational limits before multi-region launch

The current storage adapter is intentionally a single-instance persistent-volume driver. It is safe for one Render service and fails closed when the persistent mount is absent. A true multi-instance / multi-region launch needs a transactional managed database and a distributed realtime gateway; do not increase instance count until that adapter is deployed and load-tested.

## Release gates

1. `GET /health` returns `status: ok`, `cloudSaves: true`, and `walletVerification: true`.
2. Create two secure accounts on separate browsers; verify cloud-save version conflict behavior.
3. Play an invite match with both accounts and verify action attempts from an unsigned client return `401`.
4. Sign a real wallet challenge; confirm the chain, contract, and displayed 1-of-1 image correspond to on-chain ownership.
5. Rotate `PACK_REWARD_SIGNING_SECRET` only with a planned migration window; pack reward signatures issued before rotation will no longer validate.
