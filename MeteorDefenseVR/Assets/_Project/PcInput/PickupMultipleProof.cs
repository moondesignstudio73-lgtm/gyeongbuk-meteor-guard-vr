using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class PickupMultipleProof {
 public static readonly string[] HudNames={"HealthPanel","StagePanel","ScorePanel","SystemMessagePanel","ShipStatusHologram","ThreatStatusHologram"};
 public static IEnumerator Run(string output,bool validate){
  var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var power=PowerUpManager.Instance;
  var settings=(PowerUpSettings)typeof(PowerUpManager).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(power);
  var rotation=camera.transform.rotation;bool input=router.InputSuspended,casting=caster.enabled,fire=weapon.AutoFireOnLock;var random=UnityEngine.Random.state;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);router.InputSuspended=true;caster.enabled=false;weapon.AutoFireOnLock=false;
  var notes=new List<string>{$"Live settings: max={settings.maximumWorldItems}; lifetime={settings.itemLifetime}; drift={settings.driftSpeed}; chance={settings.regularDropChance}. Distinct CalculateSpawnPosition locations; real meteor pool and TryDrop with explicit valid random rolls. Camera pan only after spawn. Controlled render/collider callback checks, not natural play/OS/HMD."};
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  try{
   Check(settings.maximumWorldItems==3,"Unexpected live maximum; revise fixture");
   var director=Object.FindAnyObjectByType<SfPresentationDirector>();float deadline=Time.unscaledTime+25;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
   foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)}){
    reset.ResetExperience();router.InputSuspended=true;camera.transform.rotation=rotation;yield return new WaitForSecondsRealtime(1);flow.StartMainGame();yield return new WaitForSecondsRealtime(2.5f);spawner.BeginScenario(3);
    using var probe=new PcStabilityProbe(camera,size.x,size.y);
    var origins=new List<Vector3>();
    for(int i=0;i<3;i++){
     var position=spawner.CalculateSpawnPosition((i-1)*.3f,0,1,new Vector2(30,20));origins.Add(position);
     var meteor=spawner.SpawnScenario(position,Vector3.zero,.6f,10);Check(meteor!=null,"Meteor pool empty");meteor.enabled=false;
     Check(power.TryDrop(meteor,false,i*.04f),"Supported drop path rejected");meteor.enabled=true;
    }
    spawner.StopSpawning(true);yield return new WaitForSecondsRealtime(.1f);
    var items=Object.FindObjectsByType<PowerUpItem>().Where(i=>i.IsAvailable).OrderBy(i=>i.transform.position.x).ToArray();Check(items.Length==3,"Live maximum missing");
    for(int i=0;i<origins.Count;i++)for(int j=i+1;j<origins.Count;j++)Check(Vector3.Distance(origins[i],origins[j])>1.5f,"Overlapping acquisition spheres in fixture");
    notes.Add($"{size}: origins={string.Join(";",origins)}; ids={string.Join(",",items.Select(i=>i.GetEntityId()))}; types={string.Join(",",items.Select(i=>i.Type))}");
    var centre=origins[1];
    void Aim(Vector2 vp){float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);var ray=new Vector3((vp.x-.5f)*2*tan*camera.aspect,(vp.y-.5f)*2*tan,1);camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(ray,Vector3.up));}
    void Save(string state){Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
     var labels=camera.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="PickupInstruction"&&t.isActiveAndEnabled).ToArray();var panels=labels.Select(t=>TelemetryReadabilityProof.ViewRect(camera,(RectTransform)t.transform.parent)).ToArray();var lines=new List<string>();int collisions=0;
     for(int i=0;i<labels.Length;i++){
      var t=labels[i];t.ForceMeshUpdate();Check(!t.enableAutoSizing&&!t.isTextTruncated&&t.fontSize==32,"Font shrunk/truncated");Check(!t.raycastTarget,"UI intercepts target");
      lines.Add($"label={t.text.Replace('\n','|')}; rect={panels[i]}");
      for(int j=i+1;j<panels.Length;j++)if(panels[i].Overlaps(panels[j])){collisions++;lines.Add($"panel overlap {i}/{j}");}
      foreach(var item in items.Where(it=>it.IsAvailable)){var p=camera.WorldToViewportPoint(item.transform.position);if(p.z>0&&panels[i].Contains(p)){collisions++;lines.Add($"covers centre {item.GetEntityId()} at {p}");}}
      foreach(string name in HudNames){var go=GameObject.Find(name);if(go==null)continue;var rect=go.GetComponent<RectTransform>();if(rect!=null&&panels[i].Overlaps(TelemetryReadabilityProof.ViewRect(camera,rect))){collisions++;lines.Add("HUD overlap "+name);}}
      Check(panels[i].xMin>=0&&panels[i].xMax<=1&&panels[i].yMin>=0&&panels[i].yMax<=1,"Offscreen label");
      foreach(char c in t.text)if(!char.IsWhiteSpace(c))Check(t.font.HasCharacter(c),"Missing glyph");
     }
     foreach(var item in items)lines.Add($"item={item.GetEntityId()} type={item.Type} available={item.IsAvailable} pos={item.transform.position} viewport={camera.WorldToViewportPoint(item.transform.position)} progress={item.CollectionProgress}");
     string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);File.WriteAllLines(dir+"/state.txt",lines);notes.Add($"{size}/{state}: labels={labels.Length}; collisions={collisions}");if(validate)Check(collisions==0,"Instruction overlap in "+state);
    }
    foreach(var state in new[]{("close-three",new Vector2(.5f,.55f)),("left",new Vector2(.2f,.55f)),("right",new Vector2(.8f,.55f)),("upper",new Vector2(.5f,.93f)),("lower",new Vector2(.5f,.10f))}){Aim(state.Item2);yield return new WaitForSecondsRealtime(.12f);Save(state.Item1);}
    Aim(new Vector2(.5f,.55f));yield return new WaitForSecondsRealtime(.1f);var target=items[1];Physics.SyncTransforms();Check(target.GetComponent<Collider>().Raycast(new Ray(camera.transform.position,target.transform.position-camera.transform.position),out var hit,100),"Collider ray missed");
    int acquired=0;void Acquired(PowerUpType t){acquired++;}power.PowerUpAcquired+=Acquired;
    try{target.OnGazeEnter(hit);target.OnGazeStay(hit,.15f);yield return new WaitForSecondsRealtime(.03f);Save("partial-gaze");float last=Time.unscaledTime;deadline=last+2;while(target.IsAvailable&&Time.unscaledTime<deadline){yield return new WaitForSecondsRealtime(.02f);float now=Time.unscaledTime;target.OnGazeStay(hit,now-last);last=now;}Check(acquired==1&&!target.IsAvailable,"Not acquired exactly once");}finally{power.PowerUpAcquired-=Acquired;}
    yield return new WaitForSecondsRealtime(.1f);Save("acquired");
    var reusePosition=origins[1]+Vector3.up*1.8f;var reuseMeteor=spawner.SpawnScenario(reusePosition,Vector3.zero,.6f,10);Check(power.TryDrop(reuseMeteor,false,0),"Reuse drop failed");spawner.StopSpawning(true);yield return new WaitForSecondsRealtime(.1f);Check(target.IsAvailable&&target.CollectionProgress==0,"Pool reuse did not clear dwell");Save("reused");
    reset.ResetExperience();yield return new WaitForSecondsRealtime(.2f);Check(power.ActiveWorldItems==0,"Reset retained items");Save("reset");
   }
   notes.Add("captureCompleted=True; validate="+validate);if(validate)notes.Add("passed=True");Directory.CreateDirectory(output);File.WriteAllLines(output+"/verification.txt",notes);
  }finally{spawner.EndScenario();reset.ResetExperience();camera.transform.rotation=rotation;router.InputSuspended=input;caster.enabled=casting;weapon.AutoFireOnLock=fire;UnityEngine.Random.state=random;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}
