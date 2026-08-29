using System;
using System.Collections;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using Unity.Profiling;
using UnityEngine;

namespace MeteorDefenseVR.Player
{
    [DefaultExecutionOrder(1200), DisallowMultipleComponent]
    public sealed class CockpitDamagePresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class DamagePoint
        {
            public Transform anchor;
            public Renderer[] cracks;
            public Renderer flash;
            public ParticleSystem spark;
            public Renderer scorch;
            public ParticleSystem vapor;
            public ParticleSystem dust;
            [NonSerialized] public int hits;
            [NonSerialized] public float flashRemaining;
            [NonSerialized] public float residualRemaining, residualTick, leakTick, heat;
            [NonSerialized] public int impactFrame = -1;
        }

        [SerializeField] private PlayerHealth health;
        [SerializeField] private GameFlowManager flow;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private DamagePoint[] points = Array.Empty<DamagePoint>();
        [SerializeField] private ParticleSystem exteriorImpact;
        [Tooltip("Decorative avionics ONLY. Do not assign HP, SCORE, remaining meteors or Lock UI.")]
        [SerializeField] private Renderer[] monitors = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] monitorStatic = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] warningLamps = Array.Empty<Renderer>();
        [SerializeField] private Renderer edgeDamage;
        [SerializeField] private Renderer[] emissionStrips = Array.Empty<Renderer>();
        [SerializeField] private AudioSource airLeakSource;
        [SerializeField, Range(0f, .3f)] private float airLeakVolume = .12f;
        [Header("HP fraction (stage starts BELOW threshold)")]
        [SerializeField, Range(.01f, 1f)] private float lightDamageBelow = .70f;
        [SerializeField, Range(.01f, 1f)] private float heavyDamageBelow = .40f;
        [SerializeField, Range(.01f, 1f)] private float criticalDamageBelow = .20f;
        [SerializeField, Range(.01f, .19f)] private float fracturedGlassBelow = .10f;
        [Header("Comfort / bounded feedback")]
        [SerializeField, Range(.08f, .3f)] private float impactFlashSeconds = .16f;
        [SerializeField, Range(.02f, .25f)] private float maximumEdgeAlpha = .12f;
        [SerializeField] private Vector2 lightSparkInterval = new Vector2(2.8f, 4.8f);
        [SerializeField] private Vector2 heavySparkInterval = new Vector2(.9f, 1.8f);
        [SerializeField] private Vector2 criticalSparkInterval = new Vector2(.28f, .65f);
        [SerializeField, Range(12, 48)] private int impactSparkCount = 38;
        [SerializeField, Range(.3f, 1.2f)] private float residualSparkSeconds = .75f;
        [SerializeField, Min(3f)] private float warningSoundInterval = 6f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Severity = Shader.PropertyToID("_Severity");
        private static readonly int Heat = Shader.PropertyToID("_Heat");
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("MD.CockpitDamage.Tick");
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock[] monitorRest;
        private MaterialPropertyBlock[] stripRest;
        private Color[] stripColors;
        private Color[] monitorColors;
        private float[] monitorGains;
        private float sparkCountdown, warningCountdown, flickerCountdown, flickerRemaining, presentationTime;
        private uint randomState = 0xBA715EED;
        private int lastPoint = 1;
        private int lastImpactFrame = -1;
        private bool prepared;
        private float nonCombatCleanup = -1f;
        public int DamageLevel { get; private set; }
        // Preserve the established HP-band contract (0..3); glass has an additional near-failure stage.
        public int GlassDamageLevel { get; private set; }
        public int PointCount => points.Length;
        public bool AirLeakPlaying => airLeakSource != null && airLeakSource.isPlaying;
        public bool HasImpactFlash { get {foreach(var p in points)if(p.flash!=null && p.flash.enabled)return true;return false;} }
        public int VisibleScorchCount { get {int n=0;foreach(var p in points) if(p.scorch!=null && p.scorch.enabled)n++;return n;} }
        public int ParticleCapacity { get {int n=Capacity(exteriorImpact);foreach(var p in points)n+=Capacity(p.spark)+Capacity(p.vapor)+Capacity(p.dust);return n;} }
        public int LastDamagePoint => lastPoint;
        public int ImpactCount { get; private set; }
        public int VisibleCrackCount { get { int n=0; foreach(var p in points) foreach(var r in p.cracks) if(r != null && r.enabled) n++; return n; } }
        public int ActiveParticles { get { int n=Count(exteriorImpact); foreach(var p in points) n+=Count(p.spark)+Count(p.vapor)+Count(p.dust); return n; } }
        public bool HasWarningLight { get { foreach(var r in warningLamps) if(r != null && r.enabled) return true; return false; } }

        public void Configure(PlayerHealth player, GameFlowManager gameFlow, AudioManager audio,
            DamagePoint[] damagePoints, ParticleSystem outside, Renderer[] displays, Renderer[] staticLines,
            Renderer[] lamps, Renderer edges, AudioSource airLeak = null, Renderer[] strips = null)
        {
            Unsubscribe();
            health=player; flow=gameFlow; audioManager=audio; points=damagePoints; exteriorImpact=outside;
            monitors=displays; monitorStatic=staticLines; warningLamps=lamps; edgeDamage=edges;
            airLeakSource=airLeak; emissionStrips=strips ?? Array.Empty<Renderer>();
            prepared=false;
            Prepare(); ResetPresentation(); Subscribe();
        }
        private void Awake() { Prepare(); ResetPresentation(); }
        private void OnEnable()
        {
            Prepare();
            // The authored scene creates its GameFlow in GameplaySceneBootstrap.Awake.
            if(flow==null) flow=GameFlowManager.Instance;
            Subscribe(); if(health != null) UpdateHealth(health.CurrentHealth,health.MaxHealth);
        }
        private void Start() { if(flow==null) {flow=GameFlowManager.Instance;Subscribe();} }
        private void OnDisable() { Unsubscribe(); ResetPresentation(); }
        private void Update() => Tick(Time.unscaledDeltaTime);
        private void Prepare()
        {
            if(prepared) return;
            prepared=true; properties=new MaterialPropertyBlock();
            // A culled cockpit must not pause a burst and replay stale sparks on return.
            // Authored, finite systems: no Instantiate, material clones, coroutine or catch-up backlog.
            foreach(var point in points) {EnsureFiniteBurst(point.spark);EnsureFiniteBurst(point.vapor);EnsureFiniteBurst(point.dust);}
            EnsureFiniteBurst(exteriorImpact);
            monitorRest=new MaterialPropertyBlock[monitors.Length]; monitorColors=new Color[monitors.Length];
            monitorGains=new float[monitors.Length];
            for(int i=0;i<monitors.Length;i++)
            {
                monitorRest[i]=new MaterialPropertyBlock();
                if(monitors[i]==null) continue;
                monitors[i].GetPropertyBlock(monitorRest[i]);
                monitorColors[i]=monitors[i].sharedMaterial != null ? monitors[i].sharedMaterial.GetColor(BaseColor) : Color.white;
            }
            stripRest=new MaterialPropertyBlock[emissionStrips.Length];stripColors=new Color[emissionStrips.Length];
            for(int i=0;i<emissionStrips.Length;i++)
            {
                stripRest[i]=new MaterialPropertyBlock();if(emissionStrips[i]==null)continue;
                emissionStrips[i].GetPropertyBlock(stripRest[i]);
                stripColors[i]=emissionStrips[i].sharedMaterial.GetColor(BaseColor);
            }
        }
        private void Subscribe()
        {
            if(!isActiveAndEnabled) return;
            Unsubscribe();
            if(health != null) { health.ShipImpacted+=Impact; health.HealthChanged+=UpdateHealth; health.HealthReset+=ResetPresentation; }
            if(flow != null) flow.OnStateChanged+=StateChanged;
        }
        private void Unsubscribe()
        {
            if(health != null) { health.ShipImpacted-=Impact; health.HealthChanged-=UpdateHealth; health.HealthReset-=ResetPresentation; }
            if(flow != null) flow.OnStateChanged-=StateChanged;
        }
        public int LevelFor(float fraction) => fraction < criticalDamageBelow ? 3 : fraction < heavyDamageBelow ? 2 : fraction < lightDamageBelow ? 1 : 0;
        private void UpdateHealth(float current,float maximum)
        {
            int next=LevelFor(maximum > 0 ? current/maximum : 1);
            int glass=current/Mathf.Max(1,maximum)<fracturedGlassBelow?4:next;
            bool repaired=next<DamageLevel;
            if(next!=DamageLevel) {sparkCountdown=.5f;flickerCountdown=.4f;warningCountdown=1f;}
            DamageLevel=next;GlassDamageLevel=glass;
            if(repaired && next<3) StopLeaks();
            RefreshCracks();
        }
        private void StateChanged(GameState state)
        {
            if(state==GameState.Reset || state==GameState.Boot) ResetPresentation();
            else nonCombatCleanup=state==GameState.Playing || state==GameState.BossMeteor ? -1f : .35f;
            if(!InCombat) StopLeaks();
            // A final impact may finish its <= .32s burst in Result. Tick gates all recurring
            // sparks/flicker/audio to combat; Reset/Boot clears particles immediately.
        }
        private bool InCombat => flow==null || flow.CurrentState==GameState.Playing || flow.CurrentState==GameState.BossMeteor;
        private void Impact(Vector3 position,bool applied)
        {
            if(points.Length==0) return;
            int selected=FindNearestPoint(position);
            lastPoint=selected; ImpactCount++;
            var point=points[selected]; point.flashRemaining=impactFlashSeconds;
            point.impactFrame=lastImpactFrame=Time.frameCount;
            point.residualRemaining=residualSparkSeconds;point.residualTick=.12f;point.heat=1f;
            flickerRemaining=.18f;
            if(applied) { point.hits=Mathf.Min(4,point.hits+1); audioManager?.TryPlay(AudioCue.GlassHit); }
            RefreshCracks();
            Emit(point.spark,impactSparkCount);
            if(exteriorImpact!=null) {exteriorImpact.transform.position=position; Emit(exteriorImpact,28);}
            Tick(0); // The same damage event shows flash + display glitch; no one-frame delay.
        }
        public int FindNearestPoint(Vector3 position)
        {
            int selected=0; float best=float.MaxValue;
            for(int i=0;i<points.Length;i++)
            {
                if(points[i].anchor==null) continue;
                float distance=(points[i].anchor.position-position).sqrMagnitude;
                if(distance<best) {best=distance;selected=i;}
            }
            return selected;
        }
        private void RefreshCracks()
        {
            for(int i=0;i<points.Length;i++)
            {
                var point=points[i];
                // HP-stage fractures remain accumulated when the next hit changes sides.
                int count=Mathf.Max(point.hits,point.hits>0?GlassDamageLevel:0);
                for(int j=0;j<point.cracks.Length;j++) if(point.cracks[j]!=null) point.cracks[j].enabled=j<count;
                if(point.scorch!=null)
                {
                    point.scorch.enabled=point.hits>0;
                    point.scorch.GetPropertyBlock(properties);properties.SetFloat(Severity,Mathf.Clamp01(Mathf.Max(point.hits,DamageLevel)/3f));point.scorch.SetPropertyBlock(properties);
                }
            }
            if(ImpactCount>0) GlassDamageLevel=Mathf.Max(1,GlassDamageLevel);
        }
        public void Tick(float deltaTime)
        {
            if(!prepared || (Application.isPlaying && Time.timeScale<=0)) return;
            using(TickMarker.Auto())
            {
                float dt=RuntimeValueGuard.Delta(deltaTime); presentationTime+=dt;
                bool combat=InCombat;
                foreach(var p in points)
                {
                    // The frame's delta includes time BEFORE this impact. Never consume a fresh
                    // flash in that same frame, even after a long startup/transition frame.
                    float pointDelta=p.impactFrame==Time.frameCount?0:dt;
                    p.flashRemaining=Mathf.Max(0,p.flashRemaining-pointDelta);
                    float flash=p.flashRemaining/Mathf.Max(.01f,impactFlashSeconds);
                    Tint(p.flash,new Color(4.8f,2.3f,.75f,.8f*flash*flash),flash>0);
                    p.heat=Mathf.Max(0,p.heat-pointDelta*.55f);
                    if(p.scorch!=null && p.scorch.enabled) {p.scorch.GetPropertyBlock(properties);properties.SetFloat(Heat,p.heat);p.scorch.SetPropertyBlock(properties);}
                    if(combat && p.residualRemaining>0)
                    {
                        p.residualRemaining-=pointDelta;p.residualTick-=pointDelta;
                        if(p.residualTick<=0) {Emit(p.spark,3);p.residualTick=.13f;}
                    }
                    // Edge points only. Emission is finite and driven by this timer (no autonomous loop).
                    if(combat && DamageLevel>=3 && p.vapor!=null && (p.hits>0 || p==points[lastPoint] || (lastPoint==1 && p==points[0])))
                    {
                        p.leakTick-=dt;
                        if(p.leakTick<=0) {Emit(p.vapor,1);Emit(p.dust,1);p.leakTick=.18f;}
                    }
                }
                if(!combat && nonCombatCleanup>=0f)
                {
                    nonCombatCleanup-=dt;
                    if(nonCombatCleanup<=0f) {StopSparks();nonCombatCleanup=-1f;}
                }
                if(combat && DamageLevel>0)
                {
                    sparkCountdown-=dt;
                    if(sparkCountdown<=0)
                    {
                        if(points.Length>0) Emit(points[(int)(NextRandom()*points.Length)%points.Length].spark,DamageLevel==3?9:5);
                        Vector2 range=DamageLevel==1?lightSparkInterval:DamageLevel==2?heavySparkInterval:criticalSparkInterval;
                        sparkCountdown=Mathf.Lerp(range.x,range.y,NextRandom());
                    }
                }
                flickerRemaining=Mathf.Max(0,flickerRemaining-(lastImpactFrame==Time.frameCount?0:dt));
                if(combat && DamageLevel>=2)
                {
                    flickerCountdown-=dt;
                    if(flickerCountdown<=0) {flickerRemaining=.12f;flickerCountdown=Mathf.Lerp(DamageLevel==3?.45f:1.2f,DamageLevel==3?1.1f:2.5f,NextRandom());}
                }
                for(int i=0;i<monitors.Length;i++)
                {
                    float gain=1;
                    if(combat && flickerRemaining>0 && (i%2==lastPoint%2 || DamageLevel==3)) gain=((int)(presentationTime*35)%2==0)?.08f:.35f;
                    if(combat && DamageLevel==3 && i==0) gain=.04f;
                    if(!Mathf.Approximately(monitorGains[i],gain)) {Tint(monitors[i],monitorColors[i]*gain,true);monitorGains[i]=gain;}
                }
                foreach(var r in monitorStatic) if(r!=null) r.enabled=combat && flickerRemaining>0;
                for(int i=0;i<emissionStrips.Length;i++)
                {
                    float gain=combat && flickerRemaining>0?.12f:1f;
                    Tint(emissionStrips[i],stripColors[i]*gain,true);
                }
                float pulse=.5f+.5f*Mathf.Sin(presentationTime*3f);
                foreach(var r in warningLamps) Tint(r,new Color(1,.12f,.025f,.18f+.3f*pulse),combat && DamageLevel>=2);
                Tint(edgeDamage,new Color(.9f,.23f,.08f,maximumEdgeAlpha*DamageLevel/3f),DamageLevel>=2);
                if(combat && DamageLevel==3)
                {
                    warningCountdown-=dt;
                    if(warningCountdown<=0) { audioManager?.TryPlay(AudioCue.Warning); warningCountdown=warningSoundInterval; }
                }
                UpdateLeakAudio(combat && DamageLevel>=3,dt);
            }
        }
        private float NextRandom() {randomState^=randomState<<13;randomState^=randomState>>17;randomState^=randomState<<5;return (randomState & 0xFFFFFF)/16777216f;}
        private void Tint(Renderer renderer,Color color,bool visible)
        {
            if(renderer==null) return;
            renderer.enabled=visible;
            if(!visible) return;
            renderer.GetPropertyBlock(properties); properties.SetColor(BaseColor,color);renderer.SetPropertyBlock(properties);
        }
        private static void Emit(ParticleSystem ps,int count)
        {
            if(ps==null) return;
            if(!ps.isPlaying) ps.Play(false);
            ps.Emit(Mathf.Min(count,Mathf.Max(0,ps.main.maxParticles-ps.particleCount)));
        }
        private void StopSparks()
        {
            foreach(var p in points) {Stop(p.spark);p.residualRemaining=0;}
            Stop(exteriorImpact);
            StopLeaks();
        }
        private void StopLeaks()
        {
            foreach(var p in points) {Stop(p.vapor);Stop(p.dust);p.leakTick=0;}
            if(airLeakSource!=null) {airLeakSource.Stop();airLeakSource.volume=0;}
        }
        private void UpdateLeakAudio(bool active,float dt)
        {
            if(airLeakSource==null) return;
            if(!active || audioManager==null || !audioManager.isActiveAndEnabled) {if(airLeakSource.isPlaying)airLeakSource.Stop();airLeakSource.volume=0;return;}
            airLeakSource.volume=Mathf.MoveTowards(airLeakSource.volume,audioManager.SfxOutputGain*airLeakVolume,dt*.12f);
            if(!airLeakSource.isPlaying && airLeakSource.clip!=null) airLeakSource.Play();
        }
        private static int Count(ParticleSystem ps)=>ps!=null?ps.particleCount:0;
        private static int Capacity(ParticleSystem ps)=>ps!=null?ps.main.maxParticles:0;
        private static void Stop(ParticleSystem ps) {if(ps!=null) ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private static void EnsureFiniteBurst(ParticleSystem ps)
        {
            if(ps==null) return;
            var main=ps.main;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        }
        public void ResetPresentation()
        {
            DamageLevel=0;GlassDamageLevel=0; ImpactCount=0;lastPoint=points.Length>1?1:0;lastImpactFrame=-1;
            nonCombatCleanup=-1f;
            sparkCountdown=1f;warningCountdown=1f;flickerCountdown=1f;flickerRemaining=0;presentationTime=0;
            foreach(var p in points)
            {
                p.hits=0;p.flashRemaining=0;p.residualRemaining=0;p.residualTick=0;p.heat=0;p.leakTick=0;p.impactFrame=-1;
                foreach(var r in p.cracks) if(r!=null) {r.enabled=false;r.SetPropertyBlock(null);}
                if(p.flash!=null){p.flash.enabled=false;p.flash.SetPropertyBlock(null);}
                if(p.scorch!=null){p.scorch.enabled=false;p.scorch.SetPropertyBlock(null);}
            }
            StopSparks();
            foreach(var r in warningLamps) if(r!=null) r.enabled=false;
            foreach(var r in monitorStatic) if(r!=null) r.enabled=false;
            if(edgeDamage!=null) edgeDamage.enabled=false;
            if(monitorRest!=null) for(int i=0;i<monitors.Length;i++) {monitorGains[i]=1; if(monitors[i]!=null) monitors[i].SetPropertyBlock(monitorRest[i]);}
            if(stripRest!=null) for(int i=0;i<emissionStrips.Length;i++) if(emissionStrips[i]!=null)emissionStrips[i].SetPropertyBlock(stripRest[i]);
        }
        public IEnumerator PrewarmParticles()
        {
            // Authored objects/materials: simulate at zero without emitting or showing a flash.
            foreach(var p in points) {Warm(p.spark);Warm(p.vapor);Warm(p.dust);yield return null;}
            if(exteriorImpact!=null){exteriorImpact.Simulate(0,false,true);Stop(exteriorImpact);}
        }
        private static void Warm(ParticleSystem ps) {if(ps!=null){ps.Simulate(0,false,true);Stop(ps);}}
        private void OnValidate()
        {
            lightDamageBelow=Mathf.Clamp(lightDamageBelow,.03f,1f);
            heavyDamageBelow=Mathf.Clamp(heavyDamageBelow,.02f,lightDamageBelow-.01f);
            criticalDamageBelow=Mathf.Clamp(criticalDamageBelow,.01f,heavyDamageBelow-.01f);
            fracturedGlassBelow=Mathf.Clamp(fracturedGlassBelow,.005f,criticalDamageBelow-.005f);
            warningSoundInterval=Mathf.Max(3f,warningSoundInterval);
            ValidateInterval(ref lightSparkInterval);ValidateInterval(ref heavySparkInterval);ValidateInterval(ref criticalSparkInterval);
        }
        private static void ValidateInterval(ref Vector2 range) {range.x=Mathf.Max(.18f,range.x);range.y=Mathf.Max(range.x,range.y);}
    }
}
