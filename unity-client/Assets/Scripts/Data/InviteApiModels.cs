using System;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public class InviteRoomRequest
    {
        public string username;
        public string[] deckIds;
    }

    [Serializable]
    public class InviteStartRequest
    {
        public string username;
        public string playerId;
    }

    [Serializable]
    public class InviteRoomStatusResponse
    {
        public InviteRoom room;
    }

    [Serializable]
    public class InviteMatchStateResponse
    {
        public InviteRoom room;
        public InviteMatchState matchState;
    }

    [Serializable]
    public class InviteRoomMutationResponse
    {
        public InviteRoom room;
        public InvitePlayer player;
        public InviteAssignment assignment;
    }

    [Serializable]
    public class InviteActionListResponse
    {
        public InviteRoom room;
        public InviteMatchAction[] actions;
    }

    [Serializable]
    public class InviteActionMutationResponse
    {
        public InviteRoom room;
        public InviteMatchAction action;
    }

    [Serializable]
    public class InviteMatchAction
    {
        public int sequence;
        public string actionId;
        public string type;
        public string playerId;
        public string username;
        public string role;
        public string cardId;
        public string lane;
        public int turn;
        public string createdAt;
    }

    [Serializable]
    public class InviteAssignment
    {
        public string matchId;
        public string inviteCode;
        public string mode;
        public string status;
        public InvitePlayer[] players;
        public string transport;
        public string message;
    }

    [Serializable]
    public class InviteRoom
    {
        public string inviteCode;
        public string matchId;
        public string mode;
        public string status;
        public string createdAt;
        public string updatedAt;
        public string startedAt;
        public InvitePlayer host;
        public InvitePlayer guest;
        public InvitePlayer[] players;
        public int maxPlayers;
        public InviteMatchState matchState;
        public string message;
    }

    [Serializable]
    public class InvitePlayer
    {
        public string id;
        public string role;
        public string username;
        public int deckSize;
        public bool connected;
        public string joinedAt;
    }

    [Serializable]
    public class InviteMatchState
    {
        public string status;
        public int currentTurn;
        public int maxTurn;
        public InviteRoleInts energy;
        public InviteRoleBools endedTurn;
        public InviteLaneState[] lanes;
        public InviteMatchResult result;
        public int version;
        public string message;
    }

    [Serializable]
    public class InviteRoleInts
    {
        public int host;
        public int guest;
    }

    [Serializable]
    public class InviteRoleBools
    {
        public bool host;
        public bool guest;
    }

    [Serializable]
    public class InviteLaneState
    {
        public string lane;
        public InviteCardEntry[] host;
        public InviteCardEntry[] guest;
        public int hostPower;
        public int guestPower;
        public string winner;
    }

    [Serializable]
    public class InviteCardEntry
    {
        public int sequence;
        public string cardId;
        public string name;
        public int power;
        public int appreciation;
        public string lane;
        public string playedAt;
    }

    [Serializable]
    public class InviteMatchResult
    {
        public InviteLaneState[] laneScores;
        public int hostLaneWins;
        public int guestLaneWins;
        public string winner;
    }
}
