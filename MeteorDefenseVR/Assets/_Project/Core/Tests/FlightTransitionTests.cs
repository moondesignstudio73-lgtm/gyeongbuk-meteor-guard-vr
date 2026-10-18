using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Progression;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class FlightTransitionTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator StageArrivalTracksCampaignAndCancelsOnResetWithoutBlockingInput()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            float deadline = Time.realtimeSinceStartup + 25;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsBootPresenting, Is.False);
            var camera = Camera.main; Vector3 position = camera.transform.localPosition; Quaternion rotation = camera.transform.localRotation;
            using var proof = new PcStabilityProbe(camera);
            var flow = GameFlowManager.Instance;
            var difficulty = Object.FindAnyObjectByType<DifficultyManager>(); difficulty.Select(DifficultyLevel.Normal);
            flow.StartMainGame();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(director.IsStagePresenting, Is.True); Assert.That(director.PresentedStage, Is.EqualTo(1));
            var title = camera.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == "StageTitle");
            Assert.That(title.text, Is.EqualTo("스테이지 1")); title.ForceMeshUpdate(); Assert.That(title.isTextTruncated, Is.False);
            var card = title.transform.parent.GetComponent<RectTransform>();
            var group = card.GetComponent<CanvasGroup>(); Assert.That(group.blocksRaycasts, Is.False);
            foreach (var graphic in card.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            Save(proof, router, "stage-1");
            Time.timeScale = 0; Vector2 pausedPosition = card.anchoredPosition; float pausedAlpha = group.alpha;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(card.anchoredPosition, Is.EqualTo(pausedPosition)); Assert.That(group.alpha, Is.EqualTo(pausedAlpha));
            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(1.7f); Assert.That(director.IsStagePresenting, Is.False);
            // Complete a boss through its real campaign callback and allow the normal inter-stage timer.
            var boss = Object.FindAnyObjectByType<BossClimaxController>(); boss.BeginClimax(); boss.ResetClimax();
            Assert.That(boss.CampaignCompletionHandler(1), Is.True);
            deadline = Time.realtimeSinceStartup + 8;
            while (director.PresentedStage != 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(director.PresentedStage, Is.EqualTo(2)); Assert.That(director.StagePresentationCount, Is.EqualTo(2));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(title.text, Is.EqualTo("스테이지 2")); Save(proof, router, "stage-2");
            flow.ResetGame();
            Assert.That(director.IsStagePresenting, Is.False); Assert.That(director.StagePresentationCount, Is.Zero);
            yield return new WaitForSecondsRealtime(.12f); Save(proof, router, "preparation");
            yield return new WaitForSecondsRealtime(.6f);
            difficulty.Select(DifficultyLevel.Experience); flow.StartMainGame();
            Assert.That(title.text, Is.EqualTo("출격!"), "Experience mode must not invent a campaign stage");
            flow.ShowResult(); Assert.That(director.IsStagePresenting, Is.False);
            Assert.That(camera.transform.localPosition, Is.EqualTo(position)); Assert.That(camera.transform.localRotation, Is.EqualTo(rotation));
        }
        private static void Save(PcStabilityProbe proof, GazeInputRouter router, string label)
        {
            string directory = "Docs/FlightTransitionQA/" + label; Directory.CreateDirectory(directory); proof.SaveGameOnlyProof(directory, router);
        }
    }
}
