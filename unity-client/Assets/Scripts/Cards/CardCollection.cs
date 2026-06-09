using System;
using System.Collections.Generic;

namespace AppreciatorsTcg.Cards
{
    [Serializable]
    public class CardCollection
    {
        public List<CardDefinition> cards = new List<CardDefinition>();
    }
}
