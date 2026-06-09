using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public abstract class ScreenControllerBase : MonoBehaviour
    {
        protected RectTransform Root { get; private set; }

        protected virtual void Awake()
        {
            Canvas canvas = UIFactory.CreateCanvas(GetType().Name);
            Root = canvas.GetComponent<RectTransform>();

            GameObject background = UIFactory.CreatePanel(Root, "Background", UIFactory.Background);
            UIFactory.Stretch(background.GetComponent<RectTransform>());
        }

        protected GameObject CreateCenteredPanel(string title, int titleSize = 44)
        {
            GameObject panel = UIFactory.CreateVerticalStack(Root, "Content", new Color(0.09f, 0.10f, 0.14f), 12, 24);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.20f, 0.04f), new Vector2(0.80f, 0.96f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(panel.transform, title, titleSize, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            return panel;
        }

        protected GameObject CreateFullScreenStack(string title)
        {
            GameObject panel = UIFactory.CreateVerticalStack(Root, "Content", new Color(0.07f, 0.08f, 0.12f), 12, 18);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(panel.transform, title, 38, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            return panel;
        }

        protected Button BackButton(Transform parent, string sceneName = "MainMenuScene")
        {
            return UIFactory.CreateButton(parent, "Back", () => SceneManager.LoadScene(sceneName), UIFactory.PanelAlt);
        }
    }
}
