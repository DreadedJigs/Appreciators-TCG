using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class Web3MockScreenController : ScreenControllerBase
    {
        private InputField apiInput;
        private Text messageText;

        private void Start()
        {
            GameObject panel = CreateCenteredPanel("Wallet / Web3");
            UIFactory.CreateText(panel.transform, "Wallet verification coming in Phase 4\nORIGINAL ownership sync coming in Phase 4\nCOMPANION ownership sync coming in Phase 4\nHolder cosmetics coming in Phase 4\nNFT rewards coming in Phase 4", 24, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);

            UIFactory.CreateText(panel.transform, "Backend API Base URL", 22, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            apiInput = UIFactory.CreateInputField(panel.transform, AppConfig.DefaultApiBaseUrl, AppConfig.ApiBaseUrl);
            messageText = UIFactory.CreateText(panel.transform, "Phase 1 runs offline if the backend is unavailable.", 20, TextAnchor.MiddleCenter, UIFactory.Accent);

            UIFactory.CreateButton(panel.transform, "Save API URL", SaveApiUrl, UIFactory.Blue);
            UIFactory.CreateButton(panel.transform, "Main Menu", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);
        }

        private void SaveApiUrl()
        {
            LocalSaveSystem.SaveApiBaseUrl(apiInput.text);
            messageText.text = "API URL saved locally.";
        }
    }
}
