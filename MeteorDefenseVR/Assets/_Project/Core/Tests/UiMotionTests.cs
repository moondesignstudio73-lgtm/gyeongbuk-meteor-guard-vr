using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class UiMotionTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(120000)]
        public IEnumerator MotionPreservesInputStateResetAndCameraAndRendersStagedPanels()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return Verify();
            yield return new ExitPlayMode();
        }
        private static IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(.12f);
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            Assert.That(director, Is.Not.Null);
            var camera = Camera.main; var cameraPosition = camera.transform.localPosition; var cameraRotation = camera.transform.localRotation; float fov = camera.fieldOfView;
            var ready = Object.FindAnyObjectByType<ReadyStartGazeTarget>();
            Assert.That(ready, Is.Not.Null);
            var colliderScale = ready.transform.localScale;
            // EnterPlayMode reloads the test domain; it can return after the initial reveal.
            // Restart presentation through the real operator Reset, not a private animation API.
            Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
            router.InputSuspended = true;
            Assert.That(director.IsBootPresenting, Is.True);
            Assert.That(ready.enabled, Is.False, "Dwell must not confirm an invisible START");
            using var render = new PcStabilityProbe(camera);
            yield return new WaitForSecondsRealtime(.2f);
            Save(render, router, "reset-reveal");
            float preparedBy = Time.realtimeSinceStartup + 25;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < preparedBy) yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(director.IsBootPresenting, Is.False);
            Assert.That(ready.enabled, Is.True);
            Save(render, router, "ready");
            var caster = Object.FindAnyObjectByType<GazeRaycaster>();
            caster.SetProvider(new FixedGaze(new Ray(camera.transform.position, (ready.transform.position - camera.transform.position).normalized)));
            yield return new WaitForSecondsRealtime(.22f);
            Assert.That(director.RingSteps, Is.GreaterThan(0).And.LessThan(40));
            Save(render, router, "start-dwell");
            caster.SetProvider(router);
            var overlay = Object.FindAnyObjectByType<PcTestOverlay>();
            foreach (var button in overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                Assert.That(button.GetComponent<UIInteractionAnimator>(), Is.Not.Null, button.name);
            var feedback = ready.GetComponent<UIInteractionAnimator>();
            feedback.SetGaze(true, .5f);
            Assert.That(feedback.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Hover));
            Assert.That(feedback.GazeProgress, Is.EqualTo(.5f));
            Assert.That(ready.transform.localScale, Is.EqualTo(colliderScale), "Only the visual shell may scale");
            var buttonObject = new GameObject("QA_Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var actualButton = buttonObject.GetComponent<UnityEngine.UI.Button>();
            var interaction = buttonObject.AddComponent<UIInteractionAnimator>(); int clicks = 0;
            actualButton.onClick.AddListener(() => clicks++);
            var pointer = new PointerEventData(EventSystem.current);
            interaction.OnPointerEnter(pointer); Assert.That(interaction.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Hover));
            interaction.OnPointerDown(pointer); Assert.That(interaction.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Pressed));
            interaction.OnPointerUp(pointer); interaction.OnPointerClick(pointer);
            Assert.That(clicks, Is.Zero, "Feedback must not invoke gameplay callbacks a second time");
            interaction.SetAvailable(false); Assert.That(interaction.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Disabled));
            Object.Destroy(buttonObject);
            var flow = GameFlowManager.Instance;
            Object.FindAnyObjectByType<ReadyScreenController>().StartExperience();
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Intro), "Presentation must never delay GameFlow API");
            flow.StartMissionBriefing();
            yield return new WaitForSecondsRealtime(.23f); Save(render, router, "briefing-title");
            yield return new WaitForSecondsRealtime(.4f); Save(render, router, "briefing-body");
            flow.StartCalibration();
            yield return new WaitForSecondsRealtime(.6f); Save(render, router, "calibration");
            flow.StartMainGame();
            yield return new WaitForSecondsRealtime(.5f); Save(render, router, "playing");
            Object.FindAnyObjectByType<MeteorDefenseVR.Meteor.BossClimaxController>().BeginClimax();
            yield return new WaitForSecondsRealtime(.18f); Save(render, router, "boss-warning");
            // A visual fixture jumps states; cancel its pending boss spawn before showing victory.
            // The separate natural-flow tests defeat the real boss instead of jumping states.
            Object.FindAnyObjectByType<MeteorDefenseVR.Meteor.BossClimaxController>().ResetClimax();
            flow.CompleteMission();
            yield return new WaitForSecondsRealtime(1.12f); Save(render, router, "victory-title");
            yield return new WaitForSecondsRealtime(.6f); Save(render, router, "victory-body");
            var stats = Object.FindAnyObjectByType<GameSessionStats>();
            stats.ResetSession(21); stats.SetScore(3450);
            for (int i = 0; i < 21; i++) stats.RecordMeteorDestroyed();
            for (int i = 0; i < 24; i++) { stats.RecordShotAndLock(); if (i < 22) stats.RecordSuccessfulHit(); }
            flow.ShowResult();
            yield return new WaitForSecondsRealtime(.18f);
            var resultMotion = Object.FindObjectsByType<WorldTextMotion>().Single(t => t.name == "ResultText");
            Assert.That(resultMotion.VisibleLines, Is.EqualTo(1));
            Save(render, router, "result-title");
            yield return new WaitForSecondsRealtime(.95f); Save(render, router, "result-full");
            Assert.That(resultMotion.VisibleLines, Is.EqualTo(int.MaxValue));
            Assert.That(camera.transform.Find("MissionCompleteText").GetComponentInChildren<TextMeshPro>().enabled, Is.False, "Victory must not overlap Result, including direct state jumps");
            Assert.That(resultMotion.GetComponent<WorldTextPresentation>().Display.font.HasCharacters("미션 성공! 지구를 지켜냈습니다! 입력 안내 완료"), Is.True);
            int sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            int canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
            int motionCount = Object.FindObjectsByType<WorldTextMotion>(FindObjectsInactive.Include).Length;
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            for (int i = 0; i < 30; i++)
            {
                flow.StartGame(); reset.ResetExperience();
                Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
            }
            yield return new WaitForSecondsRealtime(.15f); Save(render, router, "reset");
            yield return new WaitForSecondsRealtime(1f); Save(render, router, "reset-ready");
            Assert.That(director.IsBootPresenting, Is.False);
            Assert.That(ready.enabled, Is.True);
            Assert.That(Object.FindObjectsByType<SfPresentationDirector>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length, Is.EqualTo(canvases));
            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length, Is.EqualTo(sources));
            Assert.That(Object.FindObjectsByType<WorldTextMotion>(FindObjectsInactive.Include).Length, Is.EqualTo(motionCount));
            int previousTransitions = director.TransitionCount;
            director.enabled = false; director.enabled = true;
            flow.StartGame(); Assert.That(director.TransitionCount - previousTransitions, Is.EqualTo(1));
            Assert.That(camera.transform.localPosition, Is.EqualTo(cameraPosition));
            Assert.That(camera.transform.localRotation, Is.EqualTo(cameraRotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Assert.That(router.Webcam.IsStreaming, Is.False);
            // UnityTest already fails on unexpected Error/Exception; normal GameFlow logs are expected.
        }
        private static void Save(PcStabilityProbe render, GazeInputRouter router, string label)
        {
            string directory = "Docs/UiMotionQA/" + label;
            Directory.CreateDirectory(directory); render.SaveGameOnlyProof(directory, router);
        }
        private sealed class FixedGaze : IGazeProvider
        {
            private readonly Ray ray;
            public FixedGaze(Ray value) { ray = value; }
            public bool IsTrackingValid => true;
            public Ray GetGazeRay() => ray;
            public Vector3 GetGazeOrigin() => ray.origin;
            public Vector3 GetGazeDirection() => ray.direction;
        }
    }
}
