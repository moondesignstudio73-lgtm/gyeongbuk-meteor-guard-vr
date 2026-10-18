using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Player;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    public enum CockpitHandVisualState { Idle, Launch, Flying, Impact, Critical, MissionComplete, Reset }

    // Presentation only. This component never supplies aim, lock-on, turret, weapon or camera input.
    // A future tracked-hands driver can disable VisualAnimationEnabled and drive the exposed anchors.
    [DefaultExecutionOrder(1250), DisallowMultipleComponent]
    public sealed class CockpitHandVisualController : MonoBehaviour
    {
        [SerializeField] private Transform flightStickPivot;
        [SerializeField] private Transform throttlePivot;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Renderer[] warningLamps = System.Array.Empty<Renderer>();
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LaunchSequenceController launch;
        [SerializeField] private PlayerHealth health;
        [SerializeField, Range(.05f, .5f)] private float blendSeconds = .16f;
        [SerializeField, Range(0f, 5f)] private float maximumViewRoll = 2.2f;
        [SerializeField, Range(0f, 5f)] private float maximumViewPitch = 1.4f;

        private GameFlowManager flow;
        private Vector3 stickPosition, throttlePosition, rightPosition, leftPosition;
        private Quaternion stickRotation, throttleRotation, rightRotation, leftRotation, cameraNeutral;
        private float healthFraction = 1f, impactRemaining, animationTime;
        private bool cached, subscribed;

        public CockpitHandVisualState State { get; private set; } = CockpitHandVisualState.Reset;
        public bool VisualAnimationEnabled { get; set; } = true;
        public Transform RightHandAnchor => rightHandAnchor;
        public Transform LeftHandAnchor => leftHandAnchor;
        public Transform FlightStickPivot => flightStickPivot;
        public Transform ThrottlePivot => throttlePivot;

        public void Configure(Transform stick, Transform throttle, Transform rightHand, Transform leftHand,
            Renderer[] criticalLamps, Camera view, LaunchSequenceController launchSequence, PlayerHealth playerHealth)
        {
            Unsubscribe();
            flightStickPivot=stick;throttlePivot=throttle;rightHandAnchor=rightHand;leftHandAnchor=leftHand;
            warningLamps=criticalLamps??System.Array.Empty<Renderer>();playerCamera=view;launch=launchSequence;health=playerHealth;
            flow=GameFlowManager.Instance;cached=false;CacheNeutralPose();ResetVisualState();Subscribe();
        }

        private void Awake(){flow=GameFlowManager.Instance;CacheNeutralPose();ResetVisualState();}
        private void OnEnable(){flow=GameFlowManager.Instance;CacheNeutralPose();Subscribe();}
        private void Start(){if(flow==null){flow=GameFlowManager.Instance;Subscribe();}}
        private void OnDisable(){Unsubscribe();SetLamps(false);}

        private void CacheNeutralPose()
        {
            if(cached||flightStickPivot==null||throttlePivot==null||rightHandAnchor==null||leftHandAnchor==null)return;
            stickPosition=flightStickPivot.localPosition;stickRotation=flightStickPivot.localRotation;
            throttlePosition=throttlePivot.localPosition;throttleRotation=throttlePivot.localRotation;
            rightPosition=rightHandAnchor.localPosition;rightRotation=rightHandAnchor.localRotation;
            leftPosition=leftHandAnchor.localPosition;leftRotation=leftHandAnchor.localRotation;
            if(playerCamera!=null)cameraNeutral=playerCamera.transform.localRotation;
            cached=true;
        }

        private void Subscribe()
        {
            if(subscribed||!isActiveAndEnabled)return;
            if(flow!=null)flow.OnStateChanged+=HandleStateChanged;
            if(health!=null){health.ShipImpacted+=HandleImpact;health.HealthChanged+=HandleHealth;health.HealthReset+=ResetVisualState;}
            subscribed=true;
            if(health!=null)HandleHealth(health.CurrentHealth,health.MaxHealth);
        }
        private void Unsubscribe()
        {
            if(!subscribed)return;
            if(flow!=null)flow.OnStateChanged-=HandleStateChanged;
            if(health!=null){health.ShipImpacted-=HandleImpact;health.HealthChanged-=HandleHealth;health.HealthReset-=ResetVisualState;}
            subscribed=false;
        }

        private void HandleStateChanged(GameState state)
        {
            if(state==GameState.Reset||state==GameState.Boot)ResetVisualState();
        }
        private void HandleHealth(float current,float maximum)=>healthFraction=maximum>0?Mathf.Clamp01(current/maximum):1f;
        private void HandleImpact(Vector3 _,bool __){impactRemaining=.28f;}

        private void LateUpdate()=>TickVisual(Time.unscaledDeltaTime);
        public void TickVisual(float deltaTime)
        {
            if(!VisualAnimationEnabled||!cached||Time.timeScale<=0)return;
            if(flow!=GameFlowManager.Instance)
            {
                Unsubscribe();flow=GameFlowManager.Instance;Subscribe();
            }
            float dt=Mathf.Max(0,deltaTime);animationTime+=dt;impactRemaining=Mathf.Max(0,impactRemaining-dt);
            GameState gameState=flow!=null?flow.CurrentState:GameState.Boot;
            bool combat=gameState==GameState.Playing||gameState==GameState.BossMeteor;
            CockpitHandVisualState next=impactRemaining>0?CockpitHandVisualState.Impact:
                combat&&healthFraction<=.2f?CockpitHandVisualState.Critical:
                gameState==GameState.Launch||gameState==GameState.Countdown?CockpitHandVisualState.Launch:
                combat?CockpitHandVisualState.Flying:
                gameState==GameState.MissionComplete||gameState==GameState.Result?CockpitHandVisualState.MissionComplete:CockpitHandVisualState.Idle;
            State=next;

            float speed=launch!=null?launch.SpeedAmount:0f,engine=launch!=null?launch.EngineAmount:0f;
            float yaw=0,pitch=0;
            if(playerCamera!=null)
            {
                Vector3 e=(Quaternion.Inverse(cameraNeutral)*playerCamera.transform.localRotation).eulerAngles;
                yaw=Normalize(e.y);pitch=Normalize(e.x);
            }
            float viewRoll=Mathf.Clamp(-yaw*.035f,-maximumViewRoll,maximumViewRoll);
            float viewPitch=Mathf.Clamp(pitch*.025f,-maximumViewPitch,maximumViewPitch);
            float breath=Mathf.Sin(animationTime*1.45f),micro=Mathf.Sin(animationTime*7.1f);
            float launchPush=next==CockpitHandVisualState.Launch?Mathf.Lerp(1.5f,8f,speed):0f;
            float throttle=next==CockpitHandVisualState.Launch?Mathf.Lerp(5f,31f,Mathf.Max(engine,speed)):
                next==CockpitHandVisualState.Flying||next==CockpitHandVisualState.Critical||next==CockpitHandVisualState.Impact?24f:0f;
            float impact=next==CockpitHandVisualState.Impact?impactRemaining/.28f:0f;
            float critical=next==CockpitHandVisualState.Critical?1f:0f;
            float shake=(impact*2.8f+critical*.45f)*micro;
            float tension=impact+critical*.55f;
            Quaternion stickTarget=stickRotation*Quaternion.Euler(-launchPush+viewPitch+shake,0,viewRoll+shake*.45f);
            Quaternion throttleTarget=throttleRotation*Quaternion.Euler(-throttle,0,0);
            Quaternion rightTarget=rightRotation*Quaternion.Euler(tension*2.5f,0,-tension*1.4f);
            Quaternion leftTarget=leftRotation*Quaternion.Euler(tension*1.4f,0,tension*.8f);
            Vector3 rightTargetPosition=rightPosition+new Vector3(0,breath*.0015f-impact*.004f,0);
            Vector3 leftTargetPosition=leftPosition+new Vector3(0,breath*.0012f,impact*.002f);
            float blend=1f-Mathf.Exp(-dt/Mathf.Max(.02f,blendSeconds));
            flightStickPivot.localPosition=Vector3.Lerp(flightStickPivot.localPosition,stickPosition,blend);
            flightStickPivot.localRotation=Quaternion.Slerp(flightStickPivot.localRotation,stickTarget,blend);
            throttlePivot.localPosition=Vector3.Lerp(throttlePivot.localPosition,throttlePosition,blend);
            throttlePivot.localRotation=Quaternion.Slerp(throttlePivot.localRotation,throttleTarget,blend);
            rightHandAnchor.localPosition=Vector3.Lerp(rightHandAnchor.localPosition,rightTargetPosition,blend);
            rightHandAnchor.localRotation=Quaternion.Slerp(rightHandAnchor.localRotation,rightTarget,blend);
            leftHandAnchor.localPosition=Vector3.Lerp(leftHandAnchor.localPosition,leftTargetPosition,blend);
            leftHandAnchor.localRotation=Quaternion.Slerp(leftHandAnchor.localRotation,leftTarget,blend);
            SetLamps(critical>0||impact>.45f);
        }

        public void ResetVisualState()
        {
            CacheNeutralPose();impactRemaining=0;animationTime=0;healthFraction=health!=null?health.HealthNormalized:1f;State=CockpitHandVisualState.Reset;
            if(!cached)return;
            flightStickPivot.localPosition=stickPosition;flightStickPivot.localRotation=stickRotation;
            throttlePivot.localPosition=throttlePosition;throttlePivot.localRotation=throttleRotation;
            rightHandAnchor.localPosition=rightPosition;rightHandAnchor.localRotation=rightRotation;
            leftHandAnchor.localPosition=leftPosition;leftHandAnchor.localRotation=leftRotation;SetLamps(false);
        }
        private void SetLamps(bool visible){foreach(Renderer lamp in warningLamps)if(lamp!=null)lamp.enabled=visible;}
        private static float Normalize(float angle)=>angle>180f?angle-360f:angle;
    }
}
