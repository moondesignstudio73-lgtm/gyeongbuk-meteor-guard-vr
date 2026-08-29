using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class StabilityRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReenabledInputCalibrationAndVfxRebindExactlyOnce()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var calibration = Object.FindAnyObjectByType<PcCalibrationController>();
            var effects = Object.FindAnyObjectByType<PremiumCombatVfx>();
            foreach (MonoBehaviour component in new MonoBehaviour[] { router, calibration, effects })
            {
                for (int i = 0; i < 3; i++) { component.enabled = false; component.enabled = true; }
                var listeners = (Delegate)typeof(GameFlowManager).GetField("OnStateChanged", PrivateInstance).GetValue(GameFlowManager.Instance);
                Assert.That(listeners.GetInvocationList().Count(d => ReferenceEquals(d.Target, component)), Is.EqualTo(1), component.GetType().Name);
            }
            GameFlowManager.Instance.StartCalibration(); yield return null; yield return null;
            Assert.That(calibration.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator OperatorResetRecoversPausedSuspendedSession()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            GameFlowManager.Instance.StartMainGame(); yield return null;
            router.InputSuspended = true; router.CalibrationInProgress = true;
            Time.timeScale = 0; AudioListener.pause = true;
            Object.FindAnyObjectByType<PcTestControls>().ResetExperience(); yield return null;
            Assert.That(GameFlowManager.Instance.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(AudioListener.pause, Is.False);
            Assert.That(router.InputSuspended, Is.False); Assert.That(router.CalibrationInProgress, Is.False);
        }

        [UnityTest]
        public IEnumerator QaRenderProbeActuallyRendersAndRestoresCamera()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            Object.FindAnyObjectByType<GazeInputRouter>().SetMode(PcGazeMode.MouseGaze);
            var camera = Camera.main; var previous = camera.targetTexture;
            using (var probe = new PcStabilityProbe(camera))
            {
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(probe.RenderedFrames, Is.GreaterThan(0), "A running player loop alone is not a rendering benchmark");
                Assert.That(camera.targetTexture, Is.Not.Null);
                Assert.That(probe.AllocationCounterAvailable, Is.True);
                Assert.That(probe.LastFrameAllocation, Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(camera.targetTexture, Is.EqualTo(previous));
        }

        [Test]
        public void PlayerAnalyticsAndEngineDiagnosticsAreDisabled()
        {
            Assert.That(UnityEditor.EngineDiagnostics.EngineDiagnosticsSettings.enabled, Is.False);
            Assert.That(UnityEditor.Analytics.AnalyticsSettings.enabled, Is.False);
            Assert.That(UnityEditor.Analytics.AnalyticsSettings.initializeOnStartup, Is.False);
        }

        [Test]
        public void WebcamStopScrubsFrameAndLandmarkBuffers()
        {
            var root = new GameObject("WebcamMemoryHygieneTest");
            try
            {
                var webcam = root.AddComponent<WebcamGazeProvider>();
                var frames = new Color32[] { new Color32(12, 34, 56, 255) };
                var points = (Vector2[])typeof(WebcamGazeProvider).GetField("landmarkBuffer", PrivateInstance).GetValue(webcam);
                points[0] = new Vector2(.12f, .34f);
                typeof(WebcamGazeProvider).GetField("pixels", PrivateInstance).SetValue(webcam, frames);
                webcam.StopCapture(); webcam.StopCapture();
                Assert.That(frames[0], Is.EqualTo(default(Color32)));
                Assert.That(points.All(p => p == Vector2.zero), Is.True);
                Assert.That(webcam.IsStarting || webcam.IsStreaming || webcam.IsTrackingValid, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator StalledDeviceIsReleasedEvenWithoutFaceTracking()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            Object.FindAnyObjectByType<GazeInputRouter>().SetMode(PcGazeMode.MouseGaze);
            var root = new GameObject("StalledWebcamTest");
            try
            {
                var webcam = root.AddComponent<WebcamGazeProvider>();
                // Never Play this texture: device loss is injected without touching a real camera.
                typeof(WebcamGazeProvider).GetField("webcam", PrivateInstance).SetValue(webcam, new WebCamTexture());
                typeof(WebcamGazeProvider).GetField("captureRequested", PrivateInstance).SetValue(webcam, true);
                typeof(WebcamGazeProvider).GetField("lastFrameTime", PrivateInstance).SetValue(webcam, Time.realtimeSinceStartup - 6);
                typeof(WebcamGazeProvider).GetMethod("Update", PrivateInstance).Invoke(webcam, null);
                Assert.That(webcam.Failed, Is.True);
                Assert.That(webcam.Status, Does.Contain("frames unavailable"));
                Assert.That(webcam.PreviewTexture, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator InjectedPermissionStartupAndTrackingFailuresFallBackAndRetry()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            try
            {
                foreach (string failure in new[] { "Webcam permission denied", "Webcam startup timed out", "Webcam frames unavailable" })
                {
                    router.SetMode(PcGazeMode.WebcamGaze);
                    router.Webcam.Fail(failure);
                    // EditMode's Play-mode test ticks can resume before a player Update.
                    float fallbackDeadline = Time.realtimeSinceStartup + 2f;
                    while (router.ActiveMode != PcGazeMode.MouseGaze && Time.realtimeSinceStartup < fallbackDeadline) yield return null;
                    Assert.That(router.ActiveMode, Is.EqualTo(PcGazeMode.MouseGaze), failure);
                    Assert.That(router.Webcam.IsStreaming || router.Webcam.IsStarting, Is.False);
                    Object.FindAnyObjectByType<PcTestControls>().ResetExperience();
                    Assert.That(router.Webcam.IsStarting, Is.True, "Boot must allow a fresh startup attempt");
                    router.SetMode(PcGazeMode.MouseGaze);
                }
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator ResetCancelsPendingTransitionsAndPublicHidesDebugAfterModeSwitch()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var controls = Object.FindAnyObjectByType<PcTestControls>();
            foreach (GameState stage in new[] { GameState.EyeCalibration, GameState.Tutorial, GameState.Practice, GameState.Launch, GameState.BossMeteor, GameState.MissionComplete, GameState.Result })
            {
                GameFlowManager.Instance.ChangeState(stage); yield return null;
                controls.ResetExperience(); controls.ResetExperience();
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(GameFlowManager.Instance.CurrentState, Is.EqualTo(GameState.Boot), stage.ToString());
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
                Assert.That(PcStabilityProbe.InspectSubscriptions().coroutineHandles, Is.Zero);
                Assert.That(Object.FindAnyObjectByType<PremiumCombatVfx>().ActiveParticleCount, Is.Zero);
            }
            var config = Object.FindAnyObjectByType<ExperienceConfiguration>();
            config.Settings.experienceMode = ExperienceMode.Development; config.Apply();
            router.Webcam.ShowWebcamPreview = router.Webcam.ShowFaceDebug = true;
            yield return null;
            config.Settings.experienceMode = ExperienceMode.PublicExperience; config.Apply(); yield return null;
            Assert.That(router.Webcam.ShowWebcamPreview || router.Webcam.ShowFaceDebug || controls.DebugKeysEnabled, Is.False);
            var ui = Object.FindAnyObjectByType<PcTestOverlay>().GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { "InputPanel", "InputMenu", "Reset", "Exit", "Keys", "DebugStatus", "WebcamPreview", "StartPC" })
                Assert.That(ui.Single(t => t.name == name).gameObject.activeInHierarchy, Is.False, name);
        }
    }
}
