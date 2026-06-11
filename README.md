# Appreciators TCG

Appreciators TCG is a fast-paced, mobile-friendly strategy card game prototype built around three-lane battles across Art, Community, and Blockchain. Phase 1 is gameplay-first: NFT ownership, wallet verification, cosmetics, rewards, and holder features are mocked placeholders and do not affect match power.

Slogan: **Be Original**

Identity lines:

- We appreciate art.
- We appreciate community.
- We appreciate the blockchain.

## Tech Stack

- Unity client with Unity WebGL as the primary browser build target
- Node.js and Express backend
- Render-ready backend deployment with `render.yaml`
- Local JSON card data for offline play
- Mock Web3 and NFT systems for Phase 1

## Repository Structure

```text
unity-client/
  Assets/
    Scripts/
    Scenes/
    Resources/
    Prefabs/
    ScriptableObjects/
backend/
  src/
  data/
docs/
render.yaml
README.md
.gitignore
```

## Open the Unity Project

1. Open Unity Hub.
2. Add the `unity-client` folder as a project.
3. Use Unity `2022.3 LTS` or newer.
4. Open `Assets/Scenes/Main.unity`.
5. Press Play and use the Game tab.

The primary prototype starts from `Main.unity`, which uses `SceneBootstrapper` to load the login flow. The individual screen scenes remain available for direct testing, but `Main.unity` is the scene to open and build first.

## Run the Backend Locally

```bash
cd backend
npm install
npm start
```

The backend defaults to `http://localhost:3001`.

On Windows, if PowerShell blocks `npm`, use `npm.cmd` instead:

```powershell
cd "C:\Users\12517\Documents\appreciators tgc\backend"
npm.cmd install
npm.cmd start
```

You can also run `backend/start-backend-windows.cmd` to install dependencies if needed and keep the local backend server open in a terminal.

Useful routes:

- `GET /health`
- `GET /api/cards`
- `POST /api/profile`
- `POST /api/matchmaking/casual`
- `POST /api/wallet/verify`
- `POST /api/nft/sync`

## Deploy the Backend to Render

1. Push this repository to GitHub.
2. In Render, create a new Blueprint.
3. Select the GitHub repository.
4. Render will read the root `render.yaml`.
5. Deploy the `appreciators-tcg-backend` web service.

No database is required for Phase 1. Profiles are mock/in-memory and will reset when the service restarts.

## Run Tests

Backend:

```bash
cd backend
npm install
npm test
```

Unity:

1. Open `unity-client` in Unity.
2. Open the Unity Test Runner.
3. Run EditMode tests in `Assets/Tests/EditMode`.
4. The editor audit entry point is `AppreciatorsTcg.EditorTools.AppreciatorsPhase1Audit.RunAll`.

See `docs/DEBUG_AUDIT.md` for the current debug and audit checklist.

Full Phase 1 structure audit:

```bash
node scripts/audit-phase1.mjs
```

## Configure the Unity Backend URL

The default API URL lives in:

```text
unity-client/Assets/Resources/app-config.json
```

You can also change it in the prototype at `Wallet / Web3 Coming Soon -> Backend API Base URL`. The saved value uses Unity `PlayerPrefs`, so local gameplay still works if the backend is offline.

## Build Unity WebGL

1. Open the Unity project.
2. Go to `File -> Build Settings`.
3. Select `WebGL`.
4. Make sure these scenes are enabled in order:
   - `Main`
   - `LoginScene`
   - `MainMenuScene`
   - `CollectionScene`
   - `DeckBuilderScene`
   - `MatchScene`
   - `ResultsScene`
   - `Web3MockScene`
5. Click `Switch Platform`.
6. Click `Build`.

You can also run the scripted WebGL build from Unity batchmode with `AppreciatorsTcg.EditorTools.AppreciatorsBuildWebGL.Build`.

The match UI is designed for landscape play with large buttons and readable card panels.

## Phase 1 Gameplay

- 12-card decks
- Starting hand of 3 cards
- Draw 1 card each turn
- Energy starts at 1 and increases by 1 each turn
- Match lasts 6 turns
- Three lanes: Art, Community, Blockchain
- Max 4 cards per lane per player
- Higher power wins a lane
- Best of 3 lanes decides victory, defeat, or draw
- Offline AI opponent prioritizes playable cards and has a small preference for lanes it is losing

## Phase Roadmap

### PHASE 1 - PROTOTYPE

Features:

- Login
- Collection Screen
- Deck Builder
- AI Opponent
- Three-Lane Battles
- Basic Matchmaking

Goal: prove the game is fun.

### PHASE 2 - ALPHA

Features:

- Expanded Card Pool
- Ranked Ladder
- Progression Systems
- Improved Matchmaking

Goal: increase retention and engagement.

### PHASE 3 - BETA

Features:

- Companion Progression
- Seasons
- Events
- Tournaments

Goal: build community competition.

### PHASE 4 - BLOCKCHAIN INTEGRATION

Features:

- Wallet Verification
- ORIGINAL Ownership Sync
- COMPANION Ownership Sync
- Holder Cosmetics
- Holder Tournaments
- NFT Rewards

Goal: enhance gameplay through ownership without making NFTs mandatory.

## Known Limitations

- Phase 1 uses placeholder UI panels instead of final art.
- Placeholder PNGs live in `unity-client/Assets/Art/Placeholder` until official art is delivered.
- Wallets, NFT sync, and rewards are mocked only.
- Backend profiles use in-memory storage.
- AI is intentionally simple.
- Card targeting is deterministic for prototype speed: buffs choose eligible friendly cards automatically.
- Unity WebGL build output is not committed.
