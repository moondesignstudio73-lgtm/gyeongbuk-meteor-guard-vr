using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class ThreatSafeAreaProof {
 public static IEnumerator Run(string output,bool validate) {
  while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;
  var router=Object.FindAnyObjectByType<GazeInputRouter>();var camera=Camera.main;
  var director=Object.FindAnyObjectByType<SfPresentationDirector>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
  var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var boss=Object.FindAnyObjectByType<BossClimaxController>();
  var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);var overlay=Object.FindAnyObjectByType<GameplayHudOverlayView>();
  var caster=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();bool casting=caster.enabled,input=router.InputSuspended;
  caster.enabled=false;router.InputSuspended=true;var notes=new List<string>{"Controlled pooled targets with explicit viewport movement, scenario starts, reward callback and BeginClimax. Consecutive rendered frames, not natural gameplay, OS input or HMD acceptance."};
  int violations=0,frames=0;void Check(bool ok,string message){if(ok)return;violations++;if(validate)throw new InvalidOperationException(message);}
  IEnumerator Ready(){float until=Time.realtimeSinceStartup+40;while((director.IsBootPresenting||!Object.FindAnyObjectByType<ReadyPrewarmController>().IsComplete)&&Time.realtimeSinceStartup<until)yield return null;}
  try {
   foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1024,768),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)}) {
    reset.ResetExperience();yield return Ready();Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Hard);GameFlowManager.Instance.StartMainGame();router.InputSuspended=true;router.Simulation.Recenter();yield return new WaitForSecondsRealtime(1.5f);
    using var probe=new PcStabilityProbe(camera,size.x,size.y);var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();var indicator=Object.FindAnyObjectByType<ThreatIndicatorView>();
    string folder=Path.Combine(output,size.x+"x"+size.y);Directory.CreateDirectory(folder);
    var trace=new List<string>{"phase,step,frame,label,x,y,width,height,text"};
    var targets=new List<MeteorController>();
    void Spawn(int count){spawner.BeginScenario(count);targets.Clear();for(int i=0;i<count;i++){var p=camera.ViewportToWorldPoint(new Vector3(.5f+i*.03f,1.12f,25));var m=spawner.SpawnScenario(p,(spawner.ShipCenter-p).normalized*8,2,10);Check(m!=null,"Spawn failed");m.enabled=false;targets.Add(m);}}
    Spawn(1);yield return null;yield return null;
    foreach(string phase in new[]{"top","left-corner","right-corner","reward-multiple","boss-multiple"}) {
     if(phase=="reward-multiple"){Spawn(4);hud.AddScore(9999);}
     if(phase=="boss-multiple"){boss.ResetClimax();boss.BeginClimax();}
     var previous=new Dictionary<string,Vector2>();var previousDelta=new Dictionary<string,float>();int reversals=0;
     for(int step=0;step<24;step++) {
      float t=step/23f;
      for(int i=0;i<targets.Count;i++) {
       Vector2 v=phase=="left-corner"?new Vector2(-.12f,Mathf.Lerp(1.15f,.72f,t)):phase=="right-corner"?new Vector2(1.12f,Mathf.Lerp(.72f,1.15f,t)):new Vector2(Mathf.Lerp(.22f,.78f,t)+i*.018f,1.12f);
       var m=targets[i];m.transform.position=camera.ViewportToWorldPoint(new Vector3(v.x,v.y,25+i));m.SetMovementDirection(spawner.ShipCenter-m.transform.position);
      }
      bool done=false;Exception error=null;
      void Observe(){try{
       Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
       var labels=GameObject.Find("ThreatIndicators").GetComponentsInChildren<TextMeshProUGUI>().Where(x=>x.name.StartsWith("Threat")&&x.isActiveAndEnabled).ToArray();
       Check(labels.Length>0,"No threat labels");if(targets.Count>1)Check(labels.Length>=2,"Multiple threats collapsed");
       var rects=new List<Rect>();
       var panels=new List<RectTransform>();foreach(string name in new[]{"HealthPanel","StagePanel","ScorePanel"})panels.Add((RectTransform)overlay.TelemetryRoot.transform.Find(name));
       foreach(string name in new[]{"ShipStatusHologram","ThreatStatusHologram"}){var p=GameObject.Find(name);if(p!=null)panels.Add((RectTransform)p.transform);}
       if(overlay.FeedbackPanel.activeInHierarchy)panels.Add((RectTransform)overlay.FeedbackPanel.transform);
       if(director.CriticalNoticeBounds!=null)panels.Add(director.CriticalNoticeBounds);
       foreach(var label in labels){label.ForceMeshUpdate();var r=TelemetryReadabilityProof.ViewRect(camera,label.rectTransform);
        Check(r.xMin>=.005f&&r.yMin>=.005f&&r.xMax<=.995f&&r.yMax<=.995f,"Viewport escape "+phase);
        Check(!r.Overlaps(new Rect(.35f,.35f,.3f,.3f)),"Aim covered "+phase);
        foreach(var panel in panels)Check(!r.Overlaps(TelemetryReadabilityProof.ViewRect(camera,panel)),"Panel overlap "+phase+"/"+panel.name);
        foreach(var other in rects)Check(!r.Overlaps(other),"Multiple labels overlap");rects.Add(r);
        Check(label.fontSize==21&&!label.enableAutoSizing&&!label.isTextTruncated&&(label.text??"").Contains("m"),"Copy/font changed");
        Check((label.text??"").IndexOfAny(new[]{'▲','▼','◀','▶'})>=0,"Direction missing");
        if(previous.TryGetValue(label.name,out var old)){float dy=r.center.y-old.y;if(previousDelta.TryGetValue(label.name,out var last)&&Mathf.Abs(dy)>.005f&&Mathf.Abs(last)>.005f&&dy*last<0)reversals++;previousDelta[label.name]=dy;}
        previous[label.name]=r.center;
        trace.Add($"{phase},{step},{Time.frameCount},{label.name},{r.x},{r.y},{r.width},{r.height},{(label.text??"").Replace('\n','|')}");
       }
       frames++;
       if(validate||step==0||step==12||step==23){string path=Path.Combine(folder,phase,step.ToString("D2"));Directory.CreateDirectory(path);probe.SaveGameOnlyProof(path,router);}
      }catch(Exception e){error=new InvalidOperationException(phase+"/"+step+": "+e,e);}done=true;}
      driver.RenderCompleted+=Observe;try{while(!done)yield return null;}finally{driver.RenderCompleted-=Observe;}
      if(error!=null)throw error;
     }
     Check(reversals<=2,"Placement oscillated during monotonic sweep "+phase);notes.Add(size+"/"+phase+": direction/distance preserved; y reversals="+reversals);
    }
    File.WriteAllLines(Path.Combine(folder,"frames.csv"),trace);
    boss.ResetClimax();spawner.ClearScenarioRound();yield return null;Check(indicator.VisibleCount==0,"Despawn retained labels");Spawn(4);yield return null;yield return null;Check(indicator.VisibleCount>=2,"Reuse lost labels");
    var pausedLabels=GameObject.Find("ThreatIndicators").GetComponentsInChildren<TextMeshProUGUI>(true);router.Simulation.Pause();yield return null;var before=pausedLabels.Select(x=>x.rectTransform.anchoredPosition).ToArray();yield return new WaitForSecondsRealtime(.1f);var after=pausedLabels.Select(x=>x.rectTransform.anchoredPosition).ToArray();Check(before.SequenceEqual(after),"Paused positions changed");router.Simulation.Resume();router.InputSuspended=true;yield return null;
    reset.ResetExperience();yield return null;Check(indicator.VisibleCount==0,"Reset retained labels");notes.Add(size+": clear/reuse/pause/resume/reset passed");
   }
   notes.Add("frames="+frames+"; violations="+violations);notes.Add(validate?"passed=True":"baselineCaptured=True");
  }finally{Time.timeScale=1;router.Simulation.Resume();spawner.EndScenario();reset.ResetExperience();caster.enabled=casting;router.InputSuspended=input;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
 }
}}
