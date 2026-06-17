using System;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MatchScreenController : ScreenControllerBase
    {
        private const int MatchHandCardWidth = 142;
        private const int MatchHandCardHeight = 212;

        private BattleGame game;
        private BackendApiClient apiClient;
        private Text statusText;
        private Text messageText;
        private Button endTurnButton;
        private RectTransform opponentHudContent;
        private RectTransform playerHudContent;
        private RectTransform resourceContent;
        private RectTransform opponentHandContent;
        private RectTransform lanesContent;
        private RectTransform handContent;
        private ScrollRect handScrollRect;
        private string opponentLabel = "AI";
        private string matchIntro;
        private bool inviteMatch;
        private string inviteCode;
        private string invitePlayerId;
        private string invitePlayerRole;
        private InviteMatchState latestInviteState;
        private int lastInviteActionSequence;
        private int lastInviteStateVersion = -1;
        private int localActionCounter;
        private int selectedHandIndex = -1;
        private bool localInviteTurnEnded;
        private bool remoteInviteTurnEnded;
        private int remotePlayedCards;
        private int lastClickedHandIndex = -1;
        private float lastClickedHandTime = -1f;

        private void Start()
        {
            string pendingMode = LocalSaveSystem.LoadPendingMatchMode();
            inviteCode = LocalSaveSystem.LoadPendingInviteCode();
            string pendingMatchId = LocalSaveSystem.LoadPendingMatchId();
            string pendingOpponent = LocalSaveSystem.LoadPendingOpponentName();
            invitePlayerId = LocalSaveSystem.LoadPendingPlayerId();
            invitePlayerRole = LocalSaveSystem.LoadPendingPlayerRole();
            inviteMatch = pendingMode == "Invite 1v1";
            LocalSaveSystem.ClearPendingMatchContext();

            if (inviteMatch)
            {
                apiClient = gameObject.AddComponent<BackendApiClient>();
                opponentLabel = string.IsNullOrWhiteSpace(pendingOpponent) ? "Opponent" : pendingOpponent;
                matchIntro = $"Invite 1v1 started. Code {inviteCode}. Match {pendingMatchId}.";
            }

            game = new BattleGame(LocalSaveSystem.LoadPlayerName(), PlayerDeckService.LoadDeckOrStarter());
            game.Start();

            GameObject screen = CreateMatchTable(inviteMatch ? "Invite 1v1" : "Casual");

            GameObject opponentRow = UIFactory.CreateHorizontalStack(screen.transform, "OpponentZone", Color.clear, 12, 0);
            HorizontalLayoutGroup opponentGroup = opponentRow.GetComponent<HorizontalLayoutGroup>();
            opponentGroup.childForceExpandWidth = false;
            opponentGroup.childForceExpandHeight = false;
            opponentGroup.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement opponentRowLayout = opponentRow.AddComponent<LayoutElement>();
            opponentRowLayout.minHeight = 138;
            opponentRowLayout.preferredHeight = 150;

            GameObject opponentHudSlot = UIFactory.CreateHorizontalStack(opponentRow.transform, "OpponentHudSlot", Color.clear, 0, 0);
            LayoutElement opponentHudSlotLayout = opponentHudSlot.AddComponent<LayoutElement>();
            opponentHudSlotLayout.minWidth = 168;
            opponentHudSlotLayout.preferredWidth = 190;
            opponentHudSlotLayout.flexibleWidth = 0;
            opponentHudContent = opponentHudSlot.GetComponent<RectTransform>();

            GameObject opponentCards = UIFactory.CreateHorizontalStack(opponentRow.transform, "OpponentHandCards", Color.clear, 8, 4);
            HorizontalLayoutGroup opponentCardsGroup = opponentCards.GetComponent<HorizontalLayoutGroup>();
            opponentCardsGroup.childForceExpandWidth = false;
            opponentCardsGroup.childForceExpandHeight = false;
            opponentCardsGroup.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement opponentCardsLayout = opponentCards.AddComponent<LayoutElement>();
            opponentCardsLayout.flexibleWidth = 1;
            opponentCardsLayout.minHeight = 138;
            opponentCardsLayout.preferredHeight = 150;
            opponentHandContent = opponentCards.GetComponent<RectTransform>();

            GameObject resourcePanel = UIFactory.CreateVerticalStack(opponentRow.transform, "RoundResources", new Color(0.08f, 0.12f, 0.15f, 0.54f), 6, 8);
            LayoutElement resourcePanelLayout = resourcePanel.AddComponent<LayoutElement>();
            resourcePanelLayout.minWidth = 112;
            resourcePanelLayout.preferredWidth = 124;
            resourcePanelLayout.flexibleWidth = 0;
            statusText = UIFactory.CreateText(resourcePanel.transform, string.Empty, 18, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            resourceContent = UIFactory.CreateVerticalStack(resourcePanel.transform, "ResourceBadges", Color.clear, 5, 0).GetComponent<RectTransform>();

            GameObject boardRow = UIFactory.CreateHorizontalStack(screen.transform, "BoardRow", Color.clear, 12, 0);
            HorizontalLayoutGroup boardGroup = boardRow.GetComponent<HorizontalLayoutGroup>();
            boardGroup.childForceExpandWidth = false;
            LayoutElement boardLayout = boardRow.AddComponent<LayoutElement>();
            boardLayout.flexibleHeight = 1;

            lanesContent = UIFactory.CreateHorizontalStack(boardRow.transform, "Lanes", Color.clear, 10, 0).GetComponent<RectTransform>();
            LayoutElement lanesLayout = lanesContent.gameObject.AddComponent<LayoutElement>();
            lanesLayout.flexibleWidth = 1;
            lanesLayout.flexibleHeight = 1;

            messageText = UIFactory.CreateText(screen.transform, string.Empty, 20, TextAnchor.MiddleLeft, UIFactory.Accent);
            LayoutElement messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
            messageLayout.preferredHeight = 28;

            GameObject playerRow = UIFactory.CreateHorizontalStack(screen.transform, "PlayerZone", Color.clear, 12, 0);
            HorizontalLayoutGroup playerGroup = playerRow.GetComponent<HorizontalLayoutGroup>();
            playerGroup.childForceExpandWidth = false;
            playerGroup.childForceExpandHeight = false;
            playerGroup.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement playerRowLayout = playerRow.AddComponent<LayoutElement>();
            playerRowLayout.minHeight = 232;
            playerRowLayout.preferredHeight = 244;

            GameObject playerHudSlot = UIFactory.CreateHorizontalStack(playerRow.transform, "PlayerHudSlot", Color.clear, 0, 0);
            LayoutElement playerHudSlotLayout = playerHudSlot.AddComponent<LayoutElement>();
            playerHudSlotLayout.minWidth = 168;
            playerHudSlotLayout.preferredWidth = 190;
            playerHudSlotLayout.flexibleWidth = 0;
            playerHudContent = playerHudSlot.GetComponent<RectTransform>();

            handContent = UIFactory.CreateScrollContent(playerRow.transform, "Hand", true, out handScrollRect, true);
            Image handImage = handScrollRect.GetComponent<Image>();
            if (handImage != null)
            {
                handImage.color = new Color(0.25f, 0.12f, 0.08f, 0.38f);
            }

            LayoutElement handLayout = handScrollRect.GetComponent<LayoutElement>();
            handLayout.flexibleHeight = 0;
            handLayout.flexibleWidth = 1;
            handLayout.minHeight = 220;
            handLayout.preferredHeight = 232;

            endTurnButton = UIFactory.CreateButton(playerRow.transform, "END TURN", EndTurn, new Color(1.00f, 0.48f, 0.05f));
            LayoutElement endLayout = endTurnButton.gameObject.GetComponent<LayoutElement>();
            endLayout.minWidth = 128;
            endLayout.preferredWidth = 144;
            endLayout.flexibleWidth = 0;
            endLayout.minHeight = 190;
            endLayout.preferredHeight = 204;

            UpdateScreen();

            if (inviteMatch)
            {
                InvokeRepeating(nameof(PollInviteActions), 1.0f, 1.25f);
            }
        }

        private void UpdateScreen()
        {
            UpdateHud();
            statusText.text = $"TURN {game.Turn}/{GameConstants.MaxTurn}\nYOU ENERGY {game.Player.Energy}\nOPP ENERGY {game.Opponent.Energy}";
            messageText.text = string.IsNullOrWhiteSpace(matchIntro) ? game.LastMessage : $"{matchIntro}\n{game.LastMessage}";

            if (endTurnButton != null)
            {
                endTurnButton.interactable = !game.IsComplete && (!inviteMatch || !localInviteTurnEnded);
            }

            UpdateOpponentHand();

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
                GameObject cardPanel = UIFactory.CreateMatchHandCardPanel(
                    handContent,
                    card,
                    () => HandleHandCardClick(handIndex),
                    selectedHandIndex == i,
                    $"Cost now {cost}");
                CardInspectionTrigger trigger = cardPanel.AddComponent<CardInspectionTrigger>();
                trigger.Card = card;
                MatchHandCardInput dragInput = cardPanel.AddComponent<MatchHandCardInput>();
                dragInput.Controller = this;
                dragInput.HandIndex = handIndex;
                dragInput.Card = card;
            }
        }

        private GameObject CreateMatchTable(string modeLabel)
        {
            GameObject table = UIFactory.CreateVerticalStack(Root, "MatchTable", new Color(0.015f, 0.030f, 0.040f, 0.24f), 5, 8);
            UIFactory.SetAnchors(table.GetComponent<RectTransform>(), new Vector2(0.008f, 0.018f), new Vector2(0.992f, 0.982f), Vector2.zero, Vector2.zero);
            Text title = UIFactory.CreateText(table.transform, $"APPRECIATORS TCG  |  {modeLabel.ToUpperInvariant()}", 24, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 26;
            return table;
        }

        private void UpdateHud()
        {
            if (opponentHudContent != null)
            {
                UIFactory.ClearChildren(opponentHudContent);
                UIFactory.CreateHudPlate(opponentHudContent, opponentLabel, 30, game.Opponent.Energy, GameConstants.MaxTurn, true);
            }

            if (playerHudContent != null)
            {
                UIFactory.ClearChildren(playerHudContent);
                UIFactory.CreateHudPlate(playerHudContent, game.Player.DisplayName, 30, game.Player.Energy, GameConstants.MaxTurn, false);
            }

            if (resourceContent != null)
            {
                UIFactory.ClearChildren(resourceContent);
                resourceContent.gameObject.SetActive(false);
            }
        }

        private void CreateLane(LaneType laneType)
        {
            LaneState lane = game.GetLane(laneType);
            GameObject lanePanel = UIFactory.CreateVerticalStack(lanesContent, laneType.ToString(), new Color(0.45f, 0.78f, 0.88f, 0.20f), 4, 5);
            Image laneImage = lanePanel.GetComponent<Image>();
            if (UIAssetPack.Apply(laneImage, LaneTexturePath(laneType), false))
            {
                laneImage.color = new Color(1f, 1f, 1f, 0.08f);
            }

            LayoutElement layout = lanePanel.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            MatchLaneDropZone dropZone = lanePanel.AddComponent<MatchLaneDropZone>();
            dropZone.Controller = this;
            dropZone.Lane = laneType;

            int opponentPower = game.GetLanePower(laneType, OwnerSide.Opponent);
            int playerPower = game.GetLanePower(laneType, OwnerSide.Player);
            CreateLaneHeader(lanePanel.transform, laneType, opponentPower, playerPower);
            CreateBoardCardRow(lanePanel.transform, lane, OwnerSide.Opponent);
            CreateLaneControlBand(lanePanel.transform, opponentPower, playerPower);
            CreateBoardCardRow(lanePanel.transform, lane, OwnerSide.Player);

            LaneType capturedLane = laneType;
            Button playButton = UIFactory.CreateButton(lanePanel.transform, selectedHandIndex >= 0 ? "PLAY HERE" : "DROP CARD", () => PlaySelectedCard(capturedLane), UIFactory.Blue);
            LayoutElement playLayout = playButton.gameObject.GetComponent<LayoutElement>();
            playLayout.minHeight = 42;
            playLayout.preferredHeight = 48;
            playButton.interactable = selectedHandIndex >= 0 && lane.HasSpace(OwnerSide.Player) && CanPlayDuringInviteTurn();
        }

        private void CreateLaneHeader(Transform parent, LaneType laneType, int opponentPower, int playerPower)
        {
            GameObject header = UIFactory.CreateHorizontalStack(parent, $"{laneType}Header", new Color(0.07f, 0.15f, 0.18f, 0.58f), 6, 7);
            HorizontalLayoutGroup group = header.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childControlHeight = true;
            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.minHeight = 42;
            layout.preferredHeight = 46;
            layout.flexibleHeight = 0;

            CreateScorePill(header.transform, "OPP", opponentPower, UIFactory.Red);

            GameObject titlePanel = UIFactory.CreateVerticalStack(header.transform, "LaneTitle", Color.clear, 1, 0);
            LayoutElement titleLayout = titlePanel.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;
            titleLayout.minHeight = 34;
            titleLayout.preferredHeight = 38;
            titleLayout.flexibleHeight = 0;
            UIFactory.CreateText(titlePanel.transform, laneType.ToString().ToUpperInvariant(), 21, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateText(titlePanel.transform, LaneSubLabel(laneType), 10, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);

            CreateScorePill(header.transform, "YOU", playerPower, UIFactory.Green);
        }

        private void CreateScorePill(Transform parent, string label, int score, Color color)
        {
            GameObject pill = UIFactory.CreateVerticalStack(parent, $"{label}Score", new Color(0.96f, 0.84f, 0.62f, 0.86f), 0, 4);
            LayoutElement layout = pill.AddComponent<LayoutElement>();
            layout.minWidth = 58;
            layout.preferredWidth = 66;
            layout.minHeight = 34;
            layout.preferredHeight = 38;
            layout.flexibleWidth = 0;
            layout.flexibleHeight = 0;
            UIFactory.CreateText(pill.transform, label, 9, TextAnchor.MiddleCenter, UIFactory.CreamInk, FontStyle.Bold);
            UIFactory.CreateText(pill.transform, score.ToString(), 20, TextAnchor.MiddleCenter, color, FontStyle.Bold);
        }

        private void CreateLaneControlBand(Transform parent, int opponentPower, int playerPower)
        {
            string status = playerPower == opponentPower ? "CONTESTED" : playerPower > opponentPower ? "WINNING" : "LOSING";
            Color statusColor = playerPower == opponentPower ? UIFactory.Accent : playerPower > opponentPower ? UIFactory.Green : UIFactory.Red;
            Text text = UIFactory.CreateText(parent, status, 15, TextAnchor.MiddleCenter, statusColor, FontStyle.Bold);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 18;
            layout.preferredHeight = 22;
        }

        private static string LaneSubLabel(LaneType laneType)
        {
            switch (laneType)
            {
                case LaneType.Art:
                    return "CREATIVITY";
                case LaneType.Community:
                    return "ALLY BUFFS";
                case LaneType.Blockchain:
                    return "TECH POWER";
                default:
                    return "LANE";
            }
        }

        private static string LaneTexturePath(LaneType laneType)
        {
            switch (laneType)
            {
                case LaneType.Art:
                    return "05_board/background_crops/left_lane_floor_texture.png";
                case LaneType.Community:
                    return "05_board/background_crops/middle_lane_floor_texture.png";
                case LaneType.Blockchain:
                    return "05_board/background_crops/right_lane_floor_texture.png";
                default:
                    return "05_board/background_crops/battlefield_midfield_wide.png";
            }
        }

        private void CreateBoardCardRow(Transform parent, LaneState lane, OwnerSide side)
        {
            GameObject row = UIFactory.CreateHorizontalStack(parent, $"{side}BoardCards", new Color(0.64f, 0.90f, 1.00f, 0.12f), 6, 6);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 76;
            rowLayout.preferredHeight = 86;

            if (lane.GetCards(side).Count == 0)
            {
                Text empty = UIFactory.CreateText(row.transform, "Empty Slot", 14, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
                LayoutElement emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
                emptyLayout.flexibleWidth = 1;
                emptyLayout.preferredHeight = 64;
                return;
            }

            foreach (BattleCardInstance instance in lane.GetCards(side))
            {
                GameObject miniCard = UIFactory.CreateMiniCardPanel(row.transform, instance.Definition, $"{instance.CurrentPower}/{instance.CurrentAppreciation}");
                CardInspectionTrigger trigger = miniCard.AddComponent<CardInspectionTrigger>();
                trigger.Card = instance.Definition;
            }
        }

        private void UpdateOpponentHand()
        {
            if (opponentHandContent == null)
            {
                return;
            }

            UIFactory.ClearChildren(opponentHandContent);
            int handCount = inviteMatch
                ? Mathf.Clamp(GameConstants.StartingHandSize + game.Turn * GameConstants.CardsDrawnPerTurn - remotePlayedCards, 0, 8)
                : Mathf.Clamp(game.Opponent.Hand.Count, 0, 8);

            if (handCount == 0)
            {
                UIFactory.CreateText(opponentHandContent, "No cards", 18, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
                return;
            }

            for (int i = 0; i < handCount; i++)
            {
                UIFactory.CreateCardBackPanel(opponentHandContent, "APP", Mathf.RoundToInt(MatchHandCardWidth * 0.72f), Mathf.RoundToInt(MatchHandCardHeight * 0.72f));
            }
        }

        private void HandleHandCardClick(int handIndex)
        {
            if (handIndex == lastClickedHandIndex && Time.unscaledTime - lastClickedHandTime <= 0.42f)
            {
                lastClickedHandIndex = -1;
                lastClickedHandTime = -1f;
                PlayHandCardAutomatically(handIndex);
                return;
            }

            lastClickedHandIndex = handIndex;
            lastClickedHandTime = Time.unscaledTime;
            SelectHandCard(handIndex);
        }

        private void SelectHandCard(int handIndex)
        {
            selectedHandIndex = handIndex;
            UpdateScreen();
        }

        public bool CanStartCardDrag(int handIndex)
        {
            if (game == null || game.IsComplete || !CanPlayDuringInviteTurn())
            {
                return false;
            }

            if (handIndex < 0 || handIndex >= game.Player.Hand.Count)
            {
                return false;
            }

            CardDefinition card = game.Player.Hand[handIndex];
            return game.Player.Energy >= game.GetEffectiveCost(game.Player, card) && game.GetOpenLanes(OwnerSide.Player).Count > 0;
        }

        public void MarkDraggingHandCard(int handIndex)
        {
            selectedHandIndex = handIndex;
        }

        public void CancelDraggingHandCard()
        {
            selectedHandIndex = -1;
            UpdateScreen();
        }

        public void PlayHandCardFromDrop(int handIndex, LaneType lane)
        {
            selectedHandIndex = handIndex;
            PlaySelectedCard(lane);
        }

        private void PlayHandCardAutomatically(int handIndex)
        {
            if (!CanStartCardDrag(handIndex))
            {
                SelectHandCard(handIndex);
                return;
            }

            selectedHandIndex = handIndex;
            PlaySelectedCard(ChooseAutoLane(game.Player.Hand[handIndex]));
        }

        private LaneType ChooseAutoLane(CardDefinition card)
        {
            foreach (LaneType laneType in game.GetOpenLanes(OwnerSide.Player))
            {
                if (card.HasLaneAffinity(laneType.ToString()))
                {
                    return laneType;
                }
            }

            return game.GetOpenLanes(OwnerSide.Player)
                .OrderBy(laneType => game.GetLanePower(laneType, OwnerSide.Player))
                .ThenBy(laneType => game.GetLanePower(laneType, OwnerSide.Opponent))
                .FirstOrDefault();
        }

        private void PlaySelectedCard(LaneType lane)
        {
            if (selectedHandIndex < 0)
            {
                return;
            }

            if (!CanPlayDuringInviteTurn())
            {
                selectedHandIndex = -1;
                UpdateScreen();
                return;
            }

            if (selectedHandIndex >= game.Player.Hand.Count)
            {
                selectedHandIndex = -1;
                UpdateScreen();
                return;
            }

            CardDefinition selectedCard = game.Player.Hand[selectedHandIndex];
            bool played = game.TryPlayPlayerCard(selectedHandIndex, lane, out _);
            selectedHandIndex = -1;

            if (inviteMatch && played)
            {
                RecordInviteAction("play-card", selectedCard.id, lane.ToString());
            }

            UpdateScreen();
        }

        private void EndTurn()
        {
            selectedHandIndex = -1;
            if (inviteMatch)
            {
                if (localInviteTurnEnded)
                {
                    UpdateScreen();
                    return;
                }

                localInviteTurnEnded = true;
                RecordInviteAction("end-turn", string.Empty, string.Empty);
                if (TryAdvanceInviteTurn())
                {
                    return;
                }
            }
            else
            {
                game.EndPlayerTurnAndRunAi();
            }

            if (game.IsComplete)
            {
                SceneManager.LoadScene("ResultsScene");
                return;
            }

            UpdateScreen();
        }

        private void RecordInviteAction(string actionType, string cardId, string lane)
        {
            if (apiClient == null || string.IsNullOrWhiteSpace(inviteCode) || string.IsNullOrWhiteSpace(invitePlayerId))
            {
                return;
            }

            localActionCounter += 1;
            string actionId = $"{invitePlayerId}-{localActionCounter}";
            StartCoroutine(apiClient.RecordInviteAction(
                inviteCode,
                invitePlayerId,
                actionId,
                actionType,
                cardId,
                lane,
                game.Turn,
                _ => { },
                error => Debug.LogWarning($"Invite action sync failed: {error}")));
        }

        private void PollInviteActions()
        {
            if (apiClient == null || string.IsNullOrWhiteSpace(inviteCode))
            {
                return;
            }

            StartCoroutine(apiClient.GetInviteActions(inviteCode, lastInviteActionSequence, response =>
            {
                if (response?.room?.matchState != null)
                {
                    latestInviteState = response.room.matchState;
                }

                if (response?.actions == null || response.actions.Length == 0)
                {
                    if (latestInviteState != null && latestInviteState.version != lastInviteStateVersion)
                    {
                        lastInviteStateVersion = latestInviteState.version;
                        UpdateScreen();
                    }
                    return;
                }

                bool changed = false;
                foreach (InviteMatchAction action in response.actions)
                {
                    lastInviteActionSequence = Math.Max(lastInviteActionSequence, action.sequence);
                    if (action.playerId == invitePlayerId)
                    {
                        continue;
                    }

                    if (action.type == "play-card" && TryParseLane(action.lane, out LaneType laneType))
                    {
                        bool applied = game.ApplyRemoteCard(action.cardId, laneType, action.username, out _);
                        if (applied)
                        {
                            remotePlayedCards += 1;
                        }

                        changed |= applied;
                    }
                    else if (action.type == "end-turn" && action.turn == game.Turn)
                    {
                        remoteInviteTurnEnded = true;
                        changed = true;
                    }
                }

                if (TryAdvanceInviteTurn())
                {
                    return;
                }

                if (latestInviteState != null && latestInviteState.version != lastInviteStateVersion)
                {
                    lastInviteStateVersion = latestInviteState.version;
                    changed = true;
                }

                if (changed)
                {
                    UpdateScreen();
                }
            }, error => Debug.LogWarning($"Invite action poll failed: {error}")));
        }

        private bool CanPlayDuringInviteTurn()
        {
            return !inviteMatch || !localInviteTurnEnded;
        }

        private string InviteStatusSuffix()
        {
            if (!inviteMatch)
            {
                return string.Empty;
            }

            string waitState = localInviteTurnEnded && remoteInviteTurnEnded
                ? "advancing"
                : localInviteTurnEnded
                    ? "waiting for opponent"
                    : remoteInviteTurnEnded
                        ? "opponent ended"
                        : "live";

            string serverState = latestInviteState == null
                ? string.Empty
                : $"    Server Turn {latestInviteState.currentTurn}/{latestInviteState.maxTurn}";

            return $"    Online {waitState}{serverState}";
        }

        private bool TryAdvanceInviteTurn()
        {
            if (!inviteMatch || !localInviteTurnEnded || !remoteInviteTurnEnded)
            {
                return false;
            }

            localInviteTurnEnded = false;
            remoteInviteTurnEnded = false;
            selectedHandIndex = -1;
            game.EndPlayerTurnOnly();

            if (game.IsComplete)
            {
                SceneManager.LoadScene("ResultsScene");
                return true;
            }

            UpdateScreen();
            return false;
        }

        private static bool TryParseLane(string laneName, out LaneType laneType)
        {
            return Enum.TryParse(laneName, true, out laneType);
        }

        private void OnDestroy()
        {
            CardInspectionOverlay.Hide();
            CancelInvoke(nameof(PollInviteActions));
        }
    }
}
