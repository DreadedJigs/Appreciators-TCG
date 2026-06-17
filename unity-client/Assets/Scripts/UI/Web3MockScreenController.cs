using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class Web3MockScreenController : ScreenControllerBase
    {
        private BackendApiClient apiClient;
        private InputField walletInput;
        private InputField apiInput;
        private Text messageText;
        private Text walletStatusText;
        private Text ownershipText;
        private Text mintQuantityText;
        private Text mintSupplyText;
        private Text mintResultText;
        private int mintQuantity = 1;

        private void Start()
        {
            apiClient = gameObject.AddComponent<BackendApiClient>();

            GameObject panel = CreateFullScreenStack("Wallet / Web3");
            UIFactory.CreateText(
                panel.transform,
                "Mock wallet identity for Phase 1.5. No real wallet signatures, NFT ownership, or paid gameplay power are used yet.",
                22,
                TextAnchor.MiddleLeft,
                UIFactory.MutedTextColor);

            GameObject walletPanel = UIFactory.CreateVerticalStack(panel.transform, "MockWalletPanel", UIFactory.PanelAlt, 10, 14);
            UIFactory.CreateText(walletPanel.transform, "Mock Wallet Address", 22, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            string savedWallet = LocalSaveSystem.LoadMockWalletAddress();
            walletInput = UIFactory.CreateInputField(
                walletPanel.transform,
                "0xAPPRECIATORS...",
                string.IsNullOrWhiteSpace(savedWallet) ? "0xAPPRECIATORS000000000000000000000000000000" : savedWallet);

            GameObject walletButtons = UIFactory.CreateHorizontalStack(walletPanel.transform, "WalletActions", Color.clear, 10, 0);
            UIFactory.CreateButton(walletButtons.transform, "Mock Verify", VerifyWallet, UIFactory.Green);
            UIFactory.CreateButton(walletButtons.transform, "Mock NFT Sync", SyncOwnership, UIFactory.Blue);
            UIFactory.CreateButton(walletButtons.transform, "Disconnect", DisconnectWallet, UIFactory.PanelAlt);

            walletStatusText = UIFactory.CreateText(walletPanel.transform, WalletStatusLine(), 21, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);

            GameObject mintPanel = UIFactory.CreateVerticalStack(panel.transform, "MintSimulatorPanel", UIFactory.Panel, 8, 14);
            UIFactory.CreateText(mintPanel.transform, "Mock Mint Simulator", 24, TextAnchor.MiddleLeft, UIFactory.NeonPink, FontStyle.Bold);
            mintSupplyText = UIFactory.CreateText(mintPanel.transform, "Supply: simulator ready", 21, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);

            GameObject mintActions = UIFactory.CreateHorizontalStack(mintPanel.transform, "MintActions", Color.clear, 10, 0);
            UIFactory.CreateButton(mintActions.transform, "-", () => SetMintQuantity(mintQuantity - 1), UIFactory.PanelAlt);
            mintQuantityText = UIFactory.CreateText(mintActions.transform, string.Empty, 24, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            UIFactory.CreateButton(mintActions.transform, "+", () => SetMintQuantity(mintQuantity + 1), UIFactory.PanelAlt);
            UIFactory.CreateButton(mintActions.transform, "Mock Mint", SimulateMint, UIFactory.Green);
            UpdateMintQuantityText();

            mintResultText = UIFactory.CreateText(
                mintPanel.transform,
                "Set a quantity and press Mock Mint. No wallet signature, gas, or payment is sent.",
                20,
                TextAnchor.UpperLeft,
                UIFactory.MutedTextColor);

            GameObject ownershipPanel = UIFactory.CreateVerticalStack(panel.transform, "OwnershipPanel", UIFactory.Panel, 8, 14);
            LayoutElement ownershipLayout = ownershipPanel.AddComponent<LayoutElement>();
            ownershipLayout.flexibleHeight = 1;
            UIFactory.CreateText(ownershipPanel.transform, "Cosmetic Holder Preview", 24, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            ownershipText = UIFactory.CreateText(
                ownershipPanel.transform,
                "Verify a mock wallet, then sync ownership to preview future holder cosmetics and rewards.",
                21,
                TextAnchor.UpperLeft,
                UIFactory.MutedTextColor);

            GameObject apiPanel = UIFactory.CreateVerticalStack(panel.transform, "ApiPanel", UIFactory.PanelAlt, 8, 14);
            UIFactory.CreateText(apiPanel.transform, "Backend API Base URL", 22, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            apiInput = UIFactory.CreateInputField(apiPanel.transform, AppConfig.DefaultApiBaseUrl, AppConfig.ApiBaseUrl);
            messageText = UIFactory.CreateText(panel.transform, "Online features use the backend when available. Local AI play still works offline.", 20, TextAnchor.MiddleCenter, UIFactory.Accent);

            GameObject footer = UIFactory.CreateHorizontalStack(panel.transform, "Footer", Color.clear, 10, 0);
            UIFactory.CreateButton(footer.transform, "Save API URL", SaveApiUrl, UIFactory.Blue);
            UIFactory.CreateButton(footer.transform, "Main Menu", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);
        }

        private void SaveApiUrl()
        {
            LocalSaveSystem.SaveApiBaseUrl(apiInput.text);
            messageText.text = "API URL saved locally.";
        }

        private void VerifyWallet()
        {
            string walletAddress = CleanWalletAddress();
            messageText.text = "Verifying mock wallet...";
            StartCoroutine(apiClient.VerifyMockWallet(walletAddress, LocalSaveSystem.LoadPlayerName(), response =>
            {
                LocalSaveSystem.SaveMockWallet(response.walletAddress, response.verified);
                walletInput.text = response.walletAddress;
                walletStatusText.text = WalletStatusLine();
                ownershipText.text =
                    $"{response.holderTier}\n" +
                    $"Wallet: {response.displayAddress}\n" +
                    $"Cosmetics: {ListText(response.cosmetics)}\n\n" +
                    "Real wallet signatures remain disabled until Phase 4.";
                messageText.text = response.message;
            }, error =>
            {
                LocalSaveSystem.SaveMockWallet(walletAddress, true);
                walletStatusText.text = WalletStatusLine();
                messageText.text = $"Backend unavailable. Saved offline mock wallet. {error}";
            }));
        }

        private void SyncOwnership()
        {
            string walletAddress = CleanWalletAddress();
            messageText.text = "Syncing mock ownership...";
            StartCoroutine(apiClient.SyncMockNftOwnership(walletAddress, response =>
            {
                LocalSaveSystem.SaveMockWallet(response.walletAddress, true);
                walletInput.text = response.walletAddress;
                walletStatusText.text = WalletStatusLine();
                ownershipText.text =
                    $"ORIGINALS: {AssetList(response.originals)}\n" +
                    $"COMPANIONS: {AssetList(response.companions)}\n" +
                    $"Cosmetics: {ListText(response.cosmetics)}\n" +
                    $"Rewards: {ListText(response.rewards)}\n\n" +
                    "All synced ownership is cosmetic-only in this prototype.";
                messageText.text = response.message;
            }, error =>
            {
                messageText.text = $"Mock ownership sync failed. Check backend URL. {error}";
            }));
        }

        private void SimulateMint()
        {
            string walletAddress = CleanWalletAddress();
            messageText.text = "Simulating mint...";
            mintResultText.text = "Mint transaction pending on Appreciators Mocknet...";
            StartCoroutine(apiClient.SimulateMockMint(walletAddress, mintQuantity, response =>
            {
                LocalSaveSystem.SaveMockWallet(response.walletAddress, true);
                walletInput.text = response.walletAddress;
                walletStatusText.text = WalletStatusLine();
                mintSupplyText.text = $"Supply: {response.simulatedMinted}/{response.supplyCap} minted | {response.remainingSupply} left";
                mintResultText.text =
                    $"{response.message}\n" +
                    $"Wallet: {response.displayAddress}\n" +
                    $"Price: {response.mintPriceEth} ETH | Network: {response.network}\n" +
                    $"Tx: {ShortHash(response.txHash)}\n" +
                    $"Revealed: {MintedTokenList(response.tokens)}\n" +
                    $"Your simulated total: {response.totalMintedByWallet}";
                messageText.text = response.realTransactionSubmitted
                    ? response.message
                    : "Mock mint complete. No real transaction was submitted.";
            }, error =>
            {
                mintResultText.text = "Mint simulator needs the backend to generate token reveals and supply stats.";
                messageText.text = $"Mock mint failed. Check backend URL. {error}";
            }));
        }

        private void DisconnectWallet()
        {
            LocalSaveSystem.ClearMockWallet();
            walletInput.text = "0xAPPRECIATORS000000000000000000000000000000";
            walletStatusText.text = WalletStatusLine();
            ownershipText.text = "Mock wallet disconnected.";
            mintResultText.text = "Mock wallet disconnected. Set a quantity and press Mock Mint when ready.";
            messageText.text = "Mock wallet cleared locally.";
        }

        private void SetMintQuantity(int quantity)
        {
            mintQuantity = Mathf.Clamp(quantity, 1, 5);
            UpdateMintQuantityText();
        }

        private void UpdateMintQuantityText()
        {
            if (mintQuantityText != null)
            {
                mintQuantityText.text = $"Quantity: {mintQuantity}";
            }
        }

        private string CleanWalletAddress()
        {
            string walletAddress = walletInput == null ? string.Empty : walletInput.text.Trim();
            return string.IsNullOrWhiteSpace(walletAddress)
                ? "0xAPPRECIATORS000000000000000000000000000000"
                : walletAddress;
        }

        private static string WalletStatusLine()
        {
            string walletAddress = LocalSaveSystem.LoadMockWalletAddress();
            bool verified = LocalSaveSystem.LoadMockWalletVerified();
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                return "No mock wallet connected.";
            }

            return verified ? $"Mock wallet connected: {walletAddress}" : $"Mock wallet saved: {walletAddress}";
        }

        private static string ListText(string[] values)
        {
            return values == null || values.Length == 0 ? "None" : string.Join(", ", values);
        }

        private static string AssetList(MockOwnedAsset[] assets)
        {
            if (assets == null || assets.Length == 0)
            {
                return "None";
            }

            string[] labels = new string[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                string scope = assets[i].cosmeticOnly ? "cosmetic" : "gameplay";
                labels[i] = $"{assets[i].name} ({scope})";
            }

            return string.Join(", ", labels);
        }

        private static string MintedTokenList(MockMintedToken[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return "None";
            }

            string[] labels = new string[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                labels[i] = tokens[i].name;
            }

            return string.Join(", ", labels);
        }

        private static string ShortHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 16)
            {
                return value;
            }

            return $"{value.Substring(0, 10)}...{value.Substring(value.Length - 6)}";
        }
    }
}
