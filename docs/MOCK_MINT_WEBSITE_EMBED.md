# Appreciators.IO Mint Simulator Website Embed

Mode name: **I Declare Appreciation**

Description: **A minting simulator for our nostalgic and degen hearts.**

Use these two deliverables for the website developer:

1. Standalone widget HTML:
   `backend/public/mock-mint-simulator.html`

2. Paste-ready iframe snippet:
   `docs/mock-mint-iframe-code.html`

## Deployment Notes

The iframe `src` must use a public backend URL. Do not use `http://127.0.0.1:3001` or `http://localhost:3001` on appreciators.io, because those only point to the visitor's own computer.

After the backend is deployed, replace every `https://YOUR-BACKEND-URL` placeholder with the real backend origin, for example:

```html
https://appreciators-tcg-backend.onrender.com
```

The iframe should then point to:

```html
https://appreciators-tcg-backend.onrender.com/mock-mint-simulator.html?apiBase=https%3A%2F%2Fappreciators-tcg-backend.onrender.com
```

## API Used By The Widget

The widget calls the mint endpoint:

```html
GET /api/mint/simulate-link?walletAddress=0xAPPRECIATORSDEMO&quantity=1
```

The response is explicitly mocked:

```json
{
  "mock": true,
  "realTransactionSubmitted": false,
  "selectionMode": "random-non-sequential",
  "mintedQuantity": 1,
  "supplyCap": 6666,
  "mintPriceEth": "0.000",
  "network": "Appreciators Mocknet"
}
```

After a successful mint, the widget unlocks **Play I Declare War vs AI** and calls:

```html
GET /api/mint/war-link?walletAddress=0xAPPRECIATORSDEMO&tokenId=mock-mint-1234
```

The community leaderboard is read from:

```html
GET /api/mint/leaderboard-link
```

The leaderboard is in-memory for this simulator. It resets when the backend restarts. When the OpenSea collection link is available, use it to replace the current mock rarity bands with real collection rarity bands.

## Direct HTML Option

If the website developer wants to paste the simulator directly into an HTML editor instead of using an iframe, use the contents of `backend/public/mock-mint-simulator.html`.

In that direct-paste case, set the widget's API base:

```html
<main class="appreciators-mint-widget" data-appreciators-mint data-api-base="https://YOUR-BACKEND-URL">
```

The iframe option is usually safer because it keeps the widget CSS and JavaScript isolated from the main website.
