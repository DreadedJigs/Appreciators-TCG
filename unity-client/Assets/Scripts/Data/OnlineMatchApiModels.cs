using System;

namespace AppreciatorsTcg.Data
{
    // DTOs for the server-authoritative protocol. Card stat values and all
    // event outcomes are returned by the server; the Unity client only renders
    // them and submits player intent with an optimistic-concurrency version.
    [Serializable]
    public class OnlineMatchQueueRequest
    {
        public string mode;
        public string[] deckIds;
    }

    [Serializable]
    public class OnlineMatchQueueResponse
    {
        public bool success;
        public string status;
        public OnlineMatchTicket ticket;
        public OnlineMatchState match;
    }

    [Serializable]
    public class OnlineMatchQueueCancelRequest
    {
        public string ticketId;
    }

    [Serializable]
    public class OnlineMatchTicket
    {
        public string id;
        public string mode;
        public string queuedAt;
        public string expiresAt;
    }

    [Serializable]
    public class OnlineMatchActionRequest
    {
        public string type;
        public string actionId;
        public string cardId;
        public string attackerInstanceId;
        public string targetInstanceId;
        public string message;
        public int expectedVersion;
    }

    [Serializable]
    public class OnlineMatchActionResponse
    {
        public bool success;
        public bool idempotentReplay;
        public OnlineMatchState match;
        public OnlineMatchEvent @event;
    }

    [Serializable]
    public class OnlineMatchEventsResponse
    {
        public bool success;
        public OnlineMatchState match;
        public OnlineMatchEvent[] events;
        public int latestSequence;
        public bool integrityVerified;
    }

    [Serializable]
    public class OnlineMatchState
    {
        public string id;
        public string mode;
        public string status;
        public int version;
        public int round;
        public string phase;
        public string activeSide;
        public string yourSide;
        public string rulesVersion;
        public string seedCommitment;
        public string seedReveal;
        public int appreciationToWin;
        public OnlineMatchPlayer[] players;
        public OnlineMatchResult result;
        public OnlineMatchIntegrity integrity;
        public string updatedAt;
    }

    [Serializable]
    public class OnlineMatchPlayer
    {
        public string accountId;
        public string displayName;
        public string side;
        public int appreciation;
        public int health;
        public int deckCount;
        public int handCount;
        public OnlineMatchCard[] hand;
        public OnlineMatchBoardCard[] board;
        public OnlineMatchCard[] discard;
        public bool committedThisTurn;
    }

    [Serializable]
    public class OnlineMatchCard
    {
        public string id;
        public string name;
        public string rarity;
        public string type;
        public int attack;
        public int defense;
        public int appreciation;
        public string effectId;
        public string discardEffectId;
    }

    [Serializable]
    public class OnlineMatchBoardCard : OnlineMatchCard
    {
        public string instanceId;
        public int currentAttack;
        public int currentDefense;
        public bool exhausted;
        public int builtRound;
    }

    [Serializable]
    public class OnlineMatchEvent
    {
        public int sequence;
        public string actionId;
        public string type;
        public string actorId;
        public string side;
        public int round;
        public string phase;
        public string createdAt;
        public string previousHash;
        public string hash;
    }

    [Serializable]
    public class OnlineMatchResult
    {
        public string winnerSide;
        public string reason;
        public string completedAt;
    }

    [Serializable]
    public class OnlineMatchIntegrity
    {
        public string algorithm;
        public string latestHash;
        public int eventCount;
    }
}
