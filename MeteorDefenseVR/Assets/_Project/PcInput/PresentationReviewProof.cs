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
  public static IEnumerator Run(string output,bool validate)
  {
   Directory.CreateDirectory(output);
   while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;
   var router=Object.FindAnyObjectByType<GazeInputRouter>();var flow=GameFlowManager.Instance;
   var reset=Object.FindAnyObjectByType<OperationsResetController>();var manager=Object.FindAnyObjectByType<DifficultyManager>();
   var launch=Object.FindAnyObjectByType<LaunchSequenceController>();var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
   var result=Object.FindAnyObjectByType<ResultController>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
   var overlay=Object.FindAnyObjectByType<PcTestOverlay>();var camera=Camera.main;
   var room=GameObject.Find("LaunchSystem").transform.Find("HangarVisuals");
   var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);
   var mouse=InputSystem.AddDevice<Mouse>();var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
   var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   InputSystem.EnableDevice(mouse);mouse.MakeCurrent();
   Vector2 queued=Vector2.zero;int aimFrame=-1;
   void Aim(GazeLockOnTarget target){if(aimFrame==Time.frameCount)return;aimFrame=Time.frameCount;queued=Vector2.zero;if(router.Simulation.IsLooking&&target!=null){var aim=target.transform.position-camera.transform.position;var wanted=new Vector2(Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,-Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg);queued=new Vector2(Mathf.DeltaAngle(router.Simulation.RequestedAngles.x,wanted.x)/(.1f*router.Simulation.Sensitivity),(wanted.y-router.Simulation.RequestedAngles.y)/(.09f*router.Simulation.Sensitivity)*(router.Simulation.InvertY?1:-1));}InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.45f),delta=queued});}
   var notes=new List<string>{"Initial start uses StartExperience and synthetic relative InputSystem aim. Replay result is forced then original replay action invoked. UI gallery uses explicit states/menu actions and max-score fixture. No OS/HMD or natural whole-round claim."};
   IEnumerator Capture(PcStabilityProbe probe,string folder)
   {
    var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();bool done=false;Exception failure=null;
    Action save=()=>{try{Directory.CreateDirectory(folder);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(folder,router);
     var lines=Object.FindObjectsByType<TMP_Text>().Where(t=>t.enabled&&t.gameObject.activeInHierarchy).Select(t=>$"{t.name}|{t.GetParsedText().Replace("\n"," / ")}|font={t.fontSize}|rect={t.rectTransform.rect.size}|truncated={t.isTextTruncated}");File.WriteAllLines(Path.Combine(folder,"labels.txt"),lines);
    }catch(Exception e){failure=e;}done=true;};driver.RenderCompleted+=save;try{while(!done)yield return null;}finally{driver.RenderCompleted-=save;}if(failure!=null)throw failure;
   }
   IEnumerator Wait(float t){yield return new WaitForSecondsRealtime(t);}
   void Button(string name){overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name).onClick.Invoke();}
   try{
    using(var probe=new PcStabilityProbe(camera,1280,720)){
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
     router.Simulation.Resume();reset.ResetExperience();router.Simulation.Recenter();yield return Wait(.8f);yield return Snap("start");
     manager.Select(DifficultyLevel.Experience);yield return Snap("selected");
     flow.StartTutorial();yield return Snap("tutorial");flow.StartPractice();yield return Snap("practice");
     flow.StartMainGame();stats.SetScore(int.MaxValue);Object.FindAnyObjectByType<MeteorDefenseVR.UI.MissionHudController>().AddScore(int.MaxValue);yield return Snap("combat-max-score");
     flow.StartBoss();yield return Snap("boss");router.Simulation.Pause();yield return Snap("pause");
     Button("LookSettings");yield return Snap("look-settings");overlay.TryCloseLookSettings();
     Button("AudioSettings");yield return Snap("audio-settings");overlay.TryCloseLookSettings();router.Simulation.Resume();
     flow.ShowResult();reset.CancelResultTimer();yield return Snap("result-max-score");
     lobby.ChooseFromResult(false);yield return Snap("hangar");
     lobby.Queue(HangarLobbyController.MenuAction.Difficulty);yield return Snap("difficulty");
     lobby.Queue(HangarLobbyController.MenuAction.Back);yield return Wait(.4f);lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return Snap("records");
    }
    notes.Add("passed=True");
   }finally{File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);InputSystem.RemoveDevice(mouse);foreach(var d in devices)InputSystem.EnableDevice(d);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Time.timeScale=1;}
  }
 }
}
