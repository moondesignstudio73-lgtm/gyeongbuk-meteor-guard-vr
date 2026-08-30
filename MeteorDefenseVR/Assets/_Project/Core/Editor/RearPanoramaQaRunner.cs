using System;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    [InitializeOnLoad]
    public static class RearPanoramaQaRunner
    {
        private const string JobKey = "MeteorDefense.RearPanoramaQa.Active";
        private const string StateKey = "MeteorDefense.RearPanoramaQa.State";
        private const string IndexKey = "MeteorDefense.RearPanoramaQa.Index";
        private const string DeadlineKey = "MeteorDefense.RearPanoramaQa.Deadline";
        private const string ScenePath = "Assets/_Project/Scenes/MeteorDefense.unity";
        private static readonly (float yaw,float pitch,string name)[] Views={
            (0,0,"Front"),(-90,0,"Left"),(-135,0,"RearLeft"),(180,0,"Rear"),
            (135,0,"RearRight"),(90,0,"Right"),(0,65,"Above"),(0,-65,"Below")};
        private static PcStabilityProbe probe;
        private static TestRunnerApi testApi;
        private static RegressionCallbacks testCallbacks;

        static RearPanoramaQaRunner()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            if(SessionState.GetBool(JobKey,false)&&EditorApplication.isPlaying)
                EditorApplication.delayCall+=BeginTicking;
        }

        public static void CaptureEightDirections()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            ValidateAuthoredStructure();
            SessionState.SetBool(JobKey,true);SessionState.SetString(StateKey,"enter");SessionState.SetInt(IndexKey,0);
            EditorApplication.EnterPlaymode();
        }

        public static void RunEditModeRegression()
        {
            testApi=ScriptableObject.CreateInstance<TestRunnerApi>();testCallbacks=new RegressionCallbacks();testApi.RegisterCallbacks(testCallbacks);
            testApi.Execute(new ExecutionSettings(new Filter{
                testMode=TestMode.EditMode,
                testNames=new[]{
                    "MeteorDefenseVR.Tests.RearPanoramaCockpitTests.RearShellUsesPanoramaGlassAndSlimStructuralFrames",
                    "MeteorDefenseVR.Tests.ClassicCockpitIntegrationTests.ClassicFiveMonitorPhysicalLayoutAndClosedRearShellArePreserved",
                    "MeteorDefenseVR.Tests.ClassicCockpitIntegrationTests.CenterRadarIsLiveAndSideStatusUsesSeparateHolograms",
                    "MeteorDefenseVR.Tests.CockpitImpactPolishTests.AuthoredEffectsAreFiniteSharedAndIncludeBottomDirectionalAnchor",
                    "MeteorDefenseVR.Tests.CockpitImpactPolishTests.DamageAudioUsesExistingImpactCueAndOneQuietPreloadedLoop",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.CandidateDirectionSelectsTopBottomAndReturnsToNeutral",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.RearTargetAimsWithoutCameraDependencyAndLaserStartsAtSelectedMuzzle",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.BossUsesBothMuzzlesButAppliesOneDamageEvent",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.AuthoredSceneHasTwoExternalTurretsOneCanopyRendererAndBottomDamageAnchor",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.ExteriorTurretsAreOffAxisShortAndHiddenOnlyFromPlayerView",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.RuntimeVisibilityGuardRepairsOverwrittenPlayerMaskAndRendererLayer",
                    "MeteorDefenseVR.Tests.TurretCanopyIntegrationTests.SixDirectionPlayerViewsKeepTurretGeometryCulledAndCombatUiVisible"
                }
            }));
        }

        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if(!SessionState.GetBool(JobKey,false))return;
            if(change==PlayModeStateChange.EnteredPlayMode)BeginTicking();
            if(change==PlayModeStateChange.EnteredEditMode)
            {
                bool failed=SessionState.GetString(StateKey,"")=="failed";
                SessionState.EraseBool(JobKey);SessionState.EraseString(StateKey);SessionState.EraseInt(IndexKey);SessionState.EraseFloat(DeadlineKey);
                EditorApplication.Exit(failed?1:0);
            }
        }

        private static void BeginTicking()
        {
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            if(SessionState.GetString(StateKey,"")=="enter")SessionState.SetString(StateKey,"boot");
        }

        private static void Tick()
        {
            if(!SessionState.GetBool(JobKey,false)||!EditorApplication.isPlaying)return;
            try
            {
                string state=SessionState.GetString(StateKey,"boot");
                if(state=="boot")
                {
                    var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.WindowsVRSimulation);
                    Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Extreme);
                    GameFlowManager.Instance.StartMainGame();
                    SessionState.SetFloat(DeadlineKey,(float)EditorApplication.timeSinceStartup+8);SessionState.SetString(StateKey,"launch");return;
                }
                if(state=="launch")
                {
                    if(GameFlowManager.Instance.CurrentState!=GameState.Playing)
                    {if(EditorApplication.timeSinceStartup>SessionState.GetFloat(DeadlineKey,0))throw new TimeoutException("Playing state timeout");return;}
                    Object.FindAnyObjectByType<MeteorSpawner>().BeginScenario(Views.Length);
                    probe?.Dispose();probe=new PcStabilityProbe(Camera.main);
                    SessionState.SetInt(IndexKey,0);SessionState.SetString(StateKey,"orient");return;
                }
                int index=SessionState.GetInt(IndexKey,0);
                if(index>=Views.Length){Finish();return;}
                var view=Views[index];var routerNow=Object.FindAnyObjectByType<GazeInputRouter>();var simulation=routerNow.Simulation;
                var spawner=Object.FindAnyObjectByType<MeteorSpawner>();Camera camera=Camera.main;
                if(state=="orient")
                {
                    spawner.ClearScenarioRound();simulation.Recenter();simulation.ApplyLook(new Vector2(view.yaw/.1f,-view.pitch/.09f),1f);
                    SessionState.SetFloat(DeadlineKey,(float)EditorApplication.timeSinceStartup+.25f);SessionState.SetString(StateKey,"spawn");return;
                }
                if(EditorApplication.timeSinceStartup<SessionState.GetFloat(DeadlineKey,0))return;
                if(state=="spawn")
                {
                    MeteorController meteor=spawner.SpawnScenario(camera.transform.position+camera.transform.forward*18f,Vector3.zero,2.3f,0,10f);
                    if(meteor==null)throw new InvalidOperationException("Scenario meteor could not be spawned for "+view.name);
                    SessionState.SetFloat(DeadlineKey,(float)EditorApplication.timeSinceStartup+.18f);SessionState.SetString(StateKey,"capture");return;
                }
                if(state=="capture")
                {
                    MeteorController meteor=spawner.ActiveMeteors.Count>0?spawner.ActiveMeteors[0]:null;
                    if(meteor==null)throw new InvalidOperationException("Meteor was released before capture: "+view.name);
                    Vector3 vp=camera.WorldToViewportPoint(meteor.transform.position);
                    if(vp.z<=0||vp.x<.40f||vp.x>.60f||vp.y<.38f||vp.y>.62f)throw new InvalidOperationException($"{view.name} target viewport invalid: {vp}");
                    string directory=Path.Combine("Docs/RearPanoramaQA","After",view.name);Directory.CreateDirectory(directory);
                    Canvas.ForceUpdateCanvases();probe.SaveGameOnlyProof(directory,routerNow);
                    SessionState.SetInt(IndexKey,index+1);SessionState.SetString(StateKey,"orient");
                }
            }
            catch(Exception exception){probe?.Dispose();probe=null;Debug.LogException(exception);SessionState.SetString(StateKey,"failed");EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
        }

        private static void Finish()
        {
            Object.FindAnyObjectByType<MeteorSpawner>().EndScenario();probe?.Dispose();probe=null;
            Directory.CreateDirectory("Docs/RearPanoramaQA");
            File.WriteAllLines("Docs/RearPanoramaQA/visibility-validation.txt",new[]{
                "Windows VR Simulation: EXTREME / 8 directional target captures",
                "Front, Left, RearLeft, Rear, RearRight, Right, Above, Below: viewport center validation passed",
                "Target acquisition criterion: spawned meteor remained inside x=0.40..0.60, y=0.38..0.62 after camera rotation",
                "Rear structure criterion: side bulkheads <= 0.55m, rear/side glass >= 1.95m, ceiling spine <= 0.25m",
                "Damage anchors: Front 5 + RearLeft/Rear/RearRight 3"
            });
            SessionState.SetString(StateKey,"done");EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
        }

        private static void ValidateAuthoredStructure()
        {
            Transform rear=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PremiumCockpit/RestoredRearCockpit");
            if(rear==null)throw new InvalidOperationException("RestoredRearCockpit missing");
            Require(rear,"RearBulkhead_Port",.55f,true);Require(rear,"RearBulkhead_Starboard",.55f,true);
            Require(rear,"RearObservationGlass",1.95f,false);Require(rear,"PortObservationGlass",1.95f,false);Require(rear,"StarboardObservationGlass",1.95f,false);
            Require(rear,"CeilingSpine",.25f,true);
            if(rear.Find("CeilingObservationGlass_Port")==null||rear.Find("CeilingObservationGlass_Starboard")==null)throw new InvalidOperationException("Ceiling observation glass missing");
        }

        private static void Require(Transform root,string name,float threshold,bool maximum)
        {
            Transform child=root.Find(name);if(child==null)throw new InvalidOperationException(name+" missing");
            Vector3 size=child.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            float value=name.Contains("Bulkhead")||name=="CeilingSpine"?size.x:size.y;
            if(maximum?value>threshold:value<threshold)throw new InvalidOperationException($"{name} size {value:F3} failed threshold {threshold:F3}");
        }

        private sealed class RegressionCallbacks : ICallbacks
        {
            private readonly List<string> lines=new List<string>();private int passed,failed,skipped;
            public void RunStarted(ITestAdaptor testsToRun){lines.Add("Rear panorama targeted EditMode regression");}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result)
            {
                if(result.HasChildren)return;
                if(result.TestStatus==TestStatus.Passed)passed++;else if(result.TestStatus==TestStatus.Failed){failed++;lines.Add("FAIL "+result.FullName+": "+result.Message);}else skipped++;
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                lines.Add($"passed={passed} failed={failed} skipped={skipped} overall={result.TestStatus}");
                Directory.CreateDirectory("Docs/RearPanoramaQA");File.WriteAllLines("Docs/RearPanoramaQA/editmode-regression.txt",lines);
                testApi?.UnregisterCallbacks(this);EditorApplication.Exit(failed==0?0:1);
            }
        }
    }
}
