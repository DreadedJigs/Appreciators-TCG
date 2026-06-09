using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class CollectionScreenController : ScreenControllerBase
    {
        private void Start()
        {
            GameObject screen = CreateFullScreenStack("Collection");
            UIFactory.CreateText(screen.transform, "All Phase 1 prototype cards", 22, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);

            RectTransform content = UIFactory.CreateScrollContent(screen.transform, "CollectionScroll", false, out _);
            foreach (CardDefinition card in CardCatalog.AllCards)
            {
                UIFactory.CreateCardPanel(content, card.name, CardBody(card), ColorForType(card.type));
            }

            BackButton(screen.transform);
        }

        private static string CardBody(CardDefinition card)
        {
            string affinity = string.IsNullOrWhiteSpace(card.laneAffinity) ? "Any lane" : card.laneAffinity;
            return $"Cost {card.cost} | Power {card.power}\n{card.type} | {affinity}\n{card.effectText}";
        }

        private static Color ColorForType(string type)
        {
            if (type == GameConstants.Original)
            {
                return new Color(0.18f, 0.14f, 0.23f);
            }

            if (type == GameConstants.Companion)
            {
                return new Color(0.10f, 0.18f, 0.22f);
            }

            if (type == GameConstants.Trait)
            {
                return new Color(0.18f, 0.16f, 0.10f);
            }

            return new Color(0.12f, 0.18f, 0.13f);
        }
    }
}
