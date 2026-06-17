using System;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public class WalletVerifyResponse
    {
        public bool verified;
        public bool realSignatureVerified;
        public bool mock;
        public string walletAddress;
        public string displayAddress;
        public string username;
        public string holderTier;
        public string[] cosmetics;
        public string phase;
        public string message;
    }

    [Serializable]
    public class NftSyncResponse
    {
        public bool synced;
        public bool realOwnershipSynced;
        public bool mock;
        public string walletAddress;
        public string displayAddress;
        public MockOwnedAsset[] originals;
        public MockOwnedAsset[] companions;
        public string[] cosmetics;
        public string[] rewards;
        public string phase;
        public string message;
    }

    [Serializable]
    public class MockOwnedAsset
    {
        public string tokenId;
        public string name;
        public bool cosmeticOnly;
    }

    [Serializable]
    public class MintSimulationResponse
    {
        public bool minted;
        public bool realTransactionSubmitted;
        public bool mock;
        public string walletAddress;
        public string displayAddress;
        public int requestedQuantity;
        public int mintedQuantity;
        public string txHash;
        public MockMintedToken[] tokens;
        public int totalMintedByWallet;
        public string collectionName;
        public int supplyCap;
        public int simulatedMinted;
        public int remainingSupply;
        public string mintPriceEth;
        public string network;
        public string phase;
        public string message;
    }

    [Serializable]
    public class MockMintedToken
    {
        public string tokenId;
        public string name;
        public bool cosmeticOnly;
    }
}
