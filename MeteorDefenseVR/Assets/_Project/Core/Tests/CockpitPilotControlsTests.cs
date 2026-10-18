using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class CockpitPilotControlsTests
    {
        private const string ScenePath="Assets/_Project/Scenes/MeteorDefense.unity";
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test]public void LowerControlsAreAuthoredLowInViewAndHaveNoGameplayPhysicsOrAimBindings()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Transform root=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PilotControls");
            Assert.That(root,Is.Not.Null);var controller=root.GetComponent<CockpitHandVisualController>();Assert.That(controller,Is.Not.Null);
            foreach(string path in new[]{"LowerConsole","FlightStickPivot","ThrottlePivot","FlightStickPivot/RightHandVisual","ThrottlePivot/LeftHandVisual"})Assert.That(root.Find(path),Is.Not.Null,path);
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);Assert.That(root.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).Where(m=>m!=null).Distinct().Count(),Is.LessThanOrEqualTo(7));
            var fields=typeof(CockpitHandVisualController).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Select(f=>f.Name.ToLowerInvariant()).ToArray();
            Assert.That(fields.Any(n=>n.Contains("turret")||n.Contains("gaze")||n.Contains("target")||n.Contains("weapon")),Is.False);
            Camera camera=Camera.main;
            foreach(Transform hand in new[]{controller.RightHandAnchor,controller.LeftHandAnchor})
            {
                Renderer[] renderers=hand.GetComponentsInChildren<Renderer>(true);Assert.That(renderers.Length,Is.GreaterThan(0));
                foreach(Renderer renderer in renderers.Where(r=>r.enabled))
                {Vector3 view=camera.WorldToViewportPoint(renderer.bounds.center);Assert.That(view.z,Is.GreaterThan(0));Assert.That(view.y,Is.LessThan(.48f),renderer.name+" entered the central reticle area");}
            }
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled),Is.LessThanOrEqualTo(26),"Animated assemblies must stay draw-call bounded.");
        }

        [UnityTest,Timeout(120000)]public IEnumerator VisualStateBlendsThroughLaunchImpactCriticalMissionCompleteAndReset()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);yield return new EnterPlayMode();
            var warm=Object.FindAnyObjectByType<MeteorDefenseVR.PcInput.ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+30f;
            while(!warm.IsComplete&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(warm.IsComplete,Is.True);
            var controller=Object.FindAnyObjectByType<CockpitHandVisualController>();var flow=GameFlowManager.Instance;
            var launch=Object.FindAnyObjectByType<LaunchSequenceController>();var health=Object.FindAnyObjectByType<PlayerHealth>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            using var render=new PcStabilityProbe(Camera.main);var router=Object.FindAnyObjectByType<GazeInputRouter>();Save(render,router,"idle");
            Assert.That(controller,Is.Not.Null);Quaternion stickNeutral=controller.FlightStickPivot.localRotation;Quaternion throttleNeutral=controller.ThrottlePivot.localRotation;
            launch.SetDurations(0,0,0,10,10,10,10);flow.StartLaunch();launch.Tick(0);launch.Tick(0);launch.Tick(0);launch.Tick(5);controller.TickVisual(.25f);yield return null;
            Assert.That(controller.State,Is.EqualTo(CockpitHandVisualState.Launch));Assert.That(Quaternion.Angle(stickNeutral,controller.FlightStickPivot.localRotation),Is.GreaterThan(.1f));
            Assert.That(Quaternion.Angle(throttleNeutral,controller.ThrottlePivot.localRotation),Is.GreaterThan(.1f));
            Save(render,router,"launch");
            flow.StartMainGame();Assert.That(health.ApplyImpactDamage(85,health.transform.position),Is.True);controller.TickVisual(0);
            Assert.That(controller.State,Is.EqualTo(CockpitHandVisualState.Impact));Save(render,router,"impact");yield return new WaitForSecondsRealtime(.35f);
            controller.TickVisual(.35f);
            Assert.That(controller.State,Is.EqualTo(CockpitHandVisualState.Critical));Save(render,router,"critical");
            Quaternion viewNeutral=Camera.main.transform.localRotation;
            foreach((string name,Vector3 angle) in new[]{("left",new Vector3(0,-90,0)),("right",new Vector3(0,90,0)),("above",new Vector3(-55,0,0)),("below",new Vector3(48,0,0))})
            {Camera.main.transform.localRotation=viewNeutral*Quaternion.Euler(angle);controller.TickVisual(.1f);Save(render,router,name);}
            Camera.main.transform.localRotation=viewNeutral;controller.TickVisual(.1f);
            flow.CompleteMission();controller.TickVisual(0);yield return null;Assert.That(controller.State,Is.EqualTo(CockpitHandVisualState.MissionComplete));Save(render,router,"mission-complete");
            reset.ResetExperience();Assert.That(controller.State,Is.EqualTo(CockpitHandVisualState.Reset));
            Assert.That(controller.FlightStickPivot.localRotation,Is.EqualTo(stickNeutral));Assert.That(controller.ThrottlePivot.localRotation,Is.EqualTo(throttleNeutral));
            Assert.That(health.CurrentHealth,Is.EqualTo(health.MaxHealth));yield return null;
        }
        private static void Save(PcStabilityProbe render,GazeInputRouter router,string name)
        {string path="Docs/PilotControlsQA/"+name;Directory.CreateDirectory(path);Canvas.ForceUpdateCanvases();Camera.main.Render();render.SaveGameOnlyProof(path,router);}
    }
}
