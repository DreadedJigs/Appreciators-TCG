using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    // A local, consequence-free boss drill. The real cooperative encounter
    // remains in the Boss Arena; this screen gives verified holders a fast way
    // to learn the 180 HP / three-action Boss kit against a full AI party.
    public sealed class BossPracticeMatchController : ScreenControllerBase
    {
        private readonly int[] aiHealth = { 30, 30, 30 };
        private readonly string[] aiNames = { "AI LEARN", "AI BUILD", "AI GROW" };
        private const int BossMaximumHealth = 180;
        private int bossHealth = BossMaximumHealth;
        private int shield;
        private int actionPoints = 3;
        private int round = 1;
        private int signatureCooldown;
        private bool aiExhausted;
        private bool complete;

        private Text bossStats;
        private Text statusText;
        private Text[] aiStats = new Text[3];
        private Button strikeButton;
        private Button crushButton;
        private Button braceButton;
        private Button signatureButton;
        private Button endTurnButton;

        private void Start()
        {
            LocalSaveSystem.ClearPendingMatchContext();

            GameObject shell = UIFactory.CreatePanel(Root, "BossPracticeShell", UIFactory.GlassPanel);
            UIFactory.SetAnchors(shell.GetComponent<RectTransform>(), new Vector2(0.025f, 0.03f), new Vector2(0.975f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(shell, UIFactory.Red);

            GameObject header = UIFactory.CreateVerticalStack(shell.transform, "PracticeHeader", UIFactory.PanelAlt, 3, 10);
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.03f, 0.855f), new Vector2(0.97f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.Accent);
            Text title = UIFactory.CreateText(header.transform, "1-OF-1 BOSS vs AI PRACTICE", 31, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(title.gameObject, 34);
            Text subtitle = UIFactory.CreateText(header.transform, "YOU ARE THE BOSS  •  3 AI MEMBERS  •  PRACTICE ONLY", 15, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(subtitle.gameObject, 22);

            CreateBossSeat(shell.transform);
            CreateAiParty(shell.transform);
            CreateActions(shell.transform);

            statusText = UIFactory.CreateText(shell.transform, string.Empty, 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(statusText.rectTransform, new Vector2(0.05f, 0.165f), new Vector2(0.95f, 0.225f), Vector2.zero, Vector2.zero);
            RefreshBoard("Boss turn: spend up to 3 Action Points, then let the AI party answer.");
        }

        private void CreateBossSeat(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "VerifiedBossSeat", UIFactory.Panel, 7, 12);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.05f, 0.285f), new Vector2(0.355f, 0.825f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Red);
            Text label = UIFactory.CreateText(panel.transform, "YOUR VERIFIED 1-OF-1 BOSS", 18, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            SetHeight(label.gameObject, 28);

            GameObject portrait = UIFactory.CreatePanel(panel.transform, "VerifiedOwnerCard", UIFactory.Ink);
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
            bossStats = UIFactory.CreateText(panel.transform, string.Empty, 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(bossStats.gameObject, 58);
        }

        private void CreateAiParty(Transform parent)
        {
            GameObject panel = UIFactory.CreateHorizontalStack(parent, "AiMemberParty", UIFactory.Panel, 12, 14);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.38f, 0.285f), new Vector2(0.95f, 0.825f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Blue);
            for (int index = 0; index < 3; index += 1)
            {
                GameObject member = UIFactory.CreateVerticalStack(panel.transform, $"AiMember{index + 1}", UIFactory.GlassPanel, 6, 10);
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

        private void CreateActions(Transform parent)
        {
            GameObject actions = UIFactory.CreateHorizontalStack(parent, "BossActionBar", Color.clear, 9, 0);
            UIFactory.SetAnchors(actions.GetComponent<RectTransform>(), new Vector2(0.05f, 0.065f), new Vector2(0.95f, 0.145f), Vector2.zero, Vector2.zero);
            strikeButton = UIFactory.CreateButton(actions.transform, "STRIKE • 1 AP", Strike, UIFactory.Red);
            crushButton = UIFactory.CreateButton(actions.transform, "CRUSH • 2 AP", Crush, UIFactory.Accent);
            braceButton = UIFactory.CreateButton(actions.transform, "BRACE • 1 AP", Brace, UIFactory.Blue);
            signatureButton = UIFactory.CreateButton(actions.transform, "GALLERY LOCK • 3 AP", GalleryLock, UIFactory.PortalViolet);
            endTurnButton = UIFactory.CreateButton(actions.transform, "END BOSS TURN", EndBossTurn, UIFactory.Green);
            UIFactory.CreateButton(actions.transform, "EXIT PRACTICE", () => SceneManager.LoadScene("BossBattleScene"), UIFactory.PanelAlt);
        }

        private void Strike()
        {
            if (!SpendActions(1, "Strike")) return;
            int target = NextAiTarget();
            if (target < 0) return;
            aiHealth[target] = Mathf.Max(0, aiHealth[target] - 4);
            RefreshBoard($"Strike deals 4 damage to {aiNames[target]}.");
            CheckComplete();
        }

        private void Crush()
        {
            if (!SpendActions(2, "Crush")) return;
            int target = NextAiTarget();
            if (target < 0) return;
            aiHealth[target] = Mathf.Max(0, aiHealth[target] - 7);
            RefreshBoard($"Crush deals 7 damage and exhausts {aiNames[target]}.");
            CheckComplete();
        }

        private void Brace()
        {
            if (!SpendActions(1, "Brace")) return;
            int before = shield;
            shield = Mathf.Min(10, shield + 5);
            RefreshBoard($"Brace grants {shield - before} Shield. Shield expires when your next Boss turn begins.");
        }

        private void GalleryLock()
        {
            if (signatureCooldown > 0)
            {
                RefreshBoard($"Gallery Lock is on cooldown for {signatureCooldown} Boss turn(s).");
                return;
            }
            if (!SpendActions(3, "Gallery Lock")) return;
            aiExhausted = true;
            signatureCooldown = 2;
            RefreshBoard("Gallery Lock exhausts the AI party. Their next attack is weakened.");
        }

        private void EndBossTurn()
        {
            if (complete) return;
            int incomingDamage = 0;
            for (int index = 0; index < aiHealth.Length; index += 1)
            {
                if (aiHealth[index] > 0) incomingDamage += aiExhausted ? 1 : 3;
            }
            int absorbed = Mathf.Min(shield, incomingDamage);
            shield -= absorbed;
            bossHealth = Mathf.Max(0, bossHealth - (incomingDamage - absorbed));
            aiExhausted = false;
            signatureCooldown = Mathf.Max(0, signatureCooldown - 1);
            round += 1;
            actionPoints = 3;
            shield = 0;
            RefreshBoard($"AI party attacks for {incomingDamage}; Shield absorbed {absorbed}. Boss turn {round} begins.");
            if (bossHealth <= 0)
            {
                complete = true;
                RefreshBoard("PRACTICE DEFEAT — the AI party has downed the Boss. Restart from Boss Battles to try again.");
            }
        }

        private bool SpendActions(int cost, string action)
        {
            if (complete) return false;
            if (actionPoints < cost)
            {
                RefreshBoard($"{action} requires {cost} AP. You have {actionPoints} AP remaining.");
                return false;
            }
            actionPoints -= cost;
            return true;
        }

        private int NextAiTarget()
        {
            for (int index = 0; index < aiHealth.Length; index += 1)
            {
                if (aiHealth[index] > 0) return index;
            }
            return -1;
        }

        private void CheckComplete()
        {
            if (System.Array.Exists(aiHealth, value => value > 0)) return;
            complete = true;
            RefreshBoard("PRACTICE VICTORY — your 1-of-1 Boss defeated all three AI members.");
        }

        private void RefreshBoard(string message)
        {
            bossStats.text = $"BOSS HP  {bossHealth}/{BossMaximumHealth}\nSHIELD  {shield}/10  •  AP  {actionPoints}/3\nROUND  {round}  •  SIGNATURE CD  {signatureCooldown}";
            for (int index = 0; index < aiStats.Length; index += 1)
            {
                bool alive = aiHealth[index] > 0;
                aiStats[index].text = alive ? $"HP {aiHealth[index]}/30\nREADY" : "DEFEATED";
                aiStats[index].color = alive ? UIFactory.Cream : UIFactory.MutedTextColor;
            }
            statusText.text = message;
            bool canAct = !complete && actionPoints > 0;
            strikeButton.interactable = canAct;
            crushButton.interactable = !complete && actionPoints >= 2;
            braceButton.interactable = canAct;
            signatureButton.interactable = !complete && actionPoints >= 3 && signatureCooldown <= 0;
            endTurnButton.interactable = !complete;
        }

        private static void SetHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}
