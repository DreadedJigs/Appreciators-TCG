using System.Collections;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.Packs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class ResultsScreenController : ScreenControllerBase
    {
        private Text rewardText;
        private Text winningsBannerText;

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
                CreateMatchWinningsBanner(result);
                UIFactory.CreateText(
                    panel.transform,
                    $"Growth: You {result.playerGrowth} - AI {result.opponentGrowth}",
                    28,
                    TextAnchor.MiddleCenter,
                    UIFactory.Accent,
                    FontStyle.Bold);
                UIFactory.CreateText(
                    panel.transform,
                    $"{result.turnsPlayed} turns • Spotlight at {GameConstants.SpotlightGrowthThreshold} • first to {GameConstants.GrowthVictoryTarget}",
                    22,
                    TextAnchor.MiddleCenter,
                    UIFactory.TextColor);

                bool rankedLoss = result.winner == "Defeat" && string.Equals(result.mode, "Ranked", System.StringComparison.OrdinalIgnoreCase);
                if (result.winner == "Victory" || rankedLoss)
                {
                    rewardText = UIFactory.CreateText(
                        panel.transform,
                        result.winner == "Victory"
                            ? "Victory reward: securing 69 Appreciation Shards..."
                            : "Ranked result: applying the 5 Appreciation Shard loss...",
                        22,
                        TextAnchor.MiddleCenter,
                        UIFactory.Accent,
                        FontStyle.Bold);
                    StartCoroutine(ClaimMatchReward(result));
                }
                else
                {
                    rewardText = UIFactory.CreateText(panel.transform, "No Appreciation Shard change for this result.", 19, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
                }
            }

            UIFactory.CreateButton(panel.transform, "Play Again", () => SceneManager.LoadScene("MatchScene"), UIFactory.Green);
            UIFactory.CreateButton(panel.transform, "Main Menu", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);
        }

        private void CreateMatchWinningsBanner(MatchResult result)
        {
            if (result == null || result.winner != "Victory") return;

            GameObject banner = UIFactory.CreateVerticalStack(Root, "MatchWinningsBanner", UIFactory.GlassPanel, 3, 5);
            RectTransform rect = banner.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(0.16f, 0.84f), new Vector2(0.84f, 0.96f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(banner, UIFactory.Green);
            UIFactory.CreateText(banner.transform, "MATCH WON", 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            winningsBannerText = UIFactory.CreateText(banner.transform, "+69 APPRECIATION SHARDS", 29, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
        }

        private IEnumerator ClaimMatchReward(MatchResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.matchId))
            {
                rewardText.text = "Match completed, but the reward match ID is unavailable.";
                rewardText.color = UIFactory.Red;
                yield break;
            }

            BackendApiClient apiClient = gameObject.AddComponent<BackendApiClient>();
            MatchWinRewardResponse response = null;
            string requestError = null;
            yield return apiClient.ClaimMatchResultReward(
                LocalSaveSystem.LoadOrCreatePlayerId(),
                result.matchId,
                result.winner,
                string.IsNullOrWhiteSpace(result.mode) ? "Casual" : result.mode,
                value => response = value,
                error => requestError = error);

            // Keep victory rewards compatible with the currently deployed alpha
            // while /match-result rolls out. Ranked losses require the new route.
            if (response?.success != true && result.winner == "Victory")
            {
                yield return apiClient.ClaimMatchWinReward(
                    LocalSaveSystem.LoadOrCreatePlayerId(),
                    result.matchId,
                    value => response = value,
                    error => requestError = error);
            }

            if (response?.success == true)
            {
                if (response.inventory != null)
                {
                    new PackInventoryService(new PackSaveService()).ReplaceWithAuthoritativeSnapshot(response.inventory);
                }

                int change = response.shardsChanged != 0 ? response.shardsChanged : response.shardsAwarded;
                rewardText.text = response.idempotentReplay
                    ? $"Match reward already settled | {response.totalShardBalance:N0} Appreciation Shards"
                    : $"{change:+#;-#;0} Appreciation Shards | {response.totalShardBalance:N0} total";
                rewardText.color = change < 0 ? UIFactory.Red : UIFactory.Green;
                if (winningsBannerText != null)
                {
                    winningsBannerText.text = response.idempotentReplay
                        ? "REWARD ALREADY SECURED"
                        : $"{change:+#;-#;0} APPRECIATION SHARDS";
                    winningsBannerText.color = change < 0 ? UIFactory.Red : UIFactory.Accent;
                }
            }
            else
            {
                rewardText.text = "Match reward is pending while the Appreciation Shard service is unavailable.";
                rewardText.color = UIFactory.Red;
                Debug.LogError($"[Economy] Match reward claim failed for '{result.matchId}': {requestError}");
            }
        }
    }
}
