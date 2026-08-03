using AppreciatorsTcg.UI;
using AppreciatorsTcg.Packs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppreciatorsTcg.Core
{
    public class SceneBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            LocalSaveSystem.EnsureDarkModeDefault();
            string sceneName = SceneManager.GetActiveScene().name;

            switch (sceneName)
            {
                case "Main":
                case "LoginScene":
                    gameObject.AddComponent<LoginScreenController>();
                    break;
                case "MainMenuScene":
                    gameObject.AddComponent<MainMenuController>();
                    break;
                case "CollectionScene":
                    gameObject.AddComponent<CollectionScreenController>();
                    break;
                case "DeckBuilderScene":
                    gameObject.AddComponent<DeckBuilderScreenController>();
                    break;
                case "InviteMatchScene":
                    gameObject.AddComponent<InviteMatchController>();
                    break;
                case "PackOpeningScene":
                    gameObject.AddComponent<PackOpeningController>();
                    break;
                case "MatchScene":
                    gameObject.AddComponent<MatchScreenController>();
                    break;
                case "ResultsScene":
                    gameObject.AddComponent<ResultsScreenController>();
                    break;
                case "Web3MockScene":
                    gameObject.AddComponent<Web3MockScreenController>();
                    break;
                case "BossBattleScene":
                    gameObject.AddComponent<BossBattleScreenController>();
                    break;
                default:
                    Debug.LogWarning($"No screen controller registered for {sceneName}.");
                    break;
            }
        }
    }
}
