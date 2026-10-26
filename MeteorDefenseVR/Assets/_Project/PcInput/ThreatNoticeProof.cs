using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class ThreatNoticeProof {
 public static IEnumerator Run(string output,bool validate) {
  var camera=Camera.main;var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
  var director=Object.FindAnyObjectByType<SfPresentationDirector>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var health=Object.FindAnyObjectByType<PlayerHealth>();
  var weapon=Object.FindAnyObjectByType<LaserWeapon>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
  bool input=router.InputSuspended,fire=weapon.AutoFireOnLock,casting=caster.enabled;var rotation=camera.transform.localRotation;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);
  router.InputSuspended=true;caster.enabled=false;weapon.AutoFireOnLock=false;
  var notes=new List<string>{"Controlled real pooled target upper-edge sweep, frozen movement plus explicit viewport samples; not natural play or OS/HMD. Direction/distance derive from actual position; original reported image retained separately."};
  var age=typeof(SfPresentationDirector).GetField("noticeAge",BindingFlags.Instance|BindingFlags.NonPublic);
  var cooldown=typeof(SfPresentationDirector).GetField("dangerAllowedAt",BindingFlags.Instance|BindingFlags.NonPublic);
  void Check(bool ok,string message){if(validate&&!ok)throw new InvalidOperationException(message);}
  float deadline=Time.unscaledTime+25;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
  try {foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)})foreach(float pitch in new[]{0f,30f}) {
   reset.ResetExperience();router.InputSuspended=true;yield return new WaitForSecondsRealtime(2);
   GameFlowManager.Instance.StartMainGame();spawner.BeginScenario(1);health.RestoreCheckpoint(100);camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
   using var probe=new PcStabilityProbe(camera,size.x,size.y);
   Vector3 position=camera.ViewportToWorldPoint(new Vector3(.6f,1.12f,25));
   var meteor=spawner.SpawnScenario(position,(spawner.ShipCenter-position).normalized*8,3,10);meteor.enabled=false;
   var notice=Object.FindObjectsByType<TextMeshPro>().Single(t=>t.name=="FlightNotice");
   var backing=camera.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(t=>t.name=="LowHealthNoticeBacking");
   var indicator=Object.FindAnyObjectByType<ThreatIndicatorView>();
   string folder=$"{output}/{size.x}x{size.y}/"+(pitch==0?"space":"earth");
   Vector2 original=default;float reservedX=0;int overlapCount=0;
   void Save(string state,bool critical) {
    Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
    var label=GameObject.Find("ThreatIndicators").GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name.StartsWith("Threat"));
    Rect a=TelemetryReadabilityProof.ViewRect(camera,label.rectTransform),b=TelemetryReadabilityProof.ViewRect(camera,backing.rectTransform);
    bool overlap=critical&&a.Overlaps(b);if(overlap)overlapCount++;
    Check(!overlap,"Threat indicator overlaps critical notice at "+state);
    Check(indicator.MostUrgentThreat==meteor,"Threat association changed");
    Check(label.text.Contains(Mathf.RoundToInt(Vector3.Distance(camera.transform.position,meteor.transform.position))+"m"),"Distance missing or stale");
    Vector3 local=Quaternion.Inverse(camera.transform.rotation)*(meteor.transform.position-camera.transform.position);
    Check(label.text.Contains(ThreatProjection.Arrow(local)),"Direction no longer matches target");
    Check(!label.enableAutoSizing&&Mathf.Approximately(label.fontSize,21),"Font changed");
    Check((meteor.transform.position-position).sqrMagnitude<.0001f,"Presentation moved target");
    Check(a.xMin>=0&&a.xMax<=1&&a.yMin>=0&&a.yMax<=1,"Label escaped viewport");
    string dir=folder+"/"+state;Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);
    File.WriteAllText(dir+"/state.txt",$"age={age.GetValue(director)} alpha={notice.color.a} critical={critical} overlap={overlap} target={meteor.transform.position} projected={camera.WorldToViewportPoint(meteor.transform.position)} label={label.text} labelRect={a} noticeRect={b} labelPosition={label.rectTransform.anchoredPosition}");
    if(state=="entry")reservedX=label.rectTransform.anchoredPosition.x;
    // Side reservation is relevant only while this upper-edge path shares the notice lane.
    if(critical&&a.yMin<b.yMax&&a.yMax>b.yMin)Check(Mathf.Abs(reservedX-label.rectTransform.anchoredPosition.x)<.5f,"Indicator changed its reserved side while target crossed centre");
    if(state=="before-warning")original=label.rectTransform.anchoredPosition;
    if(state=="restored")Check(Vector2.Distance(original,label.rectTransform.anchoredPosition)<3,"Indicator did not restore tracking position");
   }
   yield return new WaitForSecondsRealtime(2);Save("before-warning",false);
   health.RestoreCheckpoint(25);cooldown.SetValue(director,0f);yield return new WaitForSecondsRealtime(.05f);Save("entry",true);
   yield return new WaitForSecondsRealtime(.15f);Save("hold",true);
   position=camera.ViewportToWorldPoint(new Vector3(.46f,1.12f,25));meteor.transform.position=position;meteor.SetMovementDirection(spawner.ShipCenter-position);
   yield return new WaitForSecondsRealtime(.13f);Save("cross-left",true);
   position=camera.ViewportToWorldPoint(new Vector3(.64f,1.12f,25));meteor.transform.position=position;meteor.SetMovementDirection(spawner.ShipCenter-position);
   yield return new WaitForSecondsRealtime(.13f);Save("cross-right",true);
   while((float)age.GetValue(director)<.95f)yield return null;Save("fade",true);
   position=camera.ViewportToWorldPoint(new Vector3(.6f,1.12f,25));meteor.transform.position=position;meteor.SetMovementDirection(spawner.ShipCenter-position);
   while((float)age.GetValue(director)<1.5f)yield return null;Save("restored",false);Check(!notice.enabled&&!backing.enabled,"Warning lingering");
   // A ray to the real target still intersects its actual collider, independent of label relocation.
   Physics.SyncTransforms();var collider=meteor.GetComponentInChildren<Collider>();
   Check(collider.Raycast(new Ray(camera.transform.position,meteor.transform.position-camera.transform.position),out var hit,100),"Real target ray failed");
   var lockTarget=meteor.GetComponent<GazeLockOnTarget>();lockTarget.OnGazeEnter(default);lockTarget.OnGazeStay(default,.1f);Check(lockTarget.State==LockOnState.Focusing||lockTarget.IsLocked,"Target focus failed");lockTarget.ForceRelease();
   notes.Add($"{size}/{pitch}: overlaps={overlapCount}; target ray/focus, direction/distance, fixed font and restored tracking checked; validate={validate}");
   meteor.enabled=true;spawner.EndScenario();reset.ResetExperience();
  }
  Directory.CreateDirectory(output);notes.Add("passed=True");File.WriteAllLines(output+"/verification.txt",notes);
  } finally {spawner.EndScenario();reset.ResetExperience();camera.transform.localRotation=rotation;router.InputSuspended=input;caster.enabled=casting;weapon.AutoFireOnLock=fire;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}
