using System.Collections;
using System.Collections.Generic;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    // A Showdown-style, consequence-free raid drill. Once both sides are on
    // screen the encounter resolves itself; the battle log is the source of
    // truth for every attack, block, Shield interaction, and result.
    public sealed class BossPracticeMatchController : ScreenControllerBase
    {
        private const int BossMaximumHealth = 180;
        private const int AiMaximumHealth = 30;
        private const int MaximumRounds = 12;
        private readonly int[] aiHealth = { AiMaximumHealth, AiMaximumHealth, AiMaximumHealth };
        private readonly string[] aiNames = { "AI LEARN", "AI BUILD", "AI GROW" };
        private readonly List<string> showdownLog = new List<string>();
        private System.Random random;

        private int bossHealth = BossMaximumHealth;
        private int shield;
        private int actionPoints;
        private int round;
        private int signatureCooldown;
        private bool aiExhausted;
        private bool complete;
        private bool battleRunning;

        private Text bossStats;
        private Text statusText;
        private Text combatLogText;
        private readonly Text[] aiStats = new Text[3];
        private RectTransform bossVisual;
        private readonly RectTransform[] aiVisuals = new RectTransform[3];
        private Button replayButton;

        private void Start()
        {
            string bossName = LocalSaveSystem.LoadPendingOpponentName();
            string bossImageUrl = LocalSaveSystem.LoadPendingBossAssetImage();
            LocalSaveSystem.ClearPendingMatchContext();
            random = new System.Random(System.Environment.TickCount);
            RectTransform playmat = UIFactory.CreateOfficialPlaymatRoot(Root);
            GameObject shell = UIFactory.CreatePanel(playmat, "BossPracticeShell", Color.clear);
            UIFactory.SetAnchors(shell.GetComponent<RectTransform>(), new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.975f), Vector2.zero, Vector2.zero);

            CreateHeader(shell.transform);
            CreateBossSeat(shell.transform, bossName, bossImageUrl);
            CreateAiParty(shell.transform);
            CreateCombatLog(shell.transform);
            CreateFooter(shell.transform);
            ResetEncounter();
            StartCoroutine(RunShowdown());
        }

        private void CreateHeader(Transform parent)
        {
            GameObject header = UIFactory.CreateVerticalStack(parent, "PracticeHeader", UIFactory.PanelAlt, 3, 10);
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.18f, 0.915f), new Vector2(0.82f, 0.985f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.Accent);
            Text title = UIFactory.CreateText(header.transform, "1-OF-1 BOSS SHOWDOWN", 25, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(title.gameObject, 34);
            Text subtitle = UIFactory.CreateText(header.transform, "AUTO BATTLE  •  VERIFIED BOSS vs 3 AI MEMBERS  •  PRACTICE ONLY", 15, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(subtitle.gameObject, 22);
        }

        private void CreateBossSeat(Transform parent, string selectedBossName, string bossImageUrl)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "VerifiedBossSeat", UIFactory.Panel, 7, 12);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.18f, 0.59f), new Vector2(0.82f, 0.90f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Red);
            string labelText = string.IsNullOrWhiteSpace(selectedBossName) || selectedBossName == "AI Party"
                ? "YOUR VERIFIED 1-OF-1 BOSS"
                : selectedBossName.ToUpperInvariant() + "  •  1-OF-1 BOSS";
            Text label = UIFactory.CreateText(panel.transform, labelText, 18, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            SetHeight(label.gameObject, 28);

            GameObject portrait = UIFactory.CreatePanel(panel.transform, "VerifiedOwnerCard", UIFactory.Ink);
            bossVisual = portrait.GetComponent<RectTransform>();
            LayoutElement portraitLayout = portrait.AddComponent<LayoutElement>();
            portraitLayout.flexibleHeight = 1;
            Image portraitImage = portrait.GetComponent<Image>();
            Texture2D texture = Resources.Load<Texture2D>("Wallet/VerifiedOwnerCardReverse");
            if (texture != null)
            {
                portraitImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                portraitImage.preserveAspect = true;
                portraitImage.color = Color.white;
            }
            if (!string.IsNullOrWhiteSpace(bossImageUrl))
            {
                StartCoroutine(LoadBossPortraitRoutine(portraitImage, bossImageUrl));
            }
            bossStats = UIFactory.CreateText(panel.transform, string.Empty, 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(bossStats.gameObject, 58);
        }

        private static IEnumerator LoadBossPortraitRoutine(Image portrait, string imageUrl)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success || portrait == null) yield break;
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) yield break;
            portrait.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            portrait.preserveAspect = true;
            portrait.color = Color.white;
        }

        private void CreateAiParty(Transform parent)
        {
            GameObject panel = UIFactory.CreateHorizontalStack(parent, "AiMemberParty", UIFactory.Panel, 12, 14);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.40f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Blue);
            for (int index = 0; index < 3; index += 1)
            {
                GameObject member = UIFactory.CreateVerticalStack(panel.transform, $"AiMember{index + 1}", UIFactory.GlassPanel, 6, 10);
                aiVisuals[index] = member.GetComponent<RectTransform>();
                UIFactory.MakeDimensionalPanel(member, index == 0 ? UIFactory.Blue : index == 1 ? UIFactory.Green : UIFactory.PortalViolet);
                Text name = UIFactory.CreateText(member.transform, aiNames[index], 19, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
                SetHeight(name.gameObject, 28);
                Text role = UIFactory.CreateText(member.transform, index == 0 ? "LEARNER" : index == 1 ? "BUILDER" : "GROWER", 14, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
                SetHeight(role.gameObject, 22);
                aiStats[index] = UIFactory.CreateText(member.transform, string.Empty, 21, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
                GameObject card = UIFactory.CreatePanel(member.transform, "AiCard", UIFactory.CardBack);
                LayoutElement cardLayout = card.AddComponent<LayoutElement>();
                cardLayout.flexibleHeight = 1;
                UIFactory.CreateText(card.transform, "AI\nCARD", 20, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            }
        }

        private void CreateCombatLog(Transform parent)
        {
            GameObject panel = UIFactory.CreatePanel(parent, "ShowdownLog", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.92f));
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.55f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.NeonCyan);
            combatLogText = UIFactory.CreateText(panel.transform, string.Empty, 15, TextAnchor.UpperLeft, UIFactory.Cream, FontStyle.Bold);
            combatLogText.resizeTextForBestFit = true;
            combatLogText.resizeTextMinSize = 10;
            combatLogText.resizeTextMaxSize = 15;
            combatLogText.lineSpacing = 1.12f;
            UIFactory.SetAnchors(combatLogText.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f), Vector2.zero, Vector2.zero);
            statusText = UIFactory.CreateText(parent, "PREPARING SHOWDOWN...", 17, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.SetAnchors(statusText.rectTransform, new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.585f), Vector2.zero, Vector2.zero);
        }

        private void CreateFooter(Transform parent)
        {
            GameObject actions = UIFactory.CreateHorizontalStack(parent, "ShowdownFooter", Color.clear, 10, 0);
            UIFactory.SetAnchors(actions.GetComponent<RectTransform>(), new Vector2(0.30f, 0.015f), new Vector2(0.70f, 0.075f), Vector2.zero, Vector2.zero);
            replayButton = UIFactory.CreateButton(actions.transform, "REPLAY SHOWDOWN", BeginReplay, UIFactory.Green);
            replayButton.interactable = false;
            UIFactory.CreateButton(actions.transform, "EXIT PRACTICE", () => SceneManager.LoadScene("BossBattleScene"), UIFactory.PanelAlt);
        }

        private void BeginReplay()
        {
            if (battleRunning) return;
            ResetEncounter();
            StartCoroutine(RunShowdown());
        }

        private void ResetEncounter()
        {
            for (int index = 0; index < aiHealth.Length; index += 1) aiHealth[index] = AiMaximumHealth;
            bossHealth = BossMaximumHealth;
            shield = 0;
            actionPoints = 0;
            round = 1;
            signatureCooldown = 0;
            aiExhausted = false;
            complete = false;
            battleRunning = false;
            showdownLog.Clear();
            AppendLog("SHOWDOWN LOADED — verified Boss enters against AI Learn, Build, and Grow.");
            RefreshBoard("BATTLE ENDS AFTER A VICTORY, DEFEAT, OR ROUND 12.");
            if (replayButton != null) replayButton.interactable = false;
        }

        private IEnumerator RunShowdown()
        {
            battleRunning = true;
            statusText.text = "BATTLE STARTING...";
            yield return new WaitForSecondsRealtime(0.8f);
            AppendLog("ARENA LOCKED — the Boss wins initiative.");
            yield return new WaitForSecondsRealtime(0.7f);

            while (!complete && round <= MaximumRounds)
            {
                yield return BossTurn();
                if (complete) break;
                yield return AiTurn();
                if (complete) break;
                round += 1;
            }

            if (!complete)
            {
                complete = true;
                AppendLog("ROUND LIMIT REACHED — the Boss survives the encounter and wins the showdown.");
            }
            battleRunning = false;
            RefreshBoard(complete && bossHealth > 0 && !AnyAiAlive() ? "BOSS VICTORY" : bossHealth <= 0 ? "AI PARTY VICTORY" : "BOSS VICTORY — ROUND LIMIT");
            replayButton.interactable = true;
        }

        private IEnumerator BossTurn()
        {
            actionPoints = 3;
            statusText.text = $"ROUND {round} — BOSS TURN";
            AppendLog($"ROUND {round}: BOSS TURN — 3 Action Points available.");
            yield return new WaitForSecondsRealtime(0.5f);

            if (signatureCooldown == 0 && round % 4 == 0)
            {
                actionPoints = 0;
                signatureCooldown = 2;
                aiExhausted = true;
                AppendLog("BOSS uses GALLERY LOCK — all AI members are exhausted; their next attack is weakened.");
                yield return Pulse(bossVisual, UIFactory.PortalViolet, 0.32f);
                for (int index = 0; index < aiVisuals.Length; index += 1) yield return Pulse(aiVisuals[index], UIFactory.PortalViolet, 0.13f);
                RefreshBoard("GALLERY LOCK RESOLVED");
                yield return new WaitForSecondsRealtime(0.45f);
                yield break;
            }

            if (bossHealth <= 85 && actionPoints > 0)
            {
                actionPoints -= 1;
                shield = Mathf.Min(10, shield + 5);
                AppendLog("BOSS uses BRACE — gains 5 temporary Shield.");
                yield return Pulse(bossVisual, UIFactory.Blue, 0.28f);
                RefreshBoard("BOSS BRACES");
                yield return new WaitForSecondsRealtime(0.35f);
            }

            while (actionPoints > 0 && AnyAiAlive())
            {
                int target = StrongestAiTarget();
                bool crush = actionPoints >= 2 && aiHealth[target] >= 7;
                int cost = crush ? 2 : 1;
                int baseDamage = crush ? 7 : 4;
                actionPoints -= cost;
                bool blocked = random.NextDouble() < 0.28d;
                int damage = blocked ? Mathf.Max(1, baseDamage - 3) : baseDamage;
                aiHealth[target] = Mathf.Max(0, aiHealth[target] - damage);
                AppendLog(blocked
                    ? $"BOSS uses {(crush ? "CRUSH" : "STRIKE")} on {aiNames[target]}; {aiNames[target]} blocks, taking {damage}."
                    : $"BOSS uses {(crush ? "CRUSH" : "STRIKE")} on {aiNames[target]} for {damage} damage.");
                yield return Pulse(bossVisual, UIFactory.Red, 0.18f);
                yield return Pulse(aiVisuals[target], blocked ? UIFactory.Blue : UIFactory.HeartRed, 0.26f);
                RefreshBoard("BOSS ACTION RESOLVED");
                yield return new WaitForSecondsRealtime(0.45f);
                if (!AnyAiAlive())
                {
                    complete = true;
                    AppendLog("ALL THREE AI MEMBERS ARE DOWN — BOSS WINS THE SHOWDOWN.");
                }
            }
        }

        private IEnumerator AiTurn()
        {
            statusText.text = $"ROUND {round} — AI PARTY TURN";
            AppendLog("AI PARTY TURN — surviving members coordinate their response.");
            yield return new WaitForSecondsRealtime(0.45f);
            for (int index = 0; index < aiHealth.Length && !complete; index += 1)
            {
                if (aiHealth[index] <= 0) continue;
                int damage = aiExhausted ? 5 : 7;
                if (random.NextDouble() < 0.24d) damage += 2;
                bool bossBlocks = shield > 0 || random.NextDouble() < 0.12d;
                int shieldAbsorbed = Mathf.Min(shield, damage);
                shield -= shieldAbsorbed;
                int remaining = Mathf.Max(0, damage - shieldAbsorbed - (bossBlocks && shieldAbsorbed == 0 ? 2 : 0));
                bossHealth = Mathf.Max(0, bossHealth - remaining);
                AppendLog(bossBlocks
                    ? $"{aiNames[index]} attacks; BOSS blocks and takes {remaining} damage."
                    : $"{aiNames[index]} attacks the BOSS for {remaining} damage.");
                yield return Pulse(aiVisuals[index], UIFactory.Green, 0.18f);
                yield return Pulse(bossVisual, bossBlocks ? UIFactory.Blue : UIFactory.HeartRed, 0.26f);
                RefreshBoard("AI ACTION RESOLVED");
                yield return new WaitForSecondsRealtime(0.4f);
                if (bossHealth <= 0)
                {
                    complete = true;
                    AppendLog("THE BOSS HAS BEEN DOWNED — AI PARTY WINS THE SHOWDOWN.");
                }
            }
            int survivingMembers = 0;
            for (int index = 0; index < aiHealth.Length; index += 1)
            {
                if (aiHealth[index] > 0) survivingMembers += 1;
            }
            // A coordinated three-player party gets one transparent team play
            // roughly half of its turns. This offsets the Boss' larger health pool
            // and keeps the automatic practice encounter close to a 50/50 result.
            if (!complete && survivingMembers >= 2 && random.NextDouble() < 0.50d)
            {
                const int comboDamage = 8;
                bossHealth = Mathf.Max(0, bossHealth - comboDamage);
                AppendLog($"AI PARTY COMBO â€” Learn, Build, and Grow chain for {comboDamage} damage.");
                yield return Pulse(bossVisual, UIFactory.HeartRed, 0.30f);
                RefreshBoard("AI PARTY COMBO RESOLVED");
                yield return new WaitForSecondsRealtime(0.35f);
                if (bossHealth <= 0)
                {
                    complete = true;
                    AppendLog("THE BOSS HAS BEEN DOWNED â€” AI PARTY WINS THE SHOWDOWN.");
                }
            }
            aiExhausted = false;
            shield = 0;
            signatureCooldown = Mathf.Max(0, signatureCooldown - 1);
        }

        private IEnumerator Pulse(RectTransform visual, Color tint, float duration)
        {
            if (visual == null) yield break;
            Image image = visual.GetComponent<Image>();
            Color originalColor = image == null ? Color.white : image.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                visual.localScale = Vector3.one * (1f + wave * 0.09f);
                if (image != null) image.color = Color.Lerp(originalColor, tint, wave * 0.42f);
                yield return null;
            }
            visual.localScale = Vector3.one;
            if (image != null) image.color = originalColor;
        }

        private int StrongestAiTarget()
        {
            int selected = 0;
            for (int index = 1; index < aiHealth.Length; index += 1)
            {
                if (aiHealth[index] > aiHealth[selected]) selected = index;
            }
            return selected;
        }

        private bool AnyAiAlive()
        {
            for (int index = 0; index < aiHealth.Length; index += 1)
            {
                if (aiHealth[index] > 0) return true;
            }
            return false;
        }

        private void AppendLog(string entry)
        {
            showdownLog.Add($"> {entry}");
            while (showdownLog.Count > 6) showdownLog.RemoveAt(0);
            if (combatLogText != null) combatLogText.text = string.Join("\n", showdownLog);
        }

        private void RefreshBoard(string message)
        {
            bossStats.text = $"BOSS HP  {bossHealth}/{BossMaximumHealth}\nSHIELD  {shield}/10  •  AP  {actionPoints}/3\nROUND  {round}  •  SIGNATURE CD  {signatureCooldown}";
            for (int index = 0; index < aiStats.Length; index += 1)
            {
                bool alive = aiHealth[index] > 0;
                aiStats[index].text = alive ? $"HP {aiHealth[index]}/{AiMaximumHealth}\n{(aiExhausted ? "EXHAUSTED" : "READY")}" : "DEFEATED";
                aiStats[index].color = alive ? UIFactory.Cream : UIFactory.MutedTextColor;
            }
            if (statusText != null) statusText.text = message;
        }

        private static void SetHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}
