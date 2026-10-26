using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
 public static class PresentationReviewProof
 {
  public static IEnumerator Run(string output,bool validate,bool screensOnly=false)
  {
   Directory.CreateDirectory(output);
   while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;
   yield return null;yield return null;
   var router=Object.FindAnyObjectByType<GazeInputRouter>();var flow=GameFlowManager.Instance;
   var reset=Object.FindAnyObjectByType<OperationsResetController>();var manager=Object.FindAnyObjectByType<DifficultyManager>();
   var launch=Object.FindAnyObjectByType<LaunchSequenceController>();var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
   var result=Object.FindAnyObjectByType<ResultController>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
   var overlay=Object.FindAnyObjectByType<PcTestOverlay>();var camera=Camera.main;
   bool originalLock=manager.Settings.lockDifficultySelection;
   var room=GameObject.Find("LaunchSystem").transform.Find("HangarVisuals");
   var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);
   var mouse=InputSystem.AddDevice<Mouse>();var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
   var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   InputSystem.EnableDevice(mouse);mouse.MakeCurrent();
   Vector2 queued=Vector2.zero;int aimFrame=-1;
   void Aim(GazeLockOnTarget target){if(aimFrame==Time.frameCount)return;aimFrame=Time.frameCount;queued=Vector2.zero;if(router.Simulation.IsLooking&&target!=null){var aim=target.transform.position-camera.transform.position;var wanted=new Vector2(Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,-Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg);queued=new Vector2(Mathf.DeltaAngle(router.Simulation.RequestedAngles.x,wanted.x)/(.1f*router.Simulation.Sensitivity),(wanted.y-router.Simulation.RequestedAngles.y)/(.09f*router.Simulation.Sensitivity)*(router.Simulation.InvertY?1:-1));}InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.45f),delta=queued});}
   var notes=new List<string>{screensOnly?"UI gallery uses explicit state changes, menu actions and max-score fixture. No launch continuity, OS/HMD or natural whole-round claim.":"Initial start uses StartExperience and synthetic relative InputSystem aim. Replay result is forced then original replay action invoked. UI gallery uses explicit states/menu actions and max-score fixture. No OS/HMD or natural whole-round claim."};
   IEnumerator Capture(PcStabilityProbe probe,string folder)
   {
    var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();bool done=false;Exception failure=null;
    Action save=()=>{try{Directory.CreateDirectory(folder);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(folder,router);
     string stateName=Path.GetFileName(folder);
     File.WriteAllText(Path.Combine(folder,"state.txt"),$"state={flow.CurrentState}; camera={camera.name}; position={camera.transform.position}; rotation={camera.transform.eulerAngles}; warm={Object.FindAnyObjectByType<ReadyPrewarmController>()?.IsComplete}");
     if(validate&&new[]{"start","selected","locked"}.Contains(stateName)){
      var selection=Object.FindAnyObjectByType<DifficultySelectionView>();
      if(flow.CurrentState!=GameState.Boot||selection==null||selection.Choices.Any(c=>c==null||!c.gameObject.activeInHierarchy))throw new InvalidOperationException("Ready gallery not visible: "+stateName+" / "+flow.CurrentState);
     }
     if(validate&&folder.Contains("tutorial")){var tutorialView=Object.FindAnyObjectByType<TutorialSafeZoneView>();tutorialView.InstructionText.ForceMeshUpdate();if(tutorialView.InstructionText.isTextTruncated||!tutorialView.InstructionText.GetParsedText().Contains(tutorialView.CurrentInstruction))throw new InvalidOperationException("Tutorial instruction was clipped");}
     if(validate&&folder.Contains("result")){
      var geometry=new List<string>();
      foreach(var label in Object.FindObjectsByType<TextMeshPro>().Where(t=>t.enabled&&t.gameObject.activeInHierarchy&&new[]{"ResultHeading","ResultScore","ResultMetrics","ResultAccuracy","ResultRank","ResultFeedback","ResultDifficulty"}.Contains(t.name))){label.ForceMeshUpdate();geometry.Add($"{label.name}|bounds={label.textBounds.size}|rect={label.rectTransform.rect.size}");if(label.isTextTruncated||label.textBounds.size.x>label.rectTransform.rect.width+1)throw new InvalidOperationException("Result horizontal overflow: "+label.name);}
      File.WriteAllLines(Path.Combine(folder,"result-geometry.txt"),geometry);
     }
     var lines=Object.FindObjectsByType<TMP_Text>().Where(t=>t.enabled&&t.gameObject.activeInHierarchy).Select(t=>$"{t.name}|{t.GetParsedText().Replace("\n"," / ")}|font={t.fontSize}|rect={t.rectTransform.rect.size}|truncated={t.isTextTruncated}");File.WriteAllLines(Path.Combine(folder,"labels.txt"),lines);
    }catch(Exception e){failure=e;}done=true;};driver.RenderCompleted+=save;try{while(!done)yield return null;}finally{driver.RenderCompleted-=save;}if(failure!=null)throw failure;
   }
   IEnumerator Wait(float t){yield return new WaitForSecondsRealtime(t);}
   IEnumerator PointerClick(UnityEngine.UI.Button button){
    var screen=camera.WorldToScreenPoint(button.transform.position);
    InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return null;yield return null;
    InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});yield return null;yield return null;
    InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return null;yield return null;
   }
   void Button(string name){overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name).onClick.Invoke();}
   try{
    if(screensOnly){manager.Settings.lockDifficultySelection=false;router.SetMode(PcGazeMode.MouseGaze);}
    if(!screensOnly)using(var probe=new PcStabilityProbe(camera,1280,720)){
     yield return Capture(probe,Path.Combine(output,"loading"));
     var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+40;
     while(!warm.IsComplete&&Time.realtimeSinceStartup<deadline)yield return null;
     router.SetMode(PcGazeMode.MouseGaze);manager.Select(DifficultyLevel.Experience);
     var ready=Object.FindAnyObjectByType<ReadyScreenController>();deadline=Time.realtimeSinceStartup+30;while(!ready.CanStart&&Time.realtimeSinceStartup<deadline)yield return null;if(!ready.CanStart)throw new InvalidOperationException("Ready screen did not become startable");
     for(int round=1;round<=2;round++){
      var records=new List<string>{"time,gameState,launchStage,hangarZ,cameraX,cameraY,cameraZ"};int frame=0;float next=0;float began=Time.realtimeSinceStartup;
      if(round==1)Object.FindAnyObjectByType<ReadyScreenController>().StartExperience();else{flow.ShowResult();reset.CancelResultTimer();yield return Wait(.4f);lobby.ChooseFromResult(true);}
      deadline=Time.realtimeSinceStartup+75;float debugAt=Time.realtimeSinceStartup+5;float playing=-1;var observed=new HashSet<GameState>();Vector3 seat=camera.transform.position;float lastZ=float.PositiveInfinity;bool reversed=false;
      while(Time.realtimeSinceStartup<deadline){
       var target=Object.FindObjectsByType<GazeLockOnTarget>().Where(t=>t.IsAvailable).OrderBy(t=>(t.transform.position-camera.transform.position).sqrMagnitude).FirstOrDefault();
       if(flow.CurrentState!=GameState.Launch&&flow.CurrentState!=GameState.Countdown)Aim(target);
       if(observed.Add(flow.CurrentState)){yield return Capture(probe,Path.Combine(output,"round-"+round+"-"+flow.CurrentState));notes.Add($"state={flow.CurrentState}; target={target?.name}; tracking={router.IsTrackingValid}; looking={router.Simulation.IsLooking}; mode={router.ActiveMode}");}
       if(Time.realtimeSinceStartup>=debugAt){debugAt=Time.realtimeSinceStartup+5;notes.Add($"tick state={flow.CurrentState}; target={target?.name}; tracking={router.IsTrackingValid}; looking={router.Simulation.IsLooking}; suspended={router.InputSuspended}; modal={router.UiModalOpen}; requested={router.Simulation.RequestedAngles}; forward={camera.transform.forward}; aim={target?.transform.position}; mouse={mouse.delta.ReadValue()}; queued={queued}; enabled={mouse.enabled}; current={Mouse.current?.deviceId}; expected={mouse.deviceId}; scale={Time.timeScale}; dt={Time.deltaTime}; hit={Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>().CurrentHit.collider?.name}; lock={target?.State}; ray={router.GetGazeRay()}");File.WriteAllLines(Path.Combine(output,"live.txt"),notes);}
       bool motion=flow.CurrentState==GameState.Launch||flow.CurrentState==GameState.Countdown;
       if(motion){float z=room.localPosition.z;if(lastZ!=float.PositiveInfinity&&z>lastZ+.001f)reversed=true;lastZ=z;
        if(Time.realtimeSinceStartup>=next){next=Time.realtimeSinceStartup+.1f;records.Add($"{Time.realtimeSinceStartup-began:F4},{flow.CurrentState},{launch.CurrentStage},{z:F5},{camera.transform.position.x:F5},{camera.transform.position.y:F5},{camera.transform.position.z:F5}");yield return Capture(probe,Path.Combine(output,"motion-"+round,(frame++).ToString("D4")));}
       }
       if(flow.CurrentState==GameState.Playing){if(playing<0){playing=Time.realtimeSinceStartup;yield return Capture(probe,Path.Combine(output,"entered-combat-"+round));}if(Time.realtimeSinceStartup-playing>1)break;}
       yield return null;
      }
      File.WriteAllLines(Path.Combine(output,"motion-"+round+".csv"),records);notes.Add($"round={round}; enteredPlaying={playing>=0}; frames={frame}; hangarReversed={reversed}; cameraTranslation={Vector3.Distance(seat,camera.transform.position)}");
      if(validate&&(playing<0||reversed||Vector3.Distance(seat,camera.transform.position)>.01f))throw new InvalidOperationException("Launch continuity: "+notes.Last());
     }
    }
    foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1024,768),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)}){
     using var probe=new PcStabilityProbe(camera,size.x,size.y);string root=Path.Combine(output,"gallery",size.x+"x"+size.y);
     IEnumerator Snap(string name){yield return Wait(.65f);yield return Capture(probe,Path.Combine(root,name));}
     router.Simulation.Resume();reset.ResetExperience();router.Simulation.Recenter();
     InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.zero});yield return null;yield return null;
     var readyScreen=Object.FindAnyObjectByType<ReadyScreenController>();float readyDeadline=Time.realtimeSinceStartup+45;
     var preparation=Object.FindAnyObjectByType<ReadyPrewarmController>();
     while(preparation!=null&&!preparation.IsComplete&&Time.realtimeSinceStartup<readyDeadline)yield return null;
     while(!readyScreen.CanStart&&Time.realtimeSinceStartup<readyDeadline)yield return null;
     if(!readyScreen.CanStart||(preparation!=null&&!preparation.IsComplete))throw new InvalidOperationException("Ready UI not interactive after loading");
     router.Simulation.Recenter();
     yield return Snap("start");
     manager.Select(DifficultyLevel.Experience);yield return Snap("selected");
     if(screensOnly){manager.Settings.lockDifficultySelection=true;foreach(var choice in Object.FindAnyObjectByType<DifficultySelectionView>().Choices)choice.Refresh();yield return Snap("locked");if(manager.Select(DifficultyLevel.Hard))throw new InvalidOperationException("Locked difficulty changed");manager.Settings.lockDifficultySelection=false;foreach(var choice in Object.FindAnyObjectByType<DifficultySelectionView>().Choices)choice.Refresh();}
     var startButton=readyScreen.transform.Find("DifficultySelection/StartMission")?.GetComponent<UnityEngine.UI.Button>();
     if(startButton!=null)yield return PointerClick(startButton);else readyScreen.StartExperience();
     if(flow.CurrentState!=GameState.Intro)throw new InvalidOperationException("Start action failed to enter intro");
     notes.Add($"{size}: start action entered Intro; tutorial gallery then entered explicitly (not calibration verification)");
     flow.StartTutorial();
     yield return Snap("tutorial");
     if(screensOnly){Object.FindAnyObjectByType<MeteorDefenseVR.Tutorial.TutorialController>().SendMessage("SetInstruction","조준점을 운석에 계속 맞춰 주세요.\n초록색으로 바뀌면 자동으로 발사돼요.");yield return Snap("tutorial-reminder");}
     flow.StartPractice();yield return Snap("practice");
     flow.StartMainGame();stats.SetScore(int.MaxValue);Object.FindAnyObjectByType<MeteorDefenseVR.UI.MissionHudController>().AddScore(int.MaxValue);yield return Snap("combat-max-score");
     flow.StartBoss();yield return Snap("boss");router.Simulation.Pause();yield return Snap("pause");
     Button("LookSettings");yield return Snap("look-settings");overlay.TryCloseLookSettings();
     Button("AudioSettings");yield return Snap("audio-settings");overlay.TryCloseLookSettings();router.Simulation.Resume();
     flow.ShowResult();reset.CancelResultTimer();yield return Snap("result-max-score");yield return Wait(.4f);yield return Snap("result-settled");
     if(screensOnly){result.Snapshot.DestroyedMeteors=int.MaxValue;result.Snapshot.Shots=int.MaxValue;result.Snapshot.SuccessfulHits=int.MaxValue;yield return Snap("result-max-metrics");}
     yield return PointerClick(Object.FindAnyObjectByType<ResultActionsView>().Menu.GetComponent<UnityEngine.UI.Button>());yield return Snap("hangar");
     if(lobby.Phase!=HangarLobbyController.LobbyPhase.Menu)throw new InvalidOperationException("Result menu action failed");
     lobby.Queue(HangarLobbyController.MenuAction.Difficulty);yield return Snap("difficulty");
     lobby.Queue(HangarLobbyController.MenuAction.Back);yield return Wait(.4f);lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return Snap("records");
     if(screensOnly){reset.ResetExperience();flow.StartMainGame();flow.ShowResult();reset.CancelResultTimer();yield return Wait(1f);
      yield return PointerClick(Object.FindAnyObjectByType<ResultActionsView>().Replay.GetComponent<UnityEngine.UI.Button>());
      if(lobby.Phase!=HangarLobbyController.LobbyPhase.ReplayPreparing||result.IsVisible)throw new InvalidOperationException("Replay action failed to reset into preparation");
      notes.Add($"{size}: result Menu and Replay synthetic InputSystem pointer clicks passed; no OS click or departure revalidation");}
    }
    notes.Add("passed=True");
   }finally{manager.Settings.lockDifficultySelection=originalLock;File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);InputSystem.RemoveDevice(mouse);foreach(var d in devices)InputSystem.EnableDevice(d);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Time.timeScale=1;}
  }
 }
}
