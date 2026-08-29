using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class PcInputTests
    {
        [TestCase(PcGazeMode.Auto, true, true, true, PcGazeMode.VREyeTracking)]
        [TestCase(PcGazeMode.Auto, false, true, true, PcGazeMode.WebcamGaze)]
        [TestCase(PcGazeMode.Auto, false, false, true, PcGazeMode.MouseGaze)]
        [TestCase(PcGazeMode.Auto, false, false, false, PcGazeMode.CameraCenterGaze)]
        [TestCase(PcGazeMode.WebcamGaze, true, false, true, PcGazeMode.MouseGaze)]
        [TestCase(PcGazeMode.MouseGaze, true, true, true, PcGazeMode.MouseGaze)]
        [TestCase(PcGazeMode.CameraCenterGaze, true, true, true, PcGazeMode.CameraCenterGaze)]
        [TestCase(PcGazeMode.VREyeTracking, false, true, true, PcGazeMode.MouseGaze)]
        public void InputPriorityAndFallback(PcGazeMode requested, bool vr, bool webcam, bool mouse, PcGazeMode expected)
            => Assert.That(GazeModePolicy.Select(requested, vr, webcam, mouse), Is.EqualTo(expected));

        [Test]
        public void FivePointCalibration_FitsEyeMotionAndClampsScreen()
        {
            var map = new WebcamCalibrationMap();
            Vector2[] samples = WebcamCalibrationMap.Targets.Select(p => new Vector2(.5f - (p.x - .5f) * .2f, .5f - (p.y - .5f) * .25f)).ToArray();
            Assert.That(map.Fit(samples), Is.True);
            for (int i = 0; i < 5; i++) Assert.That(Vector2.Distance(map.Map(samples[i]), WebcamCalibrationMap.Targets[i]), Is.LessThan(.001f));
            Assert.That(map.Map(new Vector2(-5, 9)), Is.EqualTo(new Vector2(1, 0)));
        }
        [Test] public void FlatCalibrationCannotPretendToTrackEyes() => Assert.That(new WebcamCalibrationMap().Fit(Enumerable.Repeat(Vector2.one * .5f, 5).ToArray()), Is.False);
        [Test] public void InvalidCalibrationIsRejected() => Assert.That(new WebcamCalibrationMap().Fit(new[] { Vector2.zero, Vector2.one, new Vector2(float.NaN, 0), Vector2.up, Vector2.right }), Is.False);
        [Test]
        public void Smoothing_IsFrameRateIndependentAndRejectsInvalidData()
        {
            Vector2 once = WebcamCalibrationMap.Smooth(Vector2.zero, Vector2.one, .2f, .12f, 0);
            Vector2 twice = WebcamCalibrationMap.Smooth(WebcamCalibrationMap.Smooth(Vector2.zero, Vector2.one, .1f, .12f, 0), Vector2.one, .1f, .12f, 0);
            Assert.That(Vector2.Distance(once, twice), Is.LessThan(.00001f));
            Assert.That(WebcamCalibrationMap.Smooth(once, new Vector2(float.NaN, 0), .1f, .12f, 0), Is.EqualTo(once));
            Assert.That(WebcamCalibrationMap.Smooth(once, once + Vector2.one * .001f, .1f, .12f, .01f), Is.EqualTo(once));
        }
        [Test]
        public void PixelOrientation_ConvertsBottomLeftToTopLeft()
        {
            var source = new[] { new Color32(1,0,0,255), new Color32(2,0,0,255), new Color32(3,0,0,255), new Color32(4,0,0,255) };
            var output = new Color32[4];
            WebcamGazeProvider.CopyUpright(source, output, 2, 2, 0, false);
            Assert.That(output.Select(p => p.r).ToArray(), Is.EqualTo(new byte[] { 3, 4, 1, 2 }));
            WebcamGazeProvider.CopyUpright(source, output, 2, 2, 90, false);
            Assert.That(output.Select(p => p.r).ToArray(), Is.EqualTo(new byte[] { 1, 3, 2, 4 }));
        }
        [Test]
        public void IrisFeature_UsesPupilMotionAndRejectsClosedEyes()
        {
            var points = new Vector2[478];
            points[33] = new Vector2(.25f, .4f); points[133] = new Vector2(.4f, .4f);
            points[159] = new Vector2(.325f, .38f); points[145] = new Vector2(.325f, .42f); points[468] = new Vector2(.325f, .4f);
            points[362] = new Vector2(.6f, .4f); points[263] = new Vector2(.75f, .4f);
            points[386] = new Vector2(.675f, .38f); points[374] = new Vector2(.675f, .42f); points[473] = new Vector2(.675f, .4f);
            Assert.That(WebcamGazeProvider.TryExtractEyeFeatures(points, out Vector2 center, out float confidence), Is.True);
            Assert.That(confidence, Is.GreaterThan(.55f));
            points[468] += new Vector2(.02f, 0); points[473] += new Vector2(.02f, 0);
            Assert.That(WebcamGazeProvider.TryExtractEyeFeatures(points, out Vector2 moved, out _), Is.True);
            Assert.That(moved.x, Is.GreaterThan(center.x + .1f));
            points[159] = points[145];
            Assert.That(WebcamGazeProvider.TryExtractEyeFeatures(points, out _, out _), Is.False);
        }
        [Test]
        public void ColliderAssist_IsReversibleAndDoesNotAccumulate()
        {
            var root = new GameObject("AssistTest");
            try
            {
                var collider = root.AddComponent<SphereCollider>();
                var target = root.AddComponent<GazeLockOnTarget>();
                // EditMode does not automatically invoke MonoBehaviour.Awake.
                typeof(GazeLockOnTarget).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
                target.ConfigureComfortTolerance(1f);
                float original = collider.radius;
                target.ConfigureComfortTolerance(1.4f);
                target.ConfigureComfortTolerance(1.4f);
                Assert.That(collider.radius, Is.EqualTo(original * 1.4f).Within(.0001f));
                target.ConfigureComfortTolerance(1f);
                Assert.That(collider.radius, Is.EqualTo(original).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void NativeLocalFaceModel_LoadsAndProcessesBlankImageWithoutCamera()
        {
            var model = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/face_landmarker_v2_with_blendshapes.bytes");
            Assert.That(model, Is.Not.Null);
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(Enumerable.Repeat(new Color32(0, 0, 0, 255), 64 * 64).ToArray());
                using (var detector = WebcamGazeProvider.CreateLocalLandmarker(model.bytes))
                using (var image = new Mediapipe.Image(texture))
                {
                    var result = Mediapipe.Tasks.Vision.FaceLandmarker.FaceLandmarkerResult.Alloc(1);
                    Assert.That(detector.TryDetectForVideo(image, 1, null, ref result), Is.False);
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [UnityTearDown]
        public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(180000)]
        public IEnumerator MouseGaze_PlaysNaturalFullLoopWithoutVrOrWebcam()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            // The test runner serializes this iterator across domain reload; construct closures afterwards.
            yield return PlayNaturalLoop();
            yield return new ExitPlayMode();
        }
        internal static IEnumerator PlayNaturalLoop()
        {
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var backgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Mouse simulatedMouse = InputSystem.AddDevice<Mouse>();
            var progressionSettings = Resources.Load<StageProgressionSettings>("StageProgressionSettings");
            float originalExperienceSeconds = progressionSettings.experienceTargetSeconds;
            progressionSettings.experienceTargetSeconds = 45f;
            try
            {
            GazeInputRouter router = Object.FindAnyObjectByType<GazeInputRouter>();
            router.SetMode(PcGazeMode.MouseGaze);
            var flow = GameFlowManager.Instance;
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(flow, Is.Not.Null, "Direct scene bootstrap must create GameFlow before PC input starts");
            var visited = new HashSet<GameState> { flow.CurrentState };
            using var bgmProbe = new BgmNaturalFlowProbe();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            flow.OnStateChanged += state => visited.Add(state);
            Assert.That(Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Experience), Is.True);
            Object.FindAnyObjectByType<ReadyScreenController>().StartExperience();
            float began = Time.realtimeSinceStartup;
            GazeLockOnTarget target = null;
            int maxScore = 0;
            bool checkedHud = false;
            var lookTrace = new List<string>(); float nextLookTrace = 0;
            while (Time.realtimeSinceStartup - began < 145f)
            {
                bgmProbe.Sample(flow.CurrentState);
                if (target == null || !target.IsAvailable)
                    target = Object.FindObjectsByType<GazeLockOnTarget>().Where(t => t.IsAvailable)
                        .OrderBy(t => Vector3.Distance(Camera.main.transform.position, t.transform.position)).FirstOrDefault();
                Vector2 mousePosition = target != null ? (Vector2)Camera.main.WorldToScreenPoint(target.transform.position) : new Vector2(Screen.width * .5f, Screen.height * .45f);
                UnifiedLookTests.QueueAim(simulatedMouse, router.Simulation, target != null ? target.transform : null, mousePosition);
                if (Time.realtimeSinceStartup >= nextLookTrace)
                {
                    nextLookTrace = Time.realtimeSinceStartup + 1;
                    var caster = Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
                    var stats = Object.FindAnyObjectByType<GameSessionStats>();
                    lookTrace.Add($"{Time.realtimeSinceStartup-began:F2} {flow.CurrentState} look={router.Simulation.IsLooking} valid={router.IsTrackingValid} req={router.Simulation.RequestedAngles} actual={router.Simulation.Angles} angle={(target!=null?Vector3.Angle(Camera.main.transform.forward,target.transform.position-Camera.main.transform.position):-1):F2} target={target?.name} hit={caster.CurrentHit.collider?.name} progress={target?.Progress:F2} damage={stats.DamageTaken} shots={stats.Shots}");
                }
                if (flow.CurrentState == GameState.Tutorial && Time.realtimeSinceStartup - began > 30f)
                {
                    var caster = Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
                    Assert.Fail($"Tutorial stalled. target={target?.name}; mouse={Mouse.current.position.ReadValue()}; expected={mousePosition}; valid={router.IsTrackingValid}; hit={caster.CurrentHit.collider?.name}; progress={target?.Progress}; screen={Screen.width}x{Screen.height}");
                }
                maxScore = Mathf.Max(maxScore, Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include).Score);
                if (flow.CurrentState == GameState.Playing && !checkedHud)
                {
                    yield return null;
                    var view = Object.FindAnyObjectByType<MissionHudView>();
                    Assert.That(view.transform.Find("HudHealth").GetComponent<TextMesh>().text, Does.Contain("100%"), "First visible HUD must refresh its initial HP");
                    Assert.That(Object.FindAnyObjectByType<MeteorDefenseVR.Tutorial.PracticeHudView>().GetComponent<Renderer>().enabled, Is.False, "Practice-only text must not leak into the main game");
                    checkedHud = true;
                }
                if (lobby != null && lobby.IsMenuReady) lobby.Queue(HangarLobbyController.MenuAction.Standby);
                if (visited.Contains(GameState.Result) && flow.CurrentState == GameState.Boot) break;
                yield return null;
            }
            System.IO.Directory.CreateDirectory("Docs/UnifiedLookQA"); System.IO.File.WriteAllLines("Docs/UnifiedLookQA/natural-input-trace.txt", lookTrace);
            foreach (GameState required in new[] { GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration, GameState.Tutorial, GameState.Practice, GameState.Launch, GameState.Countdown, GameState.Playing, GameState.BossMeteor, GameState.MissionComplete, GameState.Result, GameState.Reset, GameState.Boot })
                Assert.That(visited.Contains(required), Is.True, "Missing natural state: " + required + "; last state " + flow.CurrentState);
            Assert.That(maxScore, Is.GreaterThan(0));
            Assert.That(router.Webcam.IsStreaming, Is.False);
            // End this pilot's input before measuring session cleanup. A cursor left over
            // the newly revealed START can legitimately play a NEW Ready hover cue.
            bool wasSuspended = router.InputSuspended;
            router.InputSuspended = true;
            InputSystem.QueueStateEvent(simulatedMouse, new MouseState { position = new Vector2(2, 2) });
            try
            {
                yield return new WaitForSecondsRealtime(Object.FindAnyObjectByType<MeteorDefenseVR.Audio.AudioManager>().FadeDuration + .15f);
                bgmProbe.Complete();
            }
            finally { router.InputSuspended = wasSuspended; }
            }
            finally
            {
                progressionSettings.experienceTargetSeconds = originalExperienceSeconds;
                InputSystem.RemoveDevice(simulatedMouse);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                InputSystem.settings.backgroundBehavior = backgroundInput;
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BootstrapMouseGaze_PlaysNaturalFullLoopWithSpaceEnvironment()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return GameplaySceneBootstrapTests.WaitForGameplayScene();
            GameplaySceneBootstrapTests.AssertGameplayEnvironment();
            yield return PlayNaturalLoop();
            GameplaySceneBootstrapTests.AssertGameplayEnvironment();
            yield return new ExitPlayMode();
        }
    }
}
