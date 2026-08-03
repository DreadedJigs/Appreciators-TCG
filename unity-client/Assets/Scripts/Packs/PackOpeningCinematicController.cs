using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace AppreciatorsTcg.Packs
{
    /// <summary>
    /// Runs the actual Blender-authored rig in Unity. The sequence pauses at the
    /// opened seam for the player's Appreciate input, then resumes through the
    /// five-card extraction, flips, and final fan. Gameplay remains authoritative
    /// in PackOpeningController; this class only presents already-secured rewards.
    /// </summary>
    public sealed class PackOpeningCinematicController : MonoBehaviour
    {
        private const string ModelPath = "Art/Blender/PackOpening/appreciators_pack_opening_prototype";
        private const float TearPauseTime = 2.82f;
        private const float SequenceEndTime = 5.96f;
        private static readonly string[] AnimatedNodes =
        {
            "Pack_Rig",
            "Pack_TearStrip",
            "Pack_InternalGlow",
            "Card_01_Rig",
            "Card_02_Rig",
            "Card_03_Rig",
            "Card_04_Rig",
            "Card_05_Rig",
            "PackOpening_Camera"
        };

        private GameObject modelInstance;
        private RawImage outputImage;
        private CanvasGroup outputGroup;
        private RenderTexture outputTexture;
        private PlayableGraph graph;
        private readonly List<AnimationClipPlayable> activeClips = new List<AnimationClipPlayable>();
        private static readonly Dictionary<string, Sprite> RenderedSprites = new Dictionary<string, Sprite>();

        public bool LastPlaybackSucceeded { get; private set; }

        public static Sprite LoadRenderedSprite(string resourcePath)
        {
            if (RenderedSprites.TryGetValue(resourcePath, out Sprite cached) && cached != null) return cached;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            Rect sourceRect = resourcePath.EndsWith("pack_idle")
                ? new Rect(texture.width * 0.305f, texture.height * 0.02f, texture.width * 0.39f, texture.height * 0.96f)
                : new Rect(0f, 0f, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, sourceRect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            RenderedSprites[resourcePath] = sprite;
            return sprite;
        }

        public IEnumerator PlayTear(Transform stage, IReadOnlyList<PackRewardCardResult> rewards)
        {
            LastPlaybackSucceeded = BuildStage(stage, rewards);
            if (!LastPlaybackSucceeded)
            {
                yield break;
            }

            SetPlaybackSpeed(1d);
            graph.Play();
            yield return FadeOutput(0f, 1f, 0.18f);
            yield return WaitForSequenceTime(TearPauseTime);
            SetPlaybackSpeed(0d);
        }

        public IEnumerator PlayAppreciation()
        {
            if (!LastPlaybackSucceeded || !graph.IsValid())
            {
                LastPlaybackSucceeded = false;
                yield break;
            }

            SetPlaybackSpeed(1d);
            yield return WaitForSequenceTime(SequenceEndTime);
            yield return new WaitForSecondsRealtime(0.28f);
            yield return FadeOutput(1f, 0f, 0.24f);
            CleanupStage();
        }

        private bool BuildStage(Transform stage, IReadOnlyList<PackRewardCardResult> rewards)
        {
            CleanupStage();
            GameObject prefab = Resources.Load<GameObject>(ModelPath);
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(ModelPath);
            if (stage == null || prefab == null || clips == null || clips.Length == 0)
            {
                Debug.LogWarning("[PackOpening] Blender rig or animation clips are unavailable; using the procedural fallback.");
                return false;
            }

            EnsureOutput(stage);
            modelInstance = Instantiate(prefab);
            modelInstance.name = "BlenderPackOpeningRuntime";
            BindRewardFaces(rewards);
            ConfigureStageMaterials();
            AddRuntimeLighting();

            Camera cinematicCamera = FindDescendant(modelInstance.transform, "PackOpening_Camera")?.GetComponent<Camera>();
            if (cinematicCamera == null)
            {
                Debug.LogWarning("[PackOpening] Blender camera is missing; using the procedural fallback.");
                CleanupStage();
                return false;
            }

            cinematicCamera.enabled = true;
            cinematicCamera.targetTexture = outputTexture;
            cinematicCamera.clearFlags = CameraClearFlags.SolidColor;
            cinematicCamera.backgroundColor = new Color(0.008f, 0.025f, 0.070f, 1f);
            cinematicCamera.allowHDR = true;

            graph = PlayableGraph.Create("AppreciatorsBlenderPackOpening");
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            activeClips.Clear();
            foreach (string nodeName in AnimatedNodes)
            {
                Transform target = FindDescendant(modelInstance.transform, nodeName);
                AnimationClip clip = clips.FirstOrDefault(candidate => candidate.name == $"{nodeName}|{nodeName}Action");
                if (target == null || clip == null)
                {
                    Debug.LogWarning($"[PackOpening] Blender animation target '{nodeName}' is incomplete.");
                    continue;
                }

                Animator animator = target.gameObject.GetComponent<Animator>() ?? target.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, nodeName, animator);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetTime(0d);
                playable.SetSpeed(0d);
                output.SetSourcePlayable(playable);
                activeClips.Add(playable);
            }

            if (activeClips.Count < AnimatedNodes.Length)
            {
                Debug.LogWarning($"[PackOpening] Only {activeClips.Count}/{AnimatedNodes.Length} Blender tracks were composed; using fallback.");
                CleanupStage();
                return false;
            }

            outputImage.enabled = true;
            outputGroup.alpha = 0f;
            // The existing pack hold/tap surface remains the authoritative input.
            // The 3D render must never intercept the Appreciate confirmation tap.
            outputGroup.blocksRaycasts = false;
            return true;
        }

        private void BindRewardFaces(IReadOnlyList<PackRewardCardResult> rewards)
        {
            for (int index = 0; index < 5; index++)
            {
                Transform face = FindDescendant(modelInstance.transform, $"Card_{index + 1:00}_Face");
                Renderer renderer = face == null ? null : face.GetComponent<Renderer>();
                CardDefinition card = rewards != null && index < rewards.Count ? rewards[index]?.card : null;
                Sprite sprite = PackCardArtResolver.LoadCardFaceSprite(card) ?? PackCardArtResolver.LoadSprite(card);
                if (renderer == null || sprite == null)
                {
                    continue;
                }

                Material material = renderer.material;
                material.mainTexture = sprite.texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sprite.texture);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                material.color = Color.white;
            }
        }

        private void AddRuntimeLighting()
        {
            foreach (Light importedLight in modelInstance.GetComponentsInChildren<Light>(true))
            {
                importedLight.enabled = false;
            }

            CreateLight("RuntimeKey", new Vector3(-3f, -5f, 8f), new Color(0.36f, 0.82f, 1f), 2.2f);
            CreateLight("RuntimeRim", new Vector3(4f, 1f, 5f), new Color(1f, 0.16f, 0.58f), 1.6f);
            CreateLight("RuntimeFill", new Vector3(0f, 5f, 3f), new Color(1f, 0.74f, 0.28f), 1.25f);
        }

        private void ConfigureStageMaterials()
        {
            SetRendererColor("Backdrop", new Color(0.018f, 0.10f, 0.19f, 1f), new Color(0.01f, 0.08f, 0.18f, 1f));
            SetRendererColor("StageFloor", new Color(0.075f, 0.018f, 0.095f, 1f), new Color(0.08f, 0.01f, 0.12f, 1f));
            SetRendererColor("Pack_TearStrip", new Color(1f, 0.42f, 0.045f, 1f), new Color(0.45f, 0.08f, 0.01f, 1f));
        }

        private void SetRendererColor(string nodeName, Color baseColor, Color emission)
        {
            Renderer renderer = FindDescendant(modelInstance.transform, nodeName)?.GetComponent<Renderer>();
            if (renderer == null) return;
            Material material = renderer.material;
            material.color = baseColor;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
        }

        private void CreateLight(string name, Vector3 euler, Color color, float intensity)
        {
            GameObject lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(modelInstance.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(euler);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
        }

        private void EnsureOutput(Transform stage)
        {
            if (outputTexture == null)
            {
                outputTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
                {
                    name = "BlenderPackOpeningRender",
                    antiAliasing = 2,
                    useMipMap = false
                };
                outputTexture.Create();
            }

            if (outputImage == null)
            {
                GameObject overlay = new GameObject("BlenderPackOpeningOutput", typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage));
                overlay.transform.SetParent(stage, false);
                RectTransform rect = overlay.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                outputImage = overlay.GetComponent<RawImage>();
                outputImage.texture = outputTexture;
                outputImage.color = Color.white;
                outputImage.raycastTarget = false;
                outputGroup = overlay.GetComponent<CanvasGroup>();
            }
            else
            {
                outputImage.transform.SetParent(stage, false);
            }

            outputImage.transform.SetAsLastSibling();
        }

        private IEnumerator WaitForSequenceTime(float time)
        {
            float deadline = Time.realtimeSinceStartup + 9f;
            while (activeClips.Count > 0 && activeClips[0].GetTime() < time && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private void SetPlaybackSpeed(double speed)
        {
            foreach (AnimationClipPlayable playable in activeClips)
            {
                if (playable.IsValid()) playable.SetSpeed(speed);
            }
        }

        private IEnumerator FadeOutput(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                outputGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            outputGroup.alpha = to;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private void CleanupStage()
        {
            if (graph.IsValid()) graph.Destroy();
            activeClips.Clear();
            if (modelInstance != null) Destroy(modelInstance);
            modelInstance = null;
            if (outputGroup != null)
            {
                outputGroup.alpha = 0f;
                outputGroup.blocksRaycasts = false;
            }
            if (outputImage != null) outputImage.enabled = false;
        }

        private void OnDestroy()
        {
            CleanupStage();
            if (outputTexture == null) return;
            outputTexture.Release();
            Destroy(outputTexture);
        }
    }
}
