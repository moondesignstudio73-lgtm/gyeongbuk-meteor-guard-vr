using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class HangarLobbyTests
    {
        private sealed class SyntheticRay : IGazeProvider
        {
            public bool Valid; public Ray Ray;
            public bool IsTrackingValid=>Valid;
            public Ray GetGazeRay()=>Ray;
            public Vector3 GetGazeOrigin()=>Ray.origin;
            public Vector3 GetGazeDirection()=>Ray.direction;
        }
        [UnityTearDown] public IEnumerator Cleanup(){ if(Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest,Timeout(150000)]
        public IEnumerator ReturnMenuReplayCalibrationAndOperatorResetRemainSafe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return Verify(); yield return new ExitPlayMode();
        }
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>(); float deadline=Time.realtimeSinceStartup+25;
            while(!warm.IsComplete && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(warm.IsComplete,Is.True);
            var flow=GameFlowManager.Instance; var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>(); var difficulty=Object.FindAnyObjectByType<DifficultyManager>();
            var stats=Object.FindAnyObjectByType<GameSessionStats>(); var result=Object.FindAnyObjectByType<ResultController>();
            var camera=Camera.main; var position=camera.transform.position; var rotation=camera.transform.rotation; float fov=camera.fieldOfView;
            var gaze=Object.FindAnyObjectByType<GazeRaycaster>(); var ray=new SyntheticRay(); gaze.SetProvider(ray);
            using var render=new PcStabilityProbe(camera);
            int sources=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            int canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
            stats.SetScore(2300); flow.ShowResult(); Assert.That(result.IsVisible,Is.True); reset.Tick(8.1f);
            Assert.That(flow.CurrentState,Is.EqualTo(GameState.Result));
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Returning));
            Assert.That(result.IsVisible,Is.False); Assert.That(reset.ResultTimerRunning,Is.False);
            yield return new WaitForSecondsRealtime(1f); Save(render,router,"returning");
            yield return Menu(lobby); Save(render,router,"menu");
            Assert.That(lobby.LastResult.Score,Is.EqualTo(2300)); Assert.That(lobby.View.RoomVisible,Is.True);
            Assert.That(lobby.ReturnDuration,Is.InRange(2,4));
            Assert.That(lobby.View.Buttons.Take(4).All(b=>b.gameObject.activeInHierarchy),Is.True);
            // Provider-independent physics ray/dwell. No physical webcam or HMD is accessed.
            var choice=lobby.View.Buttons[1]; Physics.SyncTransforms();
            ray.Ray=new Ray(camera.transform.position,choice.transform.position-camera.transform.position); ray.Valid=true;
            gaze.Tick(.32f); Assert.That(gaze.CurrentTarget,Is.SameAs(choice));
            Assert.That(choice.Progress,Is.GreaterThan(0)); Save(render,router,"menu-dwell");
            gaze.Tick(.5f); ray.Valid=false; gaze.Tick(0); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Difficulty));
            var selection=Object.FindAnyObjectByType<DifficultySelectionView>();
            Assert.That(selection.Choices.All(c=>c.gameObject.activeInHierarchy),Is.True);
            selection.Choices[3].Choose(); Assert.That(difficulty.Selected,Is.EqualTo(DifficultyLevel.Hard));
            Assert.That(lobby.LastResult.Difficulty,Is.EqualTo(DifficultyLevel.Normal)); Save(render,router,"difficulty");
            lobby.Queue(HangarLobbyController.MenuAction.Back); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(lobby.IsMenuReady,Is.True);
            bool tutorialVisited=false; System.Action<GameState> observed=s=>{if(s==GameState.Tutorial)tutorialVisited=true;};
            flow.OnStateChanged+=observed;
            try
            {
                lobby.Queue(HangarLobbyController.MenuAction.Recalibrate); yield return new WaitForSecondsRealtime(.3f);
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.EyeCalibration)); Save(render,router,"recalibration");
                yield return Menu(lobby,8); Assert.That(tutorialVisited,Is.False); Assert.That(lobby.LastResult.Score,Is.EqualTo(2300));
            }
            finally{flow.OnStateChanged-=observed;}
            Assert.That(router.Webcam.Calibration.Fit(WebcamCalibrationMap.Targets),Is.True,"Synthetic numeric samples only");
            lobby.Queue(HangarLobbyController.MenuAction.Replay); yield return new WaitForSecondsRealtime(.3f);
            Save(render,router,"replay-preparing");
            Assert.That(router.Webcam.Calibration.IsCalibrated,Is.True,"Same-player replay preserves calibration");
            deadline=Time.realtimeSinceStartup+7;
            while(flow.CurrentState!=GameState.Playing && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing)); Assert.That(difficulty.Session.Level,Is.EqualTo(DifficultyLevel.Hard));
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Hidden)); Save(render,router,"replay-playing");
            reset.ResetExperience();
            Assert.That(router.Webcam.Calibration.IsCalibrated,Is.False,"Operator reset clears calibration");
            for(int i=0;i<30;i++){flow.ShowResult();lobby.BeginReturn();reset.ResetExperience();}
            yield return null;
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Hidden));
            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length,Is.EqualTo(sources));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length,Is.EqualTo(canvases));
            Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
            flow.ShowResult();lobby.BeginReturn();yield return Menu(lobby);
            lobby.Queue(HangarLobbyController.MenuAction.Standby);yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(flow.CurrentState,Is.EqualTo(GameState.Boot));
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Standby));
            Assert.That(difficulty.Selected,Is.EqualTo(DifficultyLevel.Normal));
            Assert.That(Object.FindAnyObjectByType<ReadyScreenController>().IsReadyVisible,Is.False); Save(render,router,"standby");
            lobby.Queue(HangarLobbyController.MenuAction.StartNewPlayer);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(flow.CurrentState,Is.EqualTo(GameState.Intro));
            Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(router.Webcam.IsStreaming,Is.False);
            gaze.SetProvider(router);
        }
        private static IEnumerator Menu(HangarLobbyController lobby,float timeout=5)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!lobby.IsMenuReady && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(lobby.IsMenuReady,Is.True,"Docking/calibration must complete before menu activation");
            yield return new WaitForSecondsRealtime(.1f);
        }
        private static void Save(PcStabilityProbe render,GazeInputRouter router,string stage)
        {
            Canvas.ForceUpdateCanvases();Camera.main.Render();
            string path="Docs/HangarLobbyQA/"+stage;Directory.CreateDirectory(path);render.SaveGameOnlyProof(path,router);
        }
        [Test]
        public void CalibrationDestinationDefaultsToTutorialAndResetClearsLobbyDestination()
        {
            var root=new GameObject("CalibrationDestinationTest");var flow=root.AddComponent<GameFlowManager>();
            try
            {
                flow.StartCalibration(GameState.Result);flow.CompleteCalibration();Assert.That(flow.CurrentState,Is.EqualTo(GameState.Result));
                flow.StartCalibration();flow.CompleteCalibration();Assert.That(flow.CurrentState,Is.EqualTo(GameState.Tutorial));
                flow.StartCalibration(GameState.Result);flow.ResetGame();flow.StartCalibration();flow.CompleteCalibration();
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.Tutorial));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
