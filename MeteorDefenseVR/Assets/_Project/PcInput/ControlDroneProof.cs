using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Tutorial;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class ControlDroneProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var practice=Object.FindAnyObjectByType<PracticeController>();
            var camera=Camera.main;var pose=camera.transform.position;var rotation=camera.transform.rotation;
            bool suspended=router.InputSuspended,modal=router.UiModalOpen;float delay=practice.InterMeteorDelay;
            var notes=new List<string>{"Automatic fixture: actual Launch state and real practice meteor damage; inter-meteor delay set to zero only in fixture. No human/HMD/webcam. Before renders hide only the new decorative body in the same state."};
            bool passed=true;
            void Check(bool value,string name){passed&=value;notes.Add(name+"="+value);}
            yield return null;yield return null;
            var old=Object.FindAnyObjectByType<ControlDroneController>();if(old!=null)Object.DestroyImmediate(old);
            yield return null;
            var drone=lobby.gameObject.AddComponent<ControlDroneController>();drone.Initialize(camera,flow,reset,lobby.Repair,router);
            router.InputSuspended=true;practice.InterMeteorDelay=0;
            yield return null;
            void DuplicateEvent()=>((Action)typeof(PracticeController).GetField("PracticeCompleted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(practice))?.Invoke();
            void FinishPractice()
            {
                for(int i=0;i<practice.TotalMeteors;i++){var meteor=practice.ActiveMeteor;Check(meteor!=null,"real practice target");if(meteor!=null){meteor.ReceiveDamage(meteor.MaxHealth);Check(!meteor.DestroyMeteor(),"duplicate meteor death rejected");}}
            }
            void Succeed(){flow.StartPractice();FinishPractice();}
            Rect Bounds()
            {
                Vector2 min=Vector2.one*100,max=Vector2.one*-100;
                foreach(var r in drone.Body.GetComponentsInChildren<Renderer>())
                {var b=r.bounds;for(int i=0;i<8;i++){var p=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}}
                return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            void Capture(string name,bool before=false)
            {
                foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                {
                    using var render=new PcStabilityProbe(camera,size.x,size.y);
                    drone.Present();bool visible=drone.Visible;if(before)drone.Body.SetActive(false);
                    Canvas.ForceUpdateCanvases();Object.FindAnyObjectByType<PausePresentationGate>()?.Apply();PcStabilityRenderDriver.RenderForProof(camera);
                    string folder=Path.Combine(output,$"{size.x}x{size.y}",name);Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);
                    if(before)drone.Body.SetActive(visible);
                    if(visible&&!before)
                    {
                        var b=Bounds();Check(b.xMin>.70f&&b.xMax<.98f&&b.yMin>.50f&&b.yMax<.85f,$"{size}/{name} edge bounds {b}");
                        bool clear=true;
                        foreach(var t in Object.FindObjectsByType<TMPro.TextMeshProUGUI>())
                        {var canvas=t.canvas;if(!t.gameObject.activeInHierarchy||canvas==null||!canvas.enabled||string.IsNullOrWhiteSpace(t.text))continue;var group=t.GetComponentInParent<CanvasGroup>();if(group!=null&&group.alpha<.01f)continue;clear&=!b.Overlaps(TelemetryReadabilityProof.ViewRect(camera,t.rectTransform));}
                        foreach(var button in Object.FindObjectsByType<UnityEngine.UI.Button>())
                        {if(button.isActiveAndEnabled&&button.interactable)clear&=!b.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)button.transform));}
                        Check(clear,$"{size}/{name} no text or button overlap");
                    }
                }
            }
            try
            {
                reset.ResetExperience();flow.StartLaunch();drone.Tick(.22f);
                Check(drone.GreetingCount==1&&drone.Current==ControlDroneController.Gesture.Greeting,"first real launch greets once");
                Check(flow.CurrentState==GameState.Launch,"launch immediate, no drone-owned gate");
                Capture("greeting-before",true);Capture("greeting-wave");drone.Tick(.22f);Capture("greeting-wave-later");
                Check(drone.Body.GetComponentsInChildren<Collider>(true).Length==0&&drone.Body.GetComponentsInChildren<MeteorDefenseVR.Combat.GazeLockOnTarget>(true).Length==0,"no colliders or gaze targets");
                Check(drone.Body.GetComponentsInChildren<Transform>(true).All(t=>t.gameObject.layer==2),"all parts ignore raycast");
                reset.ResetExperience();Check(!drone.Visible,"launch cancellation immediate");flow.StartLaunch();Check(drone.GreetingCount==1&&!drone.Visible,"retry does not repeat greeting");
                reset.ResetExperience();flow.StartPractice();DuplicateEvent();Check(drone.NodCount==0&&!drone.Visible,"fake completion cannot celebrate");
                practice.ActiveMeteor.ReachPlayer();Check(!drone.Visible&&drone.NodCount==0,"miss cannot celebrate");
                reset.ResetExperience();
                var director=Object.FindAnyObjectByType<SfPresentationDirector>();float bootDeadline=Time.realtimeSinceStartup+10;
                while(director.IsBootPresenting&&Time.realtimeSinceStartup<bootDeadline)yield return null;
                flow.StartPractice();yield return new WaitForSecondsRealtime(.2f);FinishPractice();
                Check(drone.Current==ControlDroneController.Gesture.Nod&&drone.NodCount==1,"real practice success nods");
                DuplicateEvent();Check(drone.NodCount==1,"duplicate completion ignored");
                yield return null;yield return null;
                foreach(var overlay in Object.FindObjectsByType<PracticeHudSafeOverlay>()){notes.Add("practice UI: canvas="+(overlay.SafeCanvas!=null)+" enabled="+(overlay.SafeCanvas!=null&&overlay.SafeCanvas.enabled)+" text="+(overlay.SafeStatus!=null?overlay.SafeStatus.text:"none"));}
                Check(Object.FindObjectsByType<PracticeHudSafeOverlay>().Any(p=>p.SafeCanvas!=null&&p.SafeCanvas.enabled&&p.SafeStatus.text.Contains("잘했어요")),"actual practice success message rendered alongside nod");
                drone.Tick(Mathf.Max(0,.25f-drone.Age));Capture("nod-before",true);Capture("nod-down");drone.Tick(.5f);Capture("nod-up");
                router.Simulation.Pause();drone.Tick(0);Check(!drone.Visible&&drone.Current==ControlDroneController.Gesture.None,"pause cancels");Capture("paused");router.Simulation.Resume();
                reset.ResetExperience();Succeed();router.UiModalOpen=true;drone.Tick(0);Check(!drone.Visible,"settings/modal cancels");router.UiModalOpen=false;
                reset.ResetExperience();Succeed();flow.ShowResult();Check(!drone.Visible,"result cancels");Capture("result");
                reset.ResetExperience();Check(!drone.Visible,"menu/reset clears");
                Succeed();drone.Tick(1);Check(!drone.Visible,"gesture ends without holding practice");
                yield return new WaitForSecondsRealtime(1.3f);Check(flow.CurrentState==GameState.Launch,"original practice launch timing continues");
                reset.ResetExperience();yield return null;
                int materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                for(int i=0;i<30;i++){Succeed();reset.ResetExperience();Check(!drone.Visible,"repeat reset "+i);}
                yield return null;
                Check(Object.FindObjectsByType<ControlDroneController>(FindObjectsInactive.Include).Length==1,"one companion controller after 30 resets");
                Check(Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.name=="ControlDroneBody")==1,"one pooled companion body");
                Check(Resources.FindObjectsOfTypeAll<Material>().Length==materials,"no material accumulation");
                Check(camera.transform.position==pose&&camera.transform.rotation==rotation,"camera pose unchanged");
            }
            finally
            {router.Simulation.Resume();router.UiModalOpen=modal;practice.InterMeteorDelay=delay;reset.ResetExperience();router.InputSuspended=suspended;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes.Concat(new[]{"passed="+passed}));}
            complete(passed);
        }
    }
}
