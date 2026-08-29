using System.Collections;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class UnifiedLookTests
    {
        [TestCase(1)] [TestCase(-1)]
        public void MouseDeltaKeepsTurningBeyondTwoFullTurns(int sign)
        {
            var input = new MouseLookProvider();
            for (int i = 0; i < 33; i++) input.AddDelta(new Vector2(900 * sign, 0));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, 90 * sign, 0), Quaternion.Euler(0, input.Angles.x, 0)), Is.LessThan(.01f));
        }
        [TestCase(false, -80)] [TestCase(true, 80)]
        public void PitchClampAndInvert(bool inverted, float expected)
        {
            var input = new MouseLookProvider { InvertY = inverted }; input.AddDelta(new Vector2(0, 100000));
            Assert.That(input.Angles.y, Is.EqualTo(expected));
            input.AddDelta(new Vector2(float.NaN, float.PositiveInfinity)); Assert.That(input.Angles.y, Is.EqualTo(expected));
        }
        [TestCase(.1f, 10)] [TestCase(1f, 100)] [TestCase(2f, 200)]
        public void SensitivityChangesActualAngle(float sensitivity, float expected)
        { var p = new MouseLookProvider { Sensitivity = sensitivity }; p.AddDelta(new Vector2(1000, 0)); Assert.That(p.Angles.x, Is.EqualTo(expected).Within(.001)); }

        private sealed class HeadSample : IHeadRotationSource
        { public bool HasFreshHeadRotation { get; set; } = true; public Quaternion HeadRotation { get; set; } = Quaternion.identity; }
        [TestCase(true, true, false, true)] [TestCase(true, false, true, false)]
        [TestCase(false, true, false, false)] [TestCase(false, false, true, true)]
        [TestCase(true, false, false, false)]
        public void WebcamFallbackUsesHeadPoseInFlightButEyeSamplesInMenus(bool steering, bool head, bool eyes, bool expected)
            => Assert.That(LookInputPolicy.HasRequiredWebcamTracking(steering, head, eyes), Is.EqualTo(expected));
        [TestCase(25, 0)] [TestCase(-25, 0)] [TestCase(0, 25)] [TestCase(0, -25)]
        public void WebcamHeadPoseSteersAndNeutralOrLossStops(float yaw, float pitch)
        {
            var sample = new HeadSample(); var p = new WebcamHeadTrackingProvider(sample); p.TryGetRotation(.02f, out _);
            sample.HeadRotation = Quaternion.Euler(pitch, yaw, 0);
            for (int i = 0; i < 120; i++) Assert.That(p.TryGetRotation(.02f, out _), Is.True);
            Assert.That(Mathf.Abs(yaw != 0 ? p.Angles.x : p.Angles.y), Is.GreaterThan(yaw != 0 ? 200 : 70));
            Vector2 before = p.Angles; sample.HeadRotation = Quaternion.identity; p.TryGetRotation(.02f, out _);
            Assert.That(p.Angles, Is.EqualTo(before)); sample.HasFreshHeadRotation = false;
            Assert.That(p.TryGetRotation(.1f, out _), Is.False); Assert.That(p.Angles, Is.EqualTo(before));
        }
        [TestCase(25, 15)] [TestCase(-25, -15)]
        public void MetricFacePoseConvertsHandednessWithoutEyeCoordinates(float yaw, float pitch)
        {
            Quaternion expected = Quaternion.Euler(pitch, yaw, 0);
            Matrix4x4 reflect = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var metric = reflect * Matrix4x4.Rotate(expected) * reflect;
            Assert.That(WebcamHeadTrackingProvider.TryConvertFaceMatrix(metric, out var actual), Is.True);
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.02));
            Assert.That(WebcamHeadTrackingProvider.TryConvertFaceMatrix(Matrix4x4.zero, out _), Is.False);
        }
        [TestCase(GameState.Boot, false)] [TestCase(GameState.Intro, true)] [TestCase(GameState.EyeCalibration, false)]
        [TestCase(GameState.Playing, true)] [TestCase(GameState.Result, false)] [TestCase(GameState.Reset, false)]
        public void MenusAndCalibrationReleaseDesktopLook(GameState state, bool look) => Assert.That(LookInputPolicy.GameplayLook(state), Is.EqualTo(look));
        [Test] public void DirectionTrainingProgressesFromExperienceToHard360()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>("Assets/_Project/Difficulty/Resources/DifficultySettings.asset");
            Assert.That(settings.Experience.spawnDirections, Is.EqualTo(SpawnDirectionMode.ExperienceArc));
            Assert.That(settings.easy.spawnDirections, Is.EqualTo(SpawnDirectionMode.FrontOnly));
            Assert.That(settings.normal.spawnDirections, Is.EqualTo(SpawnDirectionMode.FrontAndSides));
            Assert.That(settings.hard.spawnDirections, Is.EqualTo(SpawnDirectionMode.FullSphere));
            Assert.That(settings.easy.meteorSpeed, Is.EqualTo(settings.hard.meteorSpeed));
            Assert.That(settings.normal.showWarningDistance, Is.True); Assert.That(settings.hard.showWarningDistance, Is.True);
            Assert.That(settings.hard.maximumWarningIndicators, Is.EqualTo(5));
        }
        [Test] public void HmdNeverReceivesComfortShakeOrDesktopClamp()
        {
            foreach (var level in new[] { CameraShakeLevel.Off, CameraShakeLevel.Weak, CameraShakeLevel.Strong })
                Assert.That(LookInputPolicy.ShakeDegrees(level, true), Is.Zero);
            Assert.That(LookInputPolicy.ShakeDegrees(CameraShakeLevel.Off, false), Is.Zero);
        }
        [Test] public void ExistingTrackedPoseDriverOwnsUnclampedHmdPose()
        {
            var root = new GameObject("TrackedPoseOwnershipQA");
            try
            {
                var camera = root.AddComponent<Camera>();
                var driver = root.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                var expected = Quaternion.Euler(110, 215, 37);
                camera.transform.localRotation = expected;
                var provider = new VRHeadLookProvider(camera);
                Assert.That(provider.ExternallyDriven, Is.True);
                Assert.That(provider.TryGetRotation(.1f, out var actual), Is.True);
                Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(.02f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, expected), Is.LessThan(.02f));
                driver.enabled = false;
                Assert.That(provider.ExternallyDriven, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase(-1, 30, -2, "◀ REAR")] [TestCase(1, -30, -2, "REAR ▶")]
        public void RearUpperAndLowerWarningsPreferReachableYaw(float x, float y, float z, string expected)
        {
            var local = new Vector3(x, y, z);
            Assert.That(ThreatProjection.Direction(local), Is.EqualTo(expected));
            Assert.That(ThreatProjection.Edge(local, 1.77f).x, Is.EqualTo(Mathf.Sign(x)));
        }
        [Test] public void CenterConeAndRearWarningsFollowRotatedCamera()
        {
            var root = new GameObject("ViewMathQA");
            try
            {
                var camera = root.AddComponent<Camera>(); var view = root.AddComponent<ViewDirectionProvider>(); view.Configure(camera);
                Vector3 rear = new Vector3(-1, 0, -20);
                Assert.That(ThreatProjection.Edge(rear, 1.77f).x, Is.EqualTo(-1));
                camera.transform.rotation = Quaternion.LookRotation(rear);
                Assert.That(view.IsWithinCenterCone(rear), Is.True);
                Assert.That(Vector3.Angle(view.CenterRay.direction, rear), Is.LessThan(.01));
                Assert.That(view.IsWithinCenterCone(Vector3.forward * 20), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        // Natural pilots now feed the real InputSystem delta path, not camera writes
        // or absolute screen coordinates. Menus retain the absolute pointer path.
        private static int lastPilotFrame = -1, lastPilotMouse = -1;
        internal static void QueueAim(Mouse mouse, WindowsVRSimulation look, Transform target, Vector2 menuPosition)
        {
            // EditMode's enumerator can tick many times within one PlayerLoop frame.
            // Relative events accumulate: unlike an absolute pointer, repeating a
            // correction before the controller consumes it overshoots the target.
            if (lastPilotMouse == mouse.deviceId && lastPilotFrame == Time.frameCount) return;
            lastPilotMouse = mouse.deviceId; lastPilotFrame = Time.frameCount;
            Vector2 delta = Vector2.zero;
            if (look != null && look.IsLooking && target != null)
            {
                Vector3 aim = target.position - Camera.main.transform.position;
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                delta = new Vector2(Mathf.DeltaAngle(look.RequestedAngles.x, yaw) / (.1f * look.Sensitivity),
                    (pitch - look.RequestedAngles.y) / (.09f * look.Sensitivity) * (look.InvertY ? 1 : -1));
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = menuPosition, delta = delta });
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; AudioListener.pause = false; if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(60000)] public IEnumerator ContinuousDeltaPilotAcquiresOffsetWithoutOscillation()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode; var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>(); var target = new GameObject("DeltaPilotTarget");
            try
            {
                var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
                GameFlowManager.Instance.StartGame();
                yield return null; yield return null;
                target.transform.position = Camera.main.transform.position + Quaternion.Euler(-15, 30, 0) * Vector3.forward * 20;
                var rows = new System.Collections.Generic.List<string>();
                float deadline = Time.realtimeSinceStartup + 1.5f;
                int i = 0;
                while (Time.realtimeSinceStartup < deadline)
                {
                    QueueAim(mouse, router.Simulation, target.transform, Camera.main.WorldToScreenPoint(target.transform.position));
                    if (i < 30) rows.Add($"i={i} frame={Time.frameCount} input={InputState.updateCount} delta={mouse.delta.ReadValue()} target={router.Simulation.RequestedAngles} actual={router.Simulation.Angles}");
                    i++;
                    yield return null;
                }
                System.IO.Directory.CreateDirectory("Docs/UnifiedLookQA"); System.IO.File.WriteAllLines("Docs/UnifiedLookQA/continuous-delta.txt", rows);
                Assert.That(Vector3.Angle(Camera.main.transform.forward, target.transform.position - Camera.main.transform.position), Is.LessThan(1), "A continuous real-input pilot must converge, not alternate between pitch clamps");
            }
            finally { Object.Destroy(target); InputSystem.RemoveDevice(mouse); InputSystem.settings.editorInputBehaviorInPlayMode = oldInput; InputSystem.settings.backgroundBehavior = oldBackground; }
            yield return new ExitPlayMode();
        }
        [UnityTest, Timeout(120000)] public IEnumerator NormalMouseDeltaReachesCameraAndPauseMenuReleasesCursor()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode; var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>(); var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
                var controller = router.Simulation; var flow = GameFlowManager.Instance; var camera = Camera.main;
                using var render = new PcStabilityProbe(camera);
                yield return new WaitForSecondsRealtime(6);
                Assert.That(controller.IsLooking, Is.False); Assert.That(controller.WantsLockedCursor, Is.False);
                var spawner = Object.FindAnyObjectByType<MeteorDefenseVR.Meteor.MeteorSpawner>();
                var poolSetting = new SerializedObject(spawner).FindProperty("prewarmPerMeteorKind");
                Assert.That(poolSetting.intValue, Is.GreaterThanOrEqualTo(Object.FindAnyObjectByType<DifficultyManager>().Settings.extreme.maxConcurrentMeteors));
                Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Normal);
                flow.StartMainGame(); yield return null; yield return null;
                Object.FindAnyObjectByType<GazeRaycaster>().enabled = false; // Observe pose without accidental auto-fire during input assertions.
                for (int i = 1; i <= 8; i++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2), delta = new Vector2(900, 0) }); yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2) }); yield return new WaitForSecondsRealtime(.35f);
                    Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(0, 90 * i, 0)), Is.LessThan(.2f), "Real delta must reach camera through Update; absolute position is held at the corner");
                    Assert.That(Vector3.Angle(router.GetGazeDirection(), camera.transform.forward), Is.LessThan(.02f));
                    if (i == 2) { Directory.CreateDirectory("Docs/UnifiedLookQA/rear-view"); render.SaveGameOnlyProof("Docs/UnifiedLookQA/rear-view", router); }
                }
                controller.Recenter(); yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0, 10000) }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSecondsRealtime(.35f);
                Assert.That(controller.Angles.y, Is.EqualTo(-80).Within(.2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(controller.IsPaused, Is.True); Assert.That(controller.WantsLockedCursor, Is.False); Assert.That(Time.timeScale, Is.Zero);
                Quaternion paused = camera.transform.rotation;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(900, 900) });
                yield return new WaitForSecondsRealtime(.2f); Assert.That(Quaternion.Angle(paused, camera.transform.rotation), Is.LessThan(.01));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Directory.CreateDirectory("Docs/UnifiedLookQA/pause"); render.SaveGameOnlyProof("Docs/UnifiedLookQA/pause", router);
                foreach (var button in Object.FindAnyObjectByType<PcTestOverlay>().GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (button.name == "LookSettings") button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(router.UiModalOpen, Is.True, "Settings must block world menu gaze behind the panel");
                Assert.That(router.IsTrackingValid, Is.False);
                Directory.CreateDirectory("Docs/UnifiedLookQA/settings"); render.SaveGameOnlyProof("Docs/UnifiedLookQA/settings", router);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(controller.IsPaused, Is.False); Assert.That(controller.WantsLockedCursor, Is.True);
                Assert.That(router.UiModalOpen, Is.False, "Resume must close all screen-space modal panels");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                flow.ShowResult(); yield return null; Assert.That(controller.WantsLockedCursor, Is.False);
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience(); yield return null;
                Assert.That(controller.WantsLockedCursor, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
                flow.StartCalibration(); yield return null;
                controller.Pause(); Assert.That(Time.timeScale, Is.Zero);
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience(); yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(router.InputSuspended, Is.False);
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
                Assert.That(router.Webcam.IsStreaming, Is.False);
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); InputSystem.settings.editorInputBehaviorInPlayMode = oldInput; InputSystem.settings.backgroundBehavior = oldBackground; }
            yield return new ExitPlayMode();
        }
    }
}
