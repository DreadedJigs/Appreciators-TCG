using System;
using System.Collections;
using System.Collections.Generic;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.Packs;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class CollectionScreenController : ScreenControllerBase
    {
        private RectTransform content;
        private Text statusText;

        private void Start()
        {
            GameObject screen = CreateFullScreenStack("Collection");
            statusText = UIFactory.CreateText(screen.transform, "Loading account collection...", 22, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);

            content = UIFactory.CreateGridScrollContent(screen.transform, "CollectionScroll", new Vector2(210f, 270f), 6, out _);
            RebuildCollection(new Dictionary<string, int>());

            BackButton(screen.transform);

            if (BackendApiClient.HasSecureSession)
            {
                StartCoroutine(LoadAccountCollection());
            }
            else
            {
                statusText.text = "Official metadata card collection  •  Sign in to see your account quantities";
            }
        }

        private IEnumerator LoadAccountCollection()
        {
            BackendApiClient apiClient = gameObject.AddComponent<BackendApiClient>();
            PackInventoryResponse response = null;
            string requestError = null;
            yield return apiClient.GetPackInventory(LocalSaveSystem.LoadOrCreatePlayerId(), result => response = result, error => requestError = error);

            if (response?.inventory == null)
            {
                statusText.text = "Official metadata card collection  •  Account quantities unavailable";
                Debug.LogWarning($"[Collection] Inventory sync failed: {requestError}");
                yield break;
            }

            Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PlayerCardInventoryEntry entry in response.inventory.cards ?? Array.Empty<PlayerCardInventoryEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.cardId)) continue;
                quantities[entry.cardId] = Math.Max(0, entry.ownedCount > 0 ? entry.ownedCount : entry.quantityOwned);
            }

            new PackInventoryService(new PackSaveService()).ReplaceWithAuthoritativeSnapshot(response.inventory);
            statusText.text = "Official metadata card collection  •  x# shows copies owned";
            RebuildCollection(quantities);
        }

        private void RebuildCollection(IReadOnlyDictionary<string, int> quantities)
        {
            if (content == null) return;
            foreach (Transform child in content)
            {
                Destroy(child.gameObject);
            }

            foreach (AppreciatorsTcg.Cards.CardDefinition card in CardCatalog.AllCards)
            {
                int quantity = quantities != null && quantities.TryGetValue(card.id, out int count) ? count : 0;
                GameObject panel = UIFactory.CreateCardPanel(content, card, compact: true);
                CreateQuantityBadge(panel.transform, quantity);
            }
        }

        private static void CreateQuantityBadge(Transform parent, int quantity)
        {
            GameObject badge = UIFactory.CreatePanel(parent, "OwnedQuantity", new Color(0.015f, 0.025f, 0.065f, 0.94f));
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            UIFactory.SetAnchors(badgeRect, new Vector2(0.035f, 0.855f), new Vector2(0.285f, 0.972f), Vector2.zero, Vector2.zero);
            Image image = badge.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
            UIFactory.MakeDimensionalPanel(badge, UIFactory.NeonCyan);

            Text label = UIFactory.CreateText(badge.transform, $"x{Math.Max(0, quantity)}", 18, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            label.raycastTarget = false;
            UIFactory.Stretch(label.rectTransform);
        }
    }
}
