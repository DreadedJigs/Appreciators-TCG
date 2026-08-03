using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AppreciatorsTcg.Core;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    public interface IPackSaveService
    {
        PackInventoryState Load();
        void Save(PackInventoryState state);
        void Reset();
    }

    [Serializable]
    public class PackInventoryState
    {
        public List<PlayerCardInventoryEntry> cards = new List<PlayerCardInventoryEntry>();
        public List<PlayerPackInventoryEntry> packs = new List<PlayerPackInventoryEntry>();
        public int appreciationShards;
    }

    [Serializable]
    public class PlayerPackInventoryEntry
    {
        public string playerId;
        public string packId;
        public int count;
        public int quantityOwned;
        public string updatedAt;
    }

    public class PackSaveService : IPackSaveService
    {
        public string SavePath
        {
            get
            {
                string playerId = LocalSaveSystem.LoadOrCreatePlayerId();
                string safePlayerId = new string((playerId ?? "player")
                    .Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    .Take(64)
                    .ToArray());
                return Path.Combine(Application.persistentDataPath, $"appreciators_pack_inventory_{safePlayerId}.json");
            }
        }

        public PackInventoryState Load()
        {
            if (!File.Exists(SavePath))
            {
                return new PackInventoryState();
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                PackInventoryState state = JsonUtility.FromJson<PackInventoryState>(json);
                return state ?? new PackInventoryState();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PackOpening] Local inventory could not be loaded from '{SavePath}'. A clean alpha mirror will be used.\n{exception}");
                return new PackInventoryState();
            }
        }

        public void Save(PackInventoryState state)
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    Debug.LogError($"[PackOpening] Cannot save local inventory because '{SavePath}' has no valid directory.");
                    return;
                }

                Directory.CreateDirectory(directory);
                string json = JsonUtility.ToJson(state ?? new PackInventoryState(), true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PackOpening] Local inventory could not be saved to '{SavePath}'.\n{exception}");
            }
        }

        public void Reset()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PackOpening] Local inventory mirror could not be reset at '{SavePath}'.\n{exception}");
            }
        }
    }
}
