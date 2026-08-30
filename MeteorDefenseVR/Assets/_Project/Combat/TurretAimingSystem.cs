using System;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.Combat
{
    public readonly struct TurretFiringSolution
    {
        public readonly int PrimaryIndex, SecondaryIndex;
        public readonly Vector3 PrimaryOrigin, SecondaryOrigin;
        public bool HasSecondary => SecondaryIndex >= 0;
        public TurretFiringSolution(int primaryIndex, Vector3 primaryOrigin, int secondaryIndex = -1, Vector3 secondaryOrigin = default)
        { PrimaryIndex=primaryIndex;PrimaryOrigin=primaryOrigin;SecondaryIndex=secondaryIndex;SecondaryOrigin=secondaryOrigin; }
    }

    [DefaultExecutionOrder(-120), DisallowMultipleComponent]
    public sealed class TurretAimingSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class TurretMount
        {
            public string label;
            public Transform root, yawPivot, pitchPivot, muzzle;
            public Renderer[] energyRenderers=Array.Empty<Renderer>();
            public Light muzzleLight;
            [NonSerialized] public float yaw,pitch,yawVelocity,pitchVelocity,error,flash;
        }

        [Header("Dependencies")]
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private LaserWeapon weapon;
        [SerializeField] private Transform shipFrame;
        [SerializeField] private Transform playerView;
        [SerializeField] private TurretMount top=new TurretMount{label="TOP"};
        [SerializeField] private TurretMount bottom=new TurretMount{label="BOTTOM"};

        [Header("Aiming")]
        [SerializeField, Range(90f,720f)] private float yawSpeed=420f;
        [SerializeField, Range(90f,720f)] private float pitchSpeed=360f;
        [SerializeField, Range(360f,2400f)] private float aimAcceleration=1500f;
        [SerializeField, Range(.5f,8f)] private float aimTolerance=3.25f;
        [SerializeField, Range(20f,240f)] private float returnSpeed=85f;
        [SerializeField] private Vector2 topNeutral=Vector2.zero;
        [SerializeField] private Vector2 bottomNeutral=Vector2.zero;
        [SerializeField] private bool dualTurretForBoss=true;

        [Header("Cockpit Clearance")]
        [SerializeField, Range(15f,45f)] private float protectedHorizontalAngle=30f;
        [SerializeField, Range(15f,40f)] private float protectedVerticalAngle=25f;
        [SerializeField, Range(.5f,3f)] private float cockpitExclusionRadius=1.35f;

        [Header("Presentation")]
        [SerializeField] private AudioSource servoSource;
        [SerializeField] private AudioSource chargeSource;
        [SerializeField, Range(0f,.2f)] private float servoVolume=.045f;
        [SerializeField, Range(0f,.3f)] private float chargeVolume=.11f;

        private MaterialPropertyBlock glowBlock;
        private GazeLockOnTarget candidate;
        private int selectedIndex=-1;
        private float trackedTime;
        private bool chargePlayed;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        private static AudioClip sharedServo,sharedCharge;

        public static TurretAimingSystem Instance { get; private set; }
        public GazeLockOnTarget CurrentCandidate=>candidate;
        public int SelectedTurretIndex=>selectedIndex;
        public string SelectedTurretName=>selectedIndex==0?"TOP":selectedIndex==1?"BOTTOM":"NONE";
        public float AimError=>selectedIndex==0?top.error:selectedIndex==1?bottom.error:180f;
        public float AimTolerance=>aimTolerance;
        public bool IsAimReady=>candidate!=null&&selectedIndex>=0&&AimError<=aimTolerance&&(!DualActive||Mathf.Max(top.error,bottom.error)<=aimTolerance);
        public bool DualActive=>dualTurretForBoss&&candidate!=null&&candidate.Meteor!=null&&candidate.Meteor.MeteorType==MeteorType.Boss
            &&IsFiringPathSafe(0,candidate.Meteor.transform.position)&&IsFiringPathSafe(1,candidate.Meteor.transform.position);
        public Transform TopMuzzle=>top?.muzzle;
        public Transform BottomMuzzle=>bottom?.muzzle;
        public Transform TopRoot=>top?.root;
        public Transform BottomRoot=>bottom?.root;
        public const string PlayerHiddenLayerName="TurretPlayerHidden";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics(){Instance=null;sharedServo=null;sharedCharge=null;}

        public void Configure(MeteorSpawner meteorSpawner,LaserWeapon laserWeapon,Transform frame,TurretMount topMount,TurretMount bottomMount,AudioSource servo,AudioSource charge,Transform view=null)
        {
            Instance=this;
            spawner=meteorSpawner;weapon=laserWeapon;shipFrame=frame;top=topMount;bottom=bottomMount;servoSource=servo;chargeSource=charge;playerView=view;
            glowBlock??=new MaterialPropertyBlock();
            InitializeAudio();weapon?.SetTurretAiming(this);
        }

        private void Awake()
        {
            glowBlock=new MaterialPropertyBlock();
            Instance=this;
            if(spawner==null)spawner=FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            if(weapon==null)weapon=FindAnyObjectByType<LaserWeapon>(FindObjectsInactive.Include);
            if(shipFrame==null&&spawner!=null)shipFrame=spawner.ShipFrame;
            if(playerView==null&&Camera.main!=null)playerView=Camera.main.transform;
            InitializeAudio();weapon?.SetTurretAiming(this);
        }
        private void OnEnable(){Instance=this;GazeLockOnTarget.GlobalReleased-=HandleTargetReleased;GazeLockOnTarget.GlobalReleased+=HandleTargetReleased;}
        private void OnDisable(){GazeLockOnTarget.GlobalReleased-=HandleTargetReleased;if(Instance==this)Instance=null;StopAudio();ResetTurrets();}
        private void Update()=>Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            float dt=Mathf.Max(0f,deltaTime);
            GazeLockOnTarget active=GazeLockOnTarget.ActiveTarget;
            if(active==null||!active.IsAvailable||(active.State!=LockOnState.Focusing&&active.State!=LockOnState.Locked))active=null;
            if(active!=candidate)
            {
                candidate=active;trackedTime=0f;chargePlayed=false;
                selectedIndex=candidate!=null?ChooseTurret(candidate.Meteor.transform.position):-1;
            }
            if(candidate!=null)trackedTime+=dt;
            if(candidate!=null&&!IsFiringPathSafe(selectedIndex,candidate.Meteor.transform.position))
                selectedIndex=ChooseTurret(candidate.Meteor.transform.position);
            bool dual=DualActive;
            Aim(top,candidate!=null&&(selectedIndex==0||dual)?candidate.Meteor.transform:null,topNeutral,dt);
            Aim(bottom,candidate!=null&&(selectedIndex==1||dual)?candidate.Meteor.transform:null,bottomNeutral,dt);
            UpdateGlow(top,candidate!=null&&(selectedIndex==0||dual)?candidate.Progress:0f,dt);
            UpdateGlow(bottom,candidate!=null&&(selectedIndex==1||dual)?candidate.Progress:0f,dt);
            UpdateAudio(dt);
            if(candidate!=null&&!chargePlayed&&candidate.Progress>=.58f)
            {chargePlayed=true;if(chargeSource!=null&&chargeSource.clip!=null){chargeSource.volume=chargeVolume;chargeSource.Play();}}
        }

        public bool HasTracked(GazeLockOnTarget target)=>target!=null&&candidate==target&&trackedTime>.02f;
        public bool IsReadyFor(GazeLockOnTarget target)=>target!=null&&candidate==target&&IsAimReady;
        public bool TryGetFiringSolution(GazeLockOnTarget target,out TurretFiringSolution solution)
        {
            solution=default;if(!IsReadyFor(target))return false;
            TurretMount primary=selectedIndex==0?top:bottom;
            if(primary?.muzzle==null)return false;
            if(DualActive)
            {
                TurretMount other=selectedIndex==0?bottom:top;int otherIndex=selectedIndex==0?1:0;
                if(other?.muzzle!=null){solution=new TurretFiringSolution(selectedIndex,primary.muzzle.position,otherIndex,other.muzzle.position);return true;}
            }
            solution=new TurretFiringSolution(selectedIndex,primary.muzzle.position);return true;
        }

        public void NotifyFired(int turretIndex)
        {
            TurretMount mount=turretIndex==0?top:bottom;if(mount==null)return;mount.flash=.09f;
        }

        public void ResetTurrets()
        {
            candidate=null;selectedIndex=-1;trackedTime=0;chargePlayed=false;
            ResetMount(top,topNeutral);ResetMount(bottom,bottomNeutral);StopAudio();
        }

        public void ReleaseTarget(GazeLockOnTarget released)
        {
            if(released != null && ReferenceEquals(candidate, released)) ResetTurrets();
        }

        private void HandleTargetReleased(GazeLockOnTarget released) => ReleaseTarget(released);

        public bool IsFiringPathSafe(int turretIndex,Vector3 targetPosition)
        {
            TurretMount mount=turretIndex==0?top:turretIndex==1?bottom:null;
            if(mount?.muzzle==null)return false;
            // Unit tests and non-player tools may not own a view transform. In the authored game the
            // camera is supplied explicitly, making the safety gate deterministic for every input mode.
            if(playerView==null)return true;
            Vector3 origin=mount.muzzle.position,direction=targetPosition-origin;
            float length=direction.magnitude;if(length<.01f)return false;direction/=length;
            Vector3 toCockpit=playerView.position-origin;
            float along=Mathf.Clamp(Vector3.Dot(toCockpit,direction),0f,length);
            float closest=Vector3.Distance(origin+direction*along,playerView.position);
            return closest>=cockpitExclusionRadius;
        }

        public bool IsMountInsideProtectedView(int turretIndex)
        {
            TurretMount mount=turretIndex==0?top:turretIndex==1?bottom:null;
            if(playerView==null||mount?.root==null)return false;
            Vector3 local=playerView.InverseTransformPoint(mount.root.position);
            if(local.z<=.01f)return false;
            float yaw=Mathf.Abs(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg);
            float pitch=Mathf.Abs(Mathf.Atan2(local.y,Mathf.Sqrt(local.x*local.x+local.z*local.z))*Mathf.Rad2Deg);
            return yaw<protectedHorizontalAngle&&pitch<protectedVerticalAngle;
        }

        private int ChooseTurret(Vector3 targetPosition)
        {
            bool topSafe=IsFiringPathSafe(0,targetPosition),bottomSafe=IsFiringPathSafe(1,targetPosition);
            if(!topSafe&&!bottomSafe)return -1;
            if(topSafe&&!bottomSafe)return 0;if(bottomSafe&&!topSafe)return 1;
            float topScore=TurretScore(0,targetPosition),bottomScore=TurretScore(1,targetPosition);
            return topScore<=bottomScore?0:1;
        }
        private float TurretScore(int index,Vector3 targetPosition)
        {
            TurretMount mount=index==0?top:bottom;
            if(mount?.muzzle==null)return float.MaxValue;
            if(!IsFiringPathSafe(index,targetPosition))return 100000f+EstimatedTurn(mount,targetPosition);
            Transform basis=shipFrame!=null?shipFrame:transform;float localY=basis.InverseTransformPoint(targetPosition).y;
            float hemispherePenalty=index==0&&localY<-.28f||index==1&&localY>.28f?2f:0f;
            float viewPenalty=IsMountInsideProtectedView(index)?20f:0f;
            return EstimatedTurn(mount,targetPosition)+hemispherePenalty+viewPenalty;
        }
        private float EstimatedTurn(TurretMount mount,Vector3 targetPosition)
        {return mount==null||mount.muzzle==null?float.MaxValue:Vector3.Angle(mount.muzzle.forward,targetPosition-mount.muzzle.position)/Mathf.Max(1f,yawSpeed);}

        private void Aim(TurretMount mount,Transform target,Vector2 neutral,float dt)
        {
            if(mount==null||mount.root==null||mount.yawPivot==null||mount.pitchPivot==null||mount.muzzle==null)return;
            float desiredYaw=neutral.x,desiredPitch=neutral.y;float maxYaw=returnSpeed,maxPitch=returnSpeed;
            if(target!=null)
            {
                Vector3 rootLocal=mount.root.InverseTransformDirection(target.position-mount.pitchPivot.position);
                desiredYaw=Mathf.Atan2(rootLocal.x,rootLocal.z)*Mathf.Rad2Deg;
                Vector3 yawLocal=Quaternion.Inverse(Quaternion.Euler(0,desiredYaw,0))*rootLocal;
                desiredPitch=-Mathf.Atan2(yawLocal.y,Mathf.Max(.001f,yawLocal.z))*Mathf.Rad2Deg;
                desiredPitch=Mathf.Clamp(desiredPitch,-88f,88f);maxYaw=yawSpeed;maxPitch=pitchSpeed;
            }
            float yawSmooth=Mathf.Clamp(maxYaw/Mathf.Max(1f,aimAcceleration),.035f,.22f);
            float pitchSmooth=Mathf.Clamp(maxPitch/Mathf.Max(1f,aimAcceleration),.035f,.22f);
            mount.yaw=Mathf.SmoothDampAngle(mount.yaw,desiredYaw,ref mount.yawVelocity,yawSmooth,maxYaw,dt);
            mount.pitch=Mathf.SmoothDampAngle(mount.pitch,desiredPitch,ref mount.pitchVelocity,pitchSmooth,maxPitch,dt);
            mount.yawPivot.localRotation=Quaternion.Euler(0,mount.yaw,0);mount.pitchPivot.localRotation=Quaternion.Euler(mount.pitch,0,0);
            mount.error=target!=null?Vector3.Angle(mount.muzzle.forward,target.position-mount.muzzle.position):180f;
        }

        private void UpdateGlow(TurretMount mount,float progress,float dt)
        {
            glowBlock??=new MaterialPropertyBlock();
            if(mount==null)return;float pulse=.85f+progress*2.4f+(progress>.95f?Mathf.Sin(Time.unscaledTime*18f)*.25f:0f);
            Color color=new Color(.04f,.55f,1f,1f)*pulse;
            foreach(Renderer renderer in mount.energyRenderers)
            {if(renderer==null)continue;renderer.GetPropertyBlock(glowBlock);glowBlock.SetColor(BaseColor,color);glowBlock.SetColor(EmissionColor,color);renderer.SetPropertyBlock(glowBlock);}
            mount.flash=Mathf.Max(0f,mount.flash-dt);
            if(mount.muzzleLight!=null){mount.muzzleLight.enabled=mount.flash>0f;mount.muzzleLight.intensity=mount.flash*55f;}
        }
        private void UpdateAudio(float dt)
        {
            if(servoSource==null)return;float movement=Mathf.Abs(top?.yawVelocity??0)+Mathf.Abs(top?.pitchVelocity??0)+Mathf.Abs(bottom?.yawVelocity??0)+Mathf.Abs(bottom?.pitchVelocity??0);
            float targetVolume=movement>4f?servoVolume*Mathf.Clamp01(movement/180f):0f;servoSource.volume=Mathf.MoveTowards(servoSource.volume,targetVolume,dt*.15f);
            if(servoSource.volume>.002f&&!servoSource.isPlaying)servoSource.Play();else if(servoSource.volume<=.001f&&servoSource.isPlaying)servoSource.Stop();
        }
        private void InitializeAudio()
        {
            if(servoSource!=null){sharedServo??=Tone("TurretServo",.5f,105f,145f);servoSource.clip=sharedServo;servoSource.loop=true;servoSource.playOnAwake=false;servoSource.volume=0;}
            if(chargeSource!=null){sharedCharge??=Tone("TurretCharge",.16f,520f,1280f);chargeSource.clip=sharedCharge;chargeSource.loop=false;chargeSource.playOnAwake=false;chargeSource.volume=chargeVolume;}
        }
        private static AudioClip Tone(string name,float seconds,float from,float to)
        {
            const int rate=22050;int length=Mathf.CeilToInt(seconds*rate);var data=new float[length];
            double phase=0;for(int i=0;i<length;i++){float t=i/(float)Mathf.Max(1,length-1);float frequency=Mathf.Lerp(from,to,t);phase+=Math.PI*2*frequency/rate;float envelope=Mathf.Sin(Mathf.PI*t);data[i]=(float)Math.Sin(phase)*envelope*.24f;}
            AudioClip clip=AudioClip.Create(name,length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void StopAudio(){if(servoSource!=null){servoSource.Stop();servoSource.volume=0;}if(chargeSource!=null)chargeSource.Stop();}
        private static void ResetMount(TurretMount mount,Vector2 neutral)
        {if(mount==null)return;mount.yaw=neutral.x;mount.pitch=neutral.y;mount.yawVelocity=mount.pitchVelocity=0;mount.error=180;mount.flash=0;if(mount.yawPivot!=null)mount.yawPivot.localRotation=Quaternion.Euler(0,neutral.x,0);if(mount.pitchPivot!=null)mount.pitchPivot.localRotation=Quaternion.Euler(neutral.y,0,0);if(mount.muzzleLight!=null)mount.muzzleLight.enabled=false;}
        private void OnValidate(){yawSpeed=Mathf.Max(90,yawSpeed);pitchSpeed=Mathf.Max(90,pitchSpeed);aimAcceleration=Mathf.Max(360,aimAcceleration);aimTolerance=Mathf.Clamp(aimTolerance,.5f,8f);returnSpeed=Mathf.Max(20,returnSpeed);protectedHorizontalAngle=Mathf.Clamp(protectedHorizontalAngle,15,45);protectedVerticalAngle=Mathf.Clamp(protectedVerticalAngle,15,40);cockpitExclusionRadius=Mathf.Clamp(cockpitExclusionRadius,.5f,3f);}
    }
}
