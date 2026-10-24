using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
// Opt-in continuous rendered evidence, never installed in ordinary play.
public static class PickupMotionProof {
 public static IEnumerator Run(string output,bool validate) {
  var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var power=PowerUpManager.Instance;var health=Object.FindAnyObjectByType<PlayerHealth>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();
  var rotation=camera.transform.rotation;var random=UnityEngine.Random.state;float timestep=Time.captureDeltaTime;bool input=router.InputSuspended,casting=caster.enabled,fire=weapon.AutoFireOnLock;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);router.InputSuspended=true;caster.enabled=false;weapon.AutoFireOnLock=false;
  var notes=new List<string>{"Continuous synthetic camera / fixed simulation dt=1/60. Original live cap3/drop positions/drift/lifetime. Render/readback overhead is not FPS performance. No natural human/OS/HMD."};
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  string F(float value)=>value.ToString("F5",CultureInfo.InvariantCulture);
  Vector2 boundary=new Vector2(.5f,.8f);float biggest=0;string boundaryPath=Environment.GetEnvironmentVariable("PICKUP_MOTION_BOUNDARY");
  if(!string.IsNullOrEmpty(boundaryPath)){var p=File.ReadAllText(boundaryPath).Trim().Split(',');boundary=new Vector2(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture));}
  try {
   float deadline=Time.unscaledTime+25;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
   foreach(string phase in new[]{"sweep","boundary"}) {
    Time.captureDeltaTime=0;reset.ResetExperience();router.InputSuspended=true;camera.transform.rotation=rotation;yield return new WaitForSecondsRealtime(1);flow.StartMainGame();yield return new WaitForSecondsRealtime(2.5f);spawner.BeginScenario(3);
    using var probe=new PcStabilityProbe(camera,800,600);Vector3 centre=default;
    for(int i=0;i<3;i++){var pos=spawner.CalculateSpawnPosition((i-1)*.3f,0,1,new Vector2(30,20));if(i==1)centre=pos;var meteor=spawner.SpawnScenario(pos,Vector3.zero,.6f,10);Check(power.TryDrop(meteor,false,i*.04f),"Drop rejected");}
    spawner.StopSpawning(true);yield return null;var items=Object.FindObjectsByType<PowerUpItem>().Where(i=>i.IsAvailable).OrderBy(i=>i.transform.position.x).ToArray();Check(items.Length==3,"Live3 missing");
    var component=camera.GetComponent<PickupInstructionOverlay>();var entries=(IEnumerable)typeof(PickupInstructionOverlay).GetField("entries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(component);var pairs=new List<(PowerUpItem item,RectTransform panel,TextMeshProUGUI text)>();
    foreach(var entry in entries){var type=entry.GetType();var item=(PowerUpItem)type.GetField("item").GetValue(entry);if(items.Contains(item))pairs.Add((item,(RectTransform)type.GetField("panel").GetValue(entry),(TextMeshProUGUI)type.GetField("text").GetValue(entry)));}
    Check(pairs.Count==3,"Panels missing");pairs=pairs.OrderBy(p=>p.item.transform.position.x).ToList();
    string dir=output+"/"+phase;Directory.CreateDirectory(dir);var csv=new List<string>{"frame,time,aimX,aimY,item,x,y,w,h,jumpPx,targetX,targetY,progress,warning,text"};var prev=new Vector2[3];int jumps=0,collisions=0;float maxJump=0;
    Time.captureDeltaTime=1f/60;
    for(int frame=0;frame<240;frame++) {
     float t=frame/239f;Vector2 aim=phase=="sweep"?new Vector2(.5f+.06f*Mathf.Sin(t*2*Mathf.PI),Mathf.Lerp(.65f,.94f,t)):boundary+new Vector2(.006f*Mathf.Sin(frame*Mathf.PI/20),.006f*Mathf.Sin(frame*Mathf.PI/20));
     float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);var ray=new Vector3((aim.x-.5f)*2*tan*camera.aspect,(aim.y-.5f)*2*tan,1);camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(ray,Vector3.up));
     // One real presentation event during the boundary sweep; it expires normally.
     if(phase=="boundary"&&frame==60){health.RestoreCheckpoint(25);typeof(SfPresentationDirector).GetField("dangerAllowedAt",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(director,0f);}
     yield return null;Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
     var rectangles=pairs.Select(p=>TelemetryReadabilityProof.ViewRect(camera,p.panel)).ToArray();bool warning=GameObject.Find("LowHealthNoticeBacking")?.GetComponent<UnityEngine.UI.Image>()?.enabled==true;
     for(int i=0;i<3;i++) {
      var p=pairs[i];Check(p.item.IsAvailable,"Item expired during controlled path");var r=rectangles[i];var pos=Vector2.Scale(r.center,new Vector2(800,600));float jump=frame==0?0:Vector2.Distance(pos,prev[i]);prev[i]=pos;maxJump=Mathf.Max(maxJump,jump);if(jump>40){jumps++;if(phase=="sweep"&&jump>biggest&&string.IsNullOrEmpty(boundaryPath)){biggest=jump;boundary=aim;}}
      Check(p.text.fontSize==32&&!p.text.enableAutoSizing&&!p.text.isTextTruncated,"Text altered");
      for(int j=i+1;j<3;j++)if(r.Overlaps(rectangles[j]))collisions++;
      foreach(var item in items)if(r.Contains(camera.WorldToViewportPoint(item.transform.position)))collisions++;
      foreach(string name in PickupMultipleProof.HudNames){var go=GameObject.Find(name);if(go!=null){var image=go.GetComponent<UnityEngine.UI.Image>();if(image==null||image.enabled){var rt=go.GetComponent<RectTransform>();if(rt!=null&&r.Overlaps(TelemetryReadabilityProof.ViewRect(camera,rt)))collisions++;}}}
      if(r.xMin<0||r.xMax>1||r.yMin<0||r.yMax>1)collisions++;
      var target=camera.WorldToViewportPoint(p.item.transform.position);csv.Add($"{frame},{F(frame/60f)},{F(aim.x)},{F(aim.y)},{i},{F(r.x)},{F(r.y)},{F(r.width)},{F(r.height)},{F(jump)},{F(target.x)},{F(target.y)},{F(p.item.CollectionProgress)},{warning},{p.text.text.Replace('\n','|')}");
     }
     string frameDir=dir+"/"+frame.ToString("D4");Directory.CreateDirectory(frameDir);probe.SaveGameOnlyProof(frameDir,router);
    }
    File.WriteAllLines(dir+"/trace.csv",csv);File.WriteAllText(output+"/boundary.txt",F(boundary.x)+","+F(boundary.y));notes.Add($"{phase}: frames=240; jumpsOver40px={jumps}; maxJumpPx={F(maxJump)}; geometryViolations={collisions}");
    if(validate)Check(collisions==0,"Continuous geometry violation");
    if(phase=="boundary"&&validate){
     Time.captureDeltaTime=0;
     var layout=typeof(PickupInstructionOverlay).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
     for(int n=0;n<20;n++)layout.Invoke(component,null);
     var timer=System.Diagnostics.Stopwatch.StartNew();for(int n=0;n<600;n++)layout.Invoke(component,null);timer.Stop();
     notes.Add($"600 warmed live3 layout invocations: totalMs={timer.Elapsed.TotalMilliseconds:F3}; meanMs={timer.Elapsed.TotalMilliseconds/600:F5}; includes reflection overhead; no rendering/readback; stationary safe placement, not full FPS benchmark.");
     void SaveRegression(string state,int expected){
      Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
      var labels=camera.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="PickupInstruction"&&t.isActiveAndEnabled).ToArray();Check(labels.Length==expected,"Wrong label count after "+state);
      var rects=labels.Select(t=>TelemetryReadabilityProof.ViewRect(camera,(RectTransform)t.transform.parent)).ToArray();
      for(int i=0;i<labels.Length;i++){Check(labels[i].fontSize==32&&!labels[i].enableAutoSizing&&!labels[i].isTextTruncated,"Regression text changed");for(int j=i+1;j<labels.Length;j++)Check(!rects[i].Overlaps(rects[j]),"Regression label overlap");foreach(var item in items.Where(it=>it.IsAvailable))Check(!rects[i].Contains(camera.WorldToViewportPoint(item.transform.position)),"Regression target covered");foreach(string name in PickupMultipleProof.HudNames){var go=GameObject.Find(name);if(go!=null){var image=go.GetComponent<UnityEngine.UI.Image>();if(image==null||image.enabled)Check(!rects[i].Overlaps(TelemetryReadabilityProof.ViewRect(camera,go.GetComponent<RectTransform>())),"Regression HUD covered");}}}
      string rd=output+"/regression/"+state;Directory.CreateDirectory(rd);probe.SaveGameOnlyProof(rd,router);File.WriteAllLines(rd+"/state.txt",labels.Select(t=>t.text.Replace('\n','|')));notes.Add(state+": expectedLabels="+expected);
     }
     var target=items[1];Physics.SyncTransforms();Check(target.GetComponent<Collider>().Raycast(new Ray(camera.transform.position,target.transform.position-camera.transform.position),out var hit,100),"Original collider missed");int acquired=0;void Acquired(PowerUpType type){acquired++;}power.PowerUpAcquired+=Acquired;
     try{target.OnGazeEnter(hit);target.OnGazeStay(hit,.15f);yield return new WaitForSecondsRealtime(.03f);SaveRegression("partial-gaze",3);float last=Time.unscaledTime;deadline=last+2;while(target.IsAvailable&&Time.unscaledTime<deadline){yield return new WaitForSecondsRealtime(.02f);float now=Time.unscaledTime;target.OnGazeStay(hit,now-last);last=now;}Check(acquired==1&&!target.IsAvailable,"Acquisition failed");}finally{power.PowerUpAcquired-=Acquired;}
     yield return new WaitForSecondsRealtime(.05f);SaveRegression("acquired",2);
     var reuse=spawner.SpawnScenario(centre+Vector3.up*1.8f,Vector3.zero,.6f,10);Check(power.TryDrop(reuse,false,0),"Reuse rejected");spawner.StopSpawning(true);yield return new WaitForSecondsRealtime(.05f);Check(target.IsAvailable&&target.CollectionProgress==0,"Reused item retained dwell");SaveRegression("reused",3);
     reset.ResetExperience();yield return new WaitForSecondsRealtime(.1f);Check(power.ActiveWorldItems==0,"Reset retained items");SaveRegression("reset",0);
    }
   }
   Directory.CreateDirectory(output);File.WriteAllLines(output+"/verification.txt",notes);File.AppendAllText(output+"/verification.txt","\ncaptureCompleted=True\n"+(validate?"passed=True\n":""));
  }finally{Time.captureDeltaTime=timestep;spawner.EndScenario();reset.ResetExperience();camera.transform.rotation=rotation;router.InputSuspended=input;caster.enabled=casting;weapon.AutoFireOnLock=fire;UnityEngine.Random.state=random;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}
