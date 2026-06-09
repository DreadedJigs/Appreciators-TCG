using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MatchScreenController : ScreenControllerBase
    {
        private BattleGame game;
        private Text statusText;
        private Text messageText;
        private RectTransform lanesContent;
        private RectTransform handContent;
        private int selectedHandIndex = -1;

        private void Start()
        {
            game = new BattleGame(LocalSaveSystem.LoadPlayerName(), PlayerDeckService.LoadDeckOrStarter());
            game.Start();

            GameObject screen = CreateFullScreenStack("Casual Match");

            GameObject topBar = UIFactory.CreateHorizontalStack(screen.transform, "TopBar", UIFactory.Panel, 10, 8);
            LayoutElement topLayout = topBar.AddComponent<LayoutElement>();
            topLayout.preferredHeight = 74;
            statusText = UIFactory.CreateText(topBar.transform, string.Empty, 24, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            UIFactory.CreateButton(topBar.transform, "End Turn", EndTurn, UIFactory.Accent);

            lanesContent = UIFactory.CreateHorizontalStack(screen.transform, "Lanes", Color.clear, 12, 0).GetComponent<RectTransform>();
            LayoutElement lanesLayout = lanesContent.gameObject.AddComponent<LayoutElement>();
            lanesLayout.flexibleHeight = 1;

            messageText = UIFactory.CreateText(screen.transform, string.Empty, 22, TextAnchor.MiddleLeft, UIFactory.Accent);
            handContent = UIFactory.CreateScrollContent(screen.transform, "Hand", true, out _);

            UpdateScreen();
        }

        private void UpdateScreen()
        {
            statusText.text = $"Turn {game.Turn}/{GameConstants.MaxTurn}    Energy {game.Player.Energy}    Opponent Energy {game.Opponent.Energy}";
            messageText.text = game.LastMessage;

            UIFactory.ClearChildren(lanesContent);
            CreateLane(LaneType.Art);
            CreateLane(LaneType.Community);
            CreateLane(LaneType.Blockchain);

            UIFactory.ClearChildren(handContent);
            for (int i = 0; i < game.Player.Hand.Count; i++)
            {
                int handIndex = i;
                CardDefinition card = game.Player.Hand[i];
                int cost = game.GetEffectiveCost(game.Player, card);
                string selected = selectedHandIndex == i ? "SELECTED\n" : string.Empty;
                string body = $"{selected}{card.name}\nCost {cost} | Power {card.power}\n{card.type}\n{card.effectText}";
                Button button = UIFactory.CreateButton(handContent, body, () => SelectHandCard(handIndex), selectedHandIndex == i ? UIFactory.Accent : UIFactory.PanelAlt);
                LayoutElement layout = button.GetComponent<LayoutElement>();
                layout.minWidth = 240;
                layout.preferredWidth = 260;
                layout.minHeight = 170;
                layout.preferredHeight = 185;
            }
        }

        private void CreateLane(LaneType laneType)
        {
            LaneState lane = game.GetLane(laneType);
            GameObject lanePanel = UIFactory.CreateVerticalStack(lanesContent, laneType.ToString(), UIFactory.Panel, 8, 10);
            LayoutElement layout = lanePanel.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;

            int opponentPower = game.GetLanePower(laneType, OwnerSide.Opponent);
            int playerPower = game.GetLanePower(laneType, OwnerSide.Player);
            UIFactory.CreateText(lanePanel.transform, $"AI {opponentPower}", 24, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            UIFactory.CreateText(lanePanel.transform, CardsText(lane, OwnerSide.Opponent), 18, TextAnchor.UpperCenter, UIFactory.MutedTextColor);
            UIFactory.CreateText(lanePanel.transform, laneType.ToString(), 30, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateText(lanePanel.transform, CardsText(lane, OwnerSide.Player), 18, TextAnchor.UpperCenter, UIFactory.TextColor);
            UIFactory.CreateText(lanePanel.transform, $"You {playerPower}", 24, TextAnchor.MiddleCenter, UIFactory.Green, FontStyle.Bold);

            LaneType capturedLane = laneType;
            Button playButton = UIFactory.CreateButton(lanePanel.transform, selectedHandIndex >= 0 ? "Play Here" : "Select Card", () => PlaySelectedCard(capturedLane), UIFactory.Blue);
            playButton.interactable = selectedHandIndex >= 0 && lane.HasSpace(OwnerSide.Player);
        }

        private static string CardsText(LaneState lane, OwnerSide side)
        {
            if (lane.GetCards(side).Count == 0)
            {
                return "Empty";
            }

            return string.Join("\n", lane.GetCards(side).ConvertAll(card => card.ShortLabel()));
        }

        private void SelectHandCard(int handIndex)
        {
            selectedHandIndex = handIndex;
            UpdateScreen();
        }

        private void PlaySelectedCard(LaneType lane)
        {
            if (selectedHandIndex < 0)
            {
                return;
            }

            game.TryPlayPlayerCard(selectedHandIndex, lane, out _);
            selectedHandIndex = -1;
            UpdateScreen();
        }

        private void EndTurn()
        {
            selectedHandIndex = -1;
            game.EndPlayerTurnAndRunAi();

            if (game.IsComplete)
            {
                SceneManager.LoadScene("ResultsScene");
                return;
            }

            UpdateScreen();
        }
    }
}
