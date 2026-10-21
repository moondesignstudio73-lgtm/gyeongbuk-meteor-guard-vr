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
        public enum Gesture { None, Greeting, Nod }
        public Gesture Current { get; private set; }
        public int GreetingCount { get; private set; }
        public int NodCount { get; private set; }
        public float Age { get; private set; }
        public bool Visible => body != null && body.activeInHierarchy;
        public GameObject Body => body;
        private Camera view;
        private GameFlowManager flow;
        private OperationsResetController reset;
        private PracticeController practice;
        private GazeInputRouter router;
        private GameObject root, body;
        private bool greeted, practiceAcknowledged;
        public void Initialize(Camera camera, GameFlowManager manager, OperationsResetController resetter, DockRepairController repair, GazeInputRouter input)
        {
            view=camera;flow=manager;reset=resetter;router=input;
            practice=FindAnyObjectByType<PracticeController>();
            root=new GameObject("ControlDronePresentation");body=repair.CreateControlDrone(root.transform);
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
            Current=gesture;Age=0;root.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
            body.SetActive(true);Present();
        }
        private void LateUpdate()
        {
            if(Current==Gesture.None)return;
            if(!CanShow()){Cancel();return;}
            Tick(Time.deltaTime);
        }
        public void Tick(float delta)
        {
            if(Current==Gesture.None)return;
            if(!CanShow()){Cancel();return;}
            if(!RuntimeValueGuard.IsFinite(delta)||delta<0)return;
            Age+=delta;
            if(Age>=.85f){Cancel();return;}
            Present();
        }
        public void Present()
        {
            if(!Visible)return;
            const float depth=2.2f;
            float halfHeight=depth*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
            float wave=Mathf.Sin(Mathf.Clamp01(Age/.85f)*Mathf.PI*2);
            body.transform.localPosition=new Vector3(halfHeight*view.aspect*.72f,halfHeight*.36f,depth);
            body.transform.localScale=Vector3.one*.72f;
            body.transform.localRotation=Current==Gesture.Greeting?Quaternion.Euler(0,-8,12*wave):Quaternion.Euler(20*Mathf.Sin(Mathf.Clamp01(Age/.85f)*Mathf.PI),-8,0);
        }
        public void Cancel(){Current=Gesture.None;Age=0;if(body!=null)body.SetActive(false);}
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
