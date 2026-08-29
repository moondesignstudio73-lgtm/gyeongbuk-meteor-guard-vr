using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class CockpitImpactPolishTests
    {
        [UnityTearDown] public IEnumerator Cleanup() {Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        private static CockpitDamagePresentation Open()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);return Object.FindAnyObjectByType<CockpitDamagePresentation>();}
        [Test] public void AuthoredEffectsAreFiniteSharedAndIncludeBottomDirectionalAnchor()
        {
            var damage=Open();Assert.That(damage.PointCount,Is.EqualTo(5));Assert.That(damage.ParticleCapacity,Is.LessThanOrEqualTo(470));
            var rail=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/CockpitDamage/OverheadDamageRail");
            Assert.That(rail.localScale.x,Is.GreaterThanOrEqualTo(5.4f),"Overhead rail must meet the authored canopy struts");
            using var s=new SerializedObject(damage);var points=s.FindProperty("points");
            for(int i=0;i<5;i++)
            {
                var p=points.GetArrayElementAtIndex(i);var anchor=(Transform)p.FindPropertyRelative("anchor").objectReferenceValue;
                Assert.That(damage.FindNearestPoint(anchor.position),Is.EqualTo(i));
                Assert.That(p.FindPropertyRelative("cracks").arraySize,Is.EqualTo(4));
                var scorch=(Renderer)p.FindPropertyRelative("scorch").objectReferenceValue;Assert.That(scorch.sharedMaterial.shader.name,Is.EqualTo("MeteorDefense/Scorched Metal"));
                foreach(string property in new[]{"spark","vapor","dust"})
                {
                    var ps=(ParticleSystem)p.FindPropertyRelative(property).objectReferenceValue;if(ps==null)continue;
                    Assert.That(ps.main.playOnAwake,Is.False);Assert.That(ps.main.loop,Is.False);Assert.That(ps.main.maxParticles,Is.LessThanOrEqualTo(64));
                    Assert.That(ps.main.cullingMode,Is.EqualTo(ParticleSystemCullingMode.AlwaysSimulate));Assert.That(ps.emission.rateOverTime.constant,Is.Zero);
                }
                if(i==1) Assert.That(p.FindPropertyRelative("vapor").objectReferenceValue,Is.Null,"No central vapor occlusion");
            }
            foreach(string name in new[]{"FracturedGlass","ScorchedMetal","EdgeVapor"})
            {var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/CockpitDamage/"+name+".mat");Assert.That(material.shader.isSupported,Is.True);Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);}
        }
        [Test] public void DamageAudioUsesExistingImpactCueAndOneQuietPreloadedLoop()
        {
            var damage=Open();using var s=new SerializedObject(damage);var source=(AudioSource)s.FindProperty("airLeakSource").objectReferenceValue;
            Assert.That(source,Is.Not.Null);Assert.That(source.loop,Is.True);Assert.That(source.playOnAwake,Is.False);Assert.That(s.FindProperty("airLeakVolume").floatValue,Is.InRange(.05f,.15f));
            var library=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/_Project/Audio/AudioCueLibrary.asset");
            foreach(var cue in new[]{AudioCue.PlayerDamage,AudioCue.GlassHit,AudioCue.AirLeak})
            {
                var matches=library.Entries.Where(e=>e.Cue==cue).ToArray();Assert.That(matches.Length,Is.EqualTo(1));Assert.That(matches[0].Clip,Is.Not.Null);
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(matches[0].Clip));
                Assert.That(importer.defaultSampleSettings.preloadAudioData,Is.True);Assert.That(importer.defaultSampleSettings.loadType,Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            }
            Assert.That(Object.FindAnyObjectByType<AudioManager>().GetComponentsInChildren<AudioSource>(true).Length,Is.EqualTo(5));
        }
        [UnityTest,Timeout(300000)] public IEnumerator TwentyDockReplayCyclesCleanDamageAndKeepResourcesStable()
        {
            Open();yield return new EnterPlayMode();yield return Cycles();yield return new ExitPlayMode();
        }
        private static IEnumerator Cycles()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+35;
            while(!warm.IsComplete && Time.realtimeSinceStartup<deadline)yield return null;Assert.That(warm.IsComplete,Is.True);
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            Object.FindAnyObjectByType<GazeRaycaster>().enabled=false;
            lobby.Ranking.Initialize(flow,Object.FindAnyObjectByType<ResultController>(),new LocalRankingStore(null));
            var anchors=new Transform[4];
            using(var s=new SerializedObject(damage))for(int i=0;i<4;i++) anchors[i]=(Transform)s.FindProperty("points").GetArrayElementAtIndex(i).FindPropertyRelative("anchor").objectReferenceValue;
            var lines=new List<string>{"20 real hangar Return/Menu/Replay/Launch transitions with accelerated damage; not 20 natural-length boss completions."};
            int materials=0,meshes=0,transforms=0,systems=0,sources=0;
            reset.ResetExperience();flow.StartMainGame();
            using var render=new PcStabilityProbe(camera);
            for(int cycle=0;cycle<20;cycle++)
            {
                spawner.StopSpawning(true);
                Assert.That(damage.VisibleCrackCount,Is.Zero);Assert.That(damage.VisibleScorchCount,Is.Zero);Assert.That(damage.AirLeakPlaying,Is.False);
                if(cycle==0) {yield return new WaitForSecondsRealtime(.4f);Save(render,router,"clean");}
                int[] amounts=cycle==0?new[]{15,20,30,20,10}:new[]{85,10};
                int[] locations=cycle==0?new[]{0,2,1,3,0}:new[]{cycle%4,(cycle+1)%4};
                for(int i=0;i<amounts.Length;i++)
                {
                    health.Tick(1);Assert.That(health.ApplyImpactDamage(amounts[i],anchors[locations[i]].position),Is.True);
                    Assert.That(damage.LastDamagePoint,Is.EqualTo(locations[i]));Assert.That(damage.ActiveParticles,Is.GreaterThanOrEqualTo(38));
                    damage.Tick(.4f);Assert.That(damage.HasImpactFlash,Is.True,"A long impact frame must still render its flash");
                    if(cycle==0 && i==0) Save(render,router,"instant-impact");
                    Assert.That(damage.VisibleScorchCount,Is.GreaterThan(0));
                    if(cycle==0) {yield return new WaitForSecondsRealtime(.045f);Save(render,router,"impact-"+i);}
                    yield return new WaitForSecondsRealtime(.4f);
                    if(cycle==0)Save(render,router,"damage-"+i);
                }
                Assert.That(damage.GlassDamageLevel,Is.EqualTo(4));Assert.That(damage.DamageLevel,Is.EqualTo(3));
                Assert.That(damage.AirLeakPlaying,Is.True);Assert.That(damage.HasWarningLight,Is.True);
                Assert.That(damage.ActiveParticles,Is.LessThanOrEqualTo(damage.ParticleCapacity));
                if(cycle==0)
                {
                    // Pause must not emit extra bursts or progress flash/flicker; no camera motion.
                    int impactCount=damage.ImpactCount,cracks=damage.VisibleCrackCount;
                    Time.timeScale=0;AudioListener.pause=true;yield return new WaitForSecondsRealtime(.25f);
                    Assert.That(damage.ImpactCount,Is.EqualTo(impactCount));Assert.That(damage.VisibleCrackCount,Is.EqualTo(cracks));
                    Time.timeScale=1;AudioListener.pause=false;
                    for(int n=0;n<30;n++)damage.Tick(0);
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    long watchBytes=GC.GetAllocatedBytesForCurrentThread();
                    for(int n=0;n<200;n++)damage.Tick(0);
                    long tickBytes=GC.GetAllocatedBytesForCurrentThread()-watchBytes;watch.Stop();
                    Assert.That(tickBytes,Is.Zero,"Steady-state damage Tick must not allocate managed objects");
                    lines.Add($"200 steady Tick(0): managedBytes={tickBytes} totalMs={watch.Elapsed.TotalMilliseconds:F3}; not GPU/FPS profiling");
                    health.ResetHealth();Assert.That(damage.AirLeakPlaying,Is.False);Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(damage.VisibleScorchCount,Is.Zero);
                    health.Tick(1);health.ApplyImpactDamage(95,anchors[3].position);yield return new WaitForSecondsRealtime(.3f);
                    Save(render,router,"upper-critical");
                    camera.transform.rotation=rotation*Quaternion.Euler(-32,0,0);yield return null;Save(render,router,"upper-look");
                    camera.transform.rotation=rotation;yield return null;
                }
                flow.ShowResult();Assert.That(damage.AirLeakPlaying,Is.False);
                Assert.That(lobby.BeginReturn(),Is.True);deadline=Time.realtimeSinceStartup+5;
                while(!lobby.IsMenuReady && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(lobby.IsMenuReady,Is.True);Assert.That(damage.ActiveParticles,Is.Zero);
                lobby.Queue(HangarLobbyController.MenuAction.Replay);
                deadline=Time.realtimeSinceStartup+8;
                while(flow.CurrentState!=GameState.Playing && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing));
                Assert.That(health.CurrentHealth,Is.EqualTo(health.MaxHealth));
                Assert.That(damage.VisibleCrackCount,Is.Zero);Assert.That(damage.VisibleScorchCount,Is.Zero);Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(damage.AirLeakPlaying,Is.False);Assert.That(damage.HasWarningLight,Is.False);
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
                Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
                int m=Resources.FindObjectsOfTypeAll<Material>().Length,mesh=Resources.FindObjectsOfTypeAll<Mesh>().Length;
                int t=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length,p=Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length,a=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                if(cycle==1){materials=m;meshes=mesh;transforms=t;systems=p;sources=a;}
                if(cycle>1){Assert.That(m,Is.LessThanOrEqualTo(materials));Assert.That(mesh,Is.LessThanOrEqualTo(meshes));Assert.That(t,Is.EqualTo(transforms));Assert.That(p,Is.EqualTo(systems));Assert.That(a,Is.EqualTo(sources));}
                lines.Add($"replay={cycle+1} materials={m} meshes={mesh} transforms={t} systems={p} sources={a} cracks={damage.VisibleCrackCount} scorch={damage.VisibleScorchCount} particles={damage.ActiveParticles} leak={damage.AirLeakPlaying}");
            }
            damage.enabled=false;damage.enabled=true;reset.ResetExperience();yield return null;
            Assert.That(damage.ActiveParticles,Is.Zero);Assert.That(damage.AirLeakPlaying,Is.False);Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks,Is.Zero);
            Save(render,router,"reset");Directory.CreateDirectory("Docs/CockpitImpactQA");File.WriteAllLines("Docs/CockpitImpactQA/20-replays.txt",lines);
            Assert.That(router.Webcam.IsStreaming,Is.False);
        }
        private static void Save(PcStabilityProbe render,GazeInputRouter router,string name)
        {string path="Docs/CockpitImpactQA/"+name;Directory.CreateDirectory(path);Canvas.ForceUpdateCanvases();Camera.main.Render();render.SaveGameOnlyProof(path,router);}
    }
}
