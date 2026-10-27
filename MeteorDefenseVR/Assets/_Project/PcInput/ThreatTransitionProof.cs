using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Explicit opt-in fixture, never invoked by ordinary gameplay.
    public static class ThreatTransitionProof
    {
        private static void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
        public static IEnumerator Run(string output,bool validate)
        {
            // Standalone starts in Bootstrap; Editor fixtures open the game scene directly.
            float readyDeadline=Time.realtimeSinceStartup+40;
            while(Object.FindAnyObjectByType<GazeInputRouter>()==null&&Time.realtimeSinceStartup<readyDeadline)yield return null;
            Check(Object.FindAnyObjectByType<GazeInputRouter>()!=null,"Game scene did not load");
            yield return null;
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;
            Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>().enabled=false;
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();
            var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            while(!warm.IsComplete||director.IsBootPresenting)yield return null;
            var camera=Camera.main;var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
            var indicator=Object.FindAnyObjectByType<ThreatIndicatorView>();
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>();
            Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Hard);
            GameFlowManager.Instance.StartMainGame();router.InputSuspended=true;
            yield return new WaitForSecondsRealtime(2);

            var trace=new List<string>{"size,phase,step,target,localX,localY,localZ,labelX,labelY,text,summary,frame,time"};
            int opposing=0,observed=0;var violations=new List<string>();
            void Verify(bool ok,string message){if(!ok)violations.Add(message+" frame="+Time.frameCount);}
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var labelField=typeof(ThreatIndicatorView).GetField("labels",flags);
            var targetField=typeof(ThreatIndicatorView).GetField("textTargets",flags);
            foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720)})
            using(var proof=new PcStabilityProbe(camera,size.x,size.y))
            foreach(string phase in new[]{"rear-reversal","centre-jitter","direction-switch","crowded"})
            {
                Object.FindAnyObjectByType<MeteorDefenseVR.Player.PlayerHealth>().RestoreCheckpoint(phase=="crowded"?25:100);
                if(phase=="crowded")typeof(SfPresentationDirector).GetField("dangerAllowedAt",flags).SetValue(director,0f);
                spawner.BeginScenario();var targets=new List<MeteorController>();
                for(int i=0;i<(phase=="crowded"?5:1);i++)
                {
                    var p=camera.transform.TransformPoint(new Vector3(-35,25,25+i*5));
                    var m=spawner.SpawnScenario(p,(spawner.ShipCenter-p).normalized*8,2,10);
                    Check(m!=null,"Spawn failed");m.enabled=false;targets.Add(m);
                }
                Vector2? jitterPosition=null;
                for(int step=0;step<32;step++)
                {
                    float sign=(step/4)%2==0?-1:1;
                    for(int i=0;i<targets.Count;i++)
                    {
                        Vector3 local=phase=="centre-jitter"?new Vector3(step%2==0?-1:1,2,-25):
                            phase=="direction-switch"?((step/4)%3==0?new Vector3(-35,2,-25):(step/4)%3==1?new Vector3(2,40,25):new Vector3(35,2,25)):
                            new Vector3(sign*(35+i*2),2+i*2,-25-i*5);
                        targets[i].transform.position=camera.transform.TransformPoint(local);
                        targets[i].SetMovementDirection(spawner.ShipCenter-targets[i].transform.position);
                    }
                    bool done=false;Exception failure=null;
                    var driver=(PcStabilityRenderDriver)typeof(PcStabilityProbe).GetField("driver",flags).GetValue(proof);
                    void Observe(){try { int beforeViolations=violations.Count;

                    Verify(GameFlowManager.Instance.CurrentState==GameState.Playing&&Time.timeScale>0&&!router.Simulation.IsPaused,"Fixture paused or left combat");
                    telemetry.Refresh();Canvas.ForceUpdateCanvases();camera.Render();
                    var labels=(TextMeshProUGUI[])labelField.GetValue(indicator);
                    var mapped=(MeteorController[])targetField.GetValue(indicator);
                    var rects=new List<Rect>();
                    for(int i=0;i<labels.Length;i++)
                    {
                        if(!labels[i].isActiveAndEnabled||mapped[i]==null)continue;
                        var local=camera.transform.InverseTransformPoint(mapped[i].transform.position);
                        var r=TelemetryReadabilityProof.ViewRect(camera,labels[i].rectTransform);
                        Verify(!r.Overlaps(new Rect(.35f,.35f,.3f,.3f)),"Aim covered");
                        Verify(!rects.Any(other=>other.Overlaps(r)),"Labels overlap");rects.Add(r);
                        Verify(!labels[i].isTextTruncated&&labels[i].fontSize==21,"Truncated or resized label");
                        if(phase=="centre-jitter") { if(jitterPosition.HasValue) Verify(Vector2.Distance(jitterPosition.Value,r.center)<.002f,"Centre jitter moved label"); jitterPosition=r.center; }
                        Verify(r.xMin>=0&&r.xMax<=1&&r.yMin>=0&&r.yMax<=1,"Viewport escape");
                        foreach(string name in new[]{"ShipStatusHologram","ThreatStatusHologram"}) {var panel=GameObject.Find(name);if(panel!=null)Verify(!r.Overlaps(TelemetryReadabilityProof.ViewRect(camera,panel.GetComponent<RectTransform>())),"Telemetry covered");}
                        var overlay=Object.FindAnyObjectByType<GameplayHudOverlayView>();
                        foreach(string name in new[]{"HealthPanel","StagePanel","ScorePanel"})Verify(!r.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)overlay.TelemetryRoot.transform.Find(name))),"Top HUD covered");
                        if(director.CriticalNoticeBounds!=null)Verify(!r.Overlaps(TelemetryReadabilityProof.ViewRect(camera,director.CriticalNoticeBounds)),"Critical notice covered");
                        string text=labels[i].text;
                        string expected=local.z<0?"REAR":Mathf.Abs(local.y)>Mathf.Abs(local.x)?(local.y>0?"ABOVE":"BELOW"):(local.x<0?"LEFT":"RIGHT");
                        Verify(text.Contains(expected)&&text.Contains(local.z<0||Mathf.Abs(local.x)>=Mathf.Abs(local.y)?(local.x<0?"◀":"▶"):local.y>0?"▲":"▼"),"Direction text disagrees with local position");
                        if(Mathf.Abs(local.x)>10&&(r.center.x-.5f)*local.x<0)opposing++;
                        observed++;
                        trace.Add($"{size.x}x{size.y},{phase},{step},{targets.IndexOf(mapped[i])},{local.x:F2},{local.y:F2},{local.z:F2},{r.center.x:F4},{r.center.y:F4},{text.Replace('\n','|')},{telemetry.ThreatReadout.text.Replace('\n','|')},{Time.frameCount},{Time.unscaledTime:F6}");
                    }
                    int expectedCount=targets.Count(target=>{
                        var p=camera.WorldToViewportPoint(target.transform.position);
                        return p.z<=0||p.x<=.06f||p.x>=.94f||p.y<=.12f||p.y>=.88f;
                    });
                    Verify(rects.Count==Mathf.Min(expectedCount,indicator.IndicatorLimit),"Expected threat labels missing");
                    string folder=$"{output}/{size.x}x{size.y}/{phase}/{step:D2}";
                    if(step%4==0||violations.Count>beforeViolations){Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);}
                    }catch(Exception e){failure=e;}done=true;}
                    driver.RenderCompleted+=Observe;
                    try{while(!done)yield return null;}finally{driver.RenderCompleted-=Observe;}
                    if(failure!=null)throw failure;
                }
            }
            Directory.CreateDirectory(output);File.WriteAllLines(output+"/trace.csv",trace);
            File.WriteAllText(output+"/summary.txt",$"Controlled pooled targets, consecutive render callbacks, controlled per-frame target movement. Not natural OS/HMD gameplay or human comprehension.\nObserved labels={observed}\nOpposite horizontal half samples={opposing}\n");
            File.WriteAllLines(output+"/violations.txt",violations);
            File.AppendAllText(output+"/summary.txt","Violations="+violations.Count+"\n");
            if(validate)Check(violations.Count==0,"Frame boundary violations: "+violations.Count);
            spawner.EndScenario();
        }
    }
}
