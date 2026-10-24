using MeteorDefenseVR.Core;

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
public static class DifficultyLockInstructionProof {
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
  var difficulty=Object.FindAnyObjectByType<DifficultyManager>();var selection=Object.FindAnyObjectByType<DifficultySelectionView>();var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var flow=GameFlowManager.Instance;
  var originalDefault=difficulty.Settings.defaultDifficulty;
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
  try{foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)}){
   difficulty.Settings.lockDifficultySelection=false;reset.ResetExperience();router.InputSuspended=true;
   float end=Time.unscaledTime+25;while((selection.Choices[0]==null||!selection.Choices[0].isActiveAndEnabled)&&Time.unscaledTime<end)yield return null;
   Assert.IsTrue(selection.Choices.All(c=>c!=null&&c.isActiveAndEnabled));caster.enabled=false;caster.SetProvider(synthetic);router.InputSuspended=true;
   using var probe=new PcStabilityProbe(camera,size.x,size.y);yield return new WaitForSecondsRealtime(.6f);
   void Save(string state){
    Canvas.ForceUpdateCanvases();camera.GetComponent<WorldUiSafeArea>().Apply();PcStabilityRenderDriver.RenderForProof(camera);Physics.SyncTransforms();
    var lines=new List<string>{$"Explicit opt-in lock-instruction QA; verify enclosing build identity; size={size}; selected={difficulty.Selected}; locked={difficulty.Settings.lockDifficultySelection}; not OS/HMD. Synthetic provider through actual GazeRaycaster/collider; not native mouse navigation."};
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
    bool inLobby=lobby.Phase==HangarLobbyController.LobbyPhase.Difficulty;
    var instructionSource=selection.transform.Find("ReadyDecorations/ReadyInstructions").GetComponent<TextMesh>();
    var prompt=inLobby?lobby.View.DifficultyAnchor.parent.Find("Title").GetComponent<TextMeshPro>():instructionSource.GetComponent<WorldTextPresentation>().Display;
    string expected=DifficultyConfig.SelectionInstruction(difficulty.Settings.lockDifficultySelection,difficulty.Selected);
    prompt.ForceMeshUpdate();Assert.IsTrue(prompt.gameObject.activeInHierarchy,"Instruction not visible");Assert.IsTrue(prompt.GetParsedText()==expected,"Instruction mismatch: "+prompt.GetParsedText()+" expected "+expected);Assert.IsFalse(prompt.isTextTruncated);
    var pmin=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var pmax=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
    foreach(var ch in prompt.textInfo.characterInfo.Take(prompt.textInfo.characterCount).Where(c=>c.isVisible))foreach(var corner in new[]{ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight}){var p=camera.WorldToViewportPoint(prompt.transform.TransformPoint(corner));Assert.Greater(p.z,0);pmin=Vector2.Min(pmin,p);pmax=Vector2.Max(pmax,p);}
    Assert.GreaterOrEqual(pmin.x,0);Assert.GreaterOrEqual(pmin.y,0);Assert.LessOrEqual(pmax.x,1);Assert.LessOrEqual(pmax.y,1);
    var promptRect=Rect.MinMaxRect(pmin.x,pmin.y,pmax.x,pmax.y);
    Assert.IsTrue(all.All(item=>!item.bounds.Overlaps(promptRect)),"Prompt/card glyph overlap");
    lines.Add($"PROMPT: font={prompt.fontSize}; text={prompt.GetParsedText()}; rect={promptRect}; selected={difficulty.Selected}; locked={difficulty.Settings.lockDifficultySelection}; CanSelect={difficulty.CanSelect}");
    string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);probe.SaveGameOnlyProof(dir,router);File.WriteAllLines(dir+"/state.txt",lines);
   }
   var levels=size.x==1280?new[]{DifficultyLevel.Experience,DifficultyLevel.Easy,DifficultyLevel.Normal,DifficultyLevel.Hard}:new[]{DifficultyLevel.Hard};
   foreach(var level in levels){
    difficulty.Settings.defaultDifficulty=level;difficulty.Settings.lockDifficultySelection=true;difficulty.ApplyOperatorDefaults();yield return new WaitForSecondsRealtime(.2f);
    Assert.IsTrue(difficulty.Selected==level,"Operator default mismatch");yield return Dwell(selection.Choices[1],.8f);Away();yield return new WaitForSecondsRealtime(.1f);
    Assert.IsTrue(difficulty.Selected==level,"Locked dwell changed selected");Assert.IsTrue(selection.Choices.All(c=>!c.GetComponent<UnityEngine.UI.Button>().interactable),"Locked choice interactable");Save("start-locked-"+level);
   }
   difficulty.Settings.lockDifficultySelection=false;difficulty.ApplyOperatorDefaults();yield return new WaitForSecondsRealtime(2f);
   yield return Dwell(selection.Choices[1],.34f);Assert.IsTrue(difficulty.Selected==DifficultyLevel.Hard,"Partial selected");Assert.Greater(selection.Choices[1].Progress,0);Assert.Less(selection.Choices[1].Progress,1);Save("start-unlocked");yield return Dwell(selection.Choices[1],.5f);Away();Assert.IsTrue(difficulty.Selected==DifficultyLevel.Easy && flow.CurrentState==GameState.Boot,"Unlocked gaze selection/start rule");
   flow.ShowResult();lobby.BeginReturn();float endMenu=Time.unscaledTime+12;while(!lobby.IsMenuReady&&Time.unscaledTime<endMenu)yield return null;Assert.IsTrue(lobby.IsMenuReady,"Lobby prepare");
   router.InputSuspended=true;caster.enabled=false;caster.SetProvider(synthetic);yield return new WaitForSecondsRealtime(.6f);yield return Dwell(lobby.View.Buttons[1],.8f);Away();yield return new WaitForSecondsRealtime(.5f);Assert.IsTrue(lobby.Phase==HangarLobbyController.LobbyPhase.Difficulty,"Open lobby difficulty");
   foreach(var level in levels){
    difficulty.Settings.lockDifficultySelection=false;difficulty.SetLobbySelection(true);difficulty.Select(level);difficulty.Settings.lockDifficultySelection=true;difficulty.SetLobbySelection(true);yield return new WaitForSecondsRealtime(.2f);
    yield return Dwell(selection.Choices[1],.8f);Away();yield return new WaitForSecondsRealtime(.1f);Assert.IsTrue(difficulty.Selected==level,"Lobby lock changed value");Save("lobby-locked-"+level);
   }
   difficulty.Settings.lockDifficultySelection=false;difficulty.SetLobbySelection(false);yield return new WaitForSecondsRealtime(.2f);Assert.IsFalse(difficulty.CanSelect);Save("lobby-unlocked-temporarily-unavailable");
   difficulty.SetLobbySelection(true);yield return new WaitForSecondsRealtime(.5f);yield return Dwell(selection.Choices[0],.8f);Away();Assert.IsTrue(difficulty.Selected==DifficultyLevel.Experience,"Lobby unlock gaze selection");
   yield return Dwell(lobby.View.Buttons[4],.8f);Away();yield return new WaitForSecondsRealtime(.5f);Assert.IsTrue(lobby.IsMenuReady,"Gaze menu return");
   string menuDir=$"{output}/{size.x}x{size.y}/menu-return";Directory.CreateDirectory(menuDir);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(menuDir,router);File.WriteAllText(menuDir+"/state.txt","Gaze menu return passed; selected="+difficulty.Selected+"; lock="+difficulty.Settings.lockDifficultySelection);
  }File.WriteAllText(Path.Combine(output,"verification.txt"),"passed=True\nFour sizes: actual operator lock defaults, unlock, temporary unavailability and synthetic gaze/back; no OS/HMD.\n");}finally{difficulty.Settings.defaultDifficulty=originalDefault;difficulty.Settings.lockDifficultySelection=locked;reset.ResetExperience();router.InputSuspended=input;caster.SetProvider(originalProvider);caster.enabled=casting;foreach(var d in devices)InputSystem.EnableDevice(d);}
 }
}}





