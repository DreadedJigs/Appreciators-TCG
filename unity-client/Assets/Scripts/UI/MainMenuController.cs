using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MainMenuController : ScreenControllerBase
    {
        private void Start()
        {
            GameObject panel = CreateCenteredPanel("Appreciators TCG", 42);
            UIFactory.CreateText(panel.transform, $"Welcome, {LocalSaveSystem.LoadPlayerName()}", 26, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            UIFactory.CreateText(panel.transform, "Be Original", 30, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);

            UIFactory.CreateButton(panel.transform, "Play Casual", () => SceneManager.LoadScene("MatchScene"), UIFactory.Green);
            UIFactory.CreateButton(panel.transform, "Collection", () => SceneManager.LoadScene("CollectionScene"), UIFactory.PanelAlt);
            UIFactory.CreateButton(panel.transform, "Deck Builder", () => SceneManager.LoadScene("DeckBuilderScene"), UIFactory.PanelAlt);
            CreateDisabled(panel.transform, "Ranked Coming Soon");
            CreateDisabled(panel.transform, "Community Wars Coming Soon");
            CreateDisabled(panel.transform, "Boss Battles Coming Soon");
            UIFactory.CreateButton(panel.transform, "Wallet / Web3 Coming Soon", () => SceneManager.LoadScene("Web3MockScene"), UIFactory.Blue);
        }

        private static void CreateDisabled(Transform parent, string label)
        {
            Button button = UIFactory.CreateButton(parent, label, () => { }, UIFactory.PanelAlt);
            button.interactable = false;
        }
    }
}
