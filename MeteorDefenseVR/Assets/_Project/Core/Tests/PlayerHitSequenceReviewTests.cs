using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
using MeteorDefenseVR.VFX;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests {
public sealed class PlayerHitSequenceReviewTests {
 sealed class HeldRay:IGazeProvider {
  public Ray Ray;public bool Valid;
  public bool IsTrackingValid=>Valid;public Ray GetGazeRay()=>Ray;
  public Vector3 GetGazeOrigin()=>Ray.origin;public Vector3 GetGazeDirection()=>Ray.direction;
 }
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)]public IEnumerator PlayerCameraActualWeaponSequence(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Review();
 }
 static IEnumerator Review(){
  const string output="Docs/DeliveryQA/player-hit-sequence-20261027/Editor";
  Directory.CreateDirectory(output);
  yield return null;yield return null;
  var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();
  var weapon=Object.FindAnyObjectByType<LaserWeapon>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
  var difficulty=Object.FindAnyObjectByType<DifficultyManager>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();
  float deadline=Time.realtimeSinceStartup+45;while(director.IsBootPresenting&&Time.realtimeSinceStartup<deadline)yield return null;
  Assert.IsFalse(director.IsBootPresenting);
  var originalProvider=caster.Provider;bool input=router.InputSuspended,autoFire=weapon.AutoFireOnLock;float captureStep=Time.captureDeltaTime;
  var ray=new HeldRay();var camera=Camera.main;var vfx=Object.FindAnyObjectByType<CombatVfxController>();
  var premium=Object.FindAnyObjectByType<PremiumCombatVfx>();var beams=Object.FindObjectsByType<LaserBeamView>();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var waves=(MeteorWaveData[])typeof(MeteorSpawner).GetField("waves",flags).GetValue(spawner);
  var notes=new List<string>{"Editor controlled player-camera sequence on product 372a75f. Fixed 1/60 game-time step, static target transform, injected valid gaze ray; production raycast, lock, turret aiming, auto-fire, damage, VFX. Not OS/HMD/human comprehension or current packaged execution."};
  try{
   router.InputSuspended=true;caster.SetProvider(ray);weapon.AutoFireOnLock=true;
   difficulty.Select(DifficultyLevel.Normal);GameFlowManager.Instance.StartMainGame();spawner.BeginScenario();
   yield return new WaitForSecondsRealtime(2);
   Time.captureDeltaTime=1f/60f;
   foreach(var type in new[]{MeteorType.Normal,MeteorType.Large}){
    var data=waves.SelectMany(w=>w.Spawns).First(e=>e.MeteorType==type);
    float health=data.Health*difficulty.Session.Health;
    Assert.That(weapon.EffectiveDamage,Is.EqualTo(1).Within(.001));
    var folder=Path.Combine(output,type.ToString());Directory.CreateDirectory(folder);
    using(var proof=new PcStabilityProbe(camera,1280,720)){
     var meteor=spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.5f,.58f,12)),Vector3.zero,data.Size,data.Damage,health,type);
     Assert.IsNotNull(meteor);meteor.enabled=false;
     var target=meteor.GetComponent<GazeLockOnTarget>();var surface=meteor.GetComponent<MeteorDamageSurface>();
     ray.Ray=new Ray(camera.transform.position,(meteor.transform.position-camera.transform.position).normalized);ray.Valid=false;
     var pose=camera.transform.position;var rotation=camera.transform.rotation;var rockPose=meteor.transform.position;var rockRotation=meteor.transform.rotation;
     int shots=0,hits=0,deaths=0,hitEffects=0,explosions=0,lastShot=-10000,deathFrame=-1,renderCount=0,lockedRenders=0;
     float maximumCameraShift=0,maximumCameraTurn=0;
     var rows=new List<string>{"kind,frame,gameTime,wallTime,health,active,lock,progress,damageFraction,beamCount,particles,rayHitsTarget"};
     var hitHealth=new List<float>();var images=new List<string>();Exception failure=null;
     string F(float x)=>x.ToString("F6",CultureInfo.InvariantCulture);
     void Record(string kind){rows.Add(string.Join(",",kind,Time.frameCount,F(Time.time),F(Time.realtimeSinceStartup),F(meteor.CurrentHealth),meteor.gameObject.activeInHierarchy,target.State,F(target.Progress),F(surface!=null?surface.DamageFraction:0),beams.Count(b=>b.IsPlaying),premium!=null?premium.ActiveParticleCount:0,ReferenceEquals(caster.CurrentTarget,target)));}
     void Focus(GazeLockOnTarget _)=>Record("focus");
     void Locked(GazeLockOnTarget _)=>Record("locked");
     void Fired(GazeLockOnTarget _,MeteorController m){if(m!=meteor)return;shots++;lastShot=Time.frameCount;Record("fired");}
     void Damaged(MeteorController m,float amount){hitHealth.Add(m.CurrentHealth);Record("damaged");}
     void Destroyed(MeteorController m){deaths++;deathFrame=Time.frameCount;Record("destroyed");}
     void Hit(MeteorController m,Vector3 p){if(m!=meteor)return;hits++;Record("hit");}
     void HitFx(Vector3 p){hitEffects++;Record("hit-vfx");}
     void Explosion(Vector3 p){explosions++;Record("explosion-vfx");}
     target.FocusStarted+=Focus;target.Locked+=Locked;weapon.Fired+=Fired;weapon.Hit+=Hit;meteor.Damaged+=Damaged;meteor.Destroyed+=Destroyed;vfx.HitVfxPlayed+=HitFx;vfx.ExplosionVfxPlayed+=Explosion;
     var driver=(PcStabilityRenderDriver)typeof(PcStabilityProbe).GetField("driver",flags).GetValue(proof);
     void Render(){try{
      Record("render");maximumCameraShift=Mathf.Max(maximumCameraShift,Vector3.Distance(pose,camera.transform.position));maximumCameraTurn=Mathf.Max(maximumCameraTurn,Quaternion.Angle(rotation,camera.transform.rotation));
      if(target.IsLocked)lockedRenders++;
      bool save=renderCount<2||renderCount%6==0||target.Progress>.8f||Time.frameCount-lastShot<=10||Time.frameCount-deathFrame<=10&&deathFrame>=0;
      if(save){string name="frame-"+Time.frameCount.ToString("D6");string path=Path.Combine(folder,name);Directory.CreateDirectory(path);proof.SaveGameOnlyProof(path,router);images.Add(name);}
      renderCount++;
     }catch(Exception e){failure=e;}}
     driver.RenderCompleted+=Render;
     try{
      yield return null;yield return null;ray.Valid=true;
      int start=Time.frameCount;
      while((deathFrame<0||Time.frameCount-deathFrame<60)&&Time.frameCount-start<240&&failure==null)yield return null;
      if(failure!=null)Assert.Fail(failure.ToString());
      int expected=Mathf.CeilToInt(health/weapon.EffectiveDamage);
      Assert.That(shots,Is.EqualTo(expected));Assert.That(hits,Is.EqualTo(expected));Assert.That(deaths,Is.EqualTo(1));Assert.That(explosions,Is.EqualTo(1));Assert.That(hitEffects,Is.EqualTo(expected));
      Assert.That(hitHealth.Last(),Is.EqualTo(0));if(type==MeteorType.Large)Assert.That(hitHealth.First(),Is.GreaterThan(0));
      Assert.That(maximumCameraShift,Is.LessThan(.001f));Assert.That(maximumCameraTurn,Is.LessThan(.01f));
      Assert.That(Vector3.Distance(rockPose,meteor.transform.position),Is.LessThan(.001f));Assert.That(Quaternion.Angle(rockRotation,meteor.transform.rotation),Is.LessThan(.01f));
      var guards=Object.FindObjectsByType<TurretPlayerVisibilityGuard>();Assert.IsTrue(guards.Length>0&&guards.All(g=>g.HiddenLayer>=0&&(camera.cullingMask&(1<<g.HiddenLayer))==0));
      notes.Add($"{type}: authoredHP={data.Health}; sessionHealthMultiplier={difficulty.Session.Health}; testedHP={health}; damage={weapon.EffectiveDamage}; size={data.Size}; distance=12; shots={shots}; hits={hits}; deaths={deaths}; hitVfx={hitEffects}; explosionVfx={explosions}; lockedRenderFrames={lockedRenders}; renderFrames={renderCount}; savedImages={images.Count}; cameraShift={maximumCameraShift}; cameraTurn={maximumCameraTurn}; turretHidden=True");
     }finally{
      ray.Valid=false;driver.RenderCompleted-=Render;target.FocusStarted-=Focus;target.Locked-=Locked;weapon.Fired-=Fired;weapon.Hit-=Hit;meteor.Damaged-=Damaged;meteor.Destroyed-=Destroyed;vfx.HitVfxPlayed-=HitFx;vfx.ExplosionVfxPlayed-=Explosion;
      File.WriteAllLines(Path.Combine(folder,"events-and-frames.csv"),rows);File.WriteAllLines(Path.Combine(folder,"images.txt"),images);meteor.enabled=true;
     }
    }
    spawner.ClearScenarioRound();yield return null;
   }
   notes.Add("Sequence assertions passed. Visual review and novice interpretation are separate.");
  }finally{
   Time.captureDeltaTime=captureStep;caster.SetProvider(originalProvider);router.InputSuspended=input;weapon.AutoFireOnLock=autoFire;spawner.EndScenario();
   File.WriteAllLines(Path.Combine(output,"summary.txt"),notes);
  }
 }
}}
