using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class CockpitDamageTests
    {
        [UnityTearDown] public IEnumerator Cleanup() {if(Application.isPlaying) yield return new ExitPlayMode();}

        [TestCase(.5f,0f)] [TestCase(3f,0f)] [TestCase(1f,74f)] [TestCase(6f,130f)]
        public void SweptBoundaryStopsWholeRotatedMeshAndOnlyHitsOnce(float size,float rotation)
        {
            var root=new GameObject("BoundaryTest");var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3,2,6),Quaternion.Euler(0,rotation,0));
                var boundary=root.AddComponent<ShipCollisionBoundary>();var meteor=rock.AddComponent<MeteorController>();
                meteor.Configure(MeteorType.Normal,1,100,10,100,size);meteor.SetImpactBoundary(boundary);
                meteor.SetMovementDirection(-root.transform.forward);rock.transform.position=root.transform.position+root.transform.forward*20;
                rock.transform.rotation=Quaternion.Euler(30,65,45);meteor.Spawn();int hit=0,score=0;
                meteor.ReachedPlayerEvent+=(_,__)=>hit++;meteor.ScoreAwarded+=(_,__)=>score++;
                meteor.Move(2f);meteor.Move(2f);meteor.ReachPlayer();
                Assert.That(hit,Is.EqualTo(1));Assert.That(score,Is.Zero);Assert.That(rock.activeSelf,Is.False);
                Assert.That(Vector3.Dot(rock.transform.position-root.transform.position,root.transform.forward),Is.GreaterThanOrEqualTo(meteor.CollisionRadius+boundary.Clearance-.001f));
                foreach(Vector3 vertex in rock.GetComponent<MeshFilter>().sharedMesh.vertices)
                    Assert.That(Vector3.Dot(rock.transform.TransformPoint(vertex)-root.transform.position,root.transform.forward),Is.GreaterThanOrEqualTo(0));
                Assert.That(Vector3.Dot(meteor.ImpactPosition-root.transform.position,root.transform.forward),Is.EqualTo(0).Within(.001));
            }
            finally {Object.DestroyImmediate(rock);Object.DestroyImmediate(root);}
        }
        [Test] public void BoundaryAlsoRepairsOverlappingSpawnAndLeavesOutsideMovementUntouched()
        {
            var root=new GameObject("Plane");
            try
            {
                var boundary=root.AddComponent<ShipCollisionBoundary>();
                Assert.That(boundary.Sweep(Vector3.forward*5,Vector3.forward*4,.8f,out _,out _),Is.False);
                Assert.That(boundary.Sweep(Vector3.back,Vector3.back*2,.8f,out var safe,out _),Is.True);
                Assert.That(safe.z,Is.EqualTo(.8f+boundary.Clearance).Within(.001));
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void LegacyMovementCannotOvershootTargetOnLongFrame()
        {
            var rock=new GameObject("Rock");var target=new GameObject("Target");
            try {var meteor=rock.AddComponent<MeteorController>();meteor.Configure(MeteorType.Normal,1,100,10,100,1);meteor.SetMovementTarget(target.transform);rock.transform.position=Vector3.forward*10;meteor.Spawn();meteor.Move(1);Assert.That(meteor.State,Is.EqualTo(MeteorLifecycleState.ReachedPlayer));Assert.That(rock.transform.position.z,Is.GreaterThan(0));}
            finally {Object.DestroyImmediate(rock);Object.DestroyImmediate(target);}
        }
        [TestCase(1f,0)] [TestCase(.70f,0)] [TestCase(.69f,1)] [TestCase(.40f,1)]
        [TestCase(.39f,2)] [TestCase(.20f,2)] [TestCase(.19f,3)] [TestCase(0f,3)]
        public void InspectorDefaultsRespectHpBands(float hp,int level)
        {
            var root=new GameObject("PresentationTest");
            try {Assert.That(root.AddComponent<CockpitDamagePresentation>().LevelFor(hp),Is.EqualTo(level));}
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void FailedResultNeverClaimsSuccess()
        {
            var result=new GameSessionSnapshot {MissionFailed=true,RemainingHP=0};
            Assert.That(ResultView.Format(result,"C"),Does.Contain("MISSION FAILED").And.Not.Contain("지구 방어 성공"));
            Assert.That(WorldTextPresentation.FormatResult(result,"C"),Does.Contain("MISSION FAILED").And.Not.Contain("지구 방어 성공"));
        }
        [UnityTest,Timeout(180000)] public IEnumerator TwentyDamageFailureResetCyclesKeepBoundedResources()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();yield return RunCycles();yield return new ExitPlayMode();
        }
        private static IEnumerator RunCycles()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+30;
            while(!warm.IsComplete && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(warm.IsComplete,Is.True);
            Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>().enabled=false;
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var boss=Object.FindAnyObjectByType<BossClimaxController>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();
            var boundary=Object.FindAnyObjectByType<ShipCollisionBoundary>();var result=Object.FindAnyObjectByType<ResultController>();
            var audio=Object.FindAnyObjectByType<AudioManager>();
            Assert.That(boundary,Is.Not.Null);Assert.That(damage,Is.Not.Null);Assert.That(health.Policy,Is.EqualTo(GameOverPolicy.NormalMode));
            Assert.That(router.Webcam.IsStreaming,Is.False);
            var camera=Camera.main;var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;
            int damageEvents=0,failures=0,impactAudio=0;
            health.DamageTaken+=(_,__)=>damageEvents++;health.MissionFailed+=()=>failures++;
            audio.CuePlaybackRequested+=(cue,_)=>{if(cue==AudioCue.PlayerDamage)impactAudio++;};
            var lines=new List<string>{"20 accelerated scene combat/failure/reset cycles, 100 swept meteor impacts; not 20 natural-length sessions."};
            int materialsAtBaseline=0,particlesAtBaseline=0,objectsAtBaseline=0,meshesAtBaseline=0;
            int[] hits={20,15,35,15,15};
            for(int cycle=1;cycle<=20;cycle++)
            {
                reset.ResetExperience();flow.StartMainGame();
                int previousCracks=0;
                if(cycle==1) {yield return new WaitForSecondsRealtime(.5f);SaveProof(router,"normal");}
                for(int hit=0;hit<hits.Length;hit++)
                {
                    health.Tick(1f);MeteorController meteor;
                    if(hit==4 && cycle%2==0)
                    {
                        boss.BeginClimax();boss.Tick(5);boss.Tick(5);meteor=boss.ActiveBoss;
                        Assert.That(meteor,Is.Not.Null);
                    }
                    else
                    {
                        for(int attempt=0;attempt<10 && spawner.ActiveCount==0;attempt++) spawner.Tick(1000);
                        Assert.That(spawner.ActiveCount,Is.GreaterThan(0));meteor=spawner.ActiveMeteors[0];
                    }
                    meteor.Configure(meteor.MeteorType,1,100,hits[hit],100,meteor.Size);meteor.SetMovementTarget(null);meteor.SetMovementDirection(-boundary.transform.forward);
                    meteor.transform.position=boundary.transform.position+boundary.transform.forward*20+boundary.transform.right*((hit%3-1)*1.13f);
                    meteor.Spawn();int previousEvents=damageEvents;meteor.Move(1);
                    Assert.That(damageEvents,Is.EqualTo(previousEvents+1));Assert.That(meteor.gameObject.activeSelf,Is.False);
                    meteor.Move(1);meteor.ReachPlayer();Assert.That(damageEvents,Is.EqualTo(previousEvents+1));
                    Assert.That(damage.VisibleCrackCount,Is.GreaterThanOrEqualTo(previousCracks));previousCracks=damage.VisibleCrackCount;
                    if(hit==0)
                    {
                        Assert.That(damage.LastDamagePoint,Is.EqualTo(0));Assert.That(damage.VisibleCrackCount,Is.EqualTo(1));
                        Assert.That(damage.DamageLevel,Is.Zero);
                        Assert.That(damage.ActiveParticles,Is.GreaterThan(0));
                        if(cycle==1) {yield return new WaitForSecondsRealtime(.05f);SaveProof(router,"impact-burst");}
                    }
                    if(hit==1) Assert.That(damage.DamageLevel,Is.EqualTo(1));
                    if(hit==2) Assert.That(damage.DamageLevel,Is.EqualTo(2));
                    if(hit==3) Assert.That(damage.DamageLevel,Is.EqualTo(3));
                    yield return new WaitForSecondsRealtime(.4f);
                    if(cycle==1) SaveProof(router,new[]{"first-impact","light","heavy","critical","failed"}[hit]);
                }
                Assert.That(health.IsMissionFailed,Is.True);Assert.That(flow.CurrentState,Is.EqualTo(GameState.Result));
                Assert.That(result.Snapshot.MissionFailed,Is.True);Assert.That(damage.VisibleCrackCount,Is.InRange(1,16));
                damage.Tick(10f);
                Assert.That(damage.ActiveParticles,Is.Zero,"No periodic sparks may restart in Result");
                Assert.That(damage.HasWarningLight,Is.False,"Warning animation is combat-only");
                if(cycle==1) {yield return new WaitForSecondsRealtime(1.5f);SaveProof(router,"failed-complete");}
                boss.Tick(10);boss.BeginClimax();Assert.That(boss.ActiveBoss,Is.Null);Assert.That(boss.IsRunning,Is.False);
                Assert.That(camera.transform.position,Is.EqualTo(originalPosition));Assert.That(camera.transform.rotation,Is.EqualTo(originalRotation));
                reset.ResetExperience();yield return new WaitForSecondsRealtime(.15f);
                Assert.That(damage.VisibleCrackCount,Is.Zero);Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(damage.DamageLevel,Is.Zero);Assert.That(damage.HasWarningLight,Is.False);
                Assert.That(health.CurrentHealth,Is.EqualTo(health.MaxHealth));Assert.That(flow.CurrentState,Is.EqualTo(GameState.Boot));
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
                int materials=Resources.FindObjectsOfTypeAll<Material>().Length,particles=Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length;
                int objects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length,meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
                if(cycle==2) {materialsAtBaseline=materials;particlesAtBaseline=particles;objectsAtBaseline=objects;meshesAtBaseline=meshes;}
                if(cycle>2) {Assert.That(materials,Is.LessThanOrEqualTo(materialsAtBaseline));Assert.That(particles,Is.EqualTo(particlesAtBaseline));Assert.That(objects,Is.EqualTo(objectsAtBaseline));Assert.That(meshes,Is.LessThanOrEqualTo(meshesAtBaseline));}
                lines.Add($"cycle={cycle} materials={materials} particleSystems={particles} transforms={objects} meshes={meshes} visibleCracks={damage.VisibleCrackCount} liveDamageParticles={damage.ActiveParticles}");
            }
            Assert.That(damageEvents,Is.EqualTo(100));Assert.That(failures,Is.EqualTo(20));Assert.That(impactAudio,Is.EqualTo(100));
            // Disable/re-enable must not multiply subscriptions; Reset must restore decorative panel blocks.
            damage.enabled=false;damage.enabled=true;reset.ResetExperience();yield return null;
            Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
            SaveProof(router,"reset");
            Directory.CreateDirectory("Docs/CockpitDamageQA");lines.Add($"damageEvents={damageEvents} failures={failures} PlayerDamageAudio={impactAudio} cameraUnchanged=true");File.WriteAllLines("Docs/CockpitDamageQA/20-cycles.txt",lines);
        }
        private static void SaveProof(GazeInputRouter router,string name)
        {
            using var render=new PcStabilityProbe(Camera.main);
            Canvas.ForceUpdateCanvases();Camera.main.Render();string path="Docs/CockpitDamageQA/"+name;Directory.CreateDirectory(path);render.SaveGameOnlyProof(path,router);
        }
    }
}
