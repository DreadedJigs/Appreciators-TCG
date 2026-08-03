using System;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public class BossBattleRules
    {
        public bool soloAlwaysLoses;
        public int minimumPartySize;
        public int nominalPartySize;
        public int maximumPartySize;
    }

    [Serializable]
    public class BossIdentity
    {
        public string playerId;
        public string displayName;
        public string walletDisplay;
        public bool verifiedOneOfOne;
        public string controlMode;
    }

    [Serializable]
    public class BossPartyMember
    {
        public string playerId;
        public string displayName;
        public bool ready;
        public string joinedAt;
    }

    [Serializable]
    public class BossCurrentPlayer
    {
        public string playerId;
        public bool inParty;
        public bool ready;
        public bool isBoss;
        public bool oneOfOneEligible;
    }

    [Serializable]
    public class BossBattleResult
    {
        public string battleId;
        public string initiatedBy;
        public int partySize;
        public int partyPower;
        public int bossPower;
        public string result;
        public string difficulty;
        public string summary;
        public string resolvedAt;
    }

    [Serializable]
    public class BossBattleState
    {
        public BossPoolStatus pool;
        public string poolId;
        public string status;
        public BossBattleRules rules;
        public BossIdentity boss;
        public BossPartyMember[] party;
        public int partySize;
        public int readyCount;
        public BossCurrentPlayer currentPlayer;
        public bool canStart;
        public BossBattleResult lastBattle;
        public string updatedAt;
    }

    [Serializable]
    public class BossBattleResponse
    {
        public bool success;
        public BossBattleState battle;
    }

    [Serializable]
    public class BossBattlePlayerRequest
    {
        public string playerId;
        public string displayName;
        public bool ready;
    }

    [Serializable]
    public class WalletAccountStatus
    {
        public string playerId;
        public string walletAddress;
        public string displayAddress;
        public string network;
        public string connectionState;
        public bool signatureVerified;
        public bool ownershipVerified;
        public bool oneOfOneEligible;
        public string holderRole;
        public string eligibilitySource;
        public int originalsBalance;
        public WalletOwnedAsset[] assets;
        public string verificationError;
        public string updatedAt;
    }

    [Serializable]
    public class WalletOwnedAsset
    {
        public int tokenId;
        public string name;
        public string image;
        public string metadataUrl;
        public bool oneOfOne;
    }

    [Serializable]
    public class WalletAccountResponse
    {
        public bool success;
        public WalletAccountStatus wallet;
        public string message;
        public string verificationBoundary;
    }

    [Serializable]
    public class WalletAccountRequest
    {
        public string playerId;
        public string walletAddress;
    }

    [Serializable]
    public class WalletChallengeResponse
    {
        public bool success;
        public string challengeId;
        public string walletAddress;
        public int chainId;
        public string message;
        public string expiresAt;
    }

    [Serializable]
    public class WalletVerificationRequest
    {
        public string playerId;
        public string walletAddress;
        public string challengeId;
        public string signature;
    }
}
