using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class WindowsSimulationTests
    {
        private static DifficultySettings Settings => AssetDatabase.LoadAssetAtPath<DifficultySettings>("Assets/_Project/Difficulty/Resources/DifficultySettings.asset");
        [TestCase(false, PcGazeMode.WindowsVRSimulation)] [TestCase(true, PcGazeMode.VREyeTracking)]
        public void ExplicitSimulationDoesNotReplaceRealEyeTracking(bool vr, PcGazeMode expected)
            => Assert.That(GazeModePolicy.Select(PcGazeMode.WindowsVRSimulation, vr, true, true), Is.EqualTo(expected));

        [TestCase(1,0,0)] [TestCase(-1,0,0)] [TestCase(0,1,0)] [TestCase(0,-1,0)] [TestCase(0,0,1)] [TestCase(0,0,-1)]
        public void SphereSweepsEveryDirectionWithoutTunneling(float x,float y,float z)
        {
            var root=new GameObject("SphereBoundaryQA");
            try
            {
                var boundary=root.AddComponent<ShipCollisionBoundary>(); boundary.ConfigureSphere(4.2f); var axis=new Vector3(x,y,z);
                Assert.That(boundary.Sweep(axis*30,axis*20,2,out _,out _),Is.False);
                Assert.That(boundary.Sweep(axis*30,-axis*30,2,out var safe,out var impact),Is.True);
                Assert.That(safe.magnitude,Is.EqualTo(6.32f).Within(.001)); Assert.That(Vector3.Dot(safe,axis),Is.GreaterThan(0));
                Assert.That(impact.magnitude,Is.EqualTo(4.2f).Within(.001));
            }
            finally {Object.DestroyImmediate(root);}
        }
        [TestCase(-1,0,0,"◀ LEFT")] [TestCase(1,0,0,"RIGHT ▶")] [TestCase(0,1,0,"▲ ABOVE")] [TestCase(0,-1,0,"▼ BELOW")]
        [TestCase(-1,0,-3,"◀ REAR")] [TestCase(1,0,-3,"REAR ▶")]
        public void ProjectionLabelsAndEdgesMatchLocalDirection(float x,float y,float z,string expected)
        {
            var v=new Vector3(x,y,z); Assert.That(ThreatProjection.Direction(v),Is.EqualTo(expected));
            Vector2 edge=ThreatProjection.Edge(v,16f/9); Assert.That(Mathf.Max(Mathf.Abs(edge.x),Mathf.Abs(edge.y)),Is.EqualTo(1).Within(.001));
        }
        [Test] public void ExperienceAndHardDirectionsAreDataDrivenAndLegacyValuesStayStable()
        {
            Assert.That((int)DifficultyLevel.Hard,Is.EqualTo(2)); Assert.That((int)PcGazeMode.CameraCenterGaze,Is.EqualTo(4));
            var config=Settings.Experience; Assert.That(config,Is.Not.Null); var experience=config.Capture(DifficultyLevel.Experience);
            Assert.That(experience.SpawnDirections,Is.EqualTo(SpawnDirectionMode.ExperienceArc)); Assert.That(experience.ScaleScore(100),Is.EqualTo(80));
            var balance=Settings.hard.Capture(DifficultyLevel.Hard); Assert.That(balance.AllDirections,Is.True);
            Assert.That(balance.SpawnMin,Is.GreaterThanOrEqualTo(18)); Assert.That(balance.SpawnMax,Is.LessThan(100));
            Assert.That(balance.MaxConcurrent,Is.InRange(3,5)); Assert.That(balance.ReactionSeconds,Is.GreaterThanOrEqualTo(8));
            Assert.That(Settings.normal.spawnDirections,Is.EqualTo(SpawnDirectionMode.FrontAndSides));
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1; AudioListener.pause=false; if(Application.isPlaying) yield return new ExitPlayMode(); }
        [Test] public void HeadDrivenHudKeepsFacingViewerOutsideSimulation()
        {
            var root=new GameObject("HeadPoseFixture");
            try
            {
                var camera=root.AddComponent<Camera>(); var hud=new GameObject("MissionHUD"); hud.transform.SetParent(root.transform,false);
                var ready=new GameObject("ReadyScreen"); ready.transform.SetParent(root.transform,false); ready.transform.localPosition=Vector3.forward*3;
                var safe=root.AddComponent<MeteorDefenseVR.Visual.WorldUiSafeArea>(); safe.Configure(camera,new[] {hud.transform,ready.transform}); safe.Apply();
                camera.transform.rotation=Quaternion.Euler(20,180,0); safe.Apply();
                Assert.That(Vector3.Dot(hud.transform.forward,camera.transform.forward),Is.GreaterThan(.999));
                safe.ShipLocked=true; safe.Apply(); Assert.That(ready.transform.position.z,Is.EqualTo(3).Within(.001));
                Assert.That(Vector3.Dot(ready.transform.forward,Vector3.forward),Is.GreaterThan(.999));
                safe.ShipLocked=false; safe.Apply(); Assert.That(Vector3.Dot(ready.transform.forward,camera.transform.forward),Is.GreaterThan(.999));
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void ExperienceLocalRecordRoundTripsAlongsideCampaignDifficulties()
        {
            string directory=Path.Combine("Docs/WindowsSimulationQA","ranking-"+System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"qa-records.json"); var store=new LocalRankingStore(path);
            foreach(var level in new[] {DifficultyLevel.Easy,DifficultyLevel.Normal,DifficultyLevel.Hard,DifficultyLevel.Experience})
            {
                var record=new LocalMissionRecord {Id=System.Guid.NewGuid().ToString("N"),Score=1000+(int)level,Difficulty=level,Destroyed=20,Accuracy=.9f,RemainingHP=80,Rank="S",Combo=10,PlayTime=50,Timestamp=System.DateTimeOffset.UtcNow.ToString("O",System.Globalization.CultureInfo.InvariantCulture)};
                Assert.That(store.Add(record),Is.True);
            }
            var reopened=new LocalRankingStore(path); reopened.Load(); Assert.That(reopened.Records.Count,Is.EqualTo(4));
            Assert.That(reopened.Best(DifficultyLevel.Experience).Score,Is.EqualTo(1003)); Assert.That(reopened.TopTen()[0].Difficulty,Is.EqualTo(DifficultyLevel.Experience));
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator SimulationLookPauseResetAndHard360AutoLock()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router=Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.WindowsVRSimulation);
            var simulation=router.Simulation; var camera=Camera.main; var original=camera.transform.localRotation;
            var manager=Object.FindAnyObjectByType<DifficultyManager>(); var flow=GameFlowManager.Instance;
            var operations=Object.FindAnyObjectByType<OperationsResetController>(); var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
            var progression=Object.FindAnyObjectByType<StageProgressionController>();
            var progressionField=typeof(StageProgressionController).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic);
            var originalProgression=(StageProgressionSettings)progressionField.GetValue(progression);
            var focusedProgression=Object.Instantiate(originalProgression); focusedProgression.stages=new[]{originalProgression.stages[0]}; progressionField.SetValue(progression,focusedProgression);
            using(var render=new PcStabilityProbe(camera))
            {
                yield return null;
                Assert.That(simulation.WantsLockedCursor,Is.False,"Ready leaves the menu cursor free");
                flow.StartGame();
                float inputDeadline=Time.realtimeSinceStartup+5;
                while(!simulation.IsLooking && Time.realtimeSinceStartup<inputDeadline) yield return null;
                Assert.That(simulation.IsLooking,Is.True,"Simulation mode must activate after component Start/Update"); Assert.That(simulation.WantsLockedCursor,Is.True);
                simulation.ApplyLook(new Vector2(3600,0),1); Assert.That(Quaternion.Angle(original,camera.transform.localRotation),Is.LessThan(.1));
                simulation.ApplyLook(new Vector2(0,10000),1); Assert.That(simulation.Angles.y,Is.EqualTo(-80).Within(.1));
                simulation.ApplyLook(new Vector2(0,-20000),1); Assert.That(simulation.Angles.y,Is.EqualTo(80).Within(.1)); simulation.Recenter();
                float deadline=Time.realtimeSinceStartup+35;
                while(Object.FindAnyObjectByType<SfPresentationDirector>().IsBootPresenting && Time.realtimeSinceStartup<deadline) yield return null;
                foreach(float yaw in new[] {0f,90f,180f,270f})
                {
                    simulation.Recenter(); simulation.ApplyLook(new Vector2(yaw/.1f,0),1); yield return new WaitForSecondsRealtime(.15f);
                    Save(render,router,"canopy-"+yaw);
                }
                foreach(float pitch in new[] {-65f,65f})
                { simulation.Recenter(); simulation.ApplyLook(new Vector2(0,-pitch/.09f),1); yield return new WaitForSecondsRealtime(.15f); Save(render,router,"canopy-pitch-"+pitch); }
                simulation.Recenter();
                operations.ResetExperience();
                manager.Select(DifficultyLevel.Hard); Assert.That(manager.Selected,Is.EqualTo(DifficultyLevel.Hard));
                yield return new WaitForSecondsRealtime(.3f);
                Save(render,router,"extreme-selected");
                flow.StartGame(); simulation.Pause(); var pausedState=flow.CurrentState;
                Assert.That(router.IsTrackingValid,Is.False); Assert.That(simulation.WantsLockedCursor,Is.False);
                yield return new WaitForSecondsRealtime(5.5f); Assert.That(flow.CurrentState,Is.EqualTo(pausedState),"Intro must not advance behind paused menu");
                simulation.Resume(); Assert.That(Time.timeScale,Is.EqualTo(1));
                flow.StartLaunch(); simulation.Pause();
                var launch=Object.FindAnyObjectByType<MeteorDefenseVR.Launch.LaunchSequenceController>(); var launchStage=launch.CurrentStage;
                yield return new WaitForSecondsRealtime(2); Assert.That(launch.CurrentStage,Is.EqualTo(launchStage)); Assert.That(flow.CurrentState,Is.EqualTo(GameState.Launch)); simulation.Resume();
                float launchDeadline=Time.realtimeSinceStartup+15;
                while(flow.CurrentState!=GameState.Playing && Time.realtimeSinceStartup<launchDeadline) yield return null;
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing),"Resume must finish the actual Launch sequence");
                int successful=0; var sectors=new bool[6];
                for(int cycle=0;cycle<12;cycle++)
                {
                    spawner.Tick(100); if(spawner.ActiveCount==0) break;
                    var meteor=spawner.ActiveMeteors[0]; var target=meteor.GetComponent<GazeLockOnTarget>();
                    Vector3 delta=meteor.transform.position-spawner.ShipCenter; float distance=delta.magnitude;
                    Assert.That(distance,Is.InRange(18f,44f)); Assert.That(spawner.ActiveCount,Is.LessThanOrEqualTo(5));
                    Assert.That(ThreatProjection.TimeToImpact(spawner.ShipCenter,meteor,4.2f),Is.GreaterThan(6.5f));
                    int sector=Mathf.Abs(delta.y)>Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.z))?(delta.y>0?4:5):Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?(delta.x>0?3:1):(delta.z>0?0:2); sectors[sector]=true;
                    simulation.Recenter(); yield return new WaitForSecondsRealtime(.15f);
                    if(cycle==2)
                    {
                        yield return new WaitForSecondsRealtime(.3f); Save(render,router,"rear-threat"); Assert.That(Object.FindAnyObjectByType<ThreatIndicatorView>().VisibleCount,Is.GreaterThan(0));
                        foreach(var label in camera.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                            if(label.name.StartsWith("Threat")) Debug.Log("[ThreatQA] "+label.name+" text="+label.text+" viewport="+camera.WorldToViewportPoint(label.transform.position)+" scale="+label.transform.lossyScale+" vertices="+label.textInfo.characterCount+" alpha="+label.canvasRenderer.GetAlpha());
                    }
                    // Real shared raycaster -> lock -> automatic weapon; no fire button or direct destruction.
                    float lockDeadline=Time.realtimeSinceStartup+5;
                    bool destroyed=false; System.Action<MeteorController> onDestroyed=_=>destroyed=true; meteor.Destroyed+=onDestroyed;
                    while(!destroyed && meteor.IsTargetable && Time.realtimeSinceStartup<lockDeadline)
                    {
                        Vector3 aim=meteor.transform.position-camera.transform.position;
                        float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
                        float pitch=-Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;
                        simulation.ApplyLook(new Vector2((yaw-simulation.Angles.x)/.1f,-(pitch-simulation.Angles.y)/.09f),1);
                        yield return null;
                    }
                    meteor.Destroyed-=onDestroyed;
                    var caster=Object.FindAnyObjectByType<GazeRaycaster>();
                    Assert.That(destroyed,Is.True,"Center auto-fire; pooled objects can already be active again. Cycle="+cycle+" state="+flow.CurrentState+" health="+meteor.CurrentHealth+" target="+meteor.transform.position+" aim="+Vector3.Angle(camera.transform.forward,meteor.transform.position-camera.transform.position)+" hit="+(caster.HasHit?caster.CurrentHit.collider.name:"none")); successful++;
                    Assert.That(target.HasDifficultyTuning,Is.True);
                }
                Assert.That(successful,Is.EqualTo(12)); Assert.That(sectors.All(v=>v),Is.True,"All six directions must be represented");
                Save(render,router,"extreme-combat");
                var boss=Object.FindAnyObjectByType<BossClimaxController>();
                bool bossPauseVerified=false;
                float completeDeadline=Time.realtimeSinceStartup+70;
                while(flow.CurrentState!=GameState.Result && Time.realtimeSinceStartup<completeDeadline)
                {
                    if(flow.CurrentState==GameState.BossMeteor && !bossPauseVerified)
                    {
                        bossPauseVerified=true; simulation.Pause(); var stage=boss.CurrentStage; var stats=Object.FindAnyObjectByType<GameSessionStats>(); float playTime=stats.PlayTime;
                        yield return new WaitForSecondsRealtime(1.5f); Assert.That(boss.CurrentStage,Is.EqualTo(stage)); Assert.That(stats.PlayTime,Is.EqualTo(playTime).Within(.001f)); simulation.Resume();
                    }
                    var meteor=spawner.ActiveCount>0?spawner.ActiveMeteors[0]:boss.ActiveBoss;
                    if(meteor!=null && meteor.IsTargetable)
                    {
                        Vector3 aim=meteor.transform.position-camera.transform.position;
                        float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
                        float pitch=-Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;
                        simulation.ApplyLook(new Vector2((yaw-simulation.Angles.x)/.1f,-(pitch-simulation.Angles.y)/.09f),1);
                    }
                    yield return null;
                }
                Assert.That(boss.IsComplete,Is.True,"HARD boss must finish with shared auto-lock");
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.Result));
                var latest=Object.FindAnyObjectByType<LocalRankingController>().Store.Latest;
                Assert.That(latest,Is.Not.Null); Assert.That(latest.Difficulty,Is.EqualTo(DifficultyLevel.Hard));
                Assert.That(WorldTextPresentation.FormatResult(new GameSessionSnapshot {Difficulty=DifficultyLevel.Hard},"S"),Does.Contain("HARD"));
                yield return new WaitForSecondsRealtime(2.3f); Save(render,router,"extreme-result");
                var lobby=Object.FindAnyObjectByType<HangarLobbyController>(); float menuDeadline=Time.realtimeSinceStartup+15;
                while(!lobby.IsMenuReady && Time.realtimeSinceStartup<menuDeadline) yield return null;
                Assert.That(lobby.IsMenuReady,Is.True); yield return new WaitForSecondsRealtime(.25f);
                Assert.That(simulation.IsLooking,Is.False,"Hangar menus release desktop look");
                Assert.That(simulation.WantsLockedCursor,Is.False,"Hangar menu cursor must be free");
                Save(render,router,"simulation-hangar");
                operations.ResetExperience(); yield return null;
                Assert.That(Quaternion.Angle(original,camera.transform.localRotation),Is.LessThan(.1)); Assert.That(manager.Selected,Is.EqualTo(DifficultyLevel.Normal));
                Assert.That(Object.FindAnyObjectByType<ThreatIndicatorView>().VisibleCount,Is.Zero);
                router.SetMode(PcGazeMode.MouseGaze); yield return null; Assert.That(simulation.IsLooking,Is.False); Assert.That(Time.timeScale,Is.EqualTo(1));
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
            }
            progressionField.SetValue(progression,originalProgression); Object.Destroy(focusedProgression);
            yield return new ExitPlayMode();
        }
        private static void Save(PcStabilityProbe render,GazeInputRouter router,string name)
        {string directory=Path.Combine("Docs/WindowsSimulationQA",name);Directory.CreateDirectory(directory);render.SaveGameOnlyProof(directory,router);}
    }
}
