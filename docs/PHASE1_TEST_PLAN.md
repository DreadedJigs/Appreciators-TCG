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
- Confirm casual match starts with one shared Growth Row, exactly two cards, an Action mat, and the existing end-turn control.
- Play a card on the center row and confirm the Build/Discard chooser appears before anything is committed.
- Cancel once and confirm the card animates back without recreating either hand; then choose Build and confirm the other card remains hidden.
- On another turn, choose Discard and confirm its effect resolves before the card enters the public face-up discard pile.
- Hold to inspect a card and confirm closing the enlarged view does not refresh either hand.
- Confirm card faces, hand badges, board cards, and discard cards expose Attack and Defense rather than the retired lane/cost stat set.
- End the turn and confirm the row exhausts for Growth, combinations and modifiers appear in the Tally, and the next Draw restores the hand to two cards.
- Continue to 200 Growth (or turn 11) and confirm results show the final Growth totals and Spotlight state after 150.
- Confirm Invite 1v1 can create a code, show a QR/link, join from a second browser or mobile device, start the match, sync Build and Action events, and advance turns only after both players end.
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
