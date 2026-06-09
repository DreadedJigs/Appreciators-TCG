# Phase 1 Test Plan

## Unity Editor

- Open `unity-client` in Unity 2022.3 LTS or newer.
- Open `Assets/Scenes/Main.unity`.
- Press Play and use the Game tab.
- Confirm guest login saves a local player name.
- Confirm main menu navigation works.
- Confirm collection shows 30 prototype cards.
- Confirm deck builder saves only a 12-card deck.
- Confirm casual match starts with three lanes, hand, energy, and end turn button.
- Play cards into each lane and confirm energy and lane totals update.
- End through turn 6 and confirm results show victory, defeat, or draw.
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
- `POST http://localhost:3001/api/profile`
- `POST http://localhost:3001/api/matchmaking/casual`
- `POST http://localhost:3001/api/wallet/verify`
- `POST http://localhost:3001/api/nft/sync`
