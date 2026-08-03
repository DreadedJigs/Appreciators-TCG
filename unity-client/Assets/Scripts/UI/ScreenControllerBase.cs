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
            GameObject safeArea = new GameObject("MobileSafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(canvas.transform, false);
            Root = safeArea.GetComponent<RectTransform>();
            UIFactory.Stretch(Root);
            safeArea.AddComponent<MobileSafeAreaFitter>();

            GameObject background = UIFactory.CreatePanel(Root, "Background", UIFactory.Background);
            UIFactory.Stretch(background.GetComponent<RectTransform>());
            UIFactory.CreateBackdrop(Root);
        }

        protected GameObject CreateCenteredPanel(string title, int titleSize = 44)
        {
            GameObject panel = UIFactory.CreateVerticalStack(Root, "Content", UIFactory.GlassPanel, 12, 24);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.20f, 0.04f), new Vector2(0.80f, 0.96f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(panel.transform, title, titleSize + 2, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            return panel;
        }

        protected GameObject CreateFullScreenStack(string title)
        {
            GameObject panel = UIFactory.CreateVerticalStack(Root, "Content", UIFactory.GlassPanel, 10, 16);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(panel.transform, title, 38, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            return panel;
        }

        protected Button BackButton(Transform parent, string sceneName = "MainMenuScene")
        {
            return UIFactory.CreateButton(parent, "Back", () => SceneManager.LoadScene(sceneName), UIFactory.PanelAlt);
        }
    }
}
