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
            UIFactory.CreateText(screen.transform, "Official metadata card collection", 22, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);

            RectTransform content = UIFactory.CreateGridScrollContent(screen.transform, "CollectionScroll", new Vector2(210f, 270f), 6, out _);
            foreach (CardDefinition card in CardCatalog.AllCards)
            {
                UIFactory.CreateCardPanel(content, card, compact: true);
            }

            BackButton(screen.transform);
        }
    }
}
