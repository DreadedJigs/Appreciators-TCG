using AppreciatorsTcg.Cards;
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
                UIFactory.CreateCardPanel(content, card);
            }

            BackButton(screen.transform);
        }
    }
}
