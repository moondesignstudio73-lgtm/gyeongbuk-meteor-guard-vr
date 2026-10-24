using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class ExperienceTipProof {
 sealed class RayProvider:IGazeProvider { public Ray Ray;public bool IsTrackingValid=>true;public Ray GetGazeRay()=>Ray;public Vector3 GetGazeOrigin()=>Ray.origin;public Vector3 GetGazeDirection()=>Ray.direction; }
 static float Remaining(DifficultySelectionView view)=>(float)typeof(DifficultySelectionView).GetField("extremeNoticeRemaining",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
 static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
 public static IEnumerator Run(string output,string scenario,int width,int height,bool validate){
  Directory.CreateDirectory(output);var timeline=new List<string>{"Fresh scene/view; private timer read-only, never reset. Synthetic gaze through original colliders, not OS/HMD.","time,frame,panel,tip,remaining,instruction,locked,selected,phase"};
  var view=Object.FindAnyObjectByType<DifficultySelectionView>();
  float deadline=Time.unscaledTime+30;while((view==null||view.Choices[0]==null)&&Time.unscaledTime<deadline){yield return null;view=Object.FindAnyObjectByType<DifficultySelectionView>();}
  Check(view!=null&&view.Choices[0]!=null,"View initialization");
  var manager=view.GetComponent<DifficultyManager>();var lobby=view.GetComponent<HangarLobbyController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();var camera=Camera.main;
  var panel=view.Choices[0].transform.parent;var tip=panel.Find("ExtremeNotice").GetComponent<TextMeshPro>();var instruction=view.transform.Find("ReadyDecorations/ReadyInstructions").GetComponent<WorldTextPresentation>().Display;
  bool input=router.InputSuspended,casting=caster.enabled;var previous=caster.Provider;var ray=new RayProvider();var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);router.InputSuspended=true;caster.enabled=false;caster.SetProvider(ray);
  float began=Time.unscaledTime;float hiddenFirst=-1,shownFirst=-1;bool missed=false,overlap=false;var instance=view.GetEntityId();
  void Record(){bool shown=panel.gameObject.activeInHierarchy;if(!shown&&hiddenFirst<0)hiddenFirst=Time.unscaledTime-began;if(shown&&shownFirst<0)shownFirst=Time.unscaledTime-began;timeline.Add($"{Time.unscaledTime-began:F4},{Time.frameCount},{shown},{tip.gameObject.activeInHierarchy},{Remaining(view):F4},{instruction.gameObject.activeInHierarchy},{manager.Settings.lockDifficultySelection},{manager.Selected},{lobby.Phase}");}
  void Away(){ray.Ray=new Ray(camera.transform.position,camera.transform.up);caster.Tick(0);}
  IEnumerator Dwell(Component target,float seconds){ray.Ray=new Ray(camera.transform.position,(target.transform.position-camera.transform.position).normalized);Physics.SyncTransforms();float t=0;while(t<seconds){caster.Tick(.02f);Check(ReferenceEquals(caster.CurrentTarget,target),"Gaze target "+target.name);Record();t+=.02f;yield return new WaitForSecondsRealtime(.02f);}}
  using var probe=new PcStabilityProbe(camera,width,height);
  Rect Bounds(TextMeshPro text){text.ForceMeshUpdate();Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;foreach(var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible))foreach(var corner in new[]{c.bottomLeft,c.topLeft,c.topRight,c.bottomRight}){var p=camera.WorldToViewportPoint(text.transform.TransformPoint(corner));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
  void Save(string state){Canvas.ForceUpdateCanvases();camera.GetComponent<WorldUiSafeArea>().Apply();PcStabilityRenderDriver.RenderForProof(camera);Record();bool visibleTip=tip.gameObject.activeInHierarchy;var prompt=lobby.Phase==HangarLobbyController.LobbyPhase.Difficulty?lobby.View.DifficultyAnchor.parent.Find("Title").GetComponent<TextMeshPro>():instruction;bool simultaneous=visibleTip&&prompt.gameObject.activeInHierarchy;bool intersects=simultaneous&&Bounds(tip).Overlaps(Bounds(prompt));overlap|=intersects;var folder=Path.Combine(output,state);Directory.CreateDirectory(folder);probe.SaveGameOnlyProof(folder,router);File.WriteAllText(Path.Combine(folder,"state.txt"),$"instance={instance}; scenario={scenario}; {width}x{height}; panel={panel.gameObject.activeInHierarchy}; tip={visibleTip}; remaining={Remaining(view)}; instruction={prompt.gameObject.activeInHierarchy}; text={prompt.text}; simultaneous={simultaneous}; glyphOverlap={intersects}; firstHidden={hiddenFirst}; firstShown={shownFirst}\n");}
  IEnumerator Capture(string state){
   // Sample the rendered frame after all normal LateUpdate presenters, never mid-update.
   var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();bool captured=false;Exception failure=null;
   Action completed=()=>{try{Save(state);}catch(Exception error){failure=error;}captured=true;};
   driver.RenderCompleted+=completed;
   try{while(!captured)yield return null;}finally{driver.RenderCompleted-=completed;}
   if(failure!=null)throw failure;
  }
  try{
   Record();while(!panel.gameObject.activeInHierarchy&&Time.unscaledTime<deadline){Record();yield return null;}Check(panel.gameObject.activeInHierarchy,"First visible timeout");Record();
   if(scenario=="locked-default"){
    missed=!tip.gameObject.activeInHierarchy;yield return Capture("first-visible");yield return new WaitForSecondsRealtime(.2f);yield return Capture("visible-200ms");
    if(validate){Check(manager.Selected==DifficultyLevel.Experience&&manager.Settings.lockDifficultySelection,"Actual locked Experience default");Check(instruction.gameObject.activeInHierarchy,"Fixed instruction hidden");Check(!missed,"Tip consumed before first visibility");}
    yield return Dwell(view.Choices[1],.8f);Check(manager.Selected==DifficultyLevel.Experience,"Lock rejection");Away();
   }else{
    yield return new WaitForSecondsRealtime(.6f);
    if(scenario=="lobby-select"){
     GameFlowManager.Instance.ShowResult();lobby.BeginReturn();deadline=Time.unscaledTime+15;while(!lobby.IsMenuReady&&Time.unscaledTime<deadline){Record();yield return null;}Check(lobby.IsMenuReady,"Lobby menu");router.InputSuspended=true;caster.enabled=false;caster.SetProvider(ray);yield return new WaitForSecondsRealtime(.6f);yield return Dwell(lobby.View.Buttons[1],.8f);Away();yield return new WaitForSecondsRealtime(.4f);Check(lobby.Phase==HangarLobbyController.LobbyPhase.Difficulty,"Lobby difficulty");
    }
    yield return Dwell(view.Choices[0],.8f);Away();Check(manager.Selected==DifficultyLevel.Experience,"Experience actual gaze selection");yield return Capture("selected-immediate");yield return new WaitForSecondsRealtime(.2f);yield return Capture("selected-200ms");if(validate)Check(tip.gameObject.activeInHierarchy,"Selected tip missing");
   }
   float until=Time.unscaledTime+2;while(Time.unscaledTime<until){Record();yield return null;}yield return Capture("expired");if(validate)Check(!tip.gameObject.activeInHierarchy,"Tip expiry");
   if(scenario=="lobby-select"){yield return Dwell(lobby.View.Buttons[4],.8f);Away();yield return new WaitForSecondsRealtime(.4f);Check(lobby.IsMenuReady,"Menu return");yield return Capture("menu-return");}
   if(validate)Check(!overlap,"Tip/prompt glyph overlap");
   File.WriteAllText(Path.Combine(output,"summary.txt"),$"completed=True; validate={validate}; missedFirstTip={missed}; glyphOverlap={overlap}; instance={instance}\n");
  }finally{File.WriteAllLines(Path.Combine(output,"timeline.csv"),timeline);router.InputSuspended=input;caster.SetProvider(previous);caster.enabled=casting;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}
}





