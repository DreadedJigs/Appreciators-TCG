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

The backend defaults to `http://localhost:3001` locally.

Current Render prototype backend:

```text
https://appreciators-tcg-backend.onrender.com
```

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
- `GET /api/assets/manifest`
- `POST /api/profile`
- `POST /api/matchmaking/casual`
- `POST /api/matchmaking/invite`
- `GET /api/matchmaking/invite/new`
- `GET /api/matchmaking/invite/:inviteCode`
- `POST /api/matchmaking/invite/:inviteCode/join`
- `GET /api/matchmaking/invite/:inviteCode/join-link`
- `POST /api/matchmaking/invite/:inviteCode/reconnect`
- `GET /api/matchmaking/invite/:inviteCode/reconnect-link`
- `POST /api/matchmaking/invite/:inviteCode/start`
- `GET /api/matchmaking/invite/:inviteCode/start-link`
- `GET /api/matchmaking/invite/:inviteCode/state`
- `GET /api/matchmaking/invite/:inviteCode/actions`
- `GET /api/matchmaking/invite/:inviteCode/action`
- `POST /api/wallet/verify`
- `POST /api/nft/sync`

## Deploy the Backend to Render

1. Push this repository to GitHub.
2. In Render, create a new Blueprint.
3. Select the GitHub repository.
4. Render will read the root `render.yaml`.
5. Deploy the `appreciators-tcg-backend` web service.

No database is required for Phase 1. Profiles are mock/in-memory. Invite rooms are still lightweight, but `render.yaml` enables a small JSON runtime store at `/tmp/appreciators-invite-rooms.json` for best-effort invite recovery during prototype testing.

For durable production multiplayer on Render, add a managed data store later. A paid Render persistent disk can also be attached and used by changing `INVITE_ROOM_STORE_PATH` to a file under the disk mount.

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
See `docs/ART_ASSET_PIPELINE.md` and `docs/ART_ASSET_MANIFEST.csv` for the final art drop process.

Full Phase 1 structure audit:

```bash
node scripts/audit-phase1.mjs
```

## Configure the Unity Backend URL

The default API URL lives in:

```text
unity-client/Assets/Resources/app-config.json
```

The committed WebGL default points at the current Render prototype backend. You can also change it in the prototype at `Wallet / Web3 -> Backend API Base URL`. The saved value uses Unity `PlayerPrefs`, so local gameplay still works if the backend is offline.

## Add Final Art

Official card art should be dropped into:

```text
unity-client/Assets/Resources/Art/Cards/
```

Use the exact file names in `docs/ART_ASSET_MANIFEST.csv`, such as `ghost_companion.png`. The Unity UI loads `artPath` from card data and falls back to placeholder art until final PNGs exist.

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
   - `InviteMatchScene`
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
- Invite 1v1 creates and joins private rooms through the backend. Phase 1.5 uses polling room state with synced card actions, QR/mobile-friendly join links, reconnect support, lane placement, and both-player end-turn advancement.

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

- Phase 1 uses placeholder card art until official files are delivered.
- Runtime placeholder PNGs live in `unity-client/Assets/Resources/Art/Placeholder`.
- Wallets, NFT sync, and rewards are mocked only.
- Backend profiles use in-memory storage.
- Invite rooms use in-memory plus optional JSON runtime persistence. This is enough for prototype invite testing, but production should move to a database or Render Key Value.
- AI is intentionally simple.
- Card targeting is deterministic for prototype speed: buffs choose eligible friendly cards automatically.
- Unity WebGL build output is not committed.
- Reference art must be exported as real PNG files before Unity import; AVIF files renamed with `.png` are not valid Unity card art.
