using System;
using System.Collections.Generic;
using AppreciatorsTcg.Cards;

namespace AppreciatorsTcg.Battle
{
    public class BattlePlayerState
    {
        public BattlePlayerState(string displayName, List<CardDefinition> deck, int seed)
        {
            DisplayName = displayName;
            DrawPile = new List<CardDefinition>(deck);
            Hand = new List<CardDefinition>();
            Shuffle(DrawPile, new Random(seed));
        }

        public string DisplayName { get; }
        public List<CardDefinition> DrawPile { get; }
        public List<CardDefinition> Hand { get; }
        public HashSet<string> ReturnedAfterDefeatIds { get; } = new HashSet<string>();
        public int Energy { get; set; }

        public void DrawCards(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                DrawCard();
            }
        }

        public bool DrawCard()
        {
            if (DrawPile.Count == 0)
            {
                return false;
            }

            Hand.Add(DrawPile[0]);
            DrawPile.RemoveAt(0);
            return true;
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
