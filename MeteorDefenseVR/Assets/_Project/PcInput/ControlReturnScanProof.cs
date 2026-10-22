using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class ControlReturnScanProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var cinema=Object.FindAnyObjectByType<BaseFlightCinematic>();var drone=Object.FindAnyObjectByType<ControlDroneController>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var result=Object.FindAnyObjectByType<ResultController>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var camera=Camera.main;
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            bool suspended=router.InputSuspended,modal=router.UiModalOpen,passed=true;
            var notes=new List<string>{"Fixture: real failed-result callback, then explicitly seek cinematic time for deterministic matched renders/lifecycle. Not ordinary OS or HMD play. Success return has no ship and is excluded."};
            void Check(bool condition,string label){passed&=condition;notes.Add(label+"="+condition);}
            void Seek(float progress)
            {
                typeof(BaseFlightCinematic).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(cinema,progress*BaseFlightCinematic.ReturnSeconds);
                typeof(BaseFlightCinematic).GetMethod("AnimateShot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cinema,new object[]{0f});
                drone.ObserveReturn();drone.Tick(0);
            }
            void Failure()
            {
                reset.ResetExperience();flow.StartMainGame();health.ApplyImpactDamage(100,health.transform.position);
                Check(cinema.CurrentShot==BaseFlightCinematic.Shot.Return&&result.IsPresentationPending,"real failure begins ship return");
            }
            void Capture(string name,bool before)
            {
                var line=Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).Single(x=>x.name=="ControlReturnScan");
                foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(2560,1080)})
                {
                    using var render=new PcStabilityProbe(camera,size.x,size.y);
                    bool body=drone.Body.activeSelf,beam=line.enabled;
                    if(before){drone.Body.SetActive(false);line.enabled=false;}
                    PcStabilityRenderDriver.RenderForProof(camera);
                    string folder=Path.Combine(output,$"{size.x}x{size.y}",name);Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);
                    drone.Body.SetActive(body);line.enabled=beam;
                }
            }
            try
            {
                router.InputSuspended=true;router.UiModalOpen=false;
                Failure();yield return null;int count=drone.ReturnScanCount;Seek(.57f);Check(!drone.ScanVisible,"no scan during approach");
                Seek(.66f);Check(drone.ScanVisible&&drone.Visible&&drone.ReturnScanCount==count+1,"one landing scan");
                Check(lobby.Repair.VisibleDrones==0&&!lobby.Repair.ScanVisible,"repair drones absent during control inspection");
                Check(!result.IsVisible&&result.IsPresentationPending,"no result button covered");
                Check(drone.Body.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"no input colliders");
                float hp=health.HealthNormalized;
                Capture("before",true);Capture("inspection",false);
                Seek(.72f);Check(drone.ReturnScanCount==count+1&&health.HealthNormalized==hp,"single inspection, no healing");
                Capture("inspection-end",false);
                Seek(.77f);Check(!drone.Visible&&!drone.ScanVisible,"clear before door closing");
                Seek(1f);Check(result.IsVisible&&!drone.Visible,"original result reveal unaffected");
                Check(lobby.ChooseFromResult(false),"base menu remains immediately selectable");
                Check(lobby.Repair.VisibleDrones==2&&!drone.Visible,"repair begins after inspection, no overlap");
                Failure();Seek(.66f);router.UiModalOpen=true;drone.ObserveReturn();Check(!drone.Visible&&!drone.ScanVisible,"modal cancels immediately");
                router.UiModalOpen=false;drone.ObserveReturn();Check(!drone.Visible,"closing modal does not replay scan");
                Failure();Seek(.66f);reset.ResetExperience();Check(!drone.Visible&&!drone.ScanVisible,"fast reset clears immediately");
                Failure();Seek(.66f);result.RevealPreparedResult();drone.ObserveReturn();Check(!drone.Visible&&!drone.ScanVisible,"early result reveal clears scan");
                Failure();Seek(.66f);drone.enabled=false;Check(!drone.Visible&&!drone.ScanVisible,"disable clears immediately");drone.enabled=true;
                Seek(.7f);Check(!drone.Visible,"enable does not duplicate same return");
                Failure();Seek(.66f);Time.timeScale=0;drone.ObserveReturn();Check(!drone.Visible,"pause cancels scan");Time.timeScale=1;drone.ObserveReturn();Check(!drone.Visible,"resume does not duplicate scan");
                Failure();Seek(.66f);Seek(1);Check(lobby.ChooseFromResult(true),"replay choice does not wait for scan");Check(!drone.Visible&&!drone.ScanVisible,"replay clears scan");
                reset.ResetExperience();flow.StartMainGame();flow.ShowResult();Check(!result.Snapshot.MissionFailed,"success fixture");
                int beforeSuccess=drone.ReturnScanCount;lobby.BeginReturn();drone.ObserveReturn();Check(cinema.ReturnShip==null&&!drone.Visible&&drone.ReturnScanCount==beforeSuccess,"success path without ship has no fabricated scan");
                for(int i=0;i<20;i++){Failure();int prior=drone.ReturnScanCount;Seek(.66f);Check(drone.ScanVisible&&drone.ReturnScanCount==prior+1,"next return restored "+i);reset.ResetExperience();}
                yield return null;
                Check(Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).Count(x=>x.name=="ControlReturnScan")==1,"one pooled scan after repeats");
                Check(Resources.FindObjectsOfTypeAll<GameObject>().Count(x=>x.name=="ControlDroneBody")==1,"one reused companion body");
                Check(camera.transform.position==position&&camera.transform.rotation==rotation,"tracked camera unchanged");
            }
            finally
            {
                Time.timeScale=1;router.UiModalOpen=modal;reset.ResetExperience();router.InputSuspended=suspended;
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes.Concat(new[]{"passed="+passed}));
            }
            complete(passed);
        }
    }
}
