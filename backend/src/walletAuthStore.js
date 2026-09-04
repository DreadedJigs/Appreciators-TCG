import { randomBytes } from "node:crypto";
import { getAddress, verifyMessage } from "ethers";
import { getOneOfOneOriginals } from "./originalsMetadataRepository.js";

const DEFAULT_APECHAIN_CHAIN_ID = 33139;
const KNOWN_ORIGINALS_CONTRACT = "0xd92b263b48f74d0cd21f9d2c01b6cd06f2ab96cd";
const DEVELOPMENT_RPC_URL = "https://rpc.apechain.com/http";
const CHALLENGE_TTL_MS = 5 * 60 * 1000;
const RPC_TIMEOUT_MS = 12_000;
const challenges = new Map();

/** Returns configuration only; it never performs an RPC call. */
export function getWalletVerificationStatus() {
  const config = getConfiguration();
  return {
    configured: config.configured,
    chainId: config.chainId,
    contractAddress: config.contractAddress,
    tokenCount: config.oneOfOneAssets.length,
    reason: config.reason
  };
}

export function createWalletChallenge(payload = {}) {
  pruneExpiredChallenges();
  const playerId = requirePlayerId(payload.playerId);
  const walletAddress = normalizeWalletAddress(payload.walletAddress);
  const challengeId = randomBytes(24).toString("hex");
  const nonce = randomBytes(24).toString("hex");
  const issuedAt = new Date();
  const expiresAt = new Date(issuedAt.getTime() + CHALLENGE_TTL_MS);
  const config = getConfiguration();
  const message = [
    "Appreciators TCG wallet verification",
    "",
    `URI: ${config.applicationUrl}`,
    "Version: 1",
    `Chain ID: ${config.chainId}`,
    `Account: ${playerId}`,
    `Wallet: ${walletAddress}`,
    `Nonce: ${nonce}`,
    `Issued At: ${issuedAt.toISOString()}`,
    `Expiration Time: ${expiresAt.toISOString()}`,
    "",
    "This signature proves wallet control for Appreciators TCG. It does not authorize a transaction or spend assets."
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
    chainId: config.chainId,
    message,
    expiresAt: expiresAt.toISOString(),
    verificationConfigured: config.configured
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
    recoveredAddress = getAddress(verifyMessage(challenge.message, String(payload.signature || ""))).toLowerCase();
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
  const config = getConfiguration();
  if (!config.configured) {
    return {
      network: "ApeChain",
      chainId: config.chainId,
      contractAddress: config.contractAddress,
      originalsBalance: 0,
      assets: [],
      ownershipVerified: false,
      oneOfOneEligible: false,
      eligibilitySource: "ownership verification not configured",
      verificationError: config.reason
    };
  }

  try {
    const [chainIdHex, contractCode, balanceHex] = await Promise.all([
      rpcCall(config, "eth_chainId", []),
      rpcCall(config, "eth_getCode", [config.contractAddress, "latest"]),
      ethCall(config, `0x70a08231${encodeAddress(walletAddress)}`)
    ]);
    const actualChainId = Number(BigInt(chainIdHex));
    if (actualChainId !== config.chainId) {
      throw new Error(`RPC returned chain ${actualChainId}, expected ApeChain ${config.chainId}.`);
    }
    if (!/^0x[0-9a-f]+$/i.test(contractCode) || /^0x0*$/i.test(contractCode)) {
      throw new Error("The configured Originals contract has no bytecode on the configured ApeChain RPC.");
    }

    const ownershipChecks = await Promise.all(config.oneOfOneAssets.map(async (asset) => {
      try {
        const ownerHex = await ethCall(config, `0x6352211e${encodeUint256(asset.tokenId)}`);
        return decodeAddress(ownerHex) === walletAddress.toLowerCase() ? asset : null;
      } catch {
        return null;
      }
    }));
    const assets = ownershipChecks.filter(Boolean);
    return {
      network: "ApeChain",
      chainId: config.chainId,
      contractAddress: config.contractAddress,
      originalsBalance: safeHexNumber(balanceHex),
      assets,
      ownershipVerified: true,
      oneOfOneEligible: assets.length > 0,
      eligibilitySource: "ApeChain RPC: chain ID, contract bytecode, balanceOf, and ownerOf"
    };
  } catch (error) {
    return {
      network: "ApeChain",
      chainId: config.chainId,
      contractAddress: config.contractAddress,
      originalsBalance: 0,
      assets: [],
      ownershipVerified: false,
      oneOfOneEligible: false,
      eligibilitySource: "ApeChain RPC unavailable",
      verificationError: `Wallet control was verified, but on-chain ownership could not be confirmed: ${error.message}`
    };
  }
}

function getConfiguration() {
  const isProduction = process.env.NODE_ENV === "production";
  const chainId = parseChainId(process.env.APECHAIN_CHAIN_ID || DEFAULT_APECHAIN_CHAIN_ID);
  const contractAddress = normalizeConfiguredAddress(process.env.ORIGINALS_CONTRACT_ADDRESS || KNOWN_ORIGINALS_CONTRACT);
  const rpcUrl = String(process.env.APECHAIN_RPC_URL || (isProduction ? "" : DEVELOPMENT_RPC_URL)).trim();
  const oneOfOneAssets = configuredOneOfOnes();
  const applicationUrl = String(process.env.APP_PUBLIC_URL || "https://appreciators-tcg.com").trim().replace(/\/$/, "");
  const reason = !rpcUrl
    ? "Wallet ownership is disabled until APECHAIN_RPC_URL is configured on the server."
    : !contractAddress
      ? "Wallet ownership is disabled until ORIGINALS_CONTRACT_ADDRESS is configured on the server."
      : oneOfOneAssets.length === 0
        ? "Wallet ownership is disabled until supported 1-of-1 token IDs are configured or imported metadata is available."
        : "ready";
  return { chainId, contractAddress, rpcUrl, oneOfOneAssets, applicationUrl, configured: reason === "ready", reason };
}

function configuredOneOfOnes() {
  const configuredIds = String(process.env.BOSS_ONE_OF_ONE_TOKEN_IDS || "")
    .split(",")
    .map((value) => Number.parseInt(value.trim(), 10))
    .filter((value) => Number.isInteger(value) && value > 0);
  const imported = getOneOfOneOriginals();
  if (configuredIds.length === 0) return imported;
  const importedById = new Map(imported.map((asset) => [asset.tokenId, asset]));
  return [...new Set(configuredIds)].map((tokenId) => importedById.get(tokenId)).filter(Boolean);
}

async function ethCall(config, data) {
  return rpcCall(config, "eth_call", [{ to: config.contractAddress, data }, "latest"]);
}

async function rpcCall(config, method, params) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), RPC_TIMEOUT_MS);
  try {
    const response = await fetch(config.rpcUrl, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ jsonrpc: "2.0", id: randomBytes(4).readUInt32BE(0), method, params }),
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

function parseChainId(value) {
  const chainId = Number.parseInt(value, 10);
  if (!Number.isInteger(chainId) || chainId <= 0) throw new Error("APECHAIN_CHAIN_ID must be a positive integer.");
  return chainId;
}

function normalizeConfiguredAddress(value) {
  try {
    return getAddress(String(value || "")).toLowerCase();
  } catch {
    return "";
  }
}

function pruneExpiredChallenges() {
  const now = Date.now();
  for (const [challengeId, challenge] of challenges.entries()) {
    if (challenge.expiresAt <= now) challenges.delete(challengeId);
  }
}

function normalizeWalletAddress(value) {
  try {
    return getAddress(String(value || "").trim());
  } catch {
    throw requestError("Enter a valid 42-character EVM wallet address.", 400, "INVALID_WALLET_ADDRESS");
  }
}

function requirePlayerId(value) {
  const playerId = String(value || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64);
  if (!playerId) throw requestError("playerId is required.", 400, "PLAYER_ID_REQUIRED");
  return playerId;
}

function requestError(message, statusCode, errorCode) {
  return Object.assign(new Error(message), { statusCode, errorCode });
}
