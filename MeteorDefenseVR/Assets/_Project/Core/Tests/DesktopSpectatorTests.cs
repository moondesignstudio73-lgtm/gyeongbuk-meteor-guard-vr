using System;
using System.Collections;
using System.IO;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class DesktopSpectatorTests
    {
        private sealed class FakeMirror : IDesktopMirrorSource
        { public bool Running; public int Polls, Disposals; public bool IsRunning => Running; public void Refresh() => Polls++; public void Dispose() => Disposals++; }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test] public void DefaultsAreMirror720p30AndInvalidValuesAreBounded()
        {
            var settings = new DesktopSpectatorSettings();
            Assert.That(settings.viewMode, Is.EqualTo(DesktopViewMode.Mirror)); Assert.That(settings.width, Is.EqualTo(1280));
            Assert.That(settings.height, Is.EqualTo(720)); Assert.That(settings.framesPerSecond, Is.EqualTo(30));
            settings.viewMode = (DesktopViewMode)500; settings.width = -1; settings.height = int.MaxValue;
            settings.framesPerSecond = int.MaxValue; settings.fieldOfView = float.NaN; settings.localPosition = new Vector3(float.NaN, 0, 0);
            settings.Validate(); Assert.That(settings.viewMode, Is.EqualTo(DesktopViewMode.Mirror));
            Assert.That(settings.width, Is.EqualTo(640)); Assert.That(settings.height, Is.EqualTo(1080));
            Assert.That(settings.framesPerSecond, Is.EqualTo(60)); Assert.That(settings.fieldOfView, Is.EqualTo(65));
            Assert.That(float.IsNaN(settings.localPosition.x), Is.False);
        }
        [TestCase(RuntimePlatform.WindowsPlayer, true)] [TestCase(RuntimePlatform.WindowsEditor, true)]
        [TestCase(RuntimePlatform.Android, false)] [TestCase(RuntimePlatform.WebGLPlayer, false)]
        public void DesktopOutputDoesNotRunOrCastOnStandaloneQuest(RuntimePlatform platform, bool expected)
            => Assert.That(DesktopSpectatorController.SupportsPlatform(platform), Is.EqualTo(expected));

        [Test] public void NativeMirrorAdapterWithoutLoaderIsSafeAndReentrant()
        {
            using var mirror = new DesktopMirrorSource();
            Assert.DoesNotThrow(mirror.Refresh); Assert.DoesNotThrow(mirror.Refresh);
            Assert.DoesNotThrow(mirror.Dispose); Assert.DoesNotThrow(mirror.Dispose);
        }

        [UnityTest, Timeout(120000)] public IEnumerator MonitorSwitchesReuseResourcesPreservePlayerAndShowSharedHud()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return VerifyMonitor(); yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyMonitor()
        {
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>(); float deadline = Time.realtimeSinceStartup + 25;
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(warm.IsComplete, Is.True);
            var controller = Object.FindAnyObjectByType<DesktopSpectatorController>(); Assert.That(controller, Is.Not.Null);
            var experience = Object.FindAnyObjectByType<ExperienceConfiguration>();
            experience.Settings.experienceMode = ExperienceMode.PublicExperience;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.ObserverCamera, Is.Null); Assert.That(controller.OutputTexture, Is.Null);
            Assert.That(controller.RenderedFrames, Is.Zero); Assert.That(controller.View.OperatorVisible, Is.False);
            Assert.That(controller.SelectFromOperator(DesktopViewMode.Spectator), Is.False);
            var camera = Camera.main; var originalTarget = camera.targetTexture; var originalEye = camera.stereoTargetEye;
            var originalPose = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            int rate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            int listeners = Object.FindObjectsByType<AudioListener>().Length;
            var mirror = new FakeMirror { Running = true }; controller.SetMirrorSource(mirror);
            experience.Settings.experienceMode = ExperienceMode.Operator;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.View.OperatorVisible, Is.True, "Operator output must remain available independently of PC input UI hiding in VR");
            Assert.That(controller.View.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            controller.View.SwitchButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(controller.Mode, Is.EqualTo(DesktopViewMode.Spectator)); Assert.That(controller.RenderedFrames, Is.GreaterThan(0));
            Assert.That(controller.OutputTexture.IsCreated(), Is.True); Assert.That(controller.ObserverCamera.enabled, Is.False);
            Assert.That(controller.ObserverRendersXr, Is.False);
            Assert.That(controller.ObserverCamera.GetComponent<AudioListener>(), Is.Null);
            Assert.That(controller.ObserverCamera.CompareTag("MainCamera"), Is.False);
            int turretLayer=LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);
            Assert.That(camera.cullingMask&(1<<turretLayer),Is.Zero,"Player/HMD view must cull exterior turret meshes");
            Assert.That(controller.ObserverCamera.cullingMask&(1<<turretLayer),Is.Not.Zero,"Third-person spectator keeps exterior turrets visible");
            var planes=GeometryUtility.CalculateFrustumPlanes(controller.ObserverCamera);bool turretVisible=false;
            foreach(var renderer in Object.FindAnyObjectByType<TurretAimingSystem>().GetComponentsInChildren<Renderer>(true))
                if(GeometryUtility.TestPlanesAABB(planes,renderer.bounds)){turretVisible=true;break;}
            Assert.That(turretVisible,Is.True,"At least one moving exterior turret must be framed by the third-person spectator camera");
            var observer = controller.ObserverCamera; var texture = controller.OutputTexture;
            var flow = GameFlowManager.Instance; flow.StartMainGame(); yield return new WaitForSecondsRealtime(.4f);
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            Assert.That(controller.ObserverCamera.cullingMask & (1 << hud.gameObject.layer), Is.Zero, "No duplicate player HUD in observer scene");
            Assert.That(camera.cullingMask & (1 << hud.gameObject.layer), Is.Not.Zero, "Player HUD remains visible to HMD");
            hud.SetHealth(72, 100); hud.AddScore(3450); hud.ShowLockOn();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(controller.View.ScoreText, Is.EqualTo(hud.Score.ToString("N0")));
            Assert.That(controller.View.HealthText, Is.EqualTo("72%")); Assert.That(controller.View.LockText, Is.EqualTo("LOCK ON"));
            Assert.That(controller.View.RemainingText, Is.EqualTo(hud.RemainingMeteors + " / " + hud.TotalMeteors));
            var weapon = Object.FindAnyObjectByType<LaserWeapon>(); weapon.AutoFireOnLock = false;
            GazeLockOnTarget liveTarget = null; deadline = Time.realtimeSinceStartup + 5;
            while (liveTarget == null && Time.realtimeSinceStartup < deadline)
            {
                foreach (var candidate in Object.FindObjectsByType<GazeLockOnTarget>())
                    if (candidate.IsAvailable && candidate.Meteor != null && candidate.Meteor.IsTargetable) { liveTarget = candidate; break; }
                yield return null;
            }
            Assert.That(liveTarget, Is.Not.Null); liveTarget.OnGazeEnter(default); liveTarget.OnGazeStay(default, 2);
            Assert.That(weapon.TryFire(liveTarget), Is.True);
            hud.AddScore(1); // Deliberately overwrite LOCK ON with score feedback in the SAME frame.
            Assert.That(hud.FeedbackMessage, Is.Not.EqualTo("LOCK ON"));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(controller.View.LockText, Is.EqualTo("LOCK ON"), "Monitor must retain the fired event despite score feedback");
            weapon.AutoFireOnLock = true;
            SaveMonitor(controller, router, "spectator");
            for (int i = 0; i < 30; i++)
            {
                controller.SetViewMode(DesktopViewMode.Mirror); int before = controller.RenderedFrames;
                yield return null; Assert.That(controller.RenderedFrames, Is.EqualTo(before));
                controller.SetViewMode(DesktopViewMode.Spectator); yield return null;
                Assert.That(controller.ObserverCamera, Is.SameAs(observer)); Assert.That(controller.OutputTexture, Is.SameAs(texture));
            }
            controller.Settings.framesPerSecond = 10; int start = controller.RenderedFrames; double started = Time.realtimeSinceStartupAsDouble;
            yield return new WaitForSecondsRealtime(1.1f); double elapsed = Time.realtimeSinceStartupAsDouble - started;
            int frames = controller.RenderedFrames - start;
            Assert.That(frames, Is.LessThanOrEqualTo(Math.Ceiling(elapsed * 10) + 1));
            Assert.That(Application.targetFrameRate, Is.EqualTo(rate)); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync));
            Assert.That(camera.targetTexture, Is.SameAs(originalTarget)); Assert.That(camera.stereoTargetEye, Is.EqualTo(originalEye));
            Assert.That(camera.transform.position, Is.EqualTo(originalPose)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(fov)); Assert.That(Object.FindObjectsByType<AudioListener>().Length, Is.EqualTo(listeners));
            controller.Settings.width = 960; controller.Settings.height = 540; yield return new WaitForSecondsRealtime(.25f);
            Assert.That(controller.OutputTexture.width, Is.EqualTo(960)); Assert.That(controller.OutputTexture.height, Is.EqualTo(540));
            Assert.That(texture == null, Is.True, "Replaced target released/destroyed");
            mirror.Running = false; yield return new WaitForSecondsRealtime(.15f); Assert.That(controller.XrRunning, Is.False);
            Object.FindAnyObjectByType<OperationsResetController>().ResetExperience(); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.Mode, Is.EqualTo(DesktopViewMode.Spectator), "Operator output selection survives player reset");
            controller.SetViewMode(DesktopViewMode.Mirror); yield return new WaitForSecondsRealtime(.2f);
            SaveMonitor(controller, router, "mirror");
            var oldTexture = controller.OutputTexture; controller.enabled = false;
            Assert.That(oldTexture.IsCreated(), Is.False, "GPU allocation released immediately");
            yield return new WaitForSecondsRealtime(.15f); // Destroy is deferred until the end of the player frame.
            Assert.That(controller.OutputTexture, Is.Null); Assert.That(oldTexture == null, Is.True, "Old RT destroyed"); Assert.That(observer == null, Is.True, "Observer destroyed");
            Assert.That(mirror.Disposals, Is.EqualTo(1));
            controller.enabled = true; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.View, Is.Not.Null); Assert.That(controller.ObserverCamera, Is.Null);
            int canvases = 0; foreach (var canvas in Object.FindObjectsByType<Canvas>()) if (canvas.name == "DesktopSpectatorCanvas") canvases++;
            Assert.That(canvases, Is.EqualTo(1)); Assert.That(router.Webcam.IsStreaming, Is.False);
            Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
            Directory.CreateDirectory("Docs/SpectatorQA");
            File.WriteAllText("Docs/SpectatorQA/render-check.txt", $"Spectator rendered frames: {controller.RenderedFrames}\n10fps cap: {frames} frames over {elapsed:F3}s\n30 Mirror/Spectator switch cycles: same camera and texture\nHMD camera properties, targetFrameRate, vSync, AudioListener unchanged\nFake XR adapter lifecycle tested; no physical HMD connected.\n");
        }

        [UnityTest, Timeout(180000)] public IEnumerator SpectatorViewPlaysNaturalLoopThroughHangarAndReset()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return null;
            Object.FindAnyObjectByType<DesktopSpectatorController>().SetViewMode(DesktopViewMode.Spectator);
            yield return PcInputTests.PlayNaturalLoop();
            var controller = Object.FindAnyObjectByType<DesktopSpectatorController>();
            Assert.That(controller.Mode, Is.EqualTo(DesktopViewMode.Spectator));
            Assert.That(controller.RenderedFrames, Is.GreaterThan(60));
            Assert.That(Object.FindObjectsByType<DesktopSpectatorController>().Length, Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        private static void SaveMonitor(DesktopSpectatorController controller, GazeInputRouter router, string stage)
        {
            // QA-only game render capture, never desktop/webcam/HMD capture or runtime recording.
            Assert.That(router.Webcam.IsStreaming, Is.False); Assert.That(router.Webcam.ShowWebcamPreview, Is.False);
            var canvas = controller.View.Canvas; var previousCamera = canvas.worldCamera; var previousMode = canvas.renderMode; float plane = canvas.planeDistance;
            using var render = new PcStabilityProbe(Camera.main);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = .4f;
                Canvas.ForceUpdateCanvases(); Camera.main.Render(); string folder = "Docs/SpectatorQA/" + stage;
                Directory.CreateDirectory(folder); render.SaveGameOnlyProof(folder, router);
            }
            finally { canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane; }
        }
    }
}
