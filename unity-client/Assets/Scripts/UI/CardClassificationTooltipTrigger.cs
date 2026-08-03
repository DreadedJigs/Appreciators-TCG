using AppreciatorsTcg.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public sealed class CardClassificationTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardDefinition Card { get; set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Application.isMobilePlatform) CardClassificationTooltip.Show(Card, transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!Application.isMobilePlatform) CardClassificationTooltip.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Application.isMobilePlatform) return;
            CardClassificationTooltip.Show(Card, transform);
            eventData?.Use();
        }

        private void OnDisable() => CardClassificationTooltip.Hide();
    }

    public static class CardClassificationTooltip
    {
        private static GameObject tooltip;

        public static void Show(CardDefinition card, Transform source)
        {
            Hide();
            if (card == null || source == null) return;
            Canvas canvas = source.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            tooltip = UIFactory.CreatePanel(canvas.transform, "ClassificationTooltip", UIFactory.Panel);
            RectTransform rect = tooltip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.08f);
            rect.anchorMax = new Vector2(0.5f, 0.08f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(430f, 105f);
            Text label = UIFactory.CreateText(tooltip.transform,
                $"◈  Archetype: {card.GetArchetype()}\n{UIFactory.PillarSymbol(card.GetPillar())}  Pillar: {card.GetPillar()}    {UIFactory.RaritySymbol(card.rarity)}  Rarity: {card.rarity}",
                20, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            UIFactory.Stretch(label.rectTransform, 12f);
            tooltip.transform.SetAsLastSibling();
        }

        public static void Hide()
        {
            if (tooltip != null) Object.Destroy(tooltip);
            tooltip = null;
        }
    }
}
