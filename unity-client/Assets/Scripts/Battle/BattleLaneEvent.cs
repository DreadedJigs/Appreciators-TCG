using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public enum BattleLaneEventType
    {
        ArtFirstPlayPower,
        ArtStrongestAppreciation,
        CommunityBuffEncore,
        CommunityHelpingHands,
        BlockchainCostRebate,
        BlockchainMintPulse
    }

    public sealed class BattleLaneEvent
    {
        public LaneType Lane { get; set; }
        public BattleLaneEventType EventType { get; set; }
        public string Title { get; set; }
        public string ShortText { get; set; }
        public string RulesText { get; set; }

        public static BattleLaneEvent Create(LaneType lane, int roll)
        {
            switch (lane)
            {
                case LaneType.Art:
                    return roll % 2 == 0
                        ? new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.ArtFirstPlayPower,
                            Title = "Spotlight Brush",
                            ShortText = "First play +1 Power",
                            RulesText = "The first card each side plays in Art each turn gains +1 Power."
                        }
                        : new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.ArtStrongestAppreciation,
                            Title = "Critique Spark",
                            ShortText = "Art cards +1 APP",
                            RulesText = "Cards whose strongest lane is Art gain +1 Appreciation when played here."
                        };
                case LaneType.Community:
                    return roll % 2 == 0
                        ? new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.CommunityBuffEncore,
                            Title = "Crowd Echo",
                            ShortText = "Buffs +1",
                            RulesText = "Friendly buff effects in Community grant +1 extra."
                        }
                        : new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.CommunityHelpingHands,
                            Title = "Helping Hands",
                            ShortText = "First play heals allies",
                            RulesText = "The first card each side plays in Community each turn gives adjacent allies +1 Appreciation."
                        };
                case LaneType.Blockchain:
                    return roll % 2 == 0
                        ? new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.BlockchainCostRebate,
                            Title = "Gas Rebate",
                            ShortText = "First play costs -1",
                            RulesText = "The first card each side plays in Blockchain each turn costs 1 less Appreciation."
                        }
                        : new BattleLaneEvent
                        {
                            Lane = lane,
                            EventType = BattleLaneEventType.BlockchainMintPulse,
                            Title = "Mint Pulse",
                            ShortText = "First play +1/+1",
                            RulesText = "The first card each side plays in Blockchain each turn gains +1 Power and +1 Appreciation."
                        };
                default:
                    return null;
            }
        }
    }
}
