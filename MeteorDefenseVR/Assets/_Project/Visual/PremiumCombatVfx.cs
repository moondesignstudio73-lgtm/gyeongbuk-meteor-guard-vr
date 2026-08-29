using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Visual
{
    // Eight prewarmed slots; effects never instantiate materials/objects during combat.
    public sealed class PremiumCombatVfx : MonoBehaviour
    {
        [SerializeField] private CombatVfxController source;
        [SerializeField] private Material soft,shock,fireAtlas,rockMaterial;
        [SerializeField] private Mesh fragmentMesh;
        [SerializeField] private LaserBeamView[] beams;
        private sealed class Burst {public ParticleSystem sparks,fire,smoke,fragments,ring; public float delay,scale,hold;public Vector3 position;public bool pending;}
        private Burst[] slots;
        private ParticleSystem[] muzzles;
        private bool[] beamWasActive;
        private LineRenderer[] beamLines;
        private Light flash;
        private int cursor;
        private float flashTime;
        private GameFlowManager flow;
        public static bool Available {get;private set;}
        public int SlotCount=>slots?.Length??0;
        public int ActiveParticleCount
        {
            get
            {
                int count=0;if(slots!=null)foreach(var b in slots)count+=b.sparks.particleCount+b.fire.particleCount+b.smoke.particleCount+b.fragments.particleCount+b.ring.particleCount;
                if(muzzles!=null)foreach(var muzzle in muzzles)count+=muzzle.particleCount;return count;
            }
        }
        public void Configure(CombatVfxController events,Material glow,Material ring,Material fire,Mesh mesh,Material rock,LaserBeamView[] lasers)
        {source=events;soft=glow;shock=ring;fireAtlas=fire;fragmentMesh=mesh;rockMaterial=rock;beams=lasers;}
        private void Awake()
        {
            if(source==null||soft==null)return;
            slots=new Burst[8];
            for(int i=0;i<slots.Length;i++)
            {
                var b=new Burst();slots[i]=b;
                b.sparks=Create("Sparks"+i,soft,.5f,11,.06f,64,ParticleSystemRenderMode.Stretch);
                b.fire=Create("Plasma"+i,fireAtlas,.85f,.15f,2.4f,3,ParticleSystemRenderMode.Billboard);
                var fireColor=b.fire.colorOverLifetime;var fireGradient=new Gradient();fireGradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.85f,.55f),new GradientAlphaKey(0,1)});fireColor.color=fireGradient;
                var sheet=b.fire.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=4;sheet.numTilesY=4;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,.999f));
                b.smoke=Create("Afterglow"+i,soft,1.2f,.65f,.8f,8,ParticleSystemRenderMode.Billboard);
                b.fragments=Create("RockFragments"+i,rockMaterial,1.35f,4,.13f,20,ParticleSystemRenderMode.Mesh);
                b.fragments.GetComponent<ParticleSystemRenderer>().mesh=fragmentMesh;
                b.ring=Create("Shockwave"+i,shock,.48f,0,1,1,ParticleSystemRenderMode.Billboard);
                var size=b.ring.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,7));
                var smokeSize=b.smoke.sizeOverLifetime;smokeSize.enabled=true;smokeSize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,2.4f));
            }
            muzzles=new ParticleSystem[beams.Length];beamWasActive=new bool[beams.Length];beamLines=new LineRenderer[beams.Length];
            for(int i=0;i<beams.Length;i++){muzzles[i]=Create("Muzzle"+i,soft,.12f,.25f,.28f,6,ParticleSystemRenderMode.Billboard);beamLines[i]=beams[i]!=null?beams[i].GetComponent<LineRenderer>():null;}
            var lightObject=new GameObject("PooledImpactLight");lightObject.transform.SetParent(transform,false);flash=lightObject.AddComponent<Light>();flash.type=LightType.Point;flash.range=5;flash.shadows=LightShadows.None;flash.enabled=false;
        }
        private ParticleSystem Create(string name,Material mat,float life,float speed,float size,int max,ParticleSystemRenderMode mode)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.startLifetime=new ParticleSystem.MinMaxCurve(life*.55f,life);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.25f,speed);main.startSize=new ParticleSystem.MinMaxCurve(size*.4f,size);main.maxParticles=max;main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var em=ps.emission;em.enabled=true;em.rateOverTime=0;em.rateOverDistance=0;var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;
            var col=ps.colorOverLifetime;col.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.42f,.07f),.3f),new GradientColorKey(new Color(.16f,.045f,.012f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.6f,.5f),new GradientAlphaKey(0,1)});col.color=g;
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.renderMode=mode;r.lengthScale=2;r.velocityScale=.045f;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return ps;
        }
        private void OnEnable()
        {
            Available=slots!=null;
            if(source!=null){source.HitVfxPlayed+=Hit;source.ExplosionVfxPlayed+=Explode;source.BossExplosionVfxPlayed+=Boss;}
            BindFlow();
        }
        private void Start()=>BindFlow();
        private void BindFlow()
        {
            if(flow!=null)flow.OnStateChanged-=StateChanged;
            flow=GameFlowManager.Instance;if(flow!=null)flow.OnStateChanged+=StateChanged;
        }
        private void OnDisable()
        {
            Available=false;if(source!=null){source.HitVfxPlayed-=Hit;source.ExplosionVfxPlayed-=Explode;source.BossExplosionVfxPlayed-=Boss;}
            if(flow!=null)flow.OnStateChanged-=StateChanged;Clear();
        }
        private void StateChanged(GameState state){if(state==GameState.Boot||state==GameState.Result)Clear();}
        private void Clear()
        {
            if(slots==null)return;foreach(var b in slots){b.pending=false;b.hold=0;var main=b.fire.main;main.simulationSpeed=1;Stop(b.sparks);Stop(b.fire);Stop(b.smoke);Stop(b.fragments);Stop(b.ring);}
            if(muzzles!=null)foreach(var m in muzzles)Stop(m);if(flash!=null)flash.enabled=false;flashTime=0;
            if(beamWasActive!=null)System.Array.Clear(beamWasActive,0,beamWasActive.Length);
        }
        private static void Stop(ParticleSystem ps)=>ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        private void Emit(ParticleSystem ps,Vector3 p,int count,Color color,float scale=1)
        {
            ps.transform.position=p;
            // Emit creates particles, while Play explicitly advances their lifetimes/flipbook.
            // Automatic rates are zero; only the bounded manual burst is emitted.
            if(!ps.isPlaying)ps.Play(false);
            var e=new ParticleSystem.EmitParams{startColor=color,applyShapeToPosition=true};if(scale!=1)e.startSize=ps.main.startSize.constantMax*scale;ps.Emit(e,count);
        }
        private void Hit(Vector3 p)
        {
            if(slots==null)return;var b=slots[cursor++%slots.Length];Emit(b.sparks,p,18,new Color(3,1.5f,.25f));Emit(b.fire,p,3,new Color(2,2.3f,3),.35f);
            flash.transform.position=p;flash.color=new Color(.48f,.7f,1);flashTime=.13f;
        }
        private void Explode(Vector3 p)=>Queue(p,1);
        private void Boss(Vector3 p)=>Queue(p,2.8f);
        private void Queue(Vector3 p,float scale)
        {if(slots==null)return;var b=slots[cursor++%slots.Length];b.position=p;b.scale=scale;b.delay=.065f;b.pending=true;}
        private void Update()
        {
            if(slots==null)return;
            foreach(var b in slots)
            {
                // Local plasma linger only: gaze, camera, physics and global time never pause.
                if(b.hold>0){b.hold=Mathf.Max(0,b.hold-Time.unscaledDeltaTime);var main=b.fire.main;main.simulationSpeed=b.hold>0?.18f:1f;}
                if(!b.pending)continue;b.delay-=Time.unscaledDeltaTime;if(b.delay>0)continue;b.pending=false;
                b.hold=.07f;var plasma=b.fire.main;plasma.simulationSpeed=.18f;
                Emit(b.fire,b.position,1,new Color(3.2f,3.2f,3.2f),b.scale*1.15f);Emit(b.sparks,b.position,b.scale>1?60:30,new Color(3,.85f,.08f),b.scale);
                Emit(b.fragments,b.position,b.scale>1?20:12,Color.white,b.scale);Emit(b.smoke,b.position,7,new Color(.23f,.095f,.06f,.4f),b.scale);
                Emit(b.ring,b.position,1,new Color(2,.38f,.045f,.65f),b.scale);flash.transform.position=b.position;flash.color=new Color(1,.32f,.05f);flashTime=.23f;
            }
            for(int i=0;i<beams.Length;i++)
            {bool active=beams[i]!=null&&beams[i].IsPlaying;if(active&&!beamWasActive[i]&&beamLines[i]!=null)Emit(muzzles[i],beamLines[i].GetPosition(0),5,new Color(.4f,1.3f,5));beamWasActive[i]=active;}
            flashTime=Mathf.Max(0,flashTime-Time.unscaledDeltaTime);flash.enabled=flashTime>0;flash.intensity=flashTime*24;
        }
    }
}
