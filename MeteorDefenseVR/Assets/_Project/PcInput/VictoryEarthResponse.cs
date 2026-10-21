using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.PcInput
{
    // One pooled world-space line; completion timing and player input remain owned by game flow.
    [DefaultExecutionOrder(1250), DisallowMultipleComponent]
    public sealed class VictoryEarthResponse : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new("MD.VictoryEarthResponse");
        private GameFlowManager flow;
        private BossClimaxController boss;
        private MissionCompleteController completion;
        private GameSessionStats stats;
        private PlayerHealth health;
        private HangarLobbyView hangar;
        private Transform earth;
        private Renderer earthSurface;
        private Camera view;
        private LineRenderer ring;
        private Material material;
        private readonly Vector2[] circle = new Vector2[96];
        private readonly Vector3[] positions = new Vector3[96];
        private bool deathPending, qualified, consumed, showing;
        private float age;
        public bool IsPresenting { get; private set; }
        public bool RingVisible => ring != null && ring.enabled;
        public int PresentationCount { get; private set; }
        public float Age => age;
        public int OwnedObjectCount => ring == null ? 0 : 1;
        public int OwnedMaterialCount => material == null ? 0 : 1;

        public void Initialize(GameFlowManager manager, Transform planet, HangarLobbyView bay)
        {
            if (flow != null) return;
            flow=manager; earth=planet; hangar=bay; view=Camera.main;
            boss=FindAnyObjectByType<BossClimaxController>(); completion=FindAnyObjectByType<MissionCompleteController>();
            stats=FindAnyObjectByType<GameSessionStats>(); health=FindAnyObjectByType<PlayerHealth>();
            if(flow==null || earth==null || view==null || boss==null || completion==null) { enabled=false; return; }
            earthSurface=earth.GetComponent<Renderer>();
            completion.PresentationAllowed=CanPresent;
            if(FindAnyObjectByType<GameplayHudOverlayView>()!=null)
                FindAnyObjectByType<MissionCompleteView>()?.SetExternalPresentation(true);
            var obj=new GameObject("VictoryEarthProtectionRing"); obj.transform.SetParent(transform,false);
            ring=obj.AddComponent<LineRenderer>(); material=new Material(Shader.Find("MeteorDefense/Vertex Glow")){name="Victory Earth protection"};
            ring.sharedMaterial=material; ring.useWorldSpace=true; ring.loop=true; ring.positionCount=circle.Length;
            ring.shadowCastingMode=ShadowCastingMode.Off; ring.receiveShadows=false; ring.numCornerVertices=0; ring.numCapVertices=0;
            for(int i=0;i<circle.Length;i++){float angle=i*Mathf.PI*2/circle.Length;circle[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
            ring.enabled=false;
            boss.BossDestroyed+=BossDestroyed; completion.PresentationStarted+=Present;
            flow.OnStateChanged+=StateChanged;
        }
        private void BossDestroyed()
        {
            if(flow.CurrentState==GameState.BossMeteor && !Failed && !consumed) deathPending=true;
        }
        private bool CanPresent() => qualified && !Failed && flow!=null && flow.CurrentState==GameState.MissionComplete;
        private bool Failed => health != null && health.IsMissionFailed;
        private void StateChanged(GameState state)
        {
            if(state==GameState.MissionComplete)
            {
                qualified=deathPending && boss.IsComplete && !Failed && (stats==null || !stats.CampaignMode || stats.AllStagesCleared);
                deathPending=false;
                return;
            }
            Cancel();
            if(state==GameState.Boot || state==GameState.Reset) { consumed=false; PresentationCount=0; }
        }
        private void Present()
        {
            if(!isActiveAndEnabled || !qualified || consumed || Failed || flow.CurrentState!=GameState.MissionComplete)return;
            consumed=true;IsPresenting=true;age=0;PresentationCount++;
        }
        private void LateUpdate()
        {
            using(Marker.Auto())
            {
                if(!IsPresenting)return;
                if(Failed || flow.CurrentState!=GameState.MissionComplete || !completion.IsRunning){Cancel();return;}
                if(Time.timeScale<=0){SetVisible(false,0);return;}
                age+=Time.unscaledDeltaTime;
                float fade=Mathf.SmoothStep(0,1,age/.45f);
                Vector3 forward=(earth.position-view.transform.position).normalized;
                Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
                Vector3 up=Vector3.Cross(forward,right);
                float radius=earthSurface!=null?earthSurface.bounds.extents.x:earth.lossyScale.x*.5f;
                float distance=Vector3.Distance(earth.position,view.transform.position);
                float ratio=Mathf.Clamp(radius/Mathf.Max(.01f,distance),0,.95f);
                Vector3 centre=earth.position-forward*(radius*ratio);
                float expanded=radius*Mathf.Sqrt(1-ratio*ratio)*Mathf.Lerp(1.01f,1.025f,fade);
                for(int i=0;i<positions.Length;i++)positions[i]=centre+(right*circle[i].x+up*circle[i].y)*expanded;
                ring.SetPositions(positions);ring.widthMultiplier=radius*.003f;
                ring.startColor=ring.endColor=new Color(.13f,.55f,.43f,.6f*fade);
                SetVisible(true,fade);
            }
        }
        private void SetVisible(bool visible,float amount)
        {
            if(ring!=null)ring.enabled=visible;
            if(visible || showing)hangar?.SetVictoryGuidance(visible?amount:0);
            showing=visible;
        }
        private void Cancel(){deathPending=qualified=IsPresenting=false;age=0;SetVisible(false,0);}
        private void OnDisable()=>Cancel();
        private void OnDestroy()
        {
            if(boss!=null)boss.BossDestroyed-=BossDestroyed;
            if(completion!=null){completion.PresentationStarted-=Present;if(completion.PresentationAllowed==CanPresent)completion.PresentationAllowed=null;}
            if(flow!=null)flow.OnStateChanged-=StateChanged;
            if(ring!=null)Destroy(ring.gameObject);if(material!=null)Destroy(material);
        }
    }
}
