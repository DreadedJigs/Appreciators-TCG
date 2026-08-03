#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AppreciatorsTcg.EditorTools
{
    public class AppreciatorsArtImportSettings : AssetPostprocessor
    {
        private const string CardArtRoot = "Assets/Resources/Art/Cards/";
        private const string OfficialArtRoot = "Assets/Resources/Art/Official/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CardArtRoot) && !assetPath.StartsWith(OfficialArtRoot))
            {
                return;
            }

            Configure((TextureImporter)assetImporter, assetPath);
        }

        [MenuItem("Appreciators/Apply 8K Art Import Settings")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                "Assets/Resources/Art/Cards",
                "Assets/Resources/Art/Official"
            });

            int updated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                Configure(importer, path);
                importer.SaveAndReimport();
                updated += 1;
            }

            Debug.Log($"Applied 8K-ready texture settings to {updated} Appreciators art assets.");
        }

        private static void Configure(TextureImporter importer, string path)
        {
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterPlatformSettings webGl = importer.GetPlatformTextureSettings("WebGL");
            webGl.overridden = true;
            // Keep the 8K source masters in-project while serving a GPU-safe HD tier
            // to browsers. The runtime still renders at native canvas resolution while
            // avoiding oversized downloads and mobile texture limits.
            bool heroBackground = path.EndsWith("appreciators_starfield_motif_v2_8k.png", System.StringComparison.OrdinalIgnoreCase);
            webGl.maxTextureSize = heroBackground ? 2048 : 1024;
            webGl.format = TextureImporterFormat.Automatic;
            webGl.textureCompression = TextureImporterCompression.CompressedHQ;
            webGl.compressionQuality = heroBackground ? 82 : 78;
            importer.SetPlatformTextureSettings(webGl);
        }
    }
}
#endif
