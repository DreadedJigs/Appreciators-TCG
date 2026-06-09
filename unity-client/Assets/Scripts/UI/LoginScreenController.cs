using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class LoginScreenController : ScreenControllerBase
    {
        private InputField nameInput;

        private void Start()
        {
            GameObject panel = CreateCenteredPanel("Appreciators TCG");
            UIFactory.CreateText(panel.transform, "Be Original", 34, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateText(panel.transform, "We appreciate art.\nWe appreciate community.\nWe appreciate the blockchain.", 24, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);

            nameInput = UIFactory.CreateInputField(panel.transform, "Player name", LocalSaveSystem.LoadPlayerName());
            UIFactory.CreateButton(panel.transform, "Guest Login", Login, UIFactory.Green);
            UIFactory.CreateButton(panel.transform, "Future Wallet Connect", () => SceneManager.LoadScene("Web3MockScene"), UIFactory.Blue);
        }

        private void Login()
        {
            LocalSaveSystem.SavePlayerName(nameInput.text);
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}
