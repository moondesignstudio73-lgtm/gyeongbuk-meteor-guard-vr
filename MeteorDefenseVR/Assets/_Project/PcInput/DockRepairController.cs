using System.Collections.Generic;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MeteorDefenseVR.PcInput
{
    [DisallowMultipleComponent]
    public sealed class DockRepairController : MonoBehaviour
    {
        public enum RepairPhase { Hidden, Approaching, Repairing, Ready }
        public RepairPhase Phase { get; private set; }
        public bool IsInspecting { get; private set; }
        public int VisibleDrones => droneRoot!=null && droneRoot.activeSelf ? 2 : 0;
        public bool ScanVisible => scans!=null && scans[0].enabled;
        public TMP_Text Status => label;
        public Transform PresentationRoot => root!=null?root.transform:null;
        public float Progress => health!=null && health.IsDockRepairing ? health.DockRepairProgress : Phase==RepairPhase.Ready?1:0;
        private PlayerHealth health;
        private CockpitDamagePresentation damage;
        private GameFlowManager flow;
        private OperationsResetController reset;
        private Camera cameraView;
        private GameObject root,droneRoot;
        private Transform[] drones,anchors;
        private LineRenderer[] scans;
        private Renderer[] lenses;
        private RectTransform statusPanel;
        private TextMeshProUGUI label;
        private Material hull,armor,glow,line;
        private MaterialPropertyBlock tint;
        private float elapsed,repairRate;
        private readonly List<Object> resources=new();
        private static readonly Color Cyan=new(.12f,.88f,.86f,1), Amber=new(1,.57f,.2f,1);

        public void Initialize(Camera camera,TMP_FontAsset font,GameFlowManager manager,OperationsResetController resetter)
        {
            cameraView=camera;flow=manager;reset=resetter;
            health=FindAnyObjectByType<PlayerHealth>();damage=FindAnyObjectByType<CockpitDamagePresentation>();
            Build(font);flow.OnStateChanged+=StateChanged;reset.BeforeReset+=BeforeReset;Cancel(false);
        }
        // Reuse the existing non-target body and shared materials; no duplicate scan/repair rig.
        public GameObject CreateControlDrone(Transform parent)
        {
            var copy=Instantiate(drones[0].gameObject,parent,false);copy.name="ControlDroneBody";
            foreach(var collider in copy.GetComponentsInChildren<Collider>(true)){collider.enabled=false;Destroy(collider);}
            return copy;
        }
        public LineRenderer CreateControlScan(Transform parent)
        {
            var copy=Instantiate(scans[0],parent,false);
            copy.name="ControlReturnScan";copy.enabled=false;
            return copy;
        }
        public void BeginDocked()
        {
            Cancel(false);
            if(!isActiveAndEnabled || health==null || !health.BeginDockRepair())return;
            IsInspecting=health.HealthNormalized>=.99999f;
            repairRate=(1-health.HealthNormalized)/1.4f;
            for(int i=0;i<2;i++)anchors[i]=damage!=null?damage.RepairAnchor(i):null;
            root.SetActive(true);droneRoot.SetActive(true);SetPhase(RepairPhase.Approaching);Present();
        }
        private void Update()
        {
            if(Phase==RepairPhase.Hidden || Time.timeScale<=0)return;
            Tick(Time.unscaledDeltaTime);
        }
        public void Tick(float delta)
        {
            if(Phase==RepairPhase.Hidden || !RuntimeValueGuard.IsFinite(delta) || delta<0)return;
            if(flow.CurrentState!=GameState.Result){Cancel(false);return;}
            elapsed+=delta;
            if(Phase==RepairPhase.Approaching && elapsed>=(IsInspecting?.2f:.8f))SetPhase(RepairPhase.Repairing);
            else if(Phase==RepairPhase.Repairing)
            {
                if(!IsInspecting)health.RepairAtDock(repairRate*delta);
                if((IsInspecting && elapsed>=.25f) || (!IsInspecting && health.HealthNormalized>=.99999f))
                {health.EndDockRepair(true);SetPhase(RepairPhase.Ready);}
            }
            Present();
        }
        private void SetPhase(RepairPhase phase)
        {
            Phase=phase;elapsed=0;
            label.text=phase==RepairPhase.Ready?(IsInspecting?"점검 완료 · 출격 준비":"수리 완료 · 출격 준비"):IsInspecting?"기체 점검 중":"수리 중 · 바로 출격 가능";
            label.color=phase==RepairPhase.Ready?Cyan:new Color(.9f,.95f,1);
        }
        public void Cancel(bool complete)
        {
            if(health!=null)health.EndDockRepair(complete);Phase=RepairPhase.Hidden;elapsed=0;
            if(root!=null)root.SetActive(false);
            if(scans!=null)foreach(var scan in scans)if(scan!=null)scan.enabled=false;
            if(droneRoot!=null)droneRoot.SetActive(false);
        }
        private void BeforeReset()=>Cancel(false);
        private void StateChanged(GameState state){if(state!=GameState.Result)Cancel(false);}
        private void OnDisable()=>Cancel(true);
        private void OnDestroy()
        {
            if(flow!=null)flow.OnStateChanged-=StateChanged;
            if(reset!=null)reset.BeforeReset-=BeforeReset;
            if(root!=null)Destroy(root);
            foreach(var resource in resources)if(resource!=null)Destroy(resource);
        }
        private void Present()
        {
            float depth=3.4f;
            float halfHeight=depth*Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f);
            float halfWidth=halfHeight*cameraView.aspect;
            float approach=Phase==RepairPhase.Approaching?Mathf.SmoothStep(0,1,elapsed/(IsInspecting?.2f:.8f)):1;
            float retreat=Phase==RepairPhase.Ready?Mathf.SmoothStep(0,1,elapsed/(IsInspecting?.15f:.8f)):0;
            float arrival=approach*(1-retreat);
            droneRoot.SetActive(Phase!=RepairPhase.Ready || retreat<1);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                var parked=new Vector3(side*halfWidth*.70f,.35f,depth);
                var away=new Vector3(side*(halfWidth+1.5f),1.2f,depth+2);
                drones[i].localPosition=Vector3.Lerp(away,parked,arrival);
                drones[i].localRotation=Quaternion.Euler(0,side*-12,side*(1-arrival)*12);
                tint.SetColor("_BaseColor",Phase==RepairPhase.Ready?Cyan:Amber);lenses[i].SetPropertyBlock(tint);
                scans[i].enabled=Phase==RepairPhase.Repairing;
                if(scans[i].enabled)
                {
                    Vector3 point=anchors[i]!=null?anchors[i].position:root.transform.TransformPoint(side,0,2.3f);
                    point+=root.transform.up*(Mathf.Sin(elapsed*8)*.12f);
                    scans[i].SetPosition(0,point-root.transform.right*.07f);
                    scans[i].SetPosition(1,drones[i].TransformPoint(0,0,-.16f));
                    scans[i].SetPosition(2,point+root.transform.right*.07f);
                }
            }
            // A short status rail above the existing menu; no new center-screen panel.
            float textDepth=2.7f, textHeight=2*textDepth*Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f);
            statusPanel.localScale=Vector3.one*(textHeight/720);
            statusPanel.localPosition=new Vector3(0,textHeight*.415f,textDepth);
        }
        private void Build(TMP_FontAsset font)
        {
            root=new GameObject("DockRepairPresentation");root.transform.SetPositionAndRotation(cameraView.transform.position,cameraView.transform.rotation);
            droneRoot=new GameObject("RepairDrones");droneRoot.transform.SetParent(root.transform,false);
            hull=Material("DroneHull",new Color(.10f,.18f,.23f),false);
            armor=Material("DroneArmor",new Color(.55f,.70f,.74f),false);
            glow=Material("DroneOptic",Cyan,false);
            line=new Material(Shader.Find("MeteorDefense/Vertex Glow")){name="Repair scan"};resources.Add(line);
            tint=new MaterialPropertyBlock();drones=new Transform[2];anchors=new Transform[2];scans=new LineRenderer[2];lenses=new Renderer[2];
            for(int i=0;i<2;i++)
            {
                var drone=new GameObject("RepairDrone"+i);drone.transform.SetParent(droneRoot.transform,false);drones[i]=drone.transform;
                Part(drone.transform,"Body",Vector3.zero,new Vector3(.34f,.20f,.26f),hull);
                Part(drone.transform,"TopArmor",new Vector3(0,.12f,0),new Vector3(.31f,.045f,.23f),armor);
                lenses[i]=Part(drone.transform,"Scanner",new Vector3(0,0,-.143f),new Vector3(.13f,.07f,.02f),glow);
                for(int s=-1;s<=1;s+=2)
                {
                    Part(drone.transform,"Arm",new Vector3(s*.22f,0,0),new Vector3(.14f,.045f,.06f),armor);
                    Part(drone.transform,"Thruster",new Vector3(s*.30f,0,0),new Vector3(.09f,.16f,.15f),hull);
                    Part(drone.transform,"IonVent",new Vector3(s*.30f,-.09f,0),new Vector3(.055f,.025f,.10f),glow);
                }
                var scanObject=new GameObject("RepairScan"+i);scanObject.transform.SetParent(droneRoot.transform,false);
                var scan=scanObject.AddComponent<LineRenderer>();scans[i]=scan;scan.sharedMaterial=line;
                scan.useWorldSpace=true;scan.positionCount=3;scan.widthMultiplier=.006f;scan.startColor=scan.endColor=new Color(.18f,.85f,.92f,.32f);
                scan.shadowCastingMode=ShadowCastingMode.Off;scan.receiveShadows=false;scan.enabled=false;
            }
            var panel=new GameObject("DockRepairStatus",typeof(RectTransform),typeof(Canvas));statusPanel=panel.GetComponent<RectTransform>();
            statusPanel.SetParent(root.transform,false);statusPanel.sizeDelta=new Vector2(780,62);
            var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=cameraView;canvas.sortingOrder=16;
            HangarLobbyView.Image("StatusRail",statusPanel,Vector2.zero,new Vector2(780,62),new Color(.02f,.07f,.09f,.92f));
            var text=new GameObject("RepairStatusText",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(statusPanel,false);
            label=text.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=32;label.enableAutoSizing=false;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            label.rectTransform.sizeDelta=new Vector2(760,60);label.text="수리 중 · 바로 출격 가능";label.ForceMeshUpdate(true);
        }
        private Material Material(string name,Color color,bool lit)
        {
            var mat=new Material(Shader.Find(lit?"Universal Render Pipeline/Lit":"Universal Render Pipeline/Unlit")){name=name};
            mat.SetColor("_BaseColor",color);resources.Add(mat);return mat;
        }
        private static Renderer Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return renderer;
        }
    }
}
