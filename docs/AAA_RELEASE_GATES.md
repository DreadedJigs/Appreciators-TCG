# AAA Release Gates

This document is a release checklist, not a feature promise. A gate is closed
only when its acceptance evidence is attached to the release record.

## Online-authoritative milestone

The `authoritative-v2` protocol is now implemented in the backend:

- the server owns shuffled decks, hidden hands, board state, combat, health,
  Appreciation, turn order, and victory;
- each request carries an optimistic-concurrency version and a unique action
  id, so duplicate delivery is harmless;
- every event is HMAC chained; completed matches reveal their random seed so a
  replay can verify the pre-match seed commitment;
- the Unity API layer can queue, fetch, poll, replay, and submit only secure
  account actions.

The legacy invite flow remains an alpha compatibility route. It must not award
rank, shards, packs, or tournament results. The authoritative protocol is the
only valid source for those systems.

## Required production provisioning

The current Render disk remains a safe single-instance fallback. It is not the
multi-instance data layer required for competitive launch.

1. Create a managed PostgreSQL instance in the same Render region.
2. Run `backend/db/migrations/001_secure_state.sql` with the database owner.
3. Add its private connection string as `DATABASE_URL` on the web service.
4. Set `APP_DATABASE_SSL=false` for Render's private network, or `true` for a
   public TLS database.
5. Keep the generated `MATCH_EVENT_SIGNING_SECRET` stable across deploys.
6. Confirm `GET /health/ready` reports `driver: postgresql` before enabling
   ranked rewards or more than one web-service instance.

## Before public ranked launch

- Finish server parity for every Build, Discard, combat, and leader ability;
  no client effect may decide an online outcome.
- Replace long-poll-only delivery with a Redis-backed WebSocket gateway and
  verify reconnect, resume, timeout, surrender, and forfeit behavior.
- Move shard, pack, purchase, and boss-pool changes into a transaction ledger
  with reconciliation, role-based admin actions, and a tamper-evident audit
  trail.
- Connect a transactional email provider for verification, reset, recovery,
  and security notifications; add optional MFA and account data deletion.
- Add redundant ApeChain RPC/indexer verification, transfer revocation, and
  outage monitoring. Remove every mock Web3/mint route from production builds.
- Run CI on every pull request, load-test matchmaking, perform an external
  security review, and set up error monitoring, uptime alerts, backups,
  restore drills, privacy controls, and an incident runbook.
- Complete cross-browser/device performance and accessibility certification,
  balance simulations, content review, music/art license records, localization
  readiness, and moderated player-support tooling.
