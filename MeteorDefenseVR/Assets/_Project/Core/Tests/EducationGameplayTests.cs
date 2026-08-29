using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
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
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class EducationGameplayTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode(); }
        internal static ParticipantInfo Participant() => new ParticipantInfo { Name="교육테스트",Number="QA-01",Group="자동 검증" };
        internal static void Stage(EducationSessionManager manager, EducationStage stage)
            => typeof(EducationSessionManager).GetMethod("BeginStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{stage});
        internal static void Answer(EducationSessionManager manager,string label)
        {int index=System.Array.IndexOf(manager.AnswerOptions,label);Assert.That(index,Is.GreaterThanOrEqualTo(0));manager.SubmitAnswer(index);}
        internal static void Save(PcStabilityProbe render,GazeInputRouter router,string name)
        {string folder=Path.Combine("Docs/EducationQA/Visual",name);Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);}
        [UnityTest,Timeout(150000)] public IEnumerator Stage3ActualCollisionCauseRetryRepairAndReport()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();yield return null;yield return null;
            var manager=Object.FindAnyObjectByType<EducationSessionManager>();var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
            var experience=Object.FindAnyObjectByType<ExperienceConfiguration>();experience.Settings.experienceMode=ExperienceMode.PublicExperience;experience.Apply();
            using var render=new PcStabilityProbe(Camera.main);
            manager.OpenRegistration();yield return new WaitForSecondsRealtime(.5f);Save(render,router,"registration");
            Assert.That(manager.OperatorAllowed,Is.False);manager.OpenOperator();Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Registration));
            Assert.That(manager.StartTraining(Participant(),DifficultyLevel.Normal,true),Is.True);GameFlowManager.Instance.StartMainGame();yield return null;
            Stage(manager,EducationStage.Diagnosis);var demo=manager.Tracked.First(p=>p.Value.Demonstration).Key;
            demo.Move(10);yield return null;
            Assert.That(manager.Phase,Is.EqualTo(EducationPhase.CauseQuestion));Assert.That(manager.Session.Faults.Count,Is.EqualTo(1));
            Assert.That(manager.Session.Threats.Last().Outcome,Is.EqualTo(ThreatOutcome.Collision));
            var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();Assert.That(damage.ImpactCount,Is.GreaterThan(0));
            yield return new WaitForSecondsRealtime(.4f);Save(render,router,"cause");
            int correct=System.Array.IndexOf(manager.AnswerOptions,EmergencyTrainingState.Cause(manager.Fault.Kind));
            var q=manager.Session.Questions.Last();manager.SubmitAnswer((correct+1)%3);float degraded=manager.Fault.Condition;
            manager.Tick(manager.Settings.emergency.wrongActionDelay+.1f);manager.SubmitAnswer(correct);
            Assert.That(q.Correct,Is.False);Assert.That(q.Attempts,Is.EqualTo(2));Assert.That(manager.Phase,Is.EqualTo(EducationPhase.ResponseQuestion));
            Answer(manager,EmergencyTrainingState.Action(manager.Fault.Kind));Assert.That(manager.Fault.Resolved,Is.True);Assert.That(manager.Session.Faults[0].MitigatedAt,Is.GreaterThanOrEqualTo(0));
            Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Feedback));Assert.That(manager.Fault.Condition,Is.GreaterThan(degraded));
            typeof(EducationSessionManager).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{false});
            yield return new WaitForSecondsRealtime(.5f);Save(render,router,"report");
            Assert.That(manager.Store.Records.Count,Is.EqualTo(1));Assert.That(manager.Store.Records[0].Faults.Count,Is.EqualTo(1));
            manager.NextParticipant();yield return null;
            Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Registration));Assert.That(damage.VisibleCrackCount,Is.Zero);Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(manager.Fault,Is.Null);
            manager.CloseMenu();experience.Settings.experienceMode=ExperienceMode.Operator;experience.Apply();manager.OpenOperator();
            yield return new WaitForSecondsRealtime(.4f);Save(render,router,"operator");Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Operator));
        }
        [UnityTest, Timeout(150000)] public IEnumerator Stage12UsesActualMouseDetectionWeaponAndRestoresShortGame()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();yield return null;yield return null;
            var previous=InputSystem.settings.editorInputBehaviorInPlayMode;var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                var manager=Object.FindAnyObjectByType<EducationSessionManager>();Assert.That(manager,Is.Not.Null);Assert.That(manager.enabled,Is.True);
                var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
                var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
                Assert.That(manager.StartTraining(new ParticipantInfo(),DifficultyLevel.Normal,true),Is.False);
                Assert.That(manager.StartTraining(Participant(),DifficultyLevel.Normal,true),Is.True);
                GameFlowManager.Instance.StartMainGame();yield return null;
                Assert.That(spawner.ScenarioControlled,Is.True);Assert.That(manager.Tracked.Count,Is.EqualTo(2));
                var objective=manager.Tracked.First(p=>p.Value.EvaluationTarget).Key;
                float end=Time.realtimeSinceStartup+10;
                while(manager.Phase==EducationPhase.Search&&Time.realtimeSinceStartup<end)
                { UnifiedLookTests.QueueAim(mouse,router.Simulation,objective.transform,Camera.main.WorldToScreenPoint(objective.transform.position));yield return null; }
                Assert.That(manager.Session.Threats.Count(r=>r.DetectedAt>=0),Is.EqualTo(1),"Real centered gaze must record the observation.");
                Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Feedback));
                Stage(manager,EducationStage.Prediction);yield return null;
                var urgent=manager.Tracked.OrderBy(p=>manager.Risk(p.Key).Ttc).First();
                int id=urgent.Value.Id;end=Time.realtimeSinceStartup+12;
                while(manager.Session.Questions.Last().Attempts==0&&Time.realtimeSinceStartup<end)
                { UnifiedLookTests.QueueAim(mouse,router.Simulation,urgent.Key.transform,Camera.main.WorldToScreenPoint(urgent.Key.transform.position));yield return null; }
                var answer=manager.Session.Questions.Last();Assert.That(answer.Selected,Is.EqualTo(id));Assert.That(answer.Correct,Is.True);
                Assert.That(manager.Session.Threats.First(r=>r.Id==id).ActionAt,Is.GreaterThanOrEqualTo(0));
                manager.ReturnToReady();yield return null;
                Assert.That(spawner.ScenarioControlled,Is.False);Assert.That(router.EducationModalOpen,Is.False);
                Assert.That(manager.Store.Records.Count,Is.EqualTo(1));Assert.That(manager.Store.Records[0].Aborted,Is.True);
                GameFlowManager.Instance.StartMainGame();yield return null;spawner.Tick(5);
                Assert.That(spawner.IsSpawning||spawner.ActiveCount>0,Is.True,"Original short-game wave still runs after training reset.");
            }
            finally {InputSystem.RemoveDevice(mouse);InputSystem.settings.editorInputBehaviorInPlayMode=previous;InputSystem.settings.backgroundBehavior=background;}
        }
    }
}
