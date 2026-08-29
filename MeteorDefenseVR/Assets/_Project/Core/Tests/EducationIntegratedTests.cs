using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Education;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class EducationIntegratedTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(240000)]public IEnumerator FourStagesRealInputAndTwentyResetResourceBound()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);yield return new EnterPlayMode();yield return null;yield return null;
            // Allocate LINQ capture closures after the EnterPlayMode domain reload.
            {
            float readyDeadline=Time.realtimeSinceStartup+10;
            while((!Application.isPlaying||Object.FindAnyObjectByType<GazeInputRouter>()==null||Object.FindAnyObjectByType<EducationSessionManager>()?.Store==null)&&Time.realtimeSinceStartup<readyDeadline)yield return null;
            Assert.That(Application.isPlaying,Is.True);
            var manager=Object.FindAnyObjectByType<EducationSessionManager>();var router=Object.FindAnyObjectByType<GazeInputRouter>();Assert.That(manager,Is.Not.Null);Assert.That(router,Is.Not.Null);router.SetMode(PcGazeMode.MouseGaze);
            var config=Object.Instantiate(manager.Settings);config.stageSeconds=new[]{5f,5f,5f,5f};config.feedbackSeconds=1;manager.Configure(config);
            var oldInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse=InputSystem.AddDevice<Mouse>();var flow=GameFlowManager.Instance;
            using var render=new PcStabilityProbe(Camera.main);
            try
            {
                float warmDeadline=Time.realtimeSinceStartup+30;
                while((!manager.GetComponent<ReadyPrewarmController>().IsComplete||manager.GetComponent<SfPresentationDirector>().IsBootPresenting)&&Time.realtimeSinceStartup<warmDeadline)yield return null;
                yield return new WaitForSecondsRealtime(.4f);EducationGameplayTests.Save(render,router,"ready");
                manager.StartTraining(EducationGameplayTests.Participant(),DifficultyLevel.Normal,true);
                float launchDeadline=Time.realtimeSinceStartup+12;
                while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<launchDeadline)yield return null;
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing));
                var stages=new HashSet<EducationStage>();int lastRound=-1;float end=Time.realtimeSinceStartup+85;
                while(manager.Phase!=EducationPhase.Report&&Time.realtimeSinceStartup<end)
                {
                    stages.Add(manager.Stage);
                    if(manager.Phase==EducationPhase.Search||manager.Phase==EducationPhase.Defend)
                    {
                        var target=manager.Tracked.Where(p=>p.Key!=null&&p.Key.IsTargetable&&p.Value.EvaluationTarget).OrderBy(p=>manager.Risk(p.Key).Ttc<0?float.MaxValue:manager.Risk(p.Key).Ttc).FirstOrDefault();
                        if(target.Key!=null)UnifiedLookTests.QueueAim(mouse,router.Simulation,target.Key.transform,Camera.main.WorldToScreenPoint(target.Key.transform.position));
                    }
                    else if(manager.Phase==EducationPhase.Demonstration)
                    {manager.Tracked.First(p=>p.Value.Demonstration).Key.Move(10);}
                    else if(manager.Phase==EducationPhase.CauseQuestion)
                        EducationGameplayTests.Answer(manager,EmergencyTrainingState.Cause(manager.Fault.Kind));
                    else if(manager.Phase==EducationPhase.ResponseQuestion)
                        EducationGameplayTests.Answer(manager,EmergencyTrainingState.Action(manager.Fault.Kind));
                    else if(manager.Phase==EducationPhase.TriageQuestion)
                    {
                        if(lastRound!=manager.Round){yield return new WaitForSecondsRealtime(.3f);EducationGameplayTests.Save(render,router,"integrated");lastRound=manager.Round;}
                        var risks=manager.Tracked.Where(p=>p.Key.IsTargetable&&p.Value.RequiresDefense).Select(p=>manager.Risk(p.Key)).Where(r=>r.CanCollide).OrderBy(r=>r.Ttc).ToArray();
                        int action=manager.Fault.TriageChoice(risks.Length>0?risks[0].Ttc:-1,manager.Settings.emergency);
                        string label=action==0?"손상 구역 차단 (압력 / 산소)":action==1?"경미한 전력 문제 점검":"즉시 외부 운석 방어";
                        EducationGameplayTests.Answer(manager,label);
                    }
                    else if(manager.Phase==EducationPhase.Feedback)manager.Tick(6);
                    yield return null;
                }
                Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Report));Assert.That(stages.Count,Is.EqualTo(4));Assert.That(manager.Session.CompletedStages,Is.EqualTo(4));
                Assert.That(manager.Session.Completed,Is.True);var assessment=EducationAssessment.Calculate(manager.Session);
                Assert.That(assessment.Detected,Is.GreaterThan(0));Assert.That(assessment.Defended,Is.GreaterThan(0));Assert.That(assessment.EmergencyCorrect,Is.GreaterThan(0));Assert.That(assessment.PriorityCorrect,Is.GreaterThan(0));
                yield return new WaitForSecondsRealtime(.4f);EducationGameplayTests.Save(render,router,"complete-report");
                var details=Object.FindAnyObjectByType<EducationView>().Root.transform.Find("LearningReport/ReportDetails").GetComponent<UnityEngine.UI.Button>();details.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);EducationGameplayTests.Save(render,router,"details");
                manager.ReturnToReady();yield return null;
                var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
                int materials=0,meshes=0,particles=0,sources=0,transforms=0;var rows=new List<string>{"cycle,materials,meshes,particleSystems,audioSources,transforms,poolInstantiated,duplicateSubscriptions"};
                for(int cycle=0;cycle<20;cycle++)
                {
                    Assert.That(manager.StartTraining(EducationGameplayTests.Participant(),(DifficultyLevel)(cycle%4),true),Is.True);flow.StartMainGame();
                    EducationGameplayTests.Stage(manager,EducationStage.Diagnosis);manager.Tracked.First(p=>p.Value.Demonstration).Key.Move(10);
                    EducationGameplayTests.Answer(manager,EmergencyTrainingState.Cause(manager.Fault.Kind));EducationGameplayTests.Answer(manager,EmergencyTrainingState.Action(manager.Fault.Kind));
                    Assert.That(manager.Session.Questions.Last().Correct,Is.True);
                    Object.FindAnyObjectByType<GameSessionStats>().SetScore(1234);
                    manager.ReturnToReady();yield return null;yield return null;
                    Assert.That(manager.Store.Records.Any(r=>r.GameScore==1234),Is.True,"Operator Reset must snapshot before old stats are cleared.");
                    Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(damage.VisibleCrackCount,Is.Zero);Assert.That(damage.VisibleScorchCount,Is.Zero);Assert.That(damage.AirLeakPlaying,Is.False);
                    Assert.That(manager.Fault,Is.Null);Assert.That(spawner.ActiveCount,Is.Zero);Assert.That(spawner.ScenarioControlled,Is.False);Assert.That(router.EducationModalOpen,Is.False);
                    int m=Resources.FindObjectsOfTypeAll<Material>().Length,mesh=Resources.FindObjectsOfTypeAll<Mesh>().Length,p=Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length,a=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length,t=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
                    int duplicates=PcStabilityProbe.InspectSubscriptions().duplicateCallbacks;Assert.That(duplicates,Is.Zero);
                    rows.Add($"{cycle+1},{m},{mesh},{p},{a},{t},{spawner.InstantiatedCount},{duplicates}");
                    if(cycle==4){materials=m;meshes=mesh;particles=p;sources=a;transforms=t;}
                    if(cycle>=5){Assert.That(m,Is.LessThanOrEqualTo(materials+2));Assert.That(mesh,Is.LessThanOrEqualTo(meshes+2));Assert.That(p,Is.EqualTo(particles));Assert.That(a,Is.EqualTo(sources));Assert.That(t,Is.LessThanOrEqualTo(transforms+2));}
                }
                File.WriteAllLines("Docs/EducationQA/reset20.csv",rows);
                Assert.That(manager.Store.Records.Count,Is.EqualTo(21));Assert.That(Object.FindAnyObjectByType<ResultController>().enabled,Is.True);Assert.That(manager.GetComponent<HangarLobbyController>().enabled,Is.True);
                flow.StartMainGame();yield return null;spawner.Tick(3);Assert.That(spawner.IsSpawning||spawner.ActiveCount>0,Is.True);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.editorInputBehaviorInPlayMode=oldInput;InputSystem.settings.backgroundBehavior=oldBackground;Object.Destroy(config);}
            }
        }
    }
}
