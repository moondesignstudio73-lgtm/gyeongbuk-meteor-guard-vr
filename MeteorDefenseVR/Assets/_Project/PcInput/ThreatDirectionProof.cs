using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public static class ThreatDirectionProof
    {
        private static void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
        public static IEnumerator Run(string output)
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

            var trace=new List<string>{"size,phase,step,target,localX,localY,localZ,labelX,labelY,text,summary"};
            int opposing=0,observed=0;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var labelField=typeof(ThreatIndicatorView).GetField("labels",flags);
            var targetField=typeof(ThreatIndicatorView).GetField("textTargets",flags);
            foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720)})
            using(var proof=new PcStabilityProbe(camera,size.x,size.y))
            foreach(string phase in new[]{"centre-jitter","top-crossing","rear-crossing","front-crossing","above-to-rear","multiple"})
            {
                spawner.BeginScenario();var targets=new List<MeteorController>();
                for(int i=0;i<(phase=="multiple"?4:1);i++)
                {
                    var p=camera.transform.TransformPoint(new Vector3(-35,25,25+i*5));
                    var m=spawner.SpawnScenario(p,(spawner.ShipCenter-p).normalized*8,2,10);
                    Check(m!=null,"Spawn failed");m.enabled=false;targets.Add(m);
                }
                Vector2? jitterPosition=null;
                for(int step=0;step<12;step++)
                {
                    float t=step/11f;
                    for(int i=0;i<targets.Count;i++)
                    {
                        Vector3 local=phase=="centre-jitter"?new Vector3(step%2==0?-1:1,2,-25):phase=="rear-crossing"?new Vector3(Mathf.Lerp(-35,35,t),2,-25):
                            phase=="front-crossing"?new Vector3(Mathf.Lerp(-45,45,t),0,25):
                            phase=="above-to-rear"?new Vector3(15,Mathf.Lerp(40,2,t),Mathf.Lerp(25,-25,t)):
                            new Vector3(Mathf.Lerp(-35,35,t)+i*2,32+i*2,25+i*5);
                        targets[i].transform.position=camera.transform.TransformPoint(local);
                        targets[i].SetMovementDirection(spawner.ShipCenter-targets[i].transform.position);
                    }
                    yield return new WaitForSecondsRealtime(.16f);
                    Check(GameFlowManager.Instance.CurrentState==GameState.Playing&&Time.timeScale>0&&!router.Simulation.IsPaused,"Fixture paused or left combat");
                    telemetry.Refresh();Canvas.ForceUpdateCanvases();camera.Render();
                    var labels=(TextMeshProUGUI[])labelField.GetValue(indicator);
                    var mapped=(MeteorController[])targetField.GetValue(indicator);
                    var rects=new List<Rect>();
                    for(int i=0;i<labels.Length;i++)
                    {
                        if(!labels[i].isActiveAndEnabled||mapped[i]==null)continue;
                        var local=camera.transform.InverseTransformPoint(mapped[i].transform.position);
                        var r=TelemetryReadabilityProof.ViewRect(camera,labels[i].rectTransform);
                        Check(!r.Overlaps(new Rect(.35f,.35f,.3f,.3f)),"Aim covered");
                        Check(!rects.Any(other=>other.Overlaps(r)),"Labels overlap");rects.Add(r);
                        Check(!labels[i].isTextTruncated&&labels[i].fontSize==21,"Truncated or resized label");
                        if(phase=="centre-jitter") { if(jitterPosition.HasValue) Check(Vector2.Distance(jitterPosition.Value,r.center)<.002f,"Centre jitter moved label"); jitterPosition=r.center; }
                        string text=labels[i].text;
                        string expected=local.z<0?"REAR":Mathf.Abs(local.y)>Mathf.Abs(local.x)?(local.y>0?"ABOVE":"BELOW"):(local.x<0?"LEFT":"RIGHT");
                        Check(text.Contains(expected),"Direction text disagrees with local position");
                        if(Mathf.Abs(local.x)>10&&(r.center.x-.5f)*local.x<0)opposing++;
                        observed++;
                        trace.Add($"{size.x}x{size.y},{phase},{step},{targets.IndexOf(mapped[i])},{local.x:F2},{local.y:F2},{local.z:F2},{r.center.x:F4},{r.center.y:F4},{text.Replace('\n','|')},{telemetry.ThreatReadout.text.Replace('\n','|')}");
                    }
                    int expectedCount=targets.Count(target=>{
                        var p=camera.WorldToViewportPoint(target.transform.position);
                        return p.z<=0||p.x<=.06f||p.x>=.94f||p.y<=.12f||p.y>=.88f;
                    });
                    Check(rects.Count==Mathf.Min(expectedCount,indicator.IndicatorLimit),"Expected threat labels missing");
                    string folder=$"{output}/{size.x}x{size.y}/{phase}/{step:D2}";
                    Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                }
            }
            Directory.CreateDirectory(output);File.WriteAllLines(output+"/trace.csv",trace);
            File.WriteAllText(output+"/summary.txt",$"Controlled pooled targets, explicit render sequence. Not natural OS/HMD gameplay or human comprehension.\nObserved labels={observed}\nOpposite horizontal half samples={opposing}\n");
            Check(opposing==0,"Wrong-side labels beyond centre band");
            spawner.EndScenario();
        }
    }
}
