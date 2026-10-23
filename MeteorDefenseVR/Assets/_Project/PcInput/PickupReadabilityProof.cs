using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PowerUps;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class PickupReadabilityProof {
 public static IEnumerator Run(string output){
  var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();var climax=Object.FindAnyObjectByType<BossClimaxController>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var power=PowerUpManager.Instance;
  bool input=router.InputSuspended,casting=caster.enabled,fire=weapon.AutoFireOnLock;var random=UnityEngine.Random.state;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);router.InputSuspended=true;caster.enabled=false;weapon.AutoFireOnLock=false;
  var notes=new List<string>{"Actual boss destruction/drop/events; frozen boss position before death; production pickup drift/effects run normally. Seeded random for comparison. Explicit collider ray + IGazeTarget dwell, not human/OS/HMD play. Pixel originals at multiple real elapsed phases are required visual evidence."};
  void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  try{float deadline=Time.unscaledTime+25;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
   foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)}){
    reset.ResetExperience();router.InputSuspended=true;yield return new WaitForSecondsRealtime(1);flow.StartMainGame();spawner.StopSpawning(true);climax.BeginClimax();
    deadline=Time.unscaledTime+18;while(climax.ActiveBoss==null&&Time.unscaledTime<deadline)yield return null;var boss=climax.ActiveBoss;Check(boss!=null,"Boss absent");boss.enabled=false;var motion=boss.GetComponent<BossStageMotion>();if(motion!=null)motion.enabled=false;
    using var probe=new PcStabilityProbe(camera,size.x,size.y);boss.transform.position=camera.ViewportToWorldPoint(new Vector3(.5f,.6f,12));yield return new WaitForSecondsRealtime(.3f);
    UnityEngine.Random.InitState(20261024);boss.ReceiveDamage(boss.MaxHealth);float began=Time.unscaledTime;
    var item=Object.FindObjectsByType<PowerUpItem>().Single(i=>i.IsAvailable);Check(item!=null,"Actual drop absent");
    void Save(string state){Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);
     var label=item.GetComponentInChildren<TextMesh>(true);var labels=camera.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Where(t=>t.name=="PickupInstruction"&&t.gameObject.activeInHierarchy).ToArray();
     if(Object.FindAnyObjectByType<PickupInstructionOverlay>()!=null)Check(labels.Length==(item.IsAvailable?1:0),"Pickup instruction visibility mismatch");
     var lines=new List<string>{$"elapsed={Time.unscaledTime-began}; flow={flow.CurrentState}; item={item.Type}; available={item.IsAvailable}; dwell={item.CollectionProgress}; position={item.transform.position}; legacyText={label?.text.Replace('\n','|')}"};
     foreach(var t in labels){t.ForceMeshUpdate();var rect=TelemetryReadabilityProof.ViewRect(camera,t.rectTransform);Check(!t.enableAutoSizing&&!t.isTextTruncated&&t.fontSize>=32,"Instruction shrunk/truncated");foreach(char c in t.text)if(!char.IsWhiteSpace(c))Check(t.font.HasCharacter(c),"Missing pickup glyph "+c);foreach(PowerUpType type in Enum.GetValues(typeof(PowerUpType)))foreach(char c in PowerUpHudView.PlayerName(type))if(!char.IsWhiteSpace(c))Check(t.font.HasCharacter(c),"Missing item-name glyph "+c);Check(!t.raycastTarget,"Instruction intercepts gaze");Check(rect.xMin>=0&&rect.xMax<=1&&rect.yMin>=0&&rect.yMax<=1,"Instruction outside viewport");Check(!rect.Contains(camera.WorldToViewportPoint(item.transform.position)),"Instruction covers pickup aim centre");var backing=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)t.transform.parent);foreach(var ch in t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.isVisible))foreach(var v in new[]{ch.bottomLeft,ch.topRight})Check(backing.Contains(camera.WorldToViewportPoint(t.transform.TransformPoint(v))),"Glyph outside opaque backing");if(state=="gaze-progress")Check(t.text.Contains("획득 중"),"Progress text stale");lines.Add($"text={t.text.Replace('\n','|')}; font={t.fontSize}; auto={t.enableAutoSizing}; truncated={t.isTextTruncated}; rect={rect}; backing={backing}");}
     File.WriteAllLines(dir+"/state.txt",lines);notes.Add(size+"/"+state+": "+lines[0]);
    }
    foreach(float phase in new[]{.02f,.12f,.25f,.5f,1f,1.7f}){while(Time.unscaledTime-began<phase)yield return null;Save("phase-"+phase.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));}
    Physics.SyncTransforms();var collider=item.GetComponent<Collider>();Check(collider.Raycast(new Ray(camera.transform.position,item.transform.position-camera.transform.position),out var hit,100),"Real pickup ray missed");
    int acquired=0;void Acquired(PowerUpType type){if(type==item.Type)acquired++;}power.PowerUpAcquired+=Acquired;
    try{item.OnGazeEnter(hit);item.OnGazeStay(hit,.15f);yield return new WaitForSecondsRealtime(.03f);Check(item.IsAvailable&&item.CollectionProgress>0,"Partial dwell unavailable");Save("gaze-progress");
     float lastTick=Time.unscaledTime,collectDeadline=lastTick+2;while(item.IsAvailable&&Time.unscaledTime<collectDeadline){yield return new WaitForSecondsRealtime(.02f);float now=Time.unscaledTime;item.OnGazeStay(hit,now-lastTick);lastTick=now;}
     Check(!item.IsAvailable&&acquired==1,$"Acquisition not exactly once: available={item.IsAvailable}, events={acquired}, progress={item.CollectionProgress}");yield return new WaitForSecondsRealtime(.12f);Save("acquired");
    }finally{power.PowerUpAcquired-=Acquired;}
    reset.ResetExperience();yield return new WaitForSecondsRealtime(.2f);Check(power.ActiveWorldItems==0,"Reset retained pickup");Save("reset");boss.enabled=true;if(motion!=null)motion.enabled=true;
   }
   notes.Add("passed=True");Directory.CreateDirectory(output);File.WriteAllLines(output+"/verification.txt",notes);
  }finally{reset.ResetExperience();router.InputSuspended=input;caster.enabled=casting;weapon.AutoFireOnLock=fire;UnityEngine.Random.state=random;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}
