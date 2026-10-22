using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Tutorial;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // A decorative companion only. Never owns input, targets, gameplay timers or camera pose.
    [DefaultExecutionOrder(1250), DisallowMultipleComponent]
    public sealed class ControlDroneController : MonoBehaviour
    {
        public enum Gesture { None, Greeting, Nod, ReturnScan }
        public Gesture Current { get; private set; }
        public int GreetingCount { get; private set; }
        public int NodCount { get; private set; }
        public int ReturnScanCount { get; private set; }
        public bool ScanVisible => scan!=null&&scan.enabled&&scan.gameObject.activeInHierarchy;
        public float Age { get; private set; }
        public bool Visible => body != null && body.activeInHierarchy;
        public GameObject Body => body;
        private Camera view;
        private GameFlowManager flow;
        private OperationsResetController reset;
        private PracticeController practice;
        private GazeInputRouter router;
        private GameObject root, body;
        private LineRenderer scan;
        private BaseFlightCinematic cinema;
        private ResultController result;
        private DockRepairController repair;
        private int inspectedReturn;
        private bool greeted, practiceAcknowledged;
        public void Initialize(Camera camera, GameFlowManager manager, OperationsResetController resetter, DockRepairController repair, GazeInputRouter input)
        {
            view=camera;flow=manager;reset=resetter;router=input;
            this.repair=repair;cinema=GetComponent<BaseFlightCinematic>();
            result=FindAnyObjectByType<ResultController>();
            practice=FindAnyObjectByType<PracticeController>();
            root=new GameObject("ControlDronePresentation");body=repair.CreateControlDrone(root.transform);
            scan=repair.CreateControlScan(root.transform);
            // The film set is much larger than the cockpit repair rig; keep the same soft cyan beam readable.
            scan.widthMultiplier=.035f;scan.startColor=scan.endColor=new Color(.18f,.85f,.92f,.65f);
            // Ignore Raycast even if a future decorative mesh gains an accidental collider.
            foreach(var part in root.GetComponentsInChildren<Transform>(true))part.gameObject.layer=2;
            Cancel();flow.OnStateChanged+=StateChanged;reset.BeforeReset+=Cancel;
            if(practice!=null)practice.PracticeCompleted+=PracticeSucceeded;
        }
        private void StateChanged(GameState state)
        {
            Cancel();
            if(state==GameState.Practice)practiceAcknowledged=false;
            if(state==GameState.Launch&&!greeted)
            {greeted=true;if(CanShow()){GreetingCount++;Begin(Gesture.Greeting);}}
        }
        private bool CanShow()=>isActiveAndEnabled&&Time.timeScale>0&&router!=null&&!router.UiModalOpen&&!router.EducationModalOpen;
        private void PracticeSucceeded()
        {
            if(practiceAcknowledged||flow.CurrentState!=GameState.Practice||practice==null||!practice.IsComplete||practice.TotalMeteors<=0||practice.DestroyedMeteors!=practice.TotalMeteors)return;
            practiceAcknowledged=true;
            if(CanShow()){NodCount++;Begin(Gesture.Nod);}
        }
        private void Begin(Gesture gesture)
        {
            root.transform.SetParent(null,false);
            foreach(var part in root.GetComponentsInChildren<Transform>(true))part.gameObject.layer=2;
            Current=gesture;Age=0;root.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
            body.SetActive(true);Present();
        }
        private void LateUpdate()
        {
            ObserveReturn();
            if(Current==Gesture.None)return;
            if(!CanShow()){Cancel();return;}
            Tick(Time.deltaTime);
        }
        public void Tick(float delta)
        {
            if(Current==Gesture.None)return;
            if(Current==Gesture.ReturnScan)
            {
                if(!CanShow()||!ReturnWindow()||repair.VisibleDrones!=0){Cancel();return;}
                Present();return;
            }
            if(!CanShow()||Quaternion.Angle(root.transform.rotation,view.transform.rotation)>5f||Vector3.Distance(root.transform.position,view.transform.position)>.08f){Cancel();return;}
            if(!RuntimeValueGuard.IsFinite(delta)||delta<0)return;
            Age+=delta;
            if(Age>=.85f){Cancel();return;}
            Present();
        }
        public void Present()
        {
            if(!Visible)return;
            if(Current==Gesture.ReturnScan)
            {
                var ship=cinema.ReturnShip;if(ship==null){Cancel();return;}
                float t=Mathf.InverseLerp(.58f,.76f,cinema.ReturnProgress);
                body.transform.position=ship.TransformPoint(2.4f,1.5f,Mathf.Lerp(-1.5f,1.4f,t));
                body.transform.rotation=ship.rotation*Quaternion.Euler(20,90,0);
                body.transform.localScale=Vector3.one*1.8f;
                var point=ship.TransformPoint(.35f,.55f,Mathf.Lerp(-1.5f,1.4f,t));
                scan.enabled=true;
                scan.SetPosition(0,point-ship.right*.25f);
                scan.SetPosition(1,body.transform.TransformPoint(0,0,-.16f));
                scan.SetPosition(2,point+ship.right*.25f);
                return;
            }
            const float depth=2.2f;
            float halfHeight=depth*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
            float wave=Mathf.Sin(Mathf.Clamp01(Age/.85f)*Mathf.PI*2);
            body.transform.localPosition=new Vector3(halfHeight*Mathf.Min(view.aspect,16f/9f)*.72f,halfHeight*.36f,depth);
            body.transform.localScale=Vector3.one*.72f;
            body.transform.localRotation=Current==Gesture.Greeting?Quaternion.Euler(0,-8,12*wave):Quaternion.Euler(20*Mathf.Sin(Mathf.Clamp01(Age/.85f)*Mathf.PI),-8,0);
        }
        private bool ReturnWindow()=>cinema!=null&&cinema.isActiveAndEnabled&&cinema.CurrentShot==BaseFlightCinematic.Shot.Return&&cinema.ReturnShip!=null&&cinema.ReturnProgress>=.58f&&cinema.ReturnProgress<.76f&&flow.CurrentState==GameState.Result&&result!=null&&result.IsPresentationPending&&!result.IsVisible;
        public void ObserveReturn()
        {
            if(Current==Gesture.ReturnScan&&(!ReturnWindow()||!CanShow())){Cancel();return;}
            if(!ReturnWindow()||inspectedReturn==cinema.ReturnSequence)return;
            // Consume even a suppressed visit: closing a modal never restarts a cancelled scan.
            inspectedReturn=cinema.ReturnSequence;
            if(!CanShow()||repair.VisibleDrones!=0)return;
            Cancel();Current=Gesture.ReturnScan;ReturnScanCount++;
            root.transform.SetParent(cinema.ReturnSet,false);
            foreach(var part in root.GetComponentsInChildren<Transform>(true))part.gameObject.layer=cinema.ReturnShip.gameObject.layer;
            body.SetActive(true);Present();
        }
        public void Cancel(){Current=Gesture.None;Age=0;if(scan!=null)scan.enabled=false;if(body!=null)body.SetActive(false);}
        private void OnDisable()=>Cancel();
        private void OnDestroy()
        {
            if(flow!=null)flow.OnStateChanged-=StateChanged;
            if(reset!=null)reset.BeforeReset-=Cancel;
            if(practice!=null)practice.PracticeCompleted-=PracticeSucceeded;
            if(root!=null)Destroy(root);
        }
    }
}
