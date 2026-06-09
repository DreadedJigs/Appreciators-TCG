# Debug, Audit, and Test Notes

## Current Phase

Phase 1 playable prototype with placeholder art and mocked Web3 systems.

## Local Debug Checklist

- Login stores a local player name with Unity `PlayerPrefs`.
- Main menu exposes Casual, Collection, Deck Builder, and coming-soon modes.
- Collection loads all prototype cards from editable JSON.
- Deck Builder validates exactly 12 cards and saves locally.
- Match screen supports click/touch card selection, lane play, energy spend, AI turn, and six-turn resolution.
- Results screen shows lane scores and match outcome.
- Web3 screen remains mock-only and stores a configurable backend API base URL.
- `Assets/Scenes/Main.unity` is the primary runtime shell and routes through `SceneBootstrapper`. The individual scenes remain in the project for direct screen testing, but WebGL builds should start from `Main`.

## Automated Tests

Project audit:

```bash
node scripts/audit-phase1.mjs
```

Backend:

```bash
cd backend
npm install
npm test
```

The pure card-data test can run without installing dependencies:

```bash
node --test backend/test/cards.test.js
```

The API integration tests import the Express app and require `npm install` first.

Unity:

1. Open `unity-client` in Unity.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play and confirm the login screen appears in the Game tab.
4. Open Test Runner.
5. Run EditMode tests under `Assets/Tests/EditMode`.

## Audit Notes

- Placeholder panels are intentional until official art arrives.
- No real wallet connection is implemented.
- NFT ownership does not affect gameplay.
- Offline AI play remains available if the backend is unavailable.
- Backend stores mock profiles in memory only.
- Card data is duplicated in Unity Resources and backend data for Phase 1; keep the files synchronized until a shared build step is introduced.
