using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class LoadingBossReviewProof {
 public static IEnumerator Run(string output,bool validate) {
  while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;
  yield return null;yield return null;
  var router=Object.FindAnyObjectByType<GazeInputRouter>();var camera=Camera.main;var flow=GameFlowManager.Instance;
  var reset=Object.FindAnyObjectByType<OperationsResetController>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();
  var boss=Object.FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
  var view=Object.FindAnyObjectByType<GameplayHudOverlayView>();var gate=Object.FindAnyObjectByType<PausePresentationGate>();
  var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var caster=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
  bool casting=caster.enabled,input=router.InputSuspended;caster.enabled=false;router.InputSuspended=true;
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var bootField=typeof(SfPresentationDirector).GetField("booting",flags);var tick=typeof(SfPresentationDirector).GetMethod("TickTransitions",flags);
  var notice=typeof(SfPresentationDirector).GetMethod("ShowNotice",flags);
  var notes=new List<string>{"Actual production loading visual with explicit QA progress fixtures. Actual BeginClimax and Tick stage events; clock frozen only for repeatable renders. Concurrent score and critical notice callbacks. No natural whole-round, OS/HMD or listening claim."};
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  string stageMessage="";void Stage(BossClimaxStage stage,string message){stageMessage=message;notes.Add("event="+stage+"; message="+message.Replace('\n','|'));}
  boss.StageChanged+=Stage;
  IEnumerator Ready(){float deadline=Time.realtimeSinceStartup+45;while((director.IsBootPresenting||!Object.FindAnyObjectByType<ReadyPrewarmController>().IsComplete)&&Time.realtimeSinceStartup<deadline)yield return null;Check(!director.IsBootPresenting,"Loading failed to finish");}
  try {
   router.SetMode(PcGazeMode.MouseGaze);yield return Ready();
   foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1024,768),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)}) {
    using var probe=new PcStabilityProbe(camera,size.x,size.y);string root=Path.Combine(output,"gallery",size.x+"x"+size.y);
    IEnumerator Capture(string name,bool loading=false,bool critical=false) {
     yield return null;yield return null;
     var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();bool done=false;Exception failure=null;
     void Save(){try{
      Canvas.ForceUpdateCanvases();gate.Apply();PcStabilityRenderDriver.RenderForProof(camera);
      string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);probe.SaveGameOnlyProof(folder,router);
      var labels=Object.FindObjectsByType<TMP_Text>().Where(t=>t.isActiveAndEnabled).ToArray();
      File.WriteAllLines(Path.Combine(folder,"labels.txt"),labels.Select(t=>$"{t.name}|{t.GetParsedText().Replace('\n','|')}|font={t.fontSize}|bounds={t.textBounds.size}|rect={t.rectTransform.rect.size}"));
      File.WriteAllText(Path.Combine(folder,"state.txt"),$"flow={flow.CurrentState}; boss={boss.CurrentStage}; bossRunning={boss.IsRunning}; actualStageMessage={stageMessage}; controllerFeedback={hud.FeedbackMessage}; visibleFeedback={view.FeedbackValue.text}; pause={router.Simulation.IsPaused}; alpha={view.HudCanvas.GetComponent<CanvasGroup>().alpha}");
      if(validate){
       var targets=loading?labels.Where(t=>new[]{"FlightTitle","PreparationStatus","PreparationPercent","FlightTip"}.Contains(t.name)):new[]{view.FeedbackValue}.Where(t=>t.isActiveAndEnabled&&!router.Simulation.IsPaused);
       foreach(var text in targets){text.ForceMeshUpdate();Check(!text.isTextTruncated&&text.textBounds.size.x<=text.rectTransform.rect.width+1&&text.textBounds.size.y<=text.rectTransform.rect.height+1,"Text overflow: "+text.name);
        if(loading){Check(text.fontSize>=30&&text.GetParsedText()==text.text,"Loading font/copy mismatch");foreach(var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible&&char.IsLetterOrDigit(c.character))){var a=camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft));var b=camera.WorldToScreenPoint(text.transform.TransformPoint(c.topRight));Check(b.y-a.y>=16&&a.x>=10&&b.x<=size.x-10&&a.y>=10&&b.y<=size.y-10,$"Loading glyph too small or outside viewport: {size}/{text.name}/{c.character} height={b.y-a.y} a={a} b={b}");}}
       }
       if(loading)Check(Object.FindAnyObjectByType<FlightLoadingVisual>().transform.Find("PreparationPanel").GetComponent<ChamferedHudPanel>().FillColor.a==1,"Loading plate not opaque");
       if(critical){Check(view.FeedbackValue.GetParsedText()==WorldTextPresentation.PlayerMessage(stageMessage),"Boss warning clipped or replaced by concurrent feedback");var rect=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)view.FeedbackPanel.transform);Check(!rect.Overlaps(new Rect(.35f,.35f,.3f,.3f)),"Warning covers central aim region");foreach(var panelName in new[]{"HealthPanel","StagePanel","ScorePanel"})Check(!rect.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)view.TelemetryRoot.transform.Find(panelName))),"Boss warning overlaps telemetry");if(director.CriticalNoticeBounds!=null&&director.CriticalNoticeBounds.gameObject.activeInHierarchy){var danger=TelemetryReadabilityProof.ViewRect(camera,director.CriticalNoticeBounds);Check(!rect.Overlaps(danger)&&!danger.Overlaps(new Rect(.35f,.35f,.3f,.3f)),"Boss/critical warning overlaps another notice or aim");}}
      }
     }catch(Exception e){failure=e;}done=true;}
     driver.RenderCompleted+=Save;try{while(!done)yield return null;}finally{driver.RenderCompleted-=Save;}if(failure!=null)throw failure;
    }
    Time.timeScale=1;reset.ResetExperience();yield return Ready();router.InputSuspended=true;router.Simulation.Recenter();
    var visual=Object.FindAnyObjectByType<FlightLoadingVisual>(FindObjectsInactive.Include);
    var loadingRoot=visual.transform.parent.gameObject;
    Time.timeScale=0;bootField.SetValue(director,true);tick.Invoke(director,new object[]{0f});
    foreach(var step in new[]{0,40,100}){visual.Present(step/100f,step==0?"우주로 향하는 길을 준비해요":step==40?"우주선을 준비하고 있어요":"출격 준비 완료!",.4f);yield return Capture("loading-"+step,true);}
    bootField.SetValue(director,false);loadingRoot.SetActive(false);Time.timeScale=1;
    flow.StartMainGame();yield return new WaitForSecondsRealtime(1.8f);spawner.BeginScenario(0);Time.timeScale=0;
    boss.ResetClimax();boss.BeginClimax();Check(boss.CurrentStage==BossClimaxStage.Warning&&!string.IsNullOrEmpty(stageMessage),"Actual boss warning did not occur");
    yield return Capture("boss-warning",false,true);
    hud.AddScore(int.MaxValue);notice.Invoke(director,new object[]{"방어막이 약해졌어요! 운석을 막아 주세요.",FlightUiTheme.Warning,1.1f,2});
    typeof(SfPresentationDirector).GetMethod("TickFeel",flags).Invoke(director,new object[]{.1f});
    yield return Capture("boss-warning-concurrent",false,true);
    Time.timeScale=1;router.Simulation.Pause();float remaining=hud.FeedbackRemaining;float bossElapsed=(float)typeof(BossClimaxController).GetField("stageElapsed",flags).GetValue(boss);
    yield return new WaitForSecondsRealtime(.2f);yield return Capture("boss-paused");Check(router.Simulation.IsPaused&&Time.timeScale==0&&boss.CurrentStage==BossClimaxStage.Warning&&Mathf.Abs(bossElapsed-(float)typeof(BossClimaxController).GetField("stageElapsed",flags).GetValue(boss))<.001f&&Mathf.Abs(remaining-hud.FeedbackRemaining)<.001f&&view.HudCanvas.GetComponent<CanvasGroup>().alpha==0,"Pause failed to preserve/hide warning");
    router.Simulation.Resume();router.InputSuspended=true;yield return new WaitForSecondsRealtime(.12f);
    Check(Time.timeScale==1&&hud.FeedbackRemaining<remaining&&(float)typeof(BossClimaxController).GetField("stageElapsed",flags).GetValue(boss)>bossElapsed,"Resume failed to advance real warning clocks");Time.timeScale=0;yield return Capture("boss-resumed",false,true);
    boss.Tick(2);Check(boss.CurrentStage==BossClimaxStage.Narration,"Actual narration transition missing");yield return Capture("boss-narration",false,true);
    boss.Tick(2);Check(boss.CurrentStage==BossClimaxStage.Active&&boss.ActiveBoss!=null,"Actual boss spawn missing");yield return Capture("boss-active");
    if(validate){
     var rect=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)view.FeedbackPanel.transform);Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;int meshes=0;
     foreach(var renderer in boss.ActiveBoss.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&!r.forceRenderingOff&&r.GetComponent<TMP_Text>()==null)){
      meshes++;var bounds=renderer.bounds;
      for(int i=0;i<8;i++){var p=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
     }
     var target=Rect.MinMaxRect(min.x,min.y,max.x,max.y);notes.Add($"{size}: active prompt={rect}; visible boss mesh bounds={target}; meshes={meshes}; collision radius={boss.ActiveBoss.CollisionRadius} (not a visual bound)");
     Check(meshes>0&&!rect.Overlaps(target)&&!rect.Overlaps(new Rect(.35f,.35f,.3f,.3f)),$"Active prompt covers visible boss/aim: {size} panel={rect} target={target}");
    }
    reset.ResetExperience();Time.timeScale=1;yield return Ready();yield return Capture("reset-ready");Check(boss.CurrentStage==BossClimaxStage.Idle&&!boss.IsRunning&&!view.FeedbackPanel.activeInHierarchy,"Reset retained boss warning");
    notes.Add(size+": real Warning/Narration/Active events, concurrent score+critical notice, pause/resume and reset cleared");
   }
   notes.Add(validate?"passed=True":"baselineCaptured=True");
  }finally{boss.StageChanged-=Stage;Time.timeScale=1;router.Simulation.Resume();bootField.SetValue(director,false);spawner.EndScenario();reset.ResetExperience();caster.enabled=casting;router.InputSuspended=input;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
 }
}}
