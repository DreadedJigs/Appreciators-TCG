using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class ResultsScreenController : ScreenControllerBase
    {
        private void Start()
        {
            MatchResult result = MatchResultData.LastResult;
            GameObject panel = CreateCenteredPanel(result == null ? "Match Result" : result.winner);

            if (result == null)
            {
                UIFactory.CreateText(panel.transform, "No match result is available yet.", 24, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            }
            else
            {
                UIFactory.CreateText(panel.transform, $"Lane wins: You {result.playerLaneWins} - AI {result.opponentLaneWins}", 26, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
                foreach (LaneScoreResult lane in result.laneScores)
                {
                    UIFactory.CreateText(panel.transform, $"{lane.lane}: You {lane.playerPower} / AI {lane.opponentPower} / {lane.winner}", 24, TextAnchor.MiddleCenter, UIFactory.TextColor);
                }
            }

            UIFactory.CreateButton(panel.transform, "Play Again", () => SceneManager.LoadScene("MatchScene"), UIFactory.Green);
            UIFactory.CreateButton(panel.transform, "Main Menu", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);
        }
    }
}
