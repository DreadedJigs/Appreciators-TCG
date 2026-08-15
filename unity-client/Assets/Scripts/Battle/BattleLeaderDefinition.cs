using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public sealed class BattleLeaderDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public LaneType FocusLane { get; set; }
        public string AbilityName { get; set; }
        public string RulesText { get; set; }

        public static BattleLeaderDefinition SelectForDeck(IEnumerable<CardDefinition> deck)
        {
            LaneType focus = LaneType.Community;
            if (deck != null)
            {
                focus = deck
                    .Where(card => card != null)
                    .GroupBy(card => card.StrongestLane())
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key == LaneType.Community ? 0 : group.Key == LaneType.Art ? 1 : 2)
                    .Select(group => group.Key)
                    .DefaultIfEmpty(LaneType.Community)
                    .FirstOrDefault();
            }

            switch (focus)
            {
                case LaneType.Art:
                    return new BattleLeaderDefinition
                    {
                        Id = "gallery_director",
                        Name = "Gallery Director",
                        FocusLane = LaneType.Art,
                        AbilityName = "Spotlight",
                        RulesText = "Once per match: develop your strongest Art permanent +1 Growth and add 25% this Growth phase."
                    };
                case LaneType.Blockchain:
                    return new BattleLeaderDefinition
                    {
                        Id = "protocol_captain",
                        Name = "Protocol Captain",
                        FocusLane = LaneType.Blockchain,
                        AbilityName = "Gas Surge",
                        RulesText = "Once per match: queue +2 Growth and add 25% this Growth phase."
                    };
                default:
                    return new BattleLeaderDefinition
                    {
                        Id = "chainrider",
                        Name = "Chainrider",
                        FocusLane = LaneType.Community,
                        AbilityName = "Rally The Room",
                        RulesText = "Once per match: Community permanents develop +1 Growth and add 25% this Growth phase."
                    };
            }
        }
    }
}
