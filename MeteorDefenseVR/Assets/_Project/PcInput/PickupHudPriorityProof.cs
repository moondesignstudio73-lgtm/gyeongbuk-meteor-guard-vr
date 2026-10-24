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
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class PickupHudPriorityProof {
 public static IEnumerator Run(string output,bool validate){
  var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var power=PowerUpManager.Instance;var hud=Object.FindAnyObjectByType<GameplayHudOverlayView>();var overlay=Object.FindAnyObjectByType<PcTestOverlay>();var health=Object.FindAnyObjectByType<PlayerHealth>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();var gate=overlay.GetComponent<PausePresentationGate>();
  var rotation=camera.transform.rotation;bool input=router.InputSuspended,casting=caster.enabled,fire=weapon.AutoFireOnLock;var random=UnityEngine.Random.state;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);router.InputSuspended=true;caster.enabled=false;weapon.AutoFireOnLock=false;
  var notes=new List<string>{"Same supported distinct drops as multiple-edge proof; max3 live setting. Camera-only entry/overlap/exit sweep. Actual colliders, elapsed callback dwell, paused settings UI callbacks and reset. Controlled camera rendering, not human/OS/HMD."};
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  try{
   var settings=(PowerUpSettings)typeof(PowerUpManager).GetField("settings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(power);Check(settings.maximumWorldItems==3,"Maximum changed");
   float deadline=Time.unscaledTime+25;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
   foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)}){
    reset.ResetExperience();router.InputSuspended=true;camera.transform.rotation=rotation;yield return new WaitForSecondsRealtime(1);flow.StartMainGame();yield return new WaitForSecondsRealtime(2.5f);spawner.BeginScenario(3);
    using var probe=new PcStabilityProbe(camera,size.x,size.y);Vector3 centre=default;
    for(int i=0;i<3;i++){var pos=spawner.CalculateSpawnPosition((i-1)*.3f,0,1,new Vector2(30,20));if(i==1)centre=pos;var meteor=spawner.SpawnScenario(pos,Vector3.zero,.6f,10);Check(power.TryDrop(meteor,false,i*.04f),"Drop rejected");}
    spawner.StopSpawning(true);yield return new WaitForSecondsRealtime(.1f);var items=Object.FindObjectsByType<PowerUpItem>().Where(i=>i.IsAvailable).OrderBy(i=>i.transform.position.x).ToArray();Check(items.Length==3,"Three items absent");
    var stage=GameObject.Find("StagePanel").GetComponent<RectTransform>();var frame=stage.GetComponent<ChamferedHudPanel>();
    string originalCount=hud.AsteroidValue.text,originalStage=hud.DifficultyStageValue.text;float countSize=hud.AsteroidValue.fontSize,stageSize=hud.DifficultyStageValue.fontSize;
    void Aim(float y){float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);var ray=new Vector3(0,(y-.5f)*2*tan,1);camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(ray,Vector3.up));}
    void Save(string state,bool paused=false){Canvas.ForceUpdateCanvases();gate.Apply();PcStabilityRenderDriver.RenderForProof(camera);
     Check(hud.AsteroidValue.text==originalCount&&hud.DifficultyStageValue.text==originalStage,"Mandatory HUD content changed");Check(hud.AsteroidValue.fontSize==countSize&&hud.DifficultyStageValue.fontSize==stageSize,"HUD font size changed");
     var lines=new List<string>{$"state={flow.CurrentState}; paused={router.Simulation.IsPaused}; gate={gate.IsSuspended}; settings={overlay.LookSettingsOpen}; stage={originalStage}; count={originalCount}; fill={frame.FillColor}; stageRect={TelemetryReadabilityProof.ViewRect(camera,stage)}"};
     if(validate)Check(frame.FillColor.a==1,"Top information background not opaque");
     var labels=camera.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="PickupInstruction"&&t.isActiveAndEnabled).ToArray();
     if(!paused){var rects=labels.Select(t=>TelemetryReadabilityProof.ViewRect(camera,(RectTransform)t.transform.parent)).ToArray();for(int i=0;i<labels.Length;i++){var t=labels[i];Check(t.fontSize==32&&!t.enableAutoSizing&&!t.isTextTruncated,"Instruction changed");for(int j=i+1;j<labels.Length;j++)Check(!rects[i].Overlaps(rects[j]),"Instructions overlap");foreach(string n in PickupMultipleProof.HudNames){var go=GameObject.Find(n);if(go!=null)Check(!rects[i].Overlaps(TelemetryReadabilityProof.ViewRect(camera,go.GetComponent<RectTransform>())),"Instruction covers HUD");}foreach(var item in items.Where(it=>it.IsAvailable))Check(!rects[i].Contains(camera.WorldToViewportPoint(item.transform.position)),"Instruction covers target");lines.Add($"instruction={t.text.Replace('\n','|')}; rect={rects[i]}");}}
     foreach(var item in items)lines.Add($"item={item.GetEntityId()} available={item.IsAvailable} world={item.transform.position} viewport={camera.WorldToViewportPoint(item.transform.position)} radius={item.GetComponent<SphereCollider>().radius} progress={item.CollectionProgress}");
     string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);File.WriteAllLines(dir+"/state.txt",lines);notes.Add($"{size}/{state}: "+lines[0]);
    }
    Aim(.82f);yield return new WaitForSecondsRealtime(.12f);Save("entry");Aim(.94f);yield return new WaitForSecondsRealtime(.12f);Save("overlap");Aim(.78f);yield return new WaitForSecondsRealtime(.12f);Save("exit");
    Aim(.94f);health.RestoreCheckpoint(25);typeof(SfPresentationDirector).GetField("dangerAllowedAt",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(director,0f);yield return new WaitForSecondsRealtime(.12f);Check(Object.FindObjectsByType<TextMeshPro>().Any(t=>t.name=="FlightNotice"&&t.enabled),"Critical warning missing");Save("warning");
    var priorState=flow.CurrentState;router.Simulation.Pause();yield return new WaitForSecondsRealtime(.05f);gate.Apply();Check(gate.IsSuspended&&hud.HudCanvas.GetComponent<CanvasGroup>().alpha==0,"Top HUD not hidden during pause");
    overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="LookSettings").onClick.Invoke();yield return new WaitForSecondsRealtime(.1f);Check(overlay.LookSettingsOpen&&router.Simulation.IsPaused,"Paused settings absent");Save("paused-settings",true);
    overlay.TryCloseLookSettings();router.Simulation.Resume();yield return new WaitForSecondsRealtime(.05f);Check(!gate.IsSuspended&&flow.CurrentState==priorState&&hud.HudCanvas.GetComponent<CanvasGroup>().alpha==1,"Resume changed state or failed HUD restore");
    var target=items[1];Physics.SyncTransforms();Check(target.GetComponent<Collider>().Raycast(new Ray(camera.transform.position,target.transform.position-camera.transform.position),out var hit,100),"Original collider ray failed");int acquired=0;void Acquired(PowerUpType t){acquired++;}power.PowerUpAcquired+=Acquired;
    try{target.OnGazeEnter(hit);target.OnGazeStay(hit,.15f);yield return new WaitForSecondsRealtime(.03f);Save("partial-gaze");float last=Time.unscaledTime;deadline=last+2;while(target.IsAvailable&&Time.unscaledTime<deadline){yield return new WaitForSecondsRealtime(.02f);float now=Time.unscaledTime;target.OnGazeStay(hit,now-last);last=now;}Check(acquired==1&&!target.IsAvailable,"Acquisition failed");}finally{power.PowerUpAcquired-=Acquired;}
    yield return new WaitForSecondsRealtime(.1f);Save("acquired");reset.ResetExperience();yield return new WaitForSecondsRealtime(.2f);Check(power.ActiveWorldItems==0,"Reset retained items");originalCount=hud.AsteroidValue.text;originalStage=hud.DifficultyStageValue.text;Save("reset");
   }
   notes.Add("captureCompleted=True; validate="+validate);if(validate)notes.Add("passed=True");Directory.CreateDirectory(output);File.WriteAllLines(output+"/verification.txt",notes);
  }finally{overlay.TryCloseLookSettings();router.Simulation.Resume();spawner.EndScenario();reset.ResetExperience();camera.transform.rotation=rotation;router.InputSuspended=input;caster.enabled=casting;weapon.AutoFireOnLock=fire;UnityEngine.Random.state=random;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}
