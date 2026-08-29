using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Visual
{
    [DisallowMultipleComponent]
    public sealed class CockpitTelemetryPresentation : MonoBehaviour
    {
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private GameSessionStats stats;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CockpitDamagePresentation damagePresentation;
        [SerializeField] private LaserWeapon weapon;
        [SerializeField] private TurretAimingSystem turrets;
        [SerializeField] private Transform shipFrame;
        [SerializeField] private TMP_Text shipStatus;
        [SerializeField] private TMP_Text threatAnalysis;
        [SerializeField] private TMP_Text radarReadout;
        [SerializeField] private TMP_Text weaponStatus;
        [SerializeField] private TMP_Text systemsStatus;
        [SerializeField] private Transform radarSweep;
        [SerializeField] private Transform[] radarBlips = Array.Empty<Transform>();
        [SerializeField, Range(8, 16)] private int maximumBlips = 16;
        [SerializeField, Range(2f, 10f)] private float refreshRate = 8f;
        [SerializeField] private float radarRadius = .26f;
        [SerializeField] private float radarVerticalRadius = .14f;
        [SerializeField, Min(50f)] private float radarRange = 200f;

        private readonly ThreatSample[] samples = new ThreatSample[17];
        private Transform[] liveBlips = Array.Empty<Transform>();
        private Renderer[] liveRenderers = Array.Empty<Renderer>();
        private TMP_Text[] liveLabels = Array.Empty<TMP_Text>();
        private int[] clusterCounts = Array.Empty<int>();
        private float[] liveBaseScales = Array.Empty<float>();
        private ThreatSample[] displayedSamples = Array.Empty<ThreatSample>();
        private LineRenderer lookCone;
        private TMP_Text lookPitch;
        private Transform radarRoot;
        private Camera playerCamera;
        private MaterialPropertyBlock colorBlock;
        private bool radarBuilt;
        private float displayedHealth=1f;
        private float statusPulseUntil;
        private float firingUntil;
        private bool firedTop;
        private bool firedBottom;
        private float lastTargetRange;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");
        private static readonly Color Cyan = new Color(.12f, .9f, 1f, 1f);
        private static readonly Color Yellow = new Color(1f, .84f, .18f, 1f);
        private static readonly Color Orange = new Color(1f, .42f, .08f, 1f);
        private static readonly Color Red = new Color(1f, .12f, .04f, 1f);

        private float nextRefresh;
        public int VisibleBlips { get; private set; }
        public float RefreshRate => refreshRate;
        public int RadarPoolCapacity => liveBlips.Length;
        public int TrackedThreatCount { get; private set; }
        public float MostUrgentTtc { get; private set; } = float.PositiveInfinity;
        public float CurrentLookYaw { get; private set; }

        public void Configure(MeteorSpawner meteorSpawner, PlayerHealth playerHealth, GameSessionStats sessionStats,
            DifficultyManager manager, Transform reference, TMP_Text left, TMP_Text right, TMP_Text radar,
            Transform sweep, Transform[] blips, TMP_Text weaponReadout=null, TMP_Text systemsReadout=null)
        {
            spawner=meteorSpawner; health=playerHealth; stats=sessionStats; difficulty=manager; shipFrame=reference;
            shipStatus=left; threatAnalysis=right; radarReadout=radar; radarSweep=sweep; radarBlips=blips ?? Array.Empty<Transform>();
            weaponStatus=weaponReadout;systemsStatus=systemsReadout;
            damagePresentation=FindAnyObjectByType<CockpitDamagePresentation>(FindObjectsInactive.Include);
            weapon=FindAnyObjectByType<LaserWeapon>(FindObjectsInactive.Include);turrets=FindAnyObjectByType<TurretAimingSystem>(FindObjectsInactive.Include);
        }

        private void OnEnable() { ResolveReferences(); Subscribe();displayedHealth=health!=null?health.HealthNormalized:1f;BuildRadar(); nextRefresh=0; Refresh(); }
        private void OnDisable()=>Unsubscribe();
        private void Update()
        {
            float targetHealth=health!=null?health.HealthNormalized:1f;
            displayedHealth=Mathf.MoveTowards(displayedHealth,targetHealth,Time.unscaledDeltaTime*2.8f);
            UpdateMonitorDamage();
            UpdateLookPresentation();
            if (radarSweep != null) radarSweep.localRotation=Quaternion.Euler(0,0,-Time.unscaledTime*32f);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh=Time.unscaledTime+1f/Mathf.Max(1,refreshRate); Refresh();
        }

        private void UpdateMonitorDamage()
        {
            int level=damagePresentation!=null?damagePresentation.DamageLevel:0;
            int cadence=level>=3?11:19;bool flicker=level>=2&&((int)(Time.unscaledTime*24f)%cadence==0);
            float alpha=flicker?.48f:1f;
            if(shipStatus!=null)shipStatus.alpha=alpha;if(threatAnalysis!=null)threatAnalysis.alpha=alpha;
            if(radarReadout!=null)radarReadout.alpha=alpha;if(weaponStatus!=null)weaponStatus.alpha=alpha;
            if(systemsStatus!=null)systemsStatus.alpha=alpha;
        }

        private void ResolveReferences()
        {
            if (spawner == null) spawner = FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            if (boss == null) boss = FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            if (health == null) health = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (difficulty == null) difficulty = FindAnyObjectByType<DifficultyManager>(FindObjectsInactive.Include);
            if (damagePresentation == null) damagePresentation=FindAnyObjectByType<CockpitDamagePresentation>(FindObjectsInactive.Include);
            if (weapon == null) weapon=FindAnyObjectByType<LaserWeapon>(FindObjectsInactive.Include);
            if (turrets == null) turrets=FindAnyObjectByType<TurretAimingSystem>(FindObjectsInactive.Include);
            if (playerCamera == null) playerCamera = Camera.main;
            radarRoot = radarSweep != null ? radarSweep.parent : radarBlips.Length > 0 && radarBlips[0] != null ? radarBlips[0].parent : null;
        }

        private void Subscribe()
        {
            if(health!=null){health.HealthChanged-=HandleHealthChanged;health.HealthChanged+=HandleHealthChanged;}
            if(weapon!=null){weapon.Fired-=HandleFired;weapon.Fired+=HandleFired;}
        }
        private void Unsubscribe()
        {
            if(health!=null)health.HealthChanged-=HandleHealthChanged;
            if(weapon!=null)weapon.Fired-=HandleFired;
        }
        private void HandleHealthChanged(float current,float maximum){statusPulseUntil=Time.unscaledTime+.22f;}
        private void HandleFired(GazeLockOnTarget target,MeteorController meteor)
        {
            firingUntil=Time.unscaledTime+.2f;firedTop=turrets!=null&&(turrets.SelectedTurretIndex==0||turrets.DualActive);
            firedBottom=turrets!=null&&(turrets.SelectedTurretIndex==1||turrets.DualActive);
            if(meteor!=null&&spawner!=null)lastTargetRange=Vector3.Distance(spawner.ShipCenter,meteor.transform.position);
        }

        public void Refresh()
        {
            float hp=health != null ? health.HealthNormalized : 1f;
            int damageLevel=damagePresentation!=null?damagePresentation.DamageLevel:hp<.2f?3:hp<.4f?2:hp<.7f?1:0;
            float hull=Mathf.Clamp01(Mathf.Min(hp,1f-damageLevel*.22f));
            string level=hp<.15f?"CRITICAL":hp<.35f?"WARNING":hp<.7f?"DAMAGED":"NOMINAL";
            string engine=health!=null&&health.HealthDepleted?"OFFLINE":damageLevel>=3||hp<.2f?"WARNING":"ONLINE";
            string pulse=Time.unscaledTime<statusPulseUntil?"<color=#FFFFFF>":$"<color=#{StatusColor(hp)}>";
            if (shipStatus != null) shipStatus.text=$"SHIP // {pulse}{level}</color>\nSHIELD    {Mathf.RoundToInt(displayedHealth*100):000}%\nHULL      {Mathf.RoundToInt(hull*100):000}%\nENGINE    {engine}\nDAMAGE    {(health!=null?health.TotalDamageTaken:0):000}";

            MeteorController closest=null; float closestTime=float.PositiveInfinity,closestDistance=0;
            var active=spawner != null ? spawner.ActiveMeteors : null;
            if (active != null) for(int i=0;i<active.Count;i++)
            {
                MeteorController meteor=active[i]; if(meteor==null||!meteor.IsTargetable)continue;
                float distance=Vector3.Distance(spawner.ShipCenter,meteor.transform.position);
                float time=meteor.Speed>.01f?Mathf.Max(0,distance-spawner.ShipCollisionRadius-meteor.CollisionRadius)/meteor.Speed:999;
                if(time<closestTime){closest=meteor;closestTime=time;closestDistance=distance;}
            }
            string risk=closest==null?"CLEAR":closestTime<5?"CRITICAL":"WARNING";
            string vector=closest==null?"STABLE":Direction(closest.transform.position);
            if(threatAnalysis!=null) threatAnalysis.text=closest==null
                ? $"THREAT // <color=#35F4FF>CLEAR</color>\nCONTACTS  00\nVECTOR    STABLE\nRANGE     ---m\nSCORE     {(stats!=null?stats.Score:0):N0}"
                : $"THREAT // <color=#{RiskColor(closestTime)}>{risk}</color>\nCONTACTS  {active.Count:00}\nVECTOR    {vector}\nRANGE     {closestDistance:000}m\nSCORE     {(stats!=null?stats.Score:0):N0}";
            RefreshBlips(active);
            if(radarReadout!=null)
            {
                string ttc=float.IsPositiveInfinity(MostUrgentTtc)?"CLEAR":MostUrgentTtc.ToString("00.0")+"s";
                radarReadout.text=$"360° THREAT RADAR // {(difficulty!=null?DifficultyConfig.Code(difficulty.Selected):"NORMAL")}\nCONTACTS {TrackedThreatCount:00}   PRIORITY {ttc}";
            }
            RefreshSecondaryDisplays(closest,closestDistance,engine,damageLevel);
        }

        private void RefreshSecondaryDisplays(MeteorController closest,float closestDistance,string engine,int damageLevel)
        {
            GazeLockOnTarget candidate=GazeLockOnTarget.ActiveTarget;bool firing=Time.unscaledTime<firingUntil;
            string target=firing?"FIRING":candidate==null?"SEARCH":candidate.IsLocked?"LOCKED":"LOCKING";
            float range=candidate!=null&&candidate.Meteor!=null&&spawner!=null?Vector3.Distance(spawner.ShipCenter,candidate.Meteor.transform.position):firing?lastTargetRange:closestDistance;
            string top=TurretState(0,firing&&firedTop,candidate),bottom=TurretState(1,firing&&firedBottom,candidate);
            if(weaponStatus!=null)weaponStatus.text=$"WEAPON SYSTEM\nTOP TURRET      {top}\nBOTTOM TURRET   {bottom}\nTARGET          {target}\nRANGE           {(range>0?range.ToString("000")+"m":"---")}";
            if(systemsStatus!=null)systemsStatus.text=$"SENSOR ARRAY\nGAZE       {(candidate!=null?"TRACKING":"SEARCH")}\nENGINE     {engine}\nRADAR      {TrackedThreatCount:00} CONTACTS\nPOWER      {(damageLevel>=3?"LOW":"STABLE")}";
        }

        private string TurretState(int index,bool firing,GazeLockOnTarget candidate)
        {
            if(firing)return "FIRING";if(turrets==null)return "READY";
            if(candidate!=null&&(turrets.SelectedTurretIndex==index||turrets.DualActive))return turrets.IsAimReady?"READY":"AIMING";
            return "READY";
        }

        private string Direction(Vector3 world)
        {
            if(spawner==null)return "FRONT";Vector3 local=Quaternion.Inverse(spawner.ShipRotation)*(world-spawner.ShipCenter);
            float ax=Mathf.Abs(local.x),ay=Mathf.Abs(local.y),az=Mathf.Abs(local.z);
            if(ay>ax&&ay>az*.7f)return local.y>=0?"ABOVE":"BELOW";
            if(local.z<0&&az>ax*.65f)return "REAR";
            if(ax>az*.55f)return local.x<0?"LEFT":"RIGHT";
            return "FRONT";
        }

        private void RefreshBlips(System.Collections.Generic.IReadOnlyList<MeteorController> active)
        {
            BuildRadar();
            VisibleBlips=0; TrackedThreatCount=0; MostUrgentTtc=float.PositiveInfinity;
            if (spawner == null || liveBlips.Length == 0) return;
            int sampleCount=0;
            if(active!=null) for(int i=0;i<active.Count && sampleCount<samples.Length;i++)
            {
                MeteorController meteor=active[i]; if(meteor==null||!meteor.IsTargetable)continue;
                samples[sampleCount++]=Sample(meteor);
            }
            if(boss!=null&&boss.ActiveBoss!=null&&boss.ActiveBoss.IsTargetable&&sampleCount<samples.Length)
            {
                bool duplicate=false;for(int i=0;i<sampleCount;i++)if(samples[i].Meteor==boss.ActiveBoss){duplicate=true;break;}
                if(!duplicate)samples[sampleCount++]=Sample(boss.ActiveBoss);
            }
            TrackedThreatCount=sampleCount;
            Array.Sort(samples,0,sampleCount,ThreatSampleComparer.Instance);
            int displayLimit=Mathf.Min(liveBlips.Length,ThreatRadarMath.DisplayLimit(difficulty!=null?difficulty.Selected:DifficultyLevel.Normal));
            for(int i=0;i<clusterCounts.Length;i++)clusterCounts[i]=0;
            for(int i=0;i<sampleCount&&VisibleBlips<displayLimit;i++)
            {
                Vector2 point=ThreatRadarMath.Project(samples[i].Meteor.transform.position,spawner.ShipCenter,spawner.ShipRotation,radarRadius,radarVerticalRadius,radarRange);
                int cluster=-1;
                for(int j=0;j<VisibleBlips;j++)if(Vector2.Distance(point,(Vector2)liveBlips[j].localPosition)<.022f){cluster=j;break;}
                if(cluster>=0){clusterCounts[cluster]++;UpdateLabel(cluster,displayedSamples[cluster],clusterCounts[cluster]);continue;}
                int slot=VisibleBlips++; clusterCounts[slot]=1; Transform marker=liveBlips[slot]; marker.gameObject.SetActive(true);
                marker.localPosition=new Vector3(point.x,point.y,-.009f-slot*.00005f);
                bool isBoss=samples[i].Meteor.MeteorType==MeteorType.Boss;
                marker.localRotation=Quaternion.Euler(0,0,isBoss?45f:0f);
                float priority=i<5?1.2f:1f; float bossScale=isBoss?1.65f:1f;float lockScale=samples[i].Candidate?1.35f:1f;
                liveBaseScales[slot]=priority*bossScale*lockScale;marker.localScale=Vector3.one*liveBaseScales[slot];displayedSamples[slot]=samples[i];
                if(liveLabels[slot]!=null)liveLabels[slot].transform.localRotation=Quaternion.Euler(0,0,isBoss?-45f:0f);
                SetColor(liveRenderers[slot],samples[i].Candidate?new Color(.35f,1f,.48f,1f):isBoss?Orange:RiskColor(samples[i].Risk));
                UpdateLabel(slot,samples[i],1);
                MostUrgentTtc=Mathf.Min(MostUrgentTtc,samples[i].Ttc);
            }
            for(int i=VisibleBlips;i<liveBlips.Length;i++){liveBlips[i].gameObject.SetActive(false);liveBaseScales[i]=1f;}
        }

        private ThreatSample Sample(MeteorController meteor)
        {
            float distance=Vector3.Distance(spawner.ShipCenter,meteor.transform.position);
            float ttc=ThreatRadarMath.TimeToCollision(spawner.ShipCenter,meteor,spawner.ShipCollisionRadius);
            Vector3 local=Quaternion.Inverse(spawner.ShipRotation)*(meteor.transform.position-spawner.ShipCenter);
            return new ThreatSample(meteor,distance,ttc,ThreatRadarMath.Risk(ttc,distance),local.y,
                ThreatRadarMath.IsInView(playerCamera,meteor.transform.position),local.z<0f,GazeLockOnTarget.ActiveTarget?.Meteor==meteor);
        }

        private void UpdateLabel(int slot,ThreatSample sample,int cluster)
        {
            if(slot>=liveLabels.Length||liveLabels[slot]==null)return;
            bool simple=difficulty!=null&&(difficulty.Selected==DifficultyLevel.Experience||difficulty.Selected==DifficultyLevel.Easy);
            string altitude=simple||Mathf.Abs(sample.Height)<2.5f?"":sample.Height>0?"▲":"▼";
            string rear=!simple&&sample.Rear?" REAR":"";
            string outside=!simple&&!sample.InView?" ◇":"";
            string bossText=sample.Meteor.MeteorType==MeteorType.Boss?"BOSS ":"";
            string count=cluster>1?$" x{cluster}":"";
            string priority=sample.Risk>=ThreatRadarRisk.Danger?" !":"";
            string locked=sample.Candidate?"LOCK ":"";
            liveLabels[slot].text=locked+bossText+altitude+rear+outside+priority+count;
            liveLabels[slot].color=sample.Candidate?new Color(.35f,1f,.48f):sample.Meteor.MeteorType==MeteorType.Boss?Orange:RiskColor(sample.Risk);
        }

        private void UpdateLookPresentation()
        {
            if(!radarBuilt||spawner==null)return;
            if(playerCamera==null)playerCamera=Camera.main;if(playerCamera==null)return;
            CurrentLookYaw=ThreatRadarMath.LookYaw(spawner.ShipRotation,playerCamera.transform.forward);
            float pitch=ThreatRadarMath.LookPitch(spawner.ShipRotation,playerCamera.transform.forward);
            if(lookCone!=null)
            {
                float horizontalFov=2f*Mathf.Atan(Mathf.Tan(playerCamera.fieldOfView*.5f*Mathf.Deg2Rad)*playerCamera.aspect)*Mathf.Rad2Deg;
                const int arc=8;lookCone.positionCount=arc+3;lookCone.SetPosition(0,Vector3.zero);
                for(int i=0;i<=arc;i++)
                {
                    float angle=(CurrentLookYaw-horizontalFov*.5f)+(horizontalFov*i/arc);
                    float radians=angle*Mathf.Deg2Rad;
                    lookCone.SetPosition(i+1,new Vector3(Mathf.Sin(radians)*radarRadius*.88f,Mathf.Cos(radians)*radarVerticalRadius*.88f,-.012f));
                }
                lookCone.SetPosition(arc+2,Vector3.zero);
            }
            if(lookPitch!=null)lookPitch.text=pitch>12f?"LOOK ▲":pitch<-12f?"LOOK ▼":"▲";
            float pulse=.9f+.1f*Mathf.Sin(Time.unscaledTime*8f);
            for(int i=0;i<VisibleBlips;i++)if(liveBlips[i]!=null)
                liveBlips[i].localScale=Vector3.one*liveBaseScales[i]*(liveLabels[i]!=null&&liveLabels[i].text.Contains("!")?pulse:1f);
        }

        private void BuildRadar()
        {
            if(radarBuilt)return;ResolveReferences();if(radarRoot==null||radarBlips.Length==0||radarBlips[0]==null)return;
            // Authored scenes before the threat-radar upgrade serialized the old value 8.
            // Always promote that data to the bounded 16-slot pool without requiring scene regeneration.
            const int capacity=16;maximumBlips=capacity;liveBlips=new Transform[capacity];liveRenderers=new Renderer[capacity];liveLabels=new TMP_Text[capacity];clusterCounts=new int[capacity];liveBaseScales=new float[capacity];displayedSamples=new ThreatSample[capacity];
            for(int i=0;i<capacity;i++)
            {
                Transform marker=i<radarBlips.Length&&radarBlips[i]!=null?radarBlips[i]:Instantiate(radarBlips[0],radarRoot);
                marker.name="ThreatBlip_"+i;marker.gameObject.SetActive(false);liveBlips[i]=marker;liveRenderers[i]=marker.GetComponentInChildren<Renderer>(true);liveBaseScales[i]=1f;
            }
            for(int i=0;i<capacity;i++)liveLabels[i]=CreateText(liveBlips[i],"ThreatLabel",.052f,new Vector2(.11f,.035f),new Vector3(.018f,.012f,-.004f));
            Material material=radarSweep!=null&&radarSweep.TryGetComponent(out LineRenderer source)?source.sharedMaterial:liveRenderers[0].sharedMaterial;
            foreach(Transform child in radarRoot){if(child.name.StartsWith("RangeRing"))child.gameObject.SetActive(false);}
            float[] ranges={25,50,100,200};for(int i=0;i<ranges.Length;i++)CreateRangeRing(i,ranges[i],material);
            GameObject coneObject=new GameObject("HeadViewCone");coneObject.transform.SetParent(radarRoot,false);lookCone=coneObject.AddComponent<LineRenderer>();ConfigureLine(lookCone,material,.002f,true);lookCone.loop=false;
            lookPitch=CreateText(radarRoot,"HeadDirection",.058f,new Vector2(.09f,.04f),new Vector3(0,-.015f,-.013f));lookPitch.color=Cyan;
            colorBlock=new MaterialPropertyBlock();radarBuilt=true;
        }

        private void CreateRangeRing(int index,float range,Material material)
        {
            GameObject go=new GameObject("DistanceRing_"+Mathf.RoundToInt(range));go.transform.SetParent(radarRoot,false);LineRenderer ring=go.AddComponent<LineRenderer>();ConfigureLine(ring,material,.0012f,true);
            const int points=64;ring.positionCount=points;float scale=ThreatRadarMath.DistanceRadius(range,radarRange);
            for(int p=0;p<points;p++){float angle=p*Mathf.PI*2f/points;ring.SetPosition(p,new Vector3(Mathf.Sin(angle)*radarRadius*scale,Mathf.Cos(angle)*radarVerticalRadius*scale,-.006f));}
            TMP_Text label=CreateText(radarRoot,"DistanceLabel_"+index,.042f,new Vector2(.06f,.026f),new Vector3(radarRadius*scale+.012f,0,-.012f));label.text=Mathf.RoundToInt(range)+"m";label.color=new Color(Cyan.r,Cyan.g,Cyan.b,.55f);
        }

        private TMP_Text CreateText(Transform parent,string name,float size,Vector2 bounds,Vector3 position)
        {
            GameObject go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);go.transform.localPosition=position;
            TextMeshPro text=go.AddComponent<TextMeshPro>();text.font=radarReadout!=null?radarReadout.font:null;if(radarReadout!=null&&radarReadout.fontSharedMaterial!=null)text.fontSharedMaterial=radarReadout.fontSharedMaterial;
            text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;text.rectTransform.sizeDelta=bounds;text.color=Cyan;return text;
        }

        private static void ConfigureLine(LineRenderer line,Material material,float width,bool loop)
        {
            line.sharedMaterial=material;line.useWorldSpace=false;line.loop=loop;line.startWidth=line.endWidth=width;line.numCapVertices=2;line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.startColor=line.endColor=new Color(Cyan.r,Cyan.g,Cyan.b,.5f);
        }

        private void SetColor(Renderer renderer,Color value)
        {
            if(renderer==null)return;colorBlock.Clear();colorBlock.SetColor(BaseColor,value);colorBlock.SetColor(Color,value);renderer.SetPropertyBlock(colorBlock);
        }
        private static Color RiskColor(ThreatRadarRisk risk)=>risk==ThreatRadarRisk.Imminent?Red:risk==ThreatRadarRisk.Danger?Orange:risk==ThreatRadarRisk.Approaching?Yellow:Cyan;

        private readonly struct ThreatSample
        {
            public readonly MeteorController Meteor;public readonly float Distance,Ttc;public readonly ThreatRadarRisk Risk;public readonly float Height;public readonly bool InView,Rear,Candidate;
            public ThreatSample(MeteorController meteor,float distance,float ttc,ThreatRadarRisk risk,float height,bool inView,bool rear,bool candidate){Meteor=meteor;Distance=distance;Ttc=ttc;Risk=risk;Height=height;InView=inView;Rear=rear;Candidate=candidate;}
        }
        private sealed class ThreatSampleComparer:System.Collections.Generic.IComparer<ThreatSample>
        {
            public static readonly ThreatSampleComparer Instance=new ThreatSampleComparer();
            public int Compare(ThreatSample a,ThreatSample b){if(a.Candidate!=b.Candidate)return b.Candidate.CompareTo(a.Candidate);int risk=b.Risk.CompareTo(a.Risk);if(risk!=0)return risk;int ttc=a.Ttc.CompareTo(b.Ttc);return ttc!=0?ttc:a.Distance.CompareTo(b.Distance);}
        }
        private static string StatusColor(float hp)=>hp<.2f?"FF3218":hp<.4f?"FF791F":hp<.7f?"FFD43B":"35F4FF";
        private static string RiskColor(float seconds)=>seconds<4?"FF3218":seconds<8?"FF791F":seconds<15?"FFD43B":"35F4FF";
    }
}
