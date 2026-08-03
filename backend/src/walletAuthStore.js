import { randomBytes } from "node:crypto";
import { verifyMessage } from "ethers";
import { getOriginalsTokenMetadata } from "./originalsMetadataRepository.js";

const APECHAIN_ID = 33139;
const ORIGINALS_CONTRACT = "0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd";
const DEFAULT_APECHAIN_RPC_URL = "https://rpc.apechain.com/http";
const ONE_OF_ONE_TOKEN_IDS = [1618, 6239];
const CHALLENGE_TTL_MS = 5 * 60 * 1000;
const challenges = new Map();

export function createWalletChallenge(payload = {}) {
  pruneExpiredChallenges();
  const playerId = requirePlayerId(payload.playerId);
  const walletAddress = normalizeWalletAddress(payload.walletAddress);
  const challengeId = randomBytes(18).toString("hex");
  const nonce = randomBytes(16).toString("hex");
  const issuedAt = new Date();
  const expiresAt = new Date(issuedAt.getTime() + CHALLENGE_TTL_MS);
  const message = [
    "Appreciators TCG wallet verification",
    "",
    `Account: ${playerId}`,
    `Wallet: ${walletAddress}`,
    `Chain: ApeChain (${APECHAIN_ID})`,
    `Nonce: ${nonce}`,
    `Issued: ${issuedAt.toISOString()}`,
    "",
    "This signature proves wallet control. It does not authorize a transaction or spend assets."
  ].join("\n");

  challenges.set(challengeId, {
    challengeId,
    playerId,
    walletAddress: walletAddress.toLowerCase(),
    message,
    expiresAt: expiresAt.getTime()
  });

  return {
    success: true,
    challengeId,
    walletAddress,
    chainId: APECHAIN_ID,
    message,
    expiresAt: expiresAt.toISOString()
  };
}

export async function verifyWalletChallenge(payload = {}) {
  pruneExpiredChallenges();
  const challengeId = String(payload.challengeId || "").trim();
  const challenge = challenges.get(challengeId);
  if (!challenge) {
    throw requestError("The wallet challenge is missing or expired. Connect the wallet again.", 401, "WALLET_CHALLENGE_EXPIRED");
  }

  const playerId = requirePlayerId(payload.playerId);
  const walletAddress = normalizeWalletAddress(payload.walletAddress).toLowerCase();
  if (challenge.playerId !== playerId || challenge.walletAddress !== walletAddress) {
    throw requestError("The signed wallet does not match this game account challenge.", 401, "WALLET_CHALLENGE_MISMATCH");
  }

  let recoveredAddress;
  try {
    recoveredAddress = verifyMessage(challenge.message, String(payload.signature || "")).toLowerCase();
  } catch {
    throw requestError("The wallet signature is invalid.", 401, "INVALID_WALLET_SIGNATURE");
  }
  if (recoveredAddress !== walletAddress) {
    throw requestError("The signature was created by a different wallet.", 401, "WALLET_SIGNATURE_ADDRESS_MISMATCH");
  }

  challenges.delete(challengeId);
  const ownership = await readOriginalsOwnership(walletAddress);
  return {
    playerId,
    walletAddress,
    signatureVerified: true,
    ...ownership
  };
}

export function resetWalletChallengesForTests() {
  challenges.clear();
}

async function readOriginalsOwnership(walletAddress) {
  try {
    const balanceHex = await ethCall(`0x70a08231${encodeAddress(walletAddress)}`);
    const originalsBalance = safeHexNumber(balanceHex);
    const ownedOneOfOnes = [];
    for (const tokenId of ONE_OF_ONE_TOKEN_IDS) {
      const ownerHex = await ethCall(`0x6352211e${encodeUint256(tokenId)}`);
      if (decodeAddress(ownerHex) !== walletAddress.toLowerCase()) continue;
      const metadata = getOriginalsTokenMetadata(tokenId);
      const displayName = metadata.attributes?.find((attribute) => attribute.traitType === "Name")?.value || metadata.name;
      ownedOneOfOnes.push({
        tokenId,
        name: displayName,
        image: metadata.image,
        metadataUrl: metadata.metadataUrl,
        oneOfOne: true
      });
    }
    return {
      network: "ApeChain",
      chainId: APECHAIN_ID,
      contractAddress: ORIGINALS_CONTRACT,
      originalsBalance,
      assets: ownedOneOfOnes,
      ownershipVerified: true,
      oneOfOneEligible: ownedOneOfOnes.length > 0,
      eligibilitySource: "ApeChain ownerOf"
    };
  } catch (error) {
    return {
      network: "ApeChain",
      chainId: APECHAIN_ID,
      contractAddress: ORIGINALS_CONTRACT,
      originalsBalance: 0,
      assets: [],
      ownershipVerified: false,
      oneOfOneEligible: false,
      eligibilitySource: "ApeChain unavailable",
      verificationError: `Wallet control verified, but ApeChain ownership could not be read: ${error.message}`
    };
  }
}

async function ethCall(data) {
  const rpcUrl = process.env.APECHAIN_RPC_URL || DEFAULT_APECHAIN_RPC_URL;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 12_000);
  try {
    const response = await fetch(rpcUrl, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: 1,
        method: "eth_call",
        params: [{ to: ORIGINALS_CONTRACT, data }, "latest"]
      }),
      signal: controller.signal
    });
    if (!response.ok) throw new Error(`RPC HTTP ${response.status}`);
    const document = await response.json();
    if (document.error) throw new Error(document.error.message || "RPC call failed");
    if (typeof document.result !== "string") throw new Error("RPC returned no result");
    return document.result;
  } finally {
    clearTimeout(timer);
  }
}

function encodeAddress(walletAddress) {
  return walletAddress.toLowerCase().replace(/^0x/, "").padStart(64, "0");
}

function encodeUint256(value) {
  return BigInt(value).toString(16).padStart(64, "0");
}

function decodeAddress(value) {
  const hex = String(value || "").replace(/^0x/, "").padStart(64, "0");
  return `0x${hex.slice(-40)}`.toLowerCase();
}

function safeHexNumber(value) {
  const parsed = BigInt(value || "0x0");
  return Number(parsed > BigInt(Number.MAX_SAFE_INTEGER) ? BigInt(Number.MAX_SAFE_INTEGER) : parsed);
}

function pruneExpiredChallenges() {
  const now = Date.now();
  for (const [challengeId, challenge] of challenges.entries()) {
    if (challenge.expiresAt <= now) challenges.delete(challengeId);
  }
}

function normalizeWalletAddress(value) {
  const walletAddress = String(value || "").trim();
  if (!/^0x[a-fA-F0-9]{40}$/.test(walletAddress)) {
    throw requestError("Enter a valid 42-character EVM wallet address.", 400, "INVALID_WALLET_ADDRESS");
  }
  return walletAddress;
}

function requirePlayerId(value) {
  const playerId = String(value || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64);
  if (!playerId) throw requestError("playerId is required.", 400, "PLAYER_ID_REQUIRED");
  return playerId;
}

function requestError(message, statusCode, errorCode) {
  return Object.assign(new Error(message), { statusCode, errorCode });
}
