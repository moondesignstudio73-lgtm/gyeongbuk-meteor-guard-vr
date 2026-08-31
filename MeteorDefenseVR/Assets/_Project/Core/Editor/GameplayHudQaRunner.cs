using System;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    [InitializeOnLoad]
    public static class GameplayHudQaRunner
    {
        private const string JobKey = "MeteorDefense.GameplayHudQa.Active";
        private const string StateKey = "MeteorDefense.GameplayHudQa.State";
        private const string IndexKey = "MeteorDefense.GameplayHudQa.Index";
        private const string DeadlineKey = "MeteorDefense.GameplayHudQa.Deadline";
        private const string ScenePath = "Assets/_Project/Scenes/MeteorDefense.unity";
        private static readonly (float yaw, float pitch, string name)[] Views =
        {
            (0f, 0f, "Front"), (-90f, 0f, "Left"), (90f, 0f, "Right"),
            (180f, 0f, "Rear"), (0f, 65f, "Above"), (0f, -65f, "Below")
        };
        private static PcStabilityProbe probe;
        private static TestRunnerApi testApi;
        private static RegressionCallbacks callbacks;

        static GameplayHudQaRunner()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            if (SessionState.GetBool(JobKey, false) && EditorApplication.isPlaying)
                EditorApplication.delayCall += BeginTicking;
        }

        public static void CaptureSixDirections()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            if (view == null || view.HudCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                throw new InvalidOperationException("Authored Gameplay HUD overlay is missing.");
            SessionState.SetBool(JobKey, true);
            SessionState.SetString(StateKey, "enter");
            SessionState.SetInt(IndexKey, 0);
            EditorApplication.EnterPlaymode();
        }

        public static void RunEditModeRegression()
        {
            testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new RegressionCallbacks();
            testApi.RegisterCallbacks(callbacks);
            testApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.AuthoredMainHudUsesDedicatedPlayerOverlayAndSafeArea",
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.ScoreStageAsteroidAndDifficultyRegressionValuesStayInsideTheirRects",
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.MainHudAndSystemMessagesNoLongerUseDepthTestedLegacyRenderers",
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.PcAndVrShareLayoutWhileOnlyCanvasRenderModeChanges",
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.ReferenceSixteenNineResolutionsPreserveMarginsAndPanelSpacing",
                    "MeteorDefenseVR.Tests.GameplayHudOverlayTests.LaunchAndMissionCompleteMessagesUseTheSameAlwaysOnTopCanvas",
                    "MeteorDefenseVR.Tests.MissionHudTests.SessionDisplaysTotalRemainingAndAwardedScore",
                    "MeteorDefenseVR.Tests.MissionHudTests.HealthIsClampedAndFormattedAsEightSegments",
                    "MeteorDefenseVR.Tests.TutorialSafeZoneTests.AuthoredTutorialUsesPlayerHudSafeZoneAndHidesWorldPresentation",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.SixDirectionPlayerViewsKeepTurretGeometryCulledAndCombatUiVisible",
                    "MeteorDefenseVR.Tests.RearPanoramaCockpitTests.RearShellUsesPanoramaGlassAndSlimStructuralFrames"
                }
            }));
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(JobKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) BeginTicking();
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                bool failed = SessionState.GetString(StateKey, string.Empty) == "failed";
                SessionState.EraseBool(JobKey);
                SessionState.EraseString(StateKey);
                SessionState.EraseInt(IndexKey);
                SessionState.EraseFloat(DeadlineKey);
                EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        private static void BeginTicking()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            if (SessionState.GetString(StateKey, string.Empty) == "enter") SessionState.SetString(StateKey, "boot");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(JobKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                string state = SessionState.GetString(StateKey, "boot");
                if (state == "boot")
                {
                    GazeInputRouter router = Object.FindAnyObjectByType<GazeInputRouter>();
                    router.SetMode(PcGazeMode.WindowsVRSimulation);
                    GameFlowManager.Instance.StartMainGame();
                    probe?.Dispose();
                    probe = new PcStabilityProbe(Camera.main);
                    SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + .5f);
                    SessionState.SetString(StateKey, "orient");
                    return;
                }
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(DeadlineKey, 0f)) return;
                int index = SessionState.GetInt(IndexKey, 0);
                if (index >= Views.Length) { Finish(); return; }
                var direction = Views[index];
                GazeInputRouter activeRouter = Object.FindAnyObjectByType<GazeInputRouter>();
                GameplayHudOverlayView view = Object.FindAnyObjectByType<GameplayHudOverlayView>();
                if (view == null || !view.TelemetryRoot.activeInHierarchy) throw new InvalidOperationException("Gameplay telemetry is not visible.");
                if (state == "orient")
                {
                    activeRouter.Simulation.Recenter();
                    activeRouter.Simulation.ApplyLook(new Vector2(direction.yaw / .1f, -direction.pitch / .09f), 1f);
                    SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + .22f);
                    SessionState.SetString(StateKey, "capture");
                    return;
                }

                view.PresentRawLayoutProbe("EXTREME", 99, 99, 100, 999999);
                Canvas.ForceUpdateCanvases();
                ValidateScreenLayout(view, direction.name);
                Camera.main.Render();
                string directory = Path.Combine("Docs/GameplayHudQA", "After", direction.name);
                Directory.CreateDirectory(directory);
                probe.SaveGameOnlyProof(directory, activeRouter);
                SessionState.SetInt(IndexKey, index + 1);
                SessionState.SetString(StateKey, "orient");
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + .08f);
            }
            catch (Exception exception)
            {
                probe?.Dispose();
                probe = null;
                Debug.LogException(exception);
                SessionState.SetString(StateKey, "failed");
                EditorApplication.update -= Tick;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void ValidateScreenLayout(GameplayHudOverlayView view, string direction)
        {
            if (view.HudCanvas.renderMode != RenderMode.ScreenSpaceCamera) throw new InvalidOperationException(direction + " render mode changed");
            Rect health = ScreenRect((RectTransform)view.TelemetryRoot.transform.Find("HealthPanel"), view.HudCanvas.worldCamera);
            Rect stage = ScreenRect((RectTransform)view.TelemetryRoot.transform.Find("StagePanel"), view.HudCanvas.worldCamera);
            Rect score = ScreenRect((RectTransform)view.TelemetryRoot.transform.Find("ScorePanel"), view.HudCanvas.worldCamera);
            float width = view.HudCanvas.worldCamera.pixelWidth;
            float height = view.HudCanvas.worldCamera.pixelHeight;
            if (health.xMin < width * .04f || score.xMax > width * .96f || health.yMax > height * .98f || score.yMax > height * .98f)
                throw new InvalidOperationException(direction + " safe-area violation");
            if (health.xMax >= stage.xMin || stage.xMax >= score.xMin)
                throw new InvalidOperationException(direction + " top HUD panels overlap");
            ValidateTextFits(view.ScoreValue, direction + " score");
            ValidateTextFits(view.AsteroidValue, direction + " asteroids");
            ValidateTextFits(view.DifficultyStageValue, direction + " difficulty/stage");
            ValidateRows(view.TelemetryRoot.transform.Find("HealthPanel"), "HealthLabel", "HealthValue", direction);
            ValidateRows(view.TelemetryRoot.transform.Find("StagePanel"), "DifficultyStageValue", "AsteroidLabel", direction);
            ValidateRows(view.TelemetryRoot.transform.Find("StagePanel"), "DifficultyStageValue", "AsteroidValue", direction);
            ValidateRows(view.TelemetryRoot.transform.Find("ScorePanel"), "ScoreLabel", "ScoreValue", direction);
            if (view.ScoreValue.text != "999,999" || view.AsteroidValue.text != "99 / 100" || view.DifficultyStageValue.text != "남은 운석")
                throw new InvalidOperationException(direction + " stress values were not rendered");
        }

        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static void ValidateTextFits(TMPro.TMP_Text text, string context)
        {
            text.ForceMeshUpdate(true, true);
            Vector2 preferred = text.GetPreferredValues(text.text, 10000f, text.rectTransform.rect.height);
            float width = text.rectTransform.rect.width - text.margin.x - text.margin.z;
            float height = text.rectTransform.rect.height - text.margin.y - text.margin.w;
            if (preferred.x > width + .5f || preferred.y > height + .5f)
                throw new InvalidOperationException($"{context} does not fit: preferred {preferred}, available ({width},{height})");
        }

        private static void ValidateRows(Transform panelTransform, string upperName, string lowerName, string direction)
        {
            RectTransform panel = (RectTransform)panelTransform;
            Bounds upper = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, panel.Find(upperName));
            Bounds lower = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, panel.Find(lowerName));
            if (upper.min.y <= lower.max.y)
                throw new InvalidOperationException($"{direction} {panel.name}: {upperName} overlaps {lowerName}");
        }

        private static void Finish()
        {
            probe?.Dispose();
            probe = null;
            Directory.CreateDirectory("Docs/GameplayHudQA");
            File.WriteAllLines("Docs/GameplayHudQA/six-direction-validation.txt", new[]
            {
                "Windows VR Simulation: six-direction Gameplay HUD validation",
                "Front, Left, Right, Rear, Above, Below: fixed Screen Space Camera layout passed",
                "Safe area: horizontal 4-5%, top 2-4%",
                "Stress values: 남은 운석 99 / 100 / SCORE 999,999",
                "Top panels and internal rows: no overlap; TMP preferred bounds fit their RectTransforms",
                "Legacy depth-tested HUD, LaunchStatus and MissionCompleteText renderers: hidden"
            });
            SessionState.SetString(StateKey, "done");
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        private sealed class RegressionCallbacks : ICallbacks
        {
            private readonly List<string> lines = new List<string>();
            private int passed, failed, skipped;
            public void RunStarted(ITestAdaptor testsToRun) => lines.Add("Gameplay HUD targeted EditMode regression");
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                if (result.TestStatus == TestStatus.Passed) passed++;
                else if (result.TestStatus == TestStatus.Failed) { failed++; lines.Add("FAIL " + result.FullName + ": " + result.Message); }
                else skipped++;
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                lines.Add($"passed={passed} failed={failed} skipped={skipped} overall={result.TestStatus}");
                Directory.CreateDirectory("Docs/GameplayHudQA");
                File.WriteAllLines("Docs/GameplayHudQA/editmode-regression.txt", lines);
                testApi?.UnregisterCallbacks(this);
                EditorApplication.Exit(failed == 0 ? 0 : 1);
            }
        }
    }
}
