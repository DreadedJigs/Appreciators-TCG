#if UNITY_EDITOR
using System;
using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Packs;
using UnityEditor;
using UnityEngine;

namespace AppreciatorsTcg.EditorTools
{
    public static class AppreciatorsPackAudit
    {
        public static void RunAll()
        {
            try
            {
                PackOpeningService service = new PackOpeningService(new System.Random(12345));
                var cards = service.LoadCardDefinitions();
                var packs = service.LoadPackDefinitions();

                Require(cards.Count == 24, "Pack card data must mirror the 24 active non-Companion cards.");
                Require(cards.Count(card => card.lane == Lane.Art) >= 10, "Pack card data must include at least 10 Art cards.");
                Require(cards.Count(card => card.lane == Lane.Community) >= 7, "Pack card data must include at least 7 Community cards.");
                Require(cards.Count(card => card.lane == Lane.Blockchain) >= 7, "Pack card data must include at least 7 Blockchain cards.");
                Require(cards.All(card => !string.Equals(card.type, GameConstants.Companion, StringComparison.OrdinalIgnoreCase)), "Companion cards must not appear in pack rewards.");
                Require(cards.All(card => card != null && !string.IsNullOrWhiteSpace(card.id) && !string.IsNullOrWhiteSpace(card.name)), "Every pack card must have id and name data.");
                Require(packs.Count >= 6, "Pack data must include starter, random, and guaranteed shard-store tiers.");
                Require(packs.All(pack => pack.slots != null && pack.slots.Length == 5), "Every alpha pack must contain exactly 5 slots.");
                Require(packs.All(pack => !pack.attunementEnabled), "Every Appreciation Ritual pack must open Neutral without lane attunement.");
                Require(packs.Single(pack => pack.id == "random_appreciation_pack").shardCost == 300, "Random packs must cost 300 shards.");
                Require(packs.Single(pack => pack.id == "legendary_guaranteed_pack").shardCost == 1800, "Legendary guarantee packs must cost 1,800 shards.");
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/PackOpeningScene.unity") != null, "PackOpeningScene must exist.");
                Require(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/PackOpeningScene.unity"), "PackOpeningScene must be enabled in build settings.");

                PackRewardResult result = service.OpenPack(packs[0], cards, Lane.Art, Array.Empty<string>());
                Require(result.cards.Count == 5, "Opening a pack must return exactly 5 cards.");
                Require(result.attunement == Lane.Neutral, "Ritual rewards must resolve as Neutral even when a legacy lane value is supplied.");
                Require(result.cards.All(reward => reward?.card != null && !string.IsNullOrWhiteSpace(reward.card.id)), "Every simulated reward must include valid card data.");
                Require(result.cards.Any(card => card.isMysterySlot), "Opening a pack must include a mystery slot.");
                Require(result.cards.Single(card => card.slotIndex == 5).card.rarity >= Rarity.Rare, "Starter Mystery Slot must always be Rare or better.");

                var distribution = service.SimulateOpenings(packs[0], cards, Lane.Neutral, 100);
                int simulatedCards = distribution.Values.Sum();
                Require(simulatedCards == 500, "Simulating 100 packs must produce 500 card rolls.");

                Debug.Log("APPRECIATORS_PACK_AUDIT_PASS Pack opening audit completed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("APPRECIATORS_PACK_AUDIT_FAIL\n" + exception);
                throw;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
#endif
