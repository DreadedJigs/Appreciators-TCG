import { randomBytes } from "node:crypto";

const MINT_SUPPLY_CAP = 1111;
const MAX_MINT_QUANTITY = 5;
let simulatedSupplyMinted = 1001;
const walletMintCounts = new Map();

function safeWalletAddress(value) {
  const walletAddress = String(value || "").trim().slice(0, 64);
  return walletAddress || "0xAPPRECIATORS000000000000000000000000000000";
}

function safeMintQuantity(value) {
  const quantity = Number.parseInt(value, 10);
  if (!Number.isFinite(quantity)) {
    return 1;
  }

  return Math.min(Math.max(quantity, 1), MAX_MINT_QUANTITY);
}

function shortWallet(walletAddress) {
  if (walletAddress.length <= 12) {
    return walletAddress;
  }

  return `${walletAddress.slice(0, 6)}...${walletAddress.slice(-4)}`;
}

function fakeTransactionHash() {
  return `0xmock${randomBytes(20).toString("hex")}`;
}

function mintTokenName(tokenNumber, index) {
  const variants = [
    "Original Spark",
    "Community Glow",
    "Blockchain Beat",
    "Be Original",
    "Mint Party"
  ];

  return `${variants[index % variants.length]} #${String(tokenNumber).padStart(4, "0")}`;
}

export function verifyMockWallet(input = {}) {
  const walletAddress = safeWalletAddress(input.walletAddress);
  const username = String(input.username || "Guest").trim().slice(0, 24) || "Guest";

  return {
    verified: true,
    realSignatureVerified: false,
    mock: true,
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    username,
    holderTier: "Prototype Holder",
    cosmetics: [
      "Portal Card Back",
      "Neon Cyan Nameplate",
      "Be Original Match Banner"
    ],
    phase: "Phase 1.5",
    message: "Mock wallet verified locally. Real wallet signatures remain a Phase 4 feature."
  };
}

export function syncMockNftOwnership(input = {}) {
  const walletAddress = safeWalletAddress(input.walletAddress);

  return {
    synced: true,
    realOwnershipSynced: false,
    mock: true,
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    originals: [
      {
        tokenId: "mock-original-001",
        name: "Mock ORIGINAL",
        cosmeticOnly: true
      }
    ],
    companions: [
      {
        tokenId: "mock-companion-001",
        name: "Mock Companion",
        cosmeticOnly: true
      }
    ],
    cosmetics: [
      "Portal Card Back",
      "Ghost Flame Border",
      "Holder Lobby Glow"
    ],
    rewards: [
      "Phase 1 tester badge",
      "Future reward placeholder"
    ],
    phase: "Phase 1.5",
    message: "Mock ownership synced. These items are cosmetic only and do not affect card power."
  };
}

export function simulateMockMint(input = {}) {
  const walletAddress = safeWalletAddress(input.walletAddress);
  const requestedQuantity = safeMintQuantity(input.quantity);
  const remainingBeforeMint = Math.max(0, MINT_SUPPLY_CAP - simulatedSupplyMinted);
  const mintedQuantity = Math.min(requestedQuantity, remainingBeforeMint);
  const startTokenNumber = simulatedSupplyMinted + 1;
  const tokens = [];

  for (let i = 0; i < mintedQuantity; i += 1) {
    const tokenNumber = startTokenNumber + i;
    tokens.push({
      tokenId: `mock-mint-${String(tokenNumber).padStart(4, "0")}`,
      name: mintTokenName(tokenNumber, i),
      cosmeticOnly: true
    });
  }

  simulatedSupplyMinted += mintedQuantity;
  const totalMintedByWallet = (walletMintCounts.get(walletAddress) || 0) + mintedQuantity;
  walletMintCounts.set(walletAddress, totalMintedByWallet);

  return {
    minted: mintedQuantity > 0,
    realTransactionSubmitted: false,
    mock: true,
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    requestedQuantity,
    mintedQuantity,
    txHash: mintedQuantity > 0 ? fakeTransactionHash() : "0xMOCKSOLDOUT",
    tokens,
    totalMintedByWallet,
    collectionName: "Appreciators",
    supplyCap: MINT_SUPPLY_CAP,
    simulatedMinted: simulatedSupplyMinted,
    remainingSupply: Math.max(0, MINT_SUPPLY_CAP - simulatedSupplyMinted),
    mintPriceEth: "0.000",
    network: "Appreciators Mocknet",
    phase: "Mint simulator",
    message: mintedQuantity > 0
      ? `Mock mint complete. ${mintedQuantity} simulated Appreciator${mintedQuantity === 1 ? "" : "s"} revealed.`
      : "Mock mint sold out. Reset the backend to replay the simulator supply."
  };
}

export function resetMockMintForTests() {
  simulatedSupplyMinted = 1001;
  walletMintCounts.clear();
}
