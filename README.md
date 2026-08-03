# Appreciators TCG

Appreciators TCG is a mobile-friendly engine-building card game about learning the table, building a shared-row engine, and gathering Growth. Phase 1 is gameplay-first: NFT ownership, wallet verification, cosmetics, rewards, and holder features are mocked placeholders and do not affect match power.

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
- `GET /api/card-meta/summary`
- `GET /api/card-meta/cards`
- `GET /api/card-meta/cards/:tokenId`
- `GET /api/card-meta/abilities`
- `GET /api/card-meta/seasons`
- `GET /api/assets/manifest`
- `GET /api/nft/originals/traits`
- `GET /api/nft/originals/token/:tokenId`
- `POST /api/profile`
- `POST /api/matchmaking/casual`
- `POST /api/matchmaking/invite`
- `GET /api/matchmaking/invite/new`
- `GET /api/matchmaking/invite-lobby/announce`
- `GET /api/matchmaking/invite-lobby`
- `GET /api/matchmaking/invite-lobby/challenge`
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
See `docs/APPRECIATORS_ORIGINALS_METADATA_AUDIT.md` for the ApeChain Originals trait mapping.
See `docs/BALANCE_REPORT.md` for the seeded 20,000-match preliminary balance pass.

Full Phase 1 structure audit:

```bash
node scripts/audit-phase1.mjs
```

Refresh the 6,666-token ApeChain metadata snapshot:

```bash
cd backend
npm run metadata:originals
```

The import references official image URLs without downloading thousands of image files. Only the approved gameplay trait list maps into mechanics; unrelated visual traits remain metadata, and any `Dreaded Ape` entry is excluded. Run `node scripts/import-metadata-card-art.mjs` from the repository root to download the 24 approved alpha card-art mappings; provenance and proxy details live in `docs/CARD_ART_PROVENANCE.md`.

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

Use the exact file names in `docs/ART_ASSET_MANIFEST.csv`, such as `ghost_companion.png`. The Unity UI loads `artPath` from card data and falls back to deterministic placeholder art until final PNGs exist. Square art-only PNGs are preferred. Supplied portrait card-sheet references are cropped to their illustration window at runtime so mock text never replaces approved gameplay data.

The official playmat and style references live under `unity-client/Assets/Resources/Art/Official/`. The UI uses the supplied Galactic Blue, King's Gold, Wave Break, Toxic Green, Pyro Red, Grape, and supporting colors. Add the licensed Aktiv Grotesk font export as `Art/Official/StyleGuide/AppreciatorsDisplay.ttf` for an exact runtime type match.

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
   - `PackOpeningScene`
5. Click `Switch Platform`.
6. Click `Build`.

You can also run the scripted WebGL build from Unity batchmode with `AppreciatorsTcg.EditorTools.AppreciatorsBuildWebGL.Build`.

The match UI uses a responsive 16:9 design reference and renders natively at 1080p or 4K, while retaining landscape mobile scaling, touch-sized controls, and readable card panels.

## Appreciation Ritual Packs

Pack rewards are generated and awarded by the Render Node backend. Production Unity builds only request a pack opening and animate the returned signed five-card result. Exact Standard, Starter, and Event mystery odds are published by `/api/packs/catalog`.

See `docs/PACK_SERVER_AUTHORITY.md` for the API contract, signing, idempotency, and deployment notes.

## Phase 1 Gameplay

- 12-card decks
- Keep one hidden card and draw back to a two-card decision each turn
- Play a card onto the shared row, then choose **Build** or **Discard**; canceling returns it to the same hand without redrawing either player
- Cards expose only Attack and Defense as their required player-facing numeric stats
- One traditional shared Growth Row replaces three-lane combat
- Built cards exhaust to generate Growth; adjacent traits and three-domain sets add combination Growth
- Tally confirms pending Growth, penalties, and leader modifiers before checking victory
- First player to 200 Growth wins; Spotlight begins at 150 Growth and the fallback match arc is 11 turns
- Offline AI weighs long-term engine value against immediate Actions and late-game scoring
- Invite 1v1 syncs Build, Action, leader, and end-turn events through the backend while preserving private hands.

## Phase Roadmap

The 6,666-card, 22-season distribution and competitive parity rules are documented in `docs/SEASONAL_RELEASE_PLAN.md` and published by `GET /api/releases/plan`.

### PHASE 1 - PROTOTYPE

Features:

- Login
- Collection Screen
- Deck Builder
- AI Opponent
- Single-Row Growth Battles
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

- Five supplied card mocks and the official playmat are integrated. Metadata-backed alpha art fills the remaining card slots when the importer is run; documented proxies remain replaceable by final artist exports.
- Runtime placeholder PNGs live in `unity-client/Assets/Resources/Art/Placeholder`.
- Wallets, NFT sync, and rewards are mocked only.
- Backend profiles use in-memory storage.
- Invite rooms use in-memory plus optional JSON runtime persistence. This is enough for prototype invite testing, but production should move to a database or Render Key Value.
- Pack inventory uses signed server-authoritative rewards with optional `/tmp` JSON persistence. Durable production inventory still requires authenticated accounts and a transactional database.
- AI is intentionally simple.
- Card targeting is deterministic for prototype speed: buffs choose eligible friendly cards automatically.
- Unity WebGL build output is not committed.
- Reference art must be exported as real PNG files before Unity import; AVIF files renamed with `.png` are not valid Unity card art.
