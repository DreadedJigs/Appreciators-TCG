using System;
using System.Collections;
using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class BossBattleScreenController : ScreenControllerBase
    {
        private const string PoolId = "alpha_boss";
        private BackendApiClient apiClient;
        private string playerId;
        private BossBattleState battle;
        private int shardBalance;
        private bool requestActive;
        private Text vaultText;
        private Image vaultFill;
        private Text bossText;
        private Text bossModeOpenText;
        private PremiumTextShimmer bossModeShimmer;
        private Text partyText;
        private Text resultText;
        private Text statusText;
        private Button contributeButton;
        private Button fundButton;
        private Button joinButton;
        private Button readyButton;
        private Button bossRoleButton;
        private Button challengeButton;
        private Button practiceButton;

        private void Start()
        {
            apiClient = gameObject.AddComponent<BackendApiClient>();
            playerId = LocalSaveSystem.LoadOrCreatePlayerId();

            GameObject shell = UIFactory.CreatePanel(Root, "BossBattleShell", UIFactory.GlassPanel);
            UIFactory.SetAnchors(shell.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);

            GameObject header = UIFactory.CreateVerticalStack(shell.transform, "BossHeader", UIFactory.PanelAlt, 0, 5);
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.03f, 0.83f), new Vector2(0.97f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.Red);
            Text headerTitle = UIFactory.CreateText(header.transform, "BOSS BATTLES", 31, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            SetHeight(headerTitle.gameObject, 34);
            Text headerSubtitle = UIFactory.CreateText(header.transform, "POOL SHARDS  •  SUMMON A 1-OF-1  •  FORM A PARTY  •  CHALLENGE", 14, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(headerSubtitle.gameObject, 22);

            GameObject rulesPanel = CreateRulesPanel(shell.transform);
            UIFactory.SetAnchors(rulesPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.81f), Vector2.zero, Vector2.zero);
            GameObject vaultPanel = CreateVaultPanel(shell.transform);
            UIFactory.SetAnchors(vaultPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.20f), new Vector2(0.325f, 0.70f), Vector2.zero, Vector2.zero);
            GameObject bossPanel = CreateBossPanel(shell.transform);
            UIFactory.SetAnchors(bossPanel.GetComponent<RectTransform>(), new Vector2(0.345f, 0.20f), new Vector2(0.655f, 0.70f), Vector2.zero, Vector2.zero);
            CreateResultPanel(bossPanel.transform);
            GameObject partyPanel = CreatePartyPanel(shell.transform);
            UIFactory.SetAnchors(partyPanel.GetComponent<RectTransform>(), new Vector2(0.675f, 0.20f), new Vector2(0.97f, 0.70f), Vector2.zero, Vector2.zero);

            GameObject footer = UIFactory.CreateHorizontalStack(shell.transform, "BossFooter", Color.clear, 8, 0);
            UIFactory.SetAnchors(footer.GetComponent<RectTransform>(), new Vector2(0.03f, 0.035f), new Vector2(0.97f, 0.145f), Vector2.zero, Vector2.zero);
            challengeButton = UIFactory.CreateButton(footer.transform, "CHALLENGE", PrimaryAction, UIFactory.Red);
            practiceButton = UIFactory.CreateButton(footer.transform, "BOSS vs AI PRACTICE", StartPractice, UIFactory.PortalViolet);
            UIFactory.CreateButton(footer.transform, "REFRESH", () => StartCoroutine(RefreshRoutine()), UIFactory.Blue);
            UIFactory.CreateButton(footer.transform, "WALLET", () => SceneManager.LoadScene("Web3MockScene"), UIFactory.PortalViolet);
            UIFactory.CreateButton(footer.transform, "MAIN MENU", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);

            statusText = UIFactory.CreateText(shell.transform, "Connecting to the shared Boss Arena...", 17, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            UIFactory.SetAnchors(statusText.rectTransform, new Vector2(0.03f, 0.15f), new Vector2(0.97f, 0.195f), Vector2.zero, Vector2.zero);
            StartCoroutine(RefreshRoutine());
            StartCoroutine(PollRoutine());
        }

        private static GameObject CreateRulesPanel(Transform parent)
        {
            GameObject rules = UIFactory.CreateHorizontalStack(parent, "EncounterRules", UIFactory.PanelAlt, 8, 10);
            UIFactory.MakeDimensionalPanel(rules, UIFactory.Accent);
            CreateRule(rules.transform, "1 MEMBER", "ALWAYS LOSES", UIFactory.Red);
            CreateRule(rules.transform, "2 MEMBERS", "MINIMUM • HARD", UIFactory.Accent);
            CreateRule(rules.transform, "3 MEMBERS", "NOMINAL PARTY", UIFactory.Green);
            return rules;
        }

        private static void CreateRule(Transform parent, string title, string subtitle, Color color)
        {
            GameObject card = UIFactory.CreateVerticalStack(parent, title, new Color(color.r, color.g, color.b, 0.18f), 0, 4);
            Text titleText = UIFactory.CreateText(card.transform, title, 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(titleText.gameObject, 18);
            Text subtitleText = UIFactory.CreateText(card.transform, subtitle, 12, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            SetHeight(subtitleText.gameObject, 14);
        }

        private GameObject CreateVaultPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "SummonVault", UIFactory.Panel, 6, 12);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Accent);
            UIFactory.CreateText(panel.transform, "COMMUNITY SUMMON VAULT", 23, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            vaultText = UIFactory.CreateText(panel.transform, "Loading shard pool...", 19, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            GameObject bar = UIFactory.CreatePanel(panel.transform, "VaultProgress", new Color(0.02f, 0.02f, 0.08f, 0.94f));
            SetHeight(bar, 34);
            GameObject fill = UIFactory.CreatePanel(bar.transform, "NativeVaultFill", UIFactory.Accent);
            vaultFill = fill.GetComponent<Image>();
            UIFactory.SetAnchors(fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            GameObject actions = UIFactory.CreateHorizontalStack(panel.transform, "VaultActions", Color.clear, 8, 0);
            contributeButton = UIFactory.CreateButton(actions.transform, "CONTRIBUTE 100", () => Contribute(false), UIFactory.PortalViolet);
            fundButton = UIFactory.CreateButton(actions.transform, "FUND REMAINDER", () => Contribute(true), UIFactory.Accent);
            return panel;
        }

        private GameObject CreateBossPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "BossSeat", UIFactory.GlassPanel, 6, 12);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Red);
            UIFactory.CreateText(panel.transform, "1-OF-1 BOSS SEAT", 23, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            bossText = UIFactory.CreateText(panel.transform, "Waiting for summon status...", 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(bossText.gameObject, 68);
            bossModeOpenText = UIFactory.CreateText(panel.transform, "☻  BOSS MODE OPEN  ☻", 18, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(bossModeOpenText.gameObject, 26);
            bossModeShimmer = bossModeOpenText.gameObject.AddComponent<PremiumTextShimmer>();
            bossModeShimmer.Configure(bossModeOpenText);
            bossModeOpenText.gameObject.SetActive(false);
            bossRoleButton = UIFactory.CreateButton(panel.transform, "CLAIM VERIFIED BOSS SEAT", ToggleBossRole, UIFactory.Red);
            return panel;
        }

        private GameObject CreatePartyPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "MemberParty", UIFactory.GlassPanel, 6, 12);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Green);
            UIFactory.CreateText(panel.transform, "MEMBER PARTY", 23, TextAnchor.MiddleCenter, UIFactory.Green, FontStyle.Bold);
            partyText = UIFactory.CreateText(panel.transform, "No members have entered the arena.", 18, TextAnchor.UpperCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(partyText.gameObject, 92);
            GameObject actions = UIFactory.CreateHorizontalStack(panel.transform, "PartyActions", Color.clear, 8, 0);
            joinButton = UIFactory.CreateButton(actions.transform, "JOIN PARTY", ToggleParty, UIFactory.Green);
            readyButton = UIFactory.CreateButton(actions.transform, "READY", ToggleReady, UIFactory.Blue);
            Text teamNote = UIFactory.CreateText(panel.transform, "2 MINIMUM  •  3 NOMINAL", 13, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            SetHeight(teamNote.gameObject, 18);
            return panel;
        }

        private void CreateResultPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "BattleReport", UIFactory.PanelAlt, 6, 12);
            UIFactory.CreateText(panel.transform, "LAST BATTLE", 17, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            resultText = UIFactory.CreateText(panel.transform, "No challenge has resolved yet.", 15, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            SetHeight(resultText.gameObject, 64);
        }

        private IEnumerator PollRoutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(4f);
                if (!requestActive) yield return RefreshRoutine(false);
            }
        }

        private IEnumerator RefreshRoutine(bool showStatus = true)
        {
            if (requestActive) yield break;
            requestActive = true;
            if (showStatus) SetStatus("Syncing the shared Boss Arena...", UIFactory.MutedTextColor);
            BossBattleResponse battleResponse = null;
            PackInventoryResponse inventoryResponse = null;
            string requestError = null;
            yield return apiClient.GetBossBattle(PoolId, playerId, value => battleResponse = value, error => requestError = error);
            yield return apiClient.GetPackInventory(playerId, value => inventoryResponse = value, error => requestError = requestError ?? error);
            requestActive = false;

            if (battleResponse?.battle != null)
            {
                battle = battleResponse.battle;
                shardBalance = inventoryResponse?.inventory?.appreciationShards ?? shardBalance;
                RefreshDisplay();
                if (showStatus) SetStatus("Boss Arena synchronized.", UIFactory.Green);
            }
            else if (showStatus)
            {
                SetStatus($"Boss Arena unavailable. {ReadableError(requestError)}", UIFactory.Red);
            }
        }

        private void RefreshDisplay()
        {
            if (battle == null || battle.pool == null) return;
            BossPoolStatus pool = battle.pool;
            float ratio = pool.targetShards <= 0 ? 0f : Mathf.Clamp01((float)pool.totalShards / pool.targetShards);
            RectTransform fillRect = vaultFill == null ? null : vaultFill.rectTransform;
            if (fillRect != null) fillRect.anchorMax = new Vector2(ratio, 1f);
            vaultText.text = pool.unlocked
                ? $"SUMMON COMPLETE  •  {pool.totalShards:N0}/{pool.targetShards:N0} SHARDS  •  {pool.contributors} CONTRIBUTORS"
                : $"{pool.totalShards:N0}/{pool.targetShards:N0} SHARDS  •  {pool.remainingShards:N0} NEEDED  •  YOUR BALANCE {shardBalance:N0}";

            BossIdentity boss = battle.boss;
            string verification = boss != null && boss.verifiedOneOfOne ? "SERVER-VERIFIED 1-OF-1 HOLDER" : "PROVISIONAL AI • HOLDER SEAT OPEN";
            bossText.text = $"{boss?.displayName ?? "1-of-1 Boss"}\n{verification}\n{boss?.walletDisplay ?? ""}";

            BossPartyMember[] members = battle.party ?? Array.Empty<BossPartyMember>();
            partyText.text = members.Length == 0
                ? "SLOT 1  OPEN\nSLOT 2  OPEN\nSLOT 3  OPEN"
                : string.Join("\n", Enumerable.Range(0, 3).Select(index =>
                    index < members.Length
                        ? $"SLOT {index + 1}  {(members[index].displayName ?? "Member").ToUpperInvariant()}  •  {(members[index].ready ? "READY" : "NOT READY")}"
                        : $"SLOT {index + 1}  OPEN"));

            resultText.text = battle.lastBattle == null
                ? "No challenge has resolved yet. Solo entry is allowed for demonstration, but it is a guaranteed defeat."
                : $"{(battle.lastBattle.practice ? "PRACTICE" : (battle.lastBattle.result ?? "resolved").Replace('-', ' ').ToUpperInvariant())}  •  {(battle.lastBattle.difficulty ?? "boss").Replace('-', ' ').ToUpperInvariant()}\n" +
                  $"PARTY {battle.lastBattle.partyPower}  vs  BOSS {battle.lastBattle.bossPower}\n{battle.lastBattle.summary ?? "Battle resolved."}";

            bool summoned = pool.unlocked;
            int contribution = Mathf.Min(100, pool.remainingShards);
            contributeButton.interactable = !requestActive && !summoned && contribution > 0 && shardBalance >= contribution;
            fundButton.interactable = !requestActive && !summoned && pool.remainingShards > 0 && shardBalance >= pool.remainingShards;
            SetButtonLabel(contributeButton, summoned ? "SUMMONED" : $"CONTRIBUTE {contribution:N0}");
            SetButtonLabel(fundButton, summoned ? "VAULT COMPLETE" : $"FUND {pool.remainingShards:N0}");

            BossCurrentPlayer current = battle.currentPlayer ?? new BossCurrentPlayer();
            bool canPractice = current.oneOfOneEligible;
            practiceButton.interactable = !requestActive && canPractice;
            SetButtonLabel(practiceButton, canPractice ? "BOSS vs AI PRACTICE" : "VERIFY 1-OF-1 TO PRACTICE");
            if (bossModeOpenText != null)
            {
                bool bossModeOpen = current.oneOfOneEligible;
                bossModeOpenText.gameObject.SetActive(bossModeOpen);
                if (bossModeShimmer != null) bossModeShimmer.enabled = bossModeOpen;
            }
            joinButton.interactable = summoned && !current.isBoss && (current.inParty || battle.partySize < (battle.rules?.maximumPartySize ?? 3));
            SetButtonLabel(joinButton, current.inParty ? "LEAVE PARTY" : "JOIN PARTY");
            readyButton.interactable = summoned && current.inParty;
            SetButtonLabel(readyButton, current.ready ? "SET NOT READY" : "READY");
            bossRoleButton.interactable = summoned && (current.isBoss || current.oneOfOneEligible);
            SetButtonLabel(bossRoleButton, current.isBoss ? "RELEASE BOSS SEAT" : current.oneOfOneEligible ? "CLAIM VERIFIED BOSS SEAT" : "1-OF-1 WALLET REQUIRED");
            challengeButton.interactable = summoned
                ? battle.canStart && (current.inParty || current.isBoss)
                : contribution > 0 && shardBalance >= contribution;
            SetButtonLabel(
                challengeButton,
                !summoned
                    ? $"CONTRIBUTE {contribution:N0}"
                    : current.isBoss
                        ? "CHALLENGE MEMBERS"
                        : battle.partySize == 1 ? "SOLO CHALLENGE • GUARANTEED LOSS" : "CHALLENGE BOSS");
        }

        private void Contribute(bool fullRemainder)
        {
            if (battle?.pool == null || requestActive) return;
            int amount = fullRemainder ? battle.pool.remainingShards : Mathf.Min(100, battle.pool.remainingShards);
            StartCoroutine(ContributeRoutine(amount));
        }

        private IEnumerator ContributeRoutine(int amount)
        {
            requestActive = true;
            SetStatus($"Contributing {amount:N0} Appreciation Shards...", UIFactory.Accent);
            BossContributionResponse response = null;
            string requestError = null;
            yield return apiClient.ContributeBossShards($"boss_{Guid.NewGuid():N}", playerId, PoolId, amount, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.success == true)
            {
                shardBalance = response.inventory?.appreciationShards ?? shardBalance;
                SetStatus(response.unlocked ? "The 1-of-1 boss has been summoned." : $"Contributed {amount:N0} shards to the community vault.", UIFactory.Green);
                yield return RefreshRoutine(false);
            }
            else SetStatus(ReadableError(requestError), UIFactory.Red);
        }

        private void ToggleParty()
        {
            if (battle?.currentPlayer == null || requestActive) return;
            StartCoroutine(BossMutationRoutine(
                battle.currentPlayer.inParty ? "Leaving the member party..." : "Joining the member party...",
                (success, error) => battle.currentPlayer.inParty
                    ? apiClient.LeaveBossParty(PoolId, playerId, success, error)
                    : apiClient.JoinBossParty(PoolId, playerId, LocalSaveSystem.LoadPlayerName(), success, error)));
        }

        private void ToggleReady()
        {
            if (battle?.currentPlayer == null || requestActive) return;
            bool ready = !battle.currentPlayer.ready;
            StartCoroutine(BossMutationRoutine(ready ? "Marking ready..." : "Standing down...", (success, error) => apiClient.SetBossPartyReady(PoolId, playerId, ready, success, error)));
        }

        private void ToggleBossRole()
        {
            if (battle?.currentPlayer == null || requestActive) return;
            StartCoroutine(BossMutationRoutine(
                battle.currentPlayer.isBoss ? "Releasing the boss seat..." : "Verifying 1-of-1 boss eligibility...",
                (success, error) => battle.currentPlayer.isBoss
                    ? apiClient.ReleaseBossRole(PoolId, playerId, success, error)
                    : apiClient.ClaimBossRole(PoolId, playerId, LocalSaveSystem.LoadPlayerName(), success, error)));
        }

        private void PrimaryAction()
        {
            if (battle?.pool == null || requestActive) return;
            if (!battle.pool.unlocked)
            {
                Contribute(false);
                return;
            }
            Challenge();
        }

        private void Challenge()
        {
            if (requestActive) return;
            StartCoroutine(BossMutationRoutine("Resolving the boss challenge...", (success, error) => apiClient.ChallengeBoss(PoolId, playerId, success, error)));
        }

        private void StartPractice()
        {
            if (requestActive || battle?.currentPlayer?.oneOfOneEligible != true) return;
            StartCoroutine(BossMutationRoutine(
                "Launching Standard Boss practice against the AI party...",
                (success, error) => apiClient.PracticeBossAgainstAi(PoolId, playerId, success, error)));
        }

        private delegate IEnumerator BossRequest(Action<BossBattleResponse> onSuccess, Action<string> onError);

        private IEnumerator BossMutationRoutine(string pendingMessage, BossRequest request)
        {
            requestActive = true;
            SetStatus(pendingMessage, UIFactory.Accent);
            BossBattleResponse response = null;
            string requestError = null;
            yield return request(value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.battle != null)
            {
                battle = response.battle;
                RefreshDisplay();
                SetStatus(battle.lastBattle != null && battle.status == "resolved" ? battle.lastBattle.summary : "Boss Arena updated.", UIFactory.Green);
            }
            else SetStatus(ReadableError(requestError), UIFactory.Red);
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = color;
        }

        private static string ReadableError(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "The shared boss service did not respond.";
            int marker = error.IndexOf("\"message\":\"", StringComparison.Ordinal);
            if (marker >= 0)
            {
                int start = marker + 11;
                int end = error.IndexOf('"', start);
                if (end > start) return error.Substring(start, end - start);
            }
            return error.Length > 180 ? error.Substring(0, 180) + "..." : error;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text == null) return;
            text.text = label;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = 19;
        }

        private static void SetHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}
