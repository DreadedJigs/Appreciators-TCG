# Phase 1 Test Plan

## Unity Editor

- Open `unity-client` in Unity 2022.3 LTS or newer.
- Open `Assets/Scenes/Main.unity`.
- Press Play and use the Game tab.
- Confirm guest login saves a local player name.
- Confirm main menu navigation works.
- Confirm collection shows 29 approved Appreciators trait cards.
- Confirm cards show placeholder art, then replace one PNG under `Assets/Resources/Art/Cards` and confirm that card uses final art.
- Confirm deck builder saves only a 12-card deck.
- Confirm casual match starts with three lanes, hand, energy, and end turn button.
- Play cards into each lane and confirm energy and lane totals update.
- End through turn 6 and confirm results show victory, defeat, or draw.
- Confirm Invite 1v1 can create a code, show a QR/link, join from a second browser or mobile device, start the match, sync played cards to the same lanes on both clients, and advance turns only after both players end.
- Refresh one Invite 1v1 client and confirm reconnect returns to the same room/match.
- Confirm Web3 screen displays Phase 4 placeholders and saves API URL locally.
- Optional: run `AppreciatorsTcg.EditorTools.AppreciatorsPhase1Audit.RunAll` from Unity batchmode or a temporary editor menu call.

## Backend

```bash
cd backend
npm install
npm start
```

Then test:

- `GET http://localhost:3001/health`
- `GET http://localhost:3001/api/cards`
- `GET http://localhost:3001/api/assets/manifest`
- `POST http://localhost:3001/api/profile`
- `POST http://localhost:3001/api/matchmaking/casual`
- `POST http://localhost:3001/api/matchmaking/invite`
- `GET http://localhost:3001/api/matchmaking/invite/new?username=Host`
- `GET http://localhost:3001/api/matchmaking/invite/{code}/join-link?username=Guest`
- `GET http://localhost:3001/api/matchmaking/invite/{code}/start-link?username=Host&playerId={hostPlayerId}`
- `GET http://localhost:3001/api/matchmaking/invite/{code}/action?playerId={playerId}&actionId=test-1&type=end-turn&turn=1`
- `POST http://localhost:3001/api/wallet/verify`
- `POST http://localhost:3001/api/nft/sync`
