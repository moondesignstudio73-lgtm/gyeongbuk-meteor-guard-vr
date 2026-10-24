#if START_ASPECT_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;

using TMPro;

using UnityEngine;
using UnityEngine.InputSystem;

using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class StartAspectPlayerProof {
 private sealed class HeldRay:IGazeProvider { public Ray Ray;public bool IsTrackingValid=>true; public Ray GetGazeRay()=>Ray;public Vector3 GetGazeOrigin()=>Ray.origin;public Vector3 GetGazeDirection()=>Ray.direction; }
 private static class Assert {
  public static void IsTrue(bool v,string msg="Assertion failed"){if(!v)throw new System.InvalidOperationException(msg);}
  public static void IsFalse(bool v){IsTrue(!v);}
  public static void Greater(float a,float b){IsTrue(a>b);}
  public static void Less(float a,float b){IsTrue(a<b);}
  public static void GreaterOrEqual(float a,float b){IsTrue(a>=b);}
  public static void LessOrEqual(float a,float b){IsTrue(a<=b);}
 }
 public static IEnumerator Run(string output){
  yield return new WaitForSecondsRealtime(1f);

  var camera=Camera.main;var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();
  var difficulty=Object.FindAnyObjectByType<DifficultyManager>();var selection=Object.FindAnyObjectByType<DifficultySelectionView>();
  bool locked=difficulty.Settings.lockDifficultySelection,input=router.InputSuspended,casting=caster.enabled;
  var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);
  router.InputSuspended=true;caster.enabled=false;
  var originalProvider=caster.Provider;var synthetic=new HeldRay();caster.SetProvider(synthetic);
  void Aim(Transform target){synthetic.Ray=new Ray(camera.transform.position,(target.position-camera.transform.position).normalized);Physics.SyncTransforms();}
  void Away(){synthetic.Ray=new Ray(camera.transform.position,camera.transform.up);caster.Tick(0);}
  IEnumerator Dwell(Component target,float seconds){
   Aim(target.transform);float elapsed=0;
   while(elapsed<seconds){caster.Tick(.02f);Assert.IsTrue(ReferenceEquals(caster.CurrentTarget,target),"Actual gaze target mismatch: "+target.name);elapsed+=.02f;yield return new WaitForSecondsRealtime(.02f);}
  }
  try{foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)}){
   difficulty.Settings.lockDifficultySelection=false;reset.ResetExperience();router.InputSuspended=true;
   float end=Time.unscaledTime+25;while((selection.Choices[0]==null||!selection.Choices[0].isActiveAndEnabled)&&Time.unscaledTime<end)yield return null;
   Assert.IsTrue(selection.Choices.All(c=>c!=null&&c.isActiveAndEnabled));caster.enabled=false;caster.SetProvider(synthetic);router.InputSuspended=true;
   using var probe=new PcStabilityProbe(camera,size.x,size.y);yield return new WaitForSecondsRealtime(.6f);
   void Save(string state){
    Canvas.ForceUpdateCanvases();camera.GetComponent<WorldUiSafeArea>().Apply();PcStabilityRenderDriver.RenderForProof(camera);Physics.SyncTransforms();
    var lines=new List<string>{$"INSTRUMENTED START_ASPECT_QA controlled player; size={size}; selected={difficulty.Selected}; locked={difficulty.Settings.lockDifficultySelection}; not OS/HMD. Synthetic provider through actual GazeRaycaster/collider; not native mouse navigation."};
    var all=new List<(string name,Rect bounds)>();
    foreach(var choice in selection.Choices){
     var collider=choice.GetComponent<Collider>();Assert.IsTrue(collider.Raycast(new Ray(camera.transform.position,collider.bounds.center-camera.transform.position),out var hit,100),"Card collider ray misses");
     lines.Add($"choice={choice.name} progress={choice.Progress} colliderRay=True");
     foreach(var text in choice.GetComponentsInChildren<TextMeshPro>()){
      text.ForceMeshUpdate();Assert.IsFalse(text.enableAutoSizing);Assert.IsFalse(text.isTextTruncated);
      var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);float minHeight=float.PositiveInfinity;
      foreach(var ch in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible)){
       var points=new[]{ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight}.Select(v=>camera.WorldToViewportPoint(text.transform.TransformPoint(v))).ToArray();
       foreach(var p in points){Assert.Greater(p.z,0);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}minHeight=Mathf.Min(minHeight,(points.Max(p=>p.y)-points.Min(p=>p.y))*size.y);
      }
      Assert.IsFalse(float.IsInfinity(min.x));Assert.GreaterOrEqual(min.x,0);Assert.GreaterOrEqual(min.y,0);Assert.LessOrEqual(max.x,1);Assert.LessOrEqual(max.y,1);
      var r=Rect.MinMaxRect(min.x,min.y,max.x,max.y);all.Add((choice.name+"/"+text.name,r));
      lines.Add($"{choice.name}/{text.name}: font={text.fontSize}; glyphRect={r}; minGlyphPx={minHeight}; text={text.GetParsedText().Replace('\n','|')}");
     }
    }
    for(int i=0;i<all.Count;i++)for(int j=0;j<i;j++)if(all[i].bounds.Overlaps(all[j].bounds))lines.Add("GLYPH_OVERLAP "+all[j].name+" / "+all[i].name);
    string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);File.WriteAllLines(dir+"/state.txt",lines);
   }
   Away();yield return Dwell(selection.Choices[3],.8f);Assert.IsTrue(difficulty.Selected==DifficultyLevel.Hard,"Hard gaze selection");Away();
   yield return Dwell(selection.Choices[1],.34f);selection.Choices[1].SetFocus(true);Assert.Greater(selection.Choices[1].Progress,0);Assert.Less(selection.Choices[1].Progress,1);Assert.IsTrue(difficulty.Selected==DifficultyLevel.Hard,"Partial gaze changed selection");Save("selected-partial-dwell");Away();
   yield return Dwell(selection.Choices[0],.8f);Assert.IsTrue(difficulty.Selected==DifficultyLevel.Experience,"Experience gaze selection");Away();yield return new WaitForSecondsRealtime(.3f);Save("experience-selected");
   difficulty.Settings.lockDifficultySelection=true;difficulty.ApplyOperatorDefaults();yield return new WaitForSecondsRealtime(.4f);var expected=difficulty.Selected;
   yield return Dwell(selection.Choices[3],.9f);Assert.IsTrue(difficulty.Selected==expected,"Locked gaze changed selection");Assert.IsFalse(difficulty.Select(DifficultyLevel.Hard));Assert.IsTrue(selection.Choices.All(c=>!c.GetComponent<UnityEngine.UI.Button>().interactable),"Locked cards clickable");Away();Save("locked-selected");
   difficulty.Settings.lockDifficultySelection=false;difficulty.ApplyOperatorDefaults();
   var flow=GameFlowManager.Instance;var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
   flow.ShowResult();lobby.BeginReturn();float menuDeadline=Time.unscaledTime+12;while(!lobby.IsMenuReady&&Time.unscaledTime<menuDeadline)yield return null;
   Assert.IsTrue(lobby.IsMenuReady,"Prepared result returns lobby");router.InputSuspended=true;caster.enabled=false;caster.SetProvider(synthetic);
   yield return new WaitForSecondsRealtime(.6f);yield return Dwell(lobby.View.Buttons[1],.8f);Away();yield return new WaitForSecondsRealtime(.5f);
   Assert.IsTrue(lobby.Phase==HangarLobbyController.LobbyPhase.Difficulty,"Gaze opens lobby difficulty");
   Save("lobby-difficulty");
   yield return Dwell(lobby.View.Buttons[4],.8f);Away();yield return new WaitForSecondsRealtime(.5f);
   Assert.IsTrue(lobby.IsMenuReady,"Gaze back returns lobby");
   string menuDir=$"{output}/{size.x}x{size.y}/menu-return";Directory.CreateDirectory(menuDir);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(menuDir,router);
   File.WriteAllText(menuDir+"/state.txt","Actual synthetic gaze open/back passed; menu-ready=True; no physical input/HMD.\n");
  }File.WriteAllText(Path.Combine(output,"verification.txt"),"passed=True\nTwo sizes: synthetic hard/partial/experience/locked and lobby difficulty/back; no OS/HMD.\n");}finally{difficulty.Settings.lockDifficultySelection=locked;reset.ResetExperience();router.InputSuspended=input;caster.SetProvider(originalProvider);caster.enabled=casting;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}

#endif

