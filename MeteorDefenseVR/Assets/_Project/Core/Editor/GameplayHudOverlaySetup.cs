using System.IO;
using System.Collections.Generic;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    public static class GameplayHudOverlaySetup
    {
        private const string FontPath = "Assets/_Project/PcInput/Fonts/PcTestKorean.asset";
        private const string MaterialDirectory = "Assets/_Project/UI/Materials";

        [MenuItem("Meteor Defense/Apply Gameplay HUD Overlay")]
        public static void ApplyAndBake()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/MeteorDefense.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            Ensure(Camera.main);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void Ensure(Camera camera)
        {
            MissionHudController controller = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (camera == null || controller == null || font == null)
                throw new System.InvalidOperationException("Gameplay HUD requires the authored camera, MissionHudController and Korean TMP font.");

            int layer = LayerMask.NameToLayer("Player HUD");
            if (layer < 0) throw new System.InvalidOperationException("Player HUD layer is required.");
            camera.cullingMask |= 1 << layer;

            GameplayHudOverlayView existing = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            RectTransform root = existing != null
                ? (RectTransform)existing.transform
                : Rect(null, "PlayerGameplayHUD");
            root.SetParent(null, false);
            root.gameObject.layer = layer;

            Canvas canvas = Get<Canvas>(root.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = .45f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;
            var scaler = Get<UnityEngine.UI.CanvasScaler>(root.gameObject);
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            Get<UnityEngine.UI.GraphicRaycaster>(root.gameObject).enabled = false;
            CanvasGroup group = Get<CanvasGroup>(root.gameObject);
            group.interactable = false;
            group.blocksRaycasts = false;

            Material imageMaterial = GetOrCreateUiOverlayMaterial();
            Material fontMaterial = GetOrCreateFontOverlayMaterial(font);

            RectTransform safe = Rect(root, "GameplaySafeArea");
            Stretch(safe, new Vector2(86f, 54f), new Vector2(-86f, -32f));
            RectTransform telemetry = Rect(safe, "MainTelemetry");
            Stretch(telemetry, Vector2.zero, Vector2.zero);

            RectTransform left = Panel(telemetry, "HealthPanel", imageMaterial);
            Place(left, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(390f, 148f));
            Text(left, "HealthLabel", "HP / SHIELD", font, fontMaterial, 22f,
                new Vector2(.5f, 1f), new Vector2(0f, -14f), new Vector2(350f, 40f), new Color32(153, 226, 235, 255));
            TextMeshProUGUI health = Text(left, "HealthValue", "■■■■■■■■  100%", font, fontMaterial, 28f,
                new Vector2(.5f, 0f), new Vector2(0f, 4f), new Vector2(354f, 78f), new Color32(47, 231, 246, 255));

            RectTransform center = Panel(telemetry, "StagePanel", imageMaterial);
            Place(center, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(580f, 148f));
            TextMeshProUGUI stage = Text(center, "DifficultyStageValue", "NORMAL  ·  STAGE 01", font, fontMaterial, 29f,
                new Vector2(.5f, 1f), new Vector2(0f, -12f), new Vector2(540f, 54f), new Color32(47, 231, 246, 255));
            TextMeshProUGUI asteroidLabel = Text(center, "AsteroidLabel", "ASTEROIDS", font, fontMaterial, 18f,
                new Vector2(.5f, 0f), new Vector2(-95f, 4f), new Vector2(210f, 70f), new Color32(153, 226, 235, 255));
            asteroidLabel.alignment = TextAlignmentOptions.Right;
            TextMeshProUGUI asteroids = Text(center, "AsteroidValue", "18 / 21", font, fontMaterial, 32f,
                new Vector2(.5f, 0f), new Vector2(125f, 4f), new Vector2(220f, 70f), new Color32(47, 231, 246, 255));
            asteroids.alignment = TextAlignmentOptions.Left;

            RectTransform right = Panel(telemetry, "ScorePanel", imageMaterial);
            Place(right, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(360f, 148f));
            Text(right, "ScoreLabel", "SCORE", font, fontMaterial, 22f,
                new Vector2(.5f, 1f), new Vector2(0f, -14f), new Vector2(320f, 40f), new Color32(153, 226, 235, 255));
            TextMeshProUGUI score = Text(right, "ScoreValue", "999,999", font, fontMaterial, 46f,
                new Vector2(.5f, 0f), new Vector2(0f, 4f), new Vector2(324f, 78f), new Color32(47, 231, 246, 255));
            score.characterSpacing = 1f;

            RectTransform feedbackPanel = Panel(safe, "SystemMessagePanel", imageMaterial);
            Place(feedbackPanel, new Vector2(.5f, .69f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(900f, 160f));
            TextMeshProUGUI feedback = Text(feedbackPanel, "SystemMessageValue", string.Empty, font, fontMaterial, 38f,
                Vector2.one * .5f, Vector2.zero, new Vector2(850f, 132f), new Color32(47, 231, 246, 255));
            feedback.textWrappingMode = TextWrappingModes.Normal;
            feedback.overflowMode = TextOverflowModes.Truncate;
            feedbackPanel.gameObject.SetActive(false);

            foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;

            var suppressed = new List<Renderer>();
            DisableLegacyHud(controller.transform, suppressed);
            DisableLegacyMessage(camera.transform.Find("MissionCompleteText"), suppressed);
            LaunchSequenceController launch = Object.FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include);
            DisableLegacyMessage(launch != null ? launch.transform.Find("LaunchStatus") : null, suppressed);
            MissionCompleteController complete = Object.FindAnyObjectByType<MissionCompleteController>(FindObjectsInactive.Include);

            GameplayHudOverlayView view = Get<GameplayHudOverlayView>(root.gameObject);
            view.Configure(controller, controller.gameObject, camera, canvas, group, telemetry.gameObject,
                feedbackPanel.gameObject, health, stage, asteroidLabel, asteroids, score, feedback, launch, complete, suppressed.ToArray());
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(canvas);
        }

        private static void DisableLegacyHud(Transform root, List<Renderer> suppressed)
        {
            if (root == null) return;
            string[] names = { "Panel_HP", "Panel_Remaining", "Panel_Score", "HudHealth", "HudRemaining", "HudScore", "HudFeedback", "TacticalHudFrames" };
            foreach (string name in names) DisableLegacyMessage(root.Find(name), suppressed);
            var legacyEffects = root.GetComponent<MeteorDefenseVR.Visual.HudVisualEffectsView>();
            if (legacyEffects != null) legacyEffects.enabled = false;
        }

        private static void DisableLegacyMessage(Transform root, List<Renderer> suppressed)
        {
            if (root == null) return;
            foreach (WorldTextPresentation bridge in root.GetComponentsInChildren<WorldTextPresentation>(true)) bridge.enabled = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
                if (!suppressed.Contains(renderer)) suppressed.Add(renderer);
            }
            foreach (TextMeshPro text in root.GetComponentsInChildren<TextMeshPro>(true)) text.enabled = false;
        }

        private static RectTransform Panel(Transform parent, string name, Material material)
        {
            RectTransform rect = Rect(parent, name);
            UnityEngine.UI.Image image = Get<UnityEngine.UI.Image>(rect.gameObject);
            image.color = new Color(.006f, .035f, .052f, .92f);
            image.material = material;
            image.raycastTarget = false;
            UnityEngine.UI.Outline outline = Get<UnityEngine.UI.Outline>(rect.gameObject);
            outline.effectColor = new Color(.035f, .78f, .9f, .9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            RectTransform line = Rect(rect, "AccentLine");
            line.anchorMin = new Vector2(.08f, 0f);
            line.anchorMax = new Vector2(.92f, 0f);
            line.pivot = new Vector2(.5f, 0f);
            line.offsetMin = new Vector2(0f, 0f);
            line.offsetMax = new Vector2(0f, 2f);
            UnityEngine.UI.Image lineImage = Get<UnityEngine.UI.Image>(line.gameObject);
            lineImage.color = new Color32(47, 231, 246, 220);
            lineImage.material = material;
            lineImage.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string content, TMP_FontAsset font,
            Material material, float size, Vector2 anchor, Vector2 position, Vector2 bounds, Color color)
        {
            RectTransform rect = Rect(parent, name);
            rect.gameObject.SetActive(false);
            TextMeshProUGUI text = Get<TextMeshProUGUI>(rect.gameObject);
            text.font = font;
            text.fontSharedMaterial = material;
            text.text = content;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;
            text.margin = new Vector4(8f, 2f, 8f, 2f);
            Place(rect, anchor, anchor, position, bounds);
            rect.gameObject.SetActive(true);
            return text;
        }

        private static Material GetOrCreateUiOverlayMaterial()
        {
            Directory.CreateDirectory(MaterialDirectory);
            const string path = MaterialDirectory + "/GameplayHudAlwaysOnTop.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("UI/Default")) { name = "GameplayHudAlwaysOnTop" };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = 4000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateFontOverlayMaterial(TMP_FontAsset font)
        {
            Directory.CreateDirectory(MaterialDirectory);
            const string path = MaterialDirectory + "/GameplayHudFontAlwaysOnTop.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = "GameplayHudFontAlwaysOnTop" };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_ZTestMode")) material.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = 4000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T Get<T>(GameObject target) where T : Component
        {
            T value = target.GetComponent<T>();
            return value != null ? value : target.AddComponent<T>();
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null) return (RectTransform)existing;
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }
    }
}
