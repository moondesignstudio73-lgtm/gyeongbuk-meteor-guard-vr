using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Education;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput {
// Explicit short transition fixture; not a natural gameplay or OS-input claim.
public static class ReplayMenuProof {
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 public static IEnumerator Run(string output,bool validate){
  Directory.CreateDirectory(output);
  float deadline=Time.realtimeSinceStartup+45;
  while(Object.FindAnyObjectByType<ReadyScreenController>()==null&&Time.realtimeSinceStartup<deadline)yield return null;
  yield return null;
  var ready=Object.FindAnyObjectByType<ReadyScreenController>();
  while(!ready.CanStart&&Time.realtimeSinceStartup<deadline)yield return null;
  Check(ready.CanStart,"Ready timeout");
  var flow=GameFlowManager.Instance;var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
  var router=Object.FindAnyObjectByType<GazeInputRouter>();var overlay=Object.FindAnyObjectByType<PcTestOverlay>();
  var education=Object.FindAnyObjectByType<EducationSessionManager>();var result=Object.FindAnyObjectByType<ResultController>();
  var gaze=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();gaze.enabled=false;router.InputSuspended=true;
  Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name==name);
  deadline=Time.realtimeSinceStartup+45;
  while((Object.FindAnyObjectByType<SfPresentationDirector>().IsBootPresenting||!Button("LookSettings").gameObject.activeInHierarchy)&&Time.realtimeSinceStartup<deadline)yield return null;
  Check(Button("LookSettings").gameObject.activeInHierarchy,"Initial UI readiness timeout");
  var notes=new List<string>();var violations=new List<string>();
  void Verify(bool ok,string message){if(!ok)violations.Add(message);}
  using(var probe=new PcStabilityProbe(Camera.main,1280,720)){
   var driver=(PcStabilityRenderDriver)typeof(PcStabilityProbe).GetField("driver",Private).GetValue(probe);
   IEnumerator Capture(string name){bool done=false;Exception failure=null;Directory.CreateDirectory(Path.Combine(output,name));Action save=()=>{try{probe.SaveGameOnlyProof(Path.Combine(output,name),router);notes.Add(name+": state="+flow.CurrentState+" lobby="+lobby.Phase+" look="+Button("LookSettings").gameObject.activeInHierarchy+" audio="+Button("AudioSettings").gameObject.activeInHierarchy+" education="+Button("EducationStart").gameObject.activeInHierarchy);}catch(Exception e){failure=e;}finally{done=true;}};driver.RenderCompleted+=save;try{float limit=Time.realtimeSinceStartup+10;while(!done&&Time.realtimeSinceStartup<limit)yield return null;Check(done,"Render timeout");if(failure!=null)throw failure;}finally{driver.RenderCompleted-=save;}}
   void OpenClose(string name){Check(Button(name).gameObject.activeInHierarchy,"Expected menu entry "+name);Button(name).onClick.Invoke();Check(overlay.LookSettingsOpen&&router.UiModalOpen,"Settings did not open");Check(overlay.TryCloseLookSettings(),"Settings did not close");Check(!overlay.LookSettingsOpen&&!router.UiModalOpen,"Modal leaked after close");notes.Add(name+" open/close passed");}
   OpenClose("LookSettings");OpenClose("AudioSettings");
   education.OpenRegistration();Check(education.Phase==EducationPhase.Registration,"Ready education unavailable");education.CloseMenu();
   flow.StartMainGame();yield return null;flow.ShowResult();
   deadline=Time.realtimeSinceStartup+12;while(!result.IsVisible&&Time.realtimeSinceStartup<deadline)yield return null;Check(result.IsVisible,"Result timeout");
   Check(lobby.ChooseFromResult(false),"Base action rejected");yield return null;yield return null;
   Check(lobby.IsMenuReady,"Base menu missing");OpenClose("LookSettings");OpenClose("AudioSettings");yield return Capture("base-menu");
   lobby.Queue(HangarLobbyController.MenuAction.Replay);
   deadline=Time.realtimeSinceStartup+5;while(lobby.Phase!=HangarLobbyController.LobbyPhase.ReplayPreparing&&Time.realtimeSinceStartup<deadline)yield return null;
   Check(lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing,"Replay preparation missing");
   yield return Capture("replay-preparing");
   Check(lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing,"Preparation window missed");
   foreach(string name in new[]{"LookSettings","AudioSettings","EducationStart"})Verify(!Button(name).gameObject.activeInHierarchy,"Visible during replay: "+name);
   // Invoke the exact callbacks to test stale mouse/gaze selections at a phase boundary.
   Button("LookSettings").onClick.Invoke();Verify(!overlay.LookSettingsOpen,"Look settings accepted during replay");overlay.TryCloseLookSettings();
   Button("AudioSettings").onClick.Invoke();Verify(!overlay.AudioSettingsOpen,"Audio settings accepted during replay");overlay.TryCloseLookSettings();
   education.OpenRegistration();Verify(education.Phase==EducationPhase.Idle,"Registration accepted during replay");education.CloseMenu();
   int frames=0;deadline=Time.realtimeSinceStartup+25;
   while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<deadline){
    foreach(string name in new[]{"LookSettings","AudioSettings","EducationStart"})if(lobby.Phase==HangarLobbyController.LobbyPhase.Departing)Verify(!Button(name).gameObject.activeInHierarchy,"Visible during departure: "+name);
    if(frames++==4)yield return Capture("departure");else yield return null;
   }
   Check(flow.CurrentState==GameState.Playing,"Replay did not enter combat");yield return null;yield return Capture("combat");
   foreach(string name in new[]{"LookSettings","AudioSettings","EducationStart"})Verify(!Button(name).gameObject.activeInHierarchy,"Retained in combat: "+name);
   Verify(!router.UiModalOpen&&!router.EducationModalOpen,"Combat modal leaked");
   flow.ShowResult();deadline=Time.realtimeSinceStartup+12;while(!result.IsVisible&&Time.realtimeSinceStartup<deadline)yield return null;Check(result.IsVisible,"Second result timeout");
   Check(lobby.ChooseFromResult(false),"Second base action rejected");yield return null;yield return null;OpenClose("LookSettings");OpenClose("AudioSettings");yield return Capture("base-return");
  }
  File.WriteAllLines(Path.Combine(output,"notes.txt"),notes);File.WriteAllLines(Path.Combine(output,"violations.txt"),violations);
  File.WriteAllText(Path.Combine(output,"summary.txt"),"Explicit result/menu actions and callback boundary checks; no OS/HMD or natural return-motion claim.\nViolations="+violations.Count);
  if(validate)Check(violations.Count==0,string.Join("; ",violations));
 }
}}
