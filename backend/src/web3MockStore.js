import { randomBytes, randomInt } from "node:crypto";

const MINT_SUPPLY_CAP = 6666;
const INITIAL_SIMULATED_MINTED = 6001;
const MAX_MINT_QUANTITY = 5;
const RARITY_TIERS = [
  { label: "Mythic", threshold: 60, rank: 6, score: 600 },
  { label: "Legendary", threshold: 260, rank: 5, score: 500 },
  { label: "Epic", threshold: 760, rank: 4, score: 400 },
  { label: "Rare", threshold: 1960, rank: 3, score: 300 },
  { label: "Uncommon", threshold: 4260, rank: 2, score: 200 },
  { label: "Common", threshold: 10000, rank: 1, score: 100 }
];
let availableTokenNumbers = [];
const walletMintCounts = new Map();
const walletOwnedTokens = new Map();
const leaderboardEntries = new Map();

initializeMintPool();

function initializeMintPool() {
  availableTokenNumbers = Array.from({ length: MINT_SUPPLY_CAP }, (_value, index) => index + 1);

  for (let i = 0; i < INITIAL_SIMULATED_MINTED; i += 1) {
    availableTokenNumbers.splice(randomInt(availableTokenNumbers.length), 1);
  }
}

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

function simulatedSupplyMinted() {
  return MINT_SUPPLY_CAP - availableTokenNumbers.length;
}

function tokenRoll(tokenNumber, salt = 0) {
  let value = (tokenNumber + salt) >>> 0;
  value = Math.imul(value ^ (value >>> 16), 2246822519);
  value = Math.imul(value ^ (value >>> 13), 3266489917);
  return ((value ^ (value >>> 16)) >>> 0) % 10000;
}

function rarityForToken(tokenNumber) {
  const roll = tokenRoll(tokenNumber, 777);
  return RARITY_TIERS.find((tier) => roll < tier.threshold) || RARITY_TIERS[RARITY_TIERS.length - 1];
}

function drawAvailableTokenNumber(selectedNumbers) {
  if (availableTokenNumbers.length === 0) {
    return null;
  }

  let index = randomInt(availableTokenNumbers.length);
  for (let attempt = 0; attempt < 24; attempt += 1) {
    const candidateIndex = randomInt(availableTokenNumbers.length);
    const candidate = availableTokenNumbers[candidateIndex];
    const adjacentToBatch = selectedNumbers.some((tokenNumber) => Math.abs(tokenNumber - candidate) <= 1);
    if (!adjacentToBatch) {
      index = candidateIndex;
      break;
    }
  }

  const [tokenNumber] = availableTokenNumbers.splice(index, 1);
  return tokenNumber;
}

function previewAiToken() {
  const tokenNumber = randomInt(1, MINT_SUPPLY_CAP + 1);
  return buildMintedToken(tokenNumber, randomInt(0, 1000), "AI Pull");
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

function buildMintedToken(tokenNumber, index, namePrefix = null) {
  const rarity = rarityForToken(tokenNumber);
  const warPower = rarity.score + (tokenRoll(tokenNumber, 4096) % 100);
  const name = namePrefix
    ? `${namePrefix} #${String(tokenNumber).padStart(4, "0")}`
    : mintTokenName(tokenNumber, index);

  return {
    tokenId: `mock-mint-${String(tokenNumber).padStart(4, "0")}`,
    tokenNumber,
    name,
    rarity: rarity.label,
    rarityRank: rarity.rank,
    warPower,
    cosmeticOnly: true
  };
}

function getLeaderboardEntry(walletAddress) {
  if (!leaderboardEntries.has(walletAddress)) {
    leaderboardEntries.set(walletAddress, {
      walletAddress,
      displayAddress: shortWallet(walletAddress),
      mintedCount: 0,
      gamesPlayed: 0,
      wins: 0,
      losses: 0,
      draws: 0,
      score: 0,
      bestToken: null,
      lastResult: "Waiting"
    });
  }

  return leaderboardEntries.get(walletAddress);
}

function updateBestToken(entry, token) {
  if (!token) {
    return;
  }

  if (!entry.bestToken || token.warPower > entry.bestToken.warPower) {
    entry.bestToken = token;
  }
}

function sortedLeaderboard() {
  return [...leaderboardEntries.values()]
    .sort((left, right) =>
      right.score - left.score ||
      right.wins - left.wins ||
      right.mintedCount - left.mintedCount ||
      left.displayAddress.localeCompare(right.displayAddress))
    .slice(0, 10)
    .map((entry, index) => ({
      rank: index + 1,
      displayAddress: entry.displayAddress,
      walletAddress: entry.walletAddress,
      mintedCount: entry.mintedCount,
      gamesPlayed: entry.gamesPlayed,
      wins: entry.wins,
      losses: entry.losses,
      draws: entry.draws,
      score: entry.score,
      bestToken: entry.bestToken,
      lastResult: entry.lastResult
    }));
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
  const remainingBeforeMint = availableTokenNumbers.length;
  const mintedQuantity = Math.min(requestedQuantity, remainingBeforeMint);
  const tokens = [];
  const selectedNumbers = [];

  for (let i = 0; i < mintedQuantity; i += 1) {
    const tokenNumber = drawAvailableTokenNumber(selectedNumbers);
    if (tokenNumber == null) {
      break;
    }

    selectedNumbers.push(tokenNumber);
    tokens.push(buildMintedToken(tokenNumber, i));
  }

  const actualMintedQuantity = tokens.length;
  const totalMintedByWallet = (walletMintCounts.get(walletAddress) || 0) + actualMintedQuantity;
  walletMintCounts.set(walletAddress, totalMintedByWallet);
  const ownedTokens = walletOwnedTokens.get(walletAddress) || [];
  ownedTokens.push(...tokens);
  walletOwnedTokens.set(walletAddress, ownedTokens);

  const leaderboardEntry = getLeaderboardEntry(walletAddress);
  leaderboardEntry.mintedCount += actualMintedQuantity;
  leaderboardEntry.score += actualMintedQuantity;
  leaderboardEntry.lastResult = actualMintedQuantity > 0 ? "Minted" : "Sold out";
  for (const token of tokens) {
    updateBestToken(leaderboardEntry, token);
  }

  return {
    minted: actualMintedQuantity > 0,
    realTransactionSubmitted: false,
    mock: true,
    selectionMode: "random-non-sequential",
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    requestedQuantity,
    mintedQuantity: actualMintedQuantity,
    txHash: actualMintedQuantity > 0 ? fakeTransactionHash() : "0xMOCKSOLDOUT",
    tokens,
    totalMintedByWallet,
    collectionName: "Appreciators.IO Mint Simulator",
    supplyCap: MINT_SUPPLY_CAP,
    simulatedMinted: simulatedSupplyMinted(),
    remainingSupply: availableTokenNumbers.length,
    mintPriceEth: "0.000",
    network: "Appreciators Mocknet",
    phase: "Mint simulator",
    leaderboard: sortedLeaderboard(),
    message: actualMintedQuantity > 0
      ? `Mock mint complete. ${actualMintedQuantity} random Appreciator${actualMintedQuantity === 1 ? "" : "s"} revealed.`
      : "Mock mint sold out. Reset the backend to replay the simulator supply."
  };
}

export function simulateDeclareWar(input = {}) {
  const walletAddress = safeWalletAddress(input.walletAddress);
  const ownedTokens = walletOwnedTokens.get(walletAddress) || [];
  const requestedTokenId = String(input.tokenId || "").trim();
  const playerCard = ownedTokens.find((token) => token.tokenId === requestedTokenId) ||
    ownedTokens[ownedTokens.length - 1];

  if (!playerCard) {
    const error = new Error("Mint first before playing I Declare War against the AI minter.");
    error.statusCode = 409;
    throw error;
  }

  const aiCard = previewAiToken();
  let result = "draw";
  if (playerCard.warPower > aiCard.warPower) {
    result = "win";
  } else if (playerCard.warPower < aiCard.warPower) {
    result = "loss";
  }

  const leaderboardEntry = getLeaderboardEntry(walletAddress);
  leaderboardEntry.gamesPlayed += 1;
  if (result === "win") {
    leaderboardEntry.wins += 1;
    leaderboardEntry.score += 10;
  } else if (result === "loss") {
    leaderboardEntry.losses += 1;
    leaderboardEntry.score += 1;
  } else {
    leaderboardEntry.draws += 1;
    leaderboardEntry.score += 3;
  }

  leaderboardEntry.lastResult = result;
  updateBestToken(leaderboardEntry, playerCard);

  return {
    mock: true,
    mode: "I Declare War vs AI",
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    playerCard,
    aiCard,
    result,
    playerScore: playerCard.warPower,
    aiScore: aiCard.warPower,
    leaderboardEntry,
    leaderboard: sortedLeaderboard(),
    message: result === "win"
      ? "You declared appreciation and beat the AI minter."
      : result === "loss"
        ? "The AI minter won this flip. Mint energy remains undefeated."
        : "War. Both cards tied, so the appreciation is shared."
  };
}

export function getMintLeaderboard() {
  return {
    mock: true,
    mode: "Community Leaderboard",
    leaderboard: sortedLeaderboard()
  };
}

export function resetMockMintForTests() {
  initializeMintPool();
  walletMintCounts.clear();
  walletOwnedTokens.clear();
  leaderboardEntries.clear();
}
