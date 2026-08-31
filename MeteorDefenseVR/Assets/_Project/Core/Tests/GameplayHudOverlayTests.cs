using System.Collections.Generic;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class GameplayHudOverlayTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(
            "Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);

        [Test]
        public void AuthoredMainHudUsesDedicatedPlayerOverlayAndSafeArea()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            Camera camera = Camera.main;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.HudCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(view.HudCanvas.transform.parent, Is.Null);
            Assert.That(view.HudCanvas.worldCamera, Is.SameAs(camera));
            Assert.That(view.HudCanvas.sortingOrder, Is.GreaterThanOrEqualTo(200));

            var scaler = view.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(.5f).Within(.001f));

            RectTransform safe = (RectTransform)view.transform.Find("GameplaySafeArea");
            Assert.That(safe, Is.Not.Null);
            Assert.That(safe.offsetMin.x / 1920f, Is.InRange(.04f, .05f));
            Assert.That(-safe.offsetMax.x / 1920f, Is.InRange(.04f, .05f));
            Assert.That(-safe.offsetMax.y / 1080f, Is.InRange(.02f, .04f));
            Assert.That(view.TelemetryRoot.transform.Find("HealthPanel"), Is.Not.Null);
            Assert.That(view.TelemetryRoot.transform.Find("StagePanel"), Is.Not.Null);
            Assert.That(view.TelemetryRoot.transform.Find("ScorePanel"), Is.Not.Null);

            int layer = LayerMask.NameToLayer("Player HUD");
            Assert.That(view.gameObject.layer, Is.EqualTo(layer));
            Assert.That(camera.cullingMask & (1 << layer), Is.Not.Zero);
            Assert.That(view.HealthValue.raycastTarget, Is.False);
            Assert.That(view.ScoreValue.enableAutoSizing, Is.False, "Live counters must not trigger AutoSize layout work every update");
            Assert.That(view.AsteroidValue.enableAutoSizing, Is.False);
            Assert.That(view.DifficultyStageValue.enableAutoSizing, Is.False);
        }

        [Test]
        public void ScoreStageAsteroidAndDifficultyRegressionValuesStayInsideTheirRects()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            int[] scores = { 0, 25, 999, 3259, 12500, 999999 };
            int[] stages = { 1, 10, 99 };
            (int remaining, int total)[] asteroids = { (1, 1), (18, 21), (99, 100) };
            string[] difficulties = { "EASY", "NORMAL", "HARD", "EXTREME" };

            foreach (string difficulty in difficulties)
            foreach (int stage in stages)
            foreach ((int remaining, int total) in asteroids)
            foreach (int score in scores)
            {
                view.PresentRawLayoutProbe(difficulty, stage, remaining, total, score);
                AssertFits(view.DifficultyStageValue, $"{difficulty} stage {stage} difficulty/stage");
                AssertFits(view.AsteroidValue, $"{remaining}/{total}");
                AssertFits(view.ScoreValue, score.ToString());
            }

            Assert.That(view.ScoreValue.text, Is.EqualTo("999,999"));
            Assert.That(view.DifficultyStageValue.text, Is.EqualTo("EXTREME  ·  STAGE 99"));
            Assert.That(view.AsteroidValue.text, Is.EqualTo("남은 운석  99 / 100"));
            AssertRowsSeparated((RectTransform)view.TelemetryRoot.transform.Find("HealthPanel"), "HealthLabel", "HealthValue");
            AssertRowsSeparated((RectTransform)view.TelemetryRoot.transform.Find("StagePanel"), "DifficultyStageValue", "AsteroidValue");
            AssertRowsSeparated((RectTransform)view.TelemetryRoot.transform.Find("ScorePanel"), "ScoreLabel", "ScoreValue");
        }

        [Test]
        public void MainHudAndSystemMessagesNoLongerUseDepthTestedLegacyRenderers()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            view.RefreshPresentation();
            Transform legacy = Object.FindAnyObjectByType<MeteorDefenseVR.UI.MissionHudController>(FindObjectsInactive.Include).transform;
            foreach (string name in new[] { "Panel_HP", "Panel_Remaining", "Panel_Score", "HudHealth", "HudRemaining", "HudScore", "HudFeedback", "TacticalHudFrames" })
            {
                Transform item = legacy.Find(name);
                Assert.That(item, Is.Not.Null, name);
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.enabled, Is.False, name);
                }
            }

            Transform complete = Camera.main.transform.Find("MissionCompleteText");
            Transform cinematicAlert = Camera.main.transform.Find("CinematicAlert");
            Transform launch = Object.FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include).transform.Find("LaunchStatus");
            foreach (Transform item in new[] { complete, cinematicAlert, launch })
            {
                Assert.That(item, Is.Not.Null);
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.enabled, Is.False, item.name);
                }
            }

            Material fontMaterial = view.ScoreValue.fontSharedMaterial;
            if (fontMaterial.HasProperty("_ZTestMode"))
                Assert.That(fontMaterial.GetFloat("_ZTestMode"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
            if (fontMaterial.HasProperty("_ZTest"))
                Assert.That(fontMaterial.GetFloat("_ZTest"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
        }

        [Test]
        public void PcAndVrShareLayoutWhileOnlyCanvasRenderModeChanges()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            Dictionary<string, (Vector2 min, Vector2 max, Vector2 size)> layout = CaptureLayout(view);
            view.ApplyCanvasMode(true);
            Assert.That(view.HudCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(view.HudCanvas.transform.parent, Is.SameAs(Camera.main.transform));
            Assert.That(view.HudCanvas.transform.localPosition.z, Is.EqualTo(view.VrDepth).Within(.001f));
            AssertLayout(layout, view);
            view.ApplyCanvasMode(false);
            Assert.That(view.HudCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(view.HudCanvas.transform.parent, Is.Null);
            AssertLayout(layout, view);
        }

        [Test]
        public void ReferenceSixteenNineResolutionsPreserveMarginsAndPanelSpacing()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            RectTransform safe = (RectTransform)view.transform.Find("GameplaySafeArea");
            RectTransform health = (RectTransform)view.TelemetryRoot.transform.Find("HealthPanel");
            RectTransform stage = (RectTransform)view.TelemetryRoot.transform.Find("StagePanel");
            RectTransform score = (RectTransform)view.TelemetryRoot.transform.Find("ScorePanel");
            foreach ((int width, int height) in new[] { (1920, 1080), (2560, 1440), (3840, 2160) })
            {
                float scale = width / 1920f;
                float safeWidth = width - (safe.offsetMin.x - safe.offsetMax.x) * scale;
                float healthLeft = safe.offsetMin.x * scale;
                float healthRight = healthLeft + health.sizeDelta.x * scale;
                float stageLeft = healthLeft + (safeWidth - stage.sizeDelta.x * scale) * .5f;
                float stageRight = stageLeft + stage.sizeDelta.x * scale;
                float scoreRight = width + safe.offsetMax.x * scale;
                float scoreLeft = scoreRight - score.sizeDelta.x * scale;
                Assert.That(healthLeft / width, Is.InRange(.04f, .05f), $"{width}x{height} left margin");
                Assert.That((width - scoreRight) / width, Is.InRange(.04f, .05f), $"{width}x{height} right margin");
                Assert.That((-safe.offsetMax.y * scale) / height, Is.InRange(.02f, .04f), $"{width}x{height} top margin");
                Assert.That(healthRight, Is.LessThan(stageLeft), $"{width}x{height} health/stage overlap");
                Assert.That(stageRight, Is.LessThan(scoreLeft), $"{width}x{height} stage/score overlap");
            }
        }

        [Test]
        public void RestoredTopCardsStayCompactAndVisuallyConsistent()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            foreach (string name in new[] { "HealthPanel", "StagePanel", "ScorePanel" })
            {
                RectTransform panel = (RectTransform)view.TelemetryRoot.transform.Find(name);
                Assert.That(panel.sizeDelta.y / 1080f, Is.LessThanOrEqualTo(.10f), name + " height");
                UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image.enabled, Is.True, name + " mesh source");
                Assert.That(image.raycastTarget, Is.False, name + " background raycast");
                ChamferedHudPanel frame = panel.GetComponent<ChamferedHudPanel>();
                Assert.That(frame, Is.Not.Null, name + " chamfered frame");
                Assert.That(frame.FillColor.a, Is.InRange(.78f, .86f), name + " background alpha");
                Assert.That(frame.BorderColor.a, Is.InRange(.68f, .76f), name + " border alpha");
                Assert.That(frame.CornerCut, Is.InRange(10f, 14f), name + " corner cut");
                UnityEngine.UI.Outline outline = panel.GetComponent<UnityEngine.UI.Outline>();
                Assert.That(outline.enabled, Is.False, name + " rectangular outline must stay hidden");
            }
            Assert.That(((RectTransform)view.TelemetryRoot.transform.Find("HealthPanel")).sizeDelta.x, Is.LessThan(320f));
            Assert.That(((RectTransform)view.TelemetryRoot.transform.Find("StagePanel")).sizeDelta.x, Is.LessThan(420f));
            Assert.That(((RectTransform)view.TelemetryRoot.transform.Find("ScorePanel")).sizeDelta.x, Is.LessThan(260f));
        }

        [Test]
        public void LaunchAndMissionCompleteMessagesUseTheSameAlwaysOnTopCanvas()
        {
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            LaunchSequenceController launch = Object.FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include);
            MissionCompleteController complete = Object.FindAnyObjectByType<MissionCompleteController>(FindObjectsInactive.Include);
            launch.SetDurations(0f, 0f, 0f, 0f, 0f, 0f, 1f);
            launch.BeginLaunch();
            launch.Tick(0f); launch.Tick(0f); launch.Tick(0f);
            view.RefreshPresentation();
            Assert.That(view.FeedbackPanel.activeSelf, Is.True);
            Assert.That(view.FeedbackValue.text, Is.EqualTo("3"));
            launch.Tick(0f); launch.Tick(0f); launch.Tick(0f); launch.Tick(0f); launch.Tick(0f);
            view.RefreshPresentation();
            Assert.That(view.FeedbackValue.text, Is.EqualTo("MISSION START"));
            launch.ResetLaunch();

            complete.BeginMissionComplete();
            complete.Tick(MissionCompleteController.PresentationDelay + .1f);
            view.RefreshPresentation();
            Assert.That(view.FeedbackValue.text, Does.Contain("MISSION COMPLETE"));
            Assert.That(view.HudCanvas.sortingOrder, Is.GreaterThanOrEqualTo(200));
            complete.ResetSequence();
        }

        private static void AssertFits(TMP_Text text, string context)
        {
            text.ForceMeshUpdate(true, true);
            Vector2 preferred = text.GetPreferredValues(text.text, 10000f, text.rectTransform.rect.height);
            float available = text.rectTransform.rect.width - text.margin.x - text.margin.z;
            Assert.That(preferred.x, Is.LessThanOrEqualTo(available + .5f), context + " preferred width");
            float availableHeight = text.rectTransform.rect.height - text.margin.y - text.margin.w;
            Assert.That(preferred.y, Is.LessThanOrEqualTo(availableHeight + .5f), context + " preferred height");
        }

        private static void AssertRowsSeparated(RectTransform panel, string upperName, string lowerName)
        {
            RectTransform upper = (RectTransform)panel.Find(upperName);
            RectTransform lower = (RectTransform)panel.Find(lowerName);
            Bounds upperBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, upper);
            Bounds lowerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, lower);
            Assert.That(upperBounds.min.y, Is.GreaterThan(lowerBounds.max.y), $"{panel.name}: {upperName} overlaps {lowerName}");
        }

        private static Dictionary<string, (Vector2 min, Vector2 max, Vector2 size)> CaptureLayout(GameplayHudOverlayView view)
        {
            var result = new Dictionary<string, (Vector2, Vector2, Vector2)>();
            foreach (string name in new[] { "HealthPanel", "StagePanel", "ScorePanel" })
            {
                RectTransform rect = (RectTransform)view.TelemetryRoot.transform.Find(name);
                result[name] = (rect.anchorMin, rect.anchorMax, rect.sizeDelta);
            }
            return result;
        }

        private static void AssertLayout(Dictionary<string, (Vector2 min, Vector2 max, Vector2 size)> expected, GameplayHudOverlayView view)
        {
            foreach (var item in expected)
            {
                RectTransform rect = (RectTransform)view.TelemetryRoot.transform.Find(item.Key);
                Assert.That(rect.anchorMin, Is.EqualTo(item.Value.min), item.Key);
                Assert.That(rect.anchorMax, Is.EqualTo(item.Value.max), item.Key);
                Assert.That(rect.sizeDelta, Is.EqualTo(item.Value.size), item.Key);
            }
        }
    }
}
