using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    public static class CockpitDamageSetup
    {
        private const string AssetsRoot="Assets/Art/CockpitDamage";
        public static void ApplyAndBake()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var cockpit=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry");
            var premium=cockpit.Find("PremiumCockpit");
            if(premium==null) throw new InvalidOperationException("Authored cockpit missing");
            Directory.CreateDirectory(AssetsRoot);AssetDatabase.Refresh();
            var glass=Material("GlassFracture",new Color(.64f,.84f,.91f,.48f));
            var fracture=CockpitImpactAssets.SharedMaterial("FracturedGlass","MeteorDefense/Fractured Glass");
            var scorchMaterial=CockpitImpactAssets.SharedMaterial("ScorchedMetal","MeteorDefense/Scorched Metal");
            var vaporMaterial=CockpitImpactAssets.SharedMaterial("EdgeVapor","MeteorDefense/Edge Vapor");
            var sparkMaterial=CockpitImpactAssets.SharedMaterial("DamageSpark","MeteorDefense/Soft Energy");
            sparkMaterial.SetColor("_BaseColor",new Color(6,4,2,1));sparkMaterial.SetFloat("_Ring",0);
            var warning=Material("DamageAmber",new Color(1f,.15f,.03f,.4f));
            var soft=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/PlasmaSoft.mat");
            if(soft==null) throw new InvalidOperationException("Pre-existing plasma material missing");
            var health=Object.FindAnyObjectByType<PlayerHealth>();
            var root=Child(cockpit,"CockpitDamage");
            var upperRail=MeshObject(root,"OverheadDamageRail",Resources.GetBuiltinResource<Mesh>("Cube.fbx"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/AnodizedCarbon.mat"));
            // Meet both existing canopy struts at this height; never leave a floating damage bar.
            upperRail.transform.localPosition=new Vector3(0,2.03f,2.25f);upperRail.transform.localScale=new Vector3(5.45f,.10f,.16f);upperRail.enabled=true;
            var boundaryTransform=Child(cockpit,"ShipCollisionBoundary");boundaryTransform.localPosition=new Vector3(0,0,2.65f);
            var boundary=Get<ShipCollisionBoundary>(boundaryTransform.gameObject);
            Object.FindAnyObjectByType<MeteorSpawner>().SetImpactBoundary(boundary);
            Object.FindAnyObjectByType<BossClimaxController>().SetImpactBoundary(boundary);
            var points=new CockpitDamagePresentation.DamagePoint[5];
            string[] labels={"Left","Center","Right","Upper","Bottom"};
            for(int i=0;i<points.Length;i++)
            {
                var point=Child(root,"GlassImpact_"+labels[i]);
                point.localPosition=i==3?new Vector3(0,1.9f,2.3f):i==4?new Vector3(0,-1.015f,.24f):new Vector3((i-1)*1.13f,i==1?-.51f:.10f,2.30f);
                point.localRotation=i==4?Quaternion.Euler(90,0,0):Quaternion.identity;
                var cracks=new Renderer[4];
                for(int level=0;level<4;level++)
                {
                    cracks[level]=MeshObject(point,"Crack"+level,CockpitImpactAssets.Fracture(i,level),fracture);
                    // A central hit is shown below the aim corridor, not over the reticle.
                    if(i==1)cracks[level].transform.localScale=Vector3.one*.60f;
                    if(i==3)cracks[level].transform.localScale=Vector3.one*.72f;
                    if(i==4)cracks[level].transform.localScale=Vector3.one*.68f;
                }
                var flash=MeshObject(point,"ImpactFlash",Quad("ImpactFlashStrong",.9f,.9f),soft);
                flash.transform.localPosition=new Vector3(0,0,-.008f);
                var spark=Particle(root,"SparkPoint_"+labels[i],sparkMaterial,64,.65f,2.8f,.040f);
                spark.transform.localPosition=i==3?new Vector3(0,1.97f,2.12f):i==4?new Vector3(.48f,-.91f,.72f):new Vector3((i-1)*1.03f,i==1?-.42f:-.48f,1.58f);
                spark.transform.localRotation=Quaternion.LookRotation(i==3?new Vector3(.2f,-1f,.1f):i==4?new Vector3(-.2f,1f,-.2f):new Vector3((i-1)*.28f,1f,.12f));
                var main=spark.main;main.gravityModifier=.32f;main.startColor=Color.white;
                var scorch=MeshObject(root,"Scorch_"+labels[i],Quad("ScorchPatch",.30f,.19f),scorchMaterial);
                var housing=premium.Find(i==0?"PortPrimary":i==1?"Navigation":i==2?"StarboardPrimary":"Navigation");
                if(i<3)
                {
                    scorch.transform.SetPositionAndRotation(housing.TransformPoint(new Vector3(i==1?.14f:-.12f,.10f,-.076f)),housing.rotation);
                }
                else if(i==3) {scorch.transform.localPosition=new Vector3(0,2.03f,2.168f);scorch.transform.localScale=new Vector3(1.4f,.40f,1);}
                else {scorch.transform.localPosition=new Vector3(.68f,-1.005f,.72f);scorch.transform.localRotation=Quaternion.Euler(90,0,0);scorch.transform.localScale=new Vector3(1.1f,.55f,1);}
                ParticleSystem vapor=null,dust=null;
                if(i!=1)
                {
                    vapor=Particle(root,"AirLeak_"+labels[i],vaporMaterial,12,.85f,1.7f,.12f);
                    dust=Particle(root,"LeakDust_"+labels[i],soft,8,.65f,1.9f,.009f);
                    foreach(var ps in new[]{vapor,dust})
                    {
                        ps.transform.localPosition=point.localPosition+(i==4?new Vector3(.22f,.02f,.18f):new Vector3(i==0?-.17f:i==2?.17f:0,0,-.12f));
                        ps.transform.localRotation=Quaternion.LookRotation(i==4?new Vector3(.08f,-1f,.12f):new Vector3((i-1)*.16f,i==3?.15f:0,1));
                        var shape=ps.shape;shape.angle=6;shape.radius=.018f;
                        var m=ps.main;m.startColor=Color.white;
                        var c=ps.colorOverLifetime;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.7f,.85f,1),1)},new[]{new GradientAlphaKey(.5f,0),new GradientAlphaKey(0,1)});c.color=g;
                    }
                    var vr=vapor.GetComponent<ParticleSystemRenderer>();vr.lengthScale=2.5f;vr.velocityScale=.04f;
                }
                points[i]=new CockpitDamagePresentation.DamagePoint{anchor=point,cracks=cracks,flash=flash,spark=spark,scorch=scorch,vapor=vapor,dust=dust};
            }
            var outside=Particle(root,"PooledShipImpact",soft,48,.4f,2.5f,.08f);
            var monitors=new List<Renderer>(); var noise=new List<Renderer>();
            // Split ONLY the avionics batch into its five existing readouts (1 -> 5 draw calls).
            // Geometry/trim remain batched; HUD has completely separate renderers/layer.
            var batch=premium.Find("CombinedMeshes/AvionicsEmission");
            if(batch!=null) batch.GetComponent<Renderer>().enabled=false;
            string[] panels={"PortSecondary","PortPrimary","Navigation","StarboardPrimary","StarboardSecondary"};
            for(int i=0;i<panels.Length;i++)
            {
                var readout=premium.Find(panels[i]+"/Readout");
                var display=readout.GetComponent<Renderer>();display.enabled=true;monitors.Add(display);
                var staticRenderer=MeshObject(readout,"DamageStatic",NoiseMesh(panels[i]),glass);
                staticRenderer.transform.localPosition=new Vector3(0,0,-.002f);noise.Add(staticRenderer);
            }
            var lamps=new Renderer[4];
            for(int i=0;i<4;i++)
            {
                lamps[i]=MeshObject(root,"DamageWarningLamp"+i,Quad("WarningLamp",.12f,.025f),warning);
                lamps[i].transform.localPosition=i<2?new Vector3(i==0?-.45f:.45f,-.46f,1.48f):new Vector3(i==2?-1.8f:1.8f,.65f,2.24f);
                if(i>=2)lamps[i].transform.localScale=new Vector3(.3f,10f,1);
            }
            var edges=MeshObject(root,"GlassEdgeDamage",EdgeMesh(),warning);
            edges.transform.localPosition=new Vector3(0,0,2.29f);
            var presentation=Get<CockpitDamagePresentation>(health.gameObject);
            var air=Get<AudioSource>(Child(root,"PooledAirLeakAudio").gameObject);air.playOnAwake=false;air.loop=true;air.volume=0;air.spatialBlend=.25f;air.dopplerLevel=0;air.priority=180;
            air.clip=CockpitImpactAssets.Sound("CabinAirLeak",2);
            var strips=new List<Renderer>();
            var instrument=premium.Find("CombinedMeshes/InstrumentCyan");if(instrument!=null)strips.Add(instrument.GetComponent<Renderer>());
            presentation.Configure(health,Object.FindAnyObjectByType<GameFlowManager>(),Object.FindAnyObjectByType<AudioManager>(),points,outside,monitors.ToArray(),noise.ToArray(),lamps,edges,air,strips.ToArray());
            // Upgrade authored defaults once; subsequent Inspector tuning remains intact.
            using(var serialized=new SerializedObject(presentation))
            {
                var light=serialized.FindProperty("lightSparkInterval");if(light.vector2Value==new Vector2(4,7))light.vector2Value=new Vector2(2.8f,4.8f);
                var heavy=serialized.FindProperty("heavySparkInterval");if(heavy.vector2Value==new Vector2(1.7f,3.2f))heavy.vector2Value=new Vector2(.9f,1.8f);
                var critical=serialized.FindProperty("criticalSparkInterval");if(critical.vector2Value==new Vector2(.6f,1.4f))critical.vector2Value=new Vector2(.28f,.65f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // Existing Impact/HUD audio routing remains. Disable only the superseded full red lamp view.
            var legacy=health.GetComponent<PlayerDamageFeedbackView>();if(legacy!=null) legacy.enabled=false;
            health.SetPolicy(GameOverPolicy.NormalMode);
            AddImpactCues(air.clip);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[CockpitImpact] Enhanced impact presentation applied; gameplay and 360 collision boundaries preserved.");
        }
        private static T Get<T>(GameObject go) where T:Component {var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
        private static Transform Child(Transform parent,string name)
        {
            var result=parent.Find(name);if(result==null) {result=new GameObject(name).transform;result.SetParent(parent,false);}
            result.localPosition=Vector3.zero;result.localRotation=Quaternion.identity;result.localScale=Vector3.one;return result;
        }
        private static Material Material(string name,Color color)
        {
            string path=AssetsRoot+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) {m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;
        }
        private static Renderer MeshObject(Transform parent,string name,Mesh mesh,Material mat)
        {
            var t=Child(parent,name);Get<MeshFilter>(t.gameObject).sharedMesh=mesh;
            var r=Get<MeshRenderer>(t.gameObject);r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.enabled=false;return r;
        }
        private static Mesh SaveMesh(string name,List<Vector3> vertices,List<int> triangles,List<Vector2> uv=null)
        {
            string path=AssetsRoot+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(uv!=null)mesh.SetUVs(0,uv);mesh.SetColors(Enumerable.Repeat(Color.white,vertices.Count).ToArray());mesh.RecalculateBounds();mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);return mesh;
        }
        private static void Line(List<Vector3> vertices,List<int> triangles,Vector2 a,Vector2 b,float width)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=vertices.Count;
            vertices.Add(a-n);vertices.Add(a+n);vertices.Add(b+n);vertices.Add(b-n);
            triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
        }
        private static Mesh CrackMesh(int point,int level)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var rng=new System.Random(593+point*77+level*931);
            int rays=level==0?7:9;float outer=.17f+level*.135f;
            for(int ray=0;ray<rays;ray++)
            {
                float angle=(ray+(float)rng.NextDouble()*.4f)*Mathf.PI*2/rays;
                Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                Vector2 previous=d*(level==0?.014f:outer*.42f);
                for(int step=1;step<=4;step++)
                {
                    float radius=Mathf.Lerp(level==0?.014f:outer*.42f,outer,step/4f);
                    Vector2 next=d*radius+new Vector2(-d.y,d.x)*((float)rng.NextDouble()-.5f)*.04f;
                    Line(vertices,triangles,previous,next,step==4?.0017f:.0034f);
                    if(step==2 || step==3)
                        Line(vertices,triangles,next,next+d*.032f+new Vector2(-d.y,d.x)*(.022f+level*.014f),.0015f);
                    previous=next;
                }
            }
            return SaveMesh("Crack_"+point+"_"+level,vertices,triangles);
        }
        private static Mesh Quad(string name,float width,float height)
        {
            return SaveMesh(name,new List<Vector3>{new Vector3(-width/2,-height/2),new Vector3(-width/2,height/2),new Vector3(width/2,height/2),new Vector3(width/2,-height/2)},new List<int>{0,1,2,0,2,3},new List<Vector2>{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
        }
        private static Mesh NoiseMesh(string name)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var rng=new System.Random(name.Length*199);
            for(int i=0;i<18;i++) {float x=(float)rng.NextDouble()*.24f-.18f,y=(float)rng.NextDouble()*.24f-.12f;Line(vertices,triangles,new Vector2(x,y),new Vector2(x+.04f+(float)rng.NextDouble()*.10f,y),.0018f);}
            return SaveMesh("Static_"+name,vertices,triangles);
        }
        private static Mesh EdgeMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();
            Line(v,t,new Vector2(-1.84f,-.53f),new Vector2(-2.4f,1.28f),.07f);
            Line(v,t,new Vector2(1.84f,-.53f),new Vector2(2.4f,1.28f),.07f);
            Line(v,t,new Vector2(-1.82f,-.54f),new Vector2(-.6f,-.54f),.045f);
            Line(v,t,new Vector2(.6f,-.54f),new Vector2(1.82f,-.54f),.045f);
            return SaveMesh("GlassEdgeDamage",v,t);
        }
        private static ParticleSystem Particle(Transform parent,string name,Material mat,int max,float life,float speed,float size)
        {
            var t=Child(parent,name);var ps=Get<ParticleSystem>(t.gameObject);ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(life*.5f,life);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.35f,speed);main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size);main.startColor=new Color(2.1f,.7f,.12f,.75f);main.maxParticles=max;main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=0;emission.rateOverDistance=0;emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=50;shape.radius=.035f;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.24f,.03f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.4f;renderer.velocityScale=.018f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        private static void AddImpactCues(AudioClip leak)
        {
            var library=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/_Project/Audio/AudioCueLibrary.asset");
            var entries=library.Entries.Where(e=>e.Cue!=AudioCue.GlassHit && e.Cue!=AudioCue.PlayerDamage && e.Cue!=AudioCue.AirLeak).ToList();
            entries.Add(new AudioCueEntry(AudioCue.GlassHit,AudioCategory.Sfx,.56f,.16f,false,CockpitImpactAssets.Sound("GlassImpactLayer",1)));
            entries.Add(new AudioCueEntry(AudioCue.PlayerDamage,AudioCategory.Sfx,.68f,.12f,false,CockpitImpactAssets.Sound("HullImpactLayer",0)));
            // Library entry allows existing Ready audio preloader to prepare the dedicated loop too.
            entries.Add(new AudioCueEntry(AudioCue.AirLeak,AudioCategory.Sfx,.12f,1f,true,leak));
            library.Configure(entries.ToArray());EditorUtility.SetDirty(library);
        }
    }
}
