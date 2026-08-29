using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    public static class PremiumVisualSetup
    {
        private const string Root="Assets/Art/Premium";
        private static Material metal,dark,edge,cyan,amber,screen,glass,line;
        private static TMP_FontAsset font;
        public static void Ensure(Camera camera,TMP_FontAsset textFont)
        {
            font=textFont;
            Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
            Import("AvionicsAtlas",2048,TextureWrapMode.Clamp);Import("BasaltAlbedo",2048,TextureWrapMode.Repeat);Import("GunmetalAlbedo",1024,TextureWrapMode.Repeat);
            Import("ExplosionFlipbook",2048,TextureWrapMode.Clamp);
            metal=Lit("Titanium",new Color(.26f,.31f,.36f),.72f,.37f,"GunmetalAlbedo");
            dark=Lit("AnodizedCarbon",new Color(.12f,.17f,.21f),.55f,.3f,"GunmetalAlbedo");
            edge=Lit("MachinedEdges",new Color(.4f,.46f,.52f),.82f,.53f,"GunmetalAlbedo");
            string normalPath=Root+"/Textures/GunmetalRelief.png";
            if(!File.Exists(normalPath))AssetDatabase.CopyAsset(Root+"/Textures/GunmetalAlbedo.png",normalPath);
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.convertToNormalmap=true;normalImporter.heightmapScale=.015f;normalImporter.maxTextureSize=1024;normalImporter.SaveAndReimport();
            foreach(var surface in new[]{metal,dark,edge}){surface.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));surface.SetFloat("_BumpScale",.22f);surface.EnableKeyword("_NORMALMAP");}
            cyan=Unlit("InstrumentCyan",new Color(.04f,1.1f,1.5f));amber=Unlit("SafetyAmber",new Color(2.2f,.55f,.055f));
            line=Unlit("HudHairline",new Color(.05f,.53f,.63f));glass=Unlit("HudObsidian",new Color(.003f,.018f,.026f));
            glass.SetColor("_BaseColor",new Color(.003f,.018f,.026f,.94f));glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=2990;
            screen=Unlit("AvionicsEmission",new Color(.45f,1.65f,2f));screen.SetTexture("_BaseMap",Texture("AvionicsAtlas"));
            Cockpit(camera);Hangar();Environment(camera);Meteors();Hud(camera);Effects();
            AssetDatabase.SaveAssets();
        }
        private static void Import(string name,int max,TextureWrapMode wrap)
        {
            var importer=AssetImporter.GetAtPath(Root+"/Textures/"+name+".png") as TextureImporter;
            if(importer==null)return;importer.maxTextureSize=max;importer.mipmapEnabled=true;importer.wrapMode=wrap;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        private static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+".png");
        private static Material Material(string name,string shader)
        {
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);
            m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        private static Material Lit(string name,Color color,float metallic,float smooth,string texture=null)
        {var m=Material(name,"Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);if(texture!=null)m.SetTexture("_BaseMap",Texture(texture));return m;}
        private static Material Unlit(string name,Color color)
        {var m=Material(name,"Universal Render Pipeline/Unlit");m.SetColor("_BaseColor",color);return m;}
        private static Transform Child(Transform parent,string name)
        {var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;return t;}
        private static T Get<T>(GameObject go) where T:Component
        {T component=go.GetComponent<T>();return component!=null?component:go.AddComponent<T>();}
        private static Transform MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 rotation=default)
        {
            Transform t=Child(parent,name);t.localPosition=position;t.localRotation=Quaternion.Euler(rotation);
            Get<MeshFilter>(t.gameObject).sharedMesh=mesh;var r=Get<MeshRenderer>(t.gameObject);r.sharedMaterial=material;r.enabled=true;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;
        }
        private static Transform Panel(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rot=default,float bevel=.015f)
        {string key=name+"_"+size.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
            return MeshObject(parent,name,PremiumMeshBuilder.Panel(key,size.x,size.y,size.z,Mathf.Min(size.x,size.y)*.12f,Mathf.Min(bevel,Mathf.Min(size.x,size.y)*.06f)),mat,pos,rot);}
        private static void HideLegacy(Transform parent,string keep)
        {
            foreach(Transform child in parent)
                if(child.name!=keep)foreach(var r in child.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        }
        private static void Cockpit(Camera camera)
        {
            Transform anchor=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry");HideLegacy(anchor,"PremiumCockpit");
            Transform root=Child(anchor,"PremiumCockpit");
            Panel(root,"CentralPedestal",new Vector3(0,-1.08f,1.96f),new Vector3(.86f,.66f,.72f),dark,new Vector3(23,0,0),.045f);
            Monitor(root,"Navigation",new Vector3(0,-.60f,1.6f),new Vector2(.66f,.40f),new Vector3(25,0,0),3);
            for(int side=-1;side<=1;side+=2)
            {
                string s=side<0?"Port":"Starboard";
                Panel(root,s+"ConsoleBody",new Vector3(side*1.15f,-1.12f,1.98f),new Vector3(1.35f,.67f,.7f),metal,new Vector3(22,side*-11,side*-3),.055f);
                Panel(root,s+"ConsoleInset",new Vector3(side*1.15f,-1.01f,1.6f),new Vector3(1.24f,.6f,.12f),dark,new Vector3(22,side*-11,side*-3),.02f);
                Monitor(root,s+"Primary",new Vector3(side*.88f,-.62f,1.46f),new Vector2(.53f,.35f),new Vector3(25,side*-12,0),side<0?0:5);
                Monitor(root,s+"Secondary",new Vector3(side*1.42f,-.64f,1.56f),new Vector2(.45f,.34f),new Vector3(27,side*-22,0),side<0?1:2);
                Panel(root,s+"Sill",new Vector3(side*1.32f,-.64f,2.07f),new Vector3(1.45f,.08f,.18f),edge,new Vector3(9,side*-12,side*-2),.012f);
                Panel(root,s+"SillInset",new Vector3(side*1.32f,-.63f,1.96f),new Vector3(1.25f,.019f,.025f),dark,new Vector3(0,side*-12,0),.002f);
                // Canopy struts stay outside the central gaze cone, tapered by their chamfered cross section.
                Vector3 a=new Vector3(side*1.78f,-.59f,2.12f),b=new Vector3(side*2.9f,2.6f,2.25f);
                Beam(root,s+"Canopy",a,b,.085f,.18f,edge);
                Beam(root,s+"CanopyInset",a+new Vector3(0,0,-.105f),b+new Vector3(0,0,-.105f),.042f,.026f,dark);
                Beam(root,s+"CanopyFilament",a+new Vector3(side*.034f,0,-.12f),b+new Vector3(side*.034f,0,-.12f),.005f,.008f,line);
                Panel(root,s+"LightHousing",new Vector3(side*.46f,-.86f,1.48f),new Vector3(.075f,.38f,.06f),dark,new Vector3(24,0,0),.006f);
                Panel(root,s+"LightStrip",new Vector3(side*.46f,-.86f,1.441f),new Vector3(.013f,.26f,.012f),cyan,new Vector3(24,0,0),.001f);
                for(int j=0;j<8;j++)Panel(root,s+"Vent"+j,new Vector3(side*(1.12f+j*.067f),-1.05f,1.23f),new Vector3(.032f,.085f,.015f),dark,new Vector3(25,0,side*10),.002f);
                for(int j=0;j<5;j++)
                {
                    var button=Panel(root,s+"Switch"+j,new Vector3(side*(.7f+j*.105f),-1.095f,1.3f),new Vector3(.072f,.052f,.055f),edge,new Vector3(26,0,0),.006f);
                    Panel(button,"SwitchLamp",new Vector3(0,.009f,-.035f),new Vector3(.034f,.006f,.005f),j==0?amber:cyan,default,.0005f);
                }
            }
            // Accent lights are existing references owned by the cinematic controller.
            foreach(var l in anchor.GetComponentsInChildren<Light>(true)){l.range=1.65f;l.intensity=.65f;}
            Combine(root,"Cockpit");
        }
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float w,float d,Material mat)
        {var t=Panel(root,name,(a+b)*.5f,new Vector3(w,(b-a).magnitude,d),mat);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static void Monitor(Transform root,string name,Vector3 p,Vector2 size,Vector3 rot,int tile)
        {
            var housing=Panel(root,name,p,new Vector3(size.x+.09f,size.y+.09f,.085f),edge,rot,.015f);
            Panel(housing,"RubberSeal",new Vector3(0,0,-.048f),new Vector3(size.x+.028f,size.y+.028f,.018f),dark,default,.002f);
            MeshObject(housing,"Readout",PremiumMeshBuilder.Screen(name+"Screen",size.x,size.y,tile),screen,new Vector3(0,0,-.061f));
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {var bolt=Panel(housing,"Fastener"+x+y,new Vector3(x*(size.x*.5f+.026f),y*(size.y*.5f+.026f),-.047f),new Vector3(.016f,.016f,.009f),metal,default,.002f);
                Panel(bolt,"Slot",new Vector3(0,0,-.005f),new Vector3(.008f,.002f,.002f),dark,default,.0001f);}
        }
        private static void Hangar()
        {
            var launch=GameObject.Find("LaunchSystem").transform;var anchor=launch.Find("HangarVisuals");HideLegacy(anchor,"PremiumHangar");var root=Child(anchor,"PremiumHangar");
            for(int i=0;i<5;i++)
            {
                float z=3+i*1.25f;
                Panel(root,"Floor"+i,new Vector3(0,-1.5f,z),new Vector3(7,1.18f,.1f),metal,new Vector3(90,0,0),.02f);
                for(int side=-1;side<=1;side+=2)
                {
                    Panel(root,"Bulkhead"+i+side,new Vector3(side*3.3f,.6f,z),new Vector3(1.16f,4.2f,.16f),dark,new Vector3(0,side*90,0),.04f);
                    Beam(root,"Rib"+i+side,new Vector3(side*3.16f,-1.3f,z-.55f),new Vector3(side*3.16f,2.6f,z-.55f),.12f,.19f,edge);
                    Panel(root,"GuideLight"+i+side,new Vector3(side*2.4f,-1.39f,z),new Vector3(.035f,.7f,.025f),cyan,new Vector3(90,0,0),.002f);
                }
                Panel(root,"Overhead"+i,new Vector3(0,2.8f,z),new Vector3(6.4f,1.15f,.12f),dark,new Vector3(90,0,0),.03f);
                Panel(root,"CeilingLamp"+i,new Vector3(0,2.69f,z),new Vector3(2,.09f,.035f),cyan,new Vector3(90,0,0),.002f);
            }
            foreach(string suffix in new[]{"Left","Right"})
            {
                Transform door=launch.Find("HangarDoor_"+suffix);door.GetComponent<Renderer>().enabled=false;
                // Preserve the original animated door transform and its scale.
                var assembly=Child(door,"PremiumDoor");assembly.localScale=new Vector3(1/door.localScale.x,1/door.localScale.y,1/door.localScale.z);
                Panel(assembly,"DoorArmor",Vector3.zero,new Vector3(2.48f,3.17f,.22f),metal,default,.05f);
                for(int i=0;i<4;i++)Panel(assembly,"DoorInset"+i,new Vector3(0,-1.12f+i*.74f,-.135f),new Vector3(2.12f,.58f,.055f),dark,default,.018f);
                Panel(assembly,"DoorSeam",new Vector3(suffix=="Left"?1.12f:-1.12f,0,-.16f),new Vector3(.026f,2.9f,.02f),amber,default,.001f);
                Combine(assembly,"Door"+suffix);
            }
            Combine(root,"Hangar");
        }
        private static void Environment(Camera camera)
        {
            camera.farClipPlane=1800;camera.fieldOfView=65;
            RenderSettings.fog=false;RenderSettings.ambientSkyColor=new Color(.12f,.16f,.22f);RenderSettings.ambientEquatorColor=new Color(.045f,.06f,.085f);RenderSettings.ambientGroundColor=new Color(.014f,.022f,.032f);
            var sky=Material("OrbitalSky","MeteorDefense/Orbital Sky");sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VisualIntegration/Textures/SpacePanorama.png"));RenderSettings.skybox=sky;
            var earth=GameObject.Find("MissionCompleteSystem").transform.Find("EarthFocus");earth.localScale=Vector3.one*1000;
            var planet=PremiumMeshBuilder.Rock("EarthSphere",0,128,64,Vector3.one,true);earth.GetComponent<MeshFilter>().sharedMesh=planet;
            foreach(var filter in earth.GetComponentsInChildren<MeshFilter>())filter.sharedMesh=planet;
            var atmosphere=earth.Find("AtmosphereGlow");atmosphere.localScale=Vector3.one*1.008f;
            var atmosphereMaterial=atmosphere.GetComponent<Renderer>().sharedMaterial;atmosphereMaterial.renderQueue=2900;atmosphereMaterial.SetColor("_BaseColor",new Color(.05f,.42f,1,.32f));EditorUtility.SetDirty(atmosphereMaterial);
            var earthRim=earth.Find("EarthRimLight");if(earthRim!=null)earthRim.GetComponent<Light>().enabled=false;
            earth.position=new Vector3(0,-580,650);earth.rotation=Quaternion.Euler(6,-92,-18);
            var em=earth.GetComponent<Renderer>().sharedMaterial;em.SetColor("_BaseColor",Color.white);em.SetColor("_EmissionColor",new Color(.035f,.055f,.08f));EditorUtility.SetDirty(em);
            var key=GameObject.Find("Directional Light").GetComponent<Light>();key.transform.rotation=Quaternion.Euler(28,-32,0);key.color=new Color(.84f,.91f,1);key.intensity=2.2f;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Art/VisualIntegration/MeteorDefenseVisualProfile.asset");
            if(profile.TryGet(out Bloom bloom)){bloom.intensity.Override(.28f);bloom.threshold.Override(1.1f);}
            if(profile.TryGet(out Vignette vignette))vignette.intensity.Override(.07f);
            if(profile.TryGet(out ColorAdjustments color)){color.contrast.Override(7);color.postExposure.Override(.15f);color.saturation.Override(0);}
            if(!profile.TryGet(out Tonemapping tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.Neutral);
            foreach(var c in profile.components)EditorUtility.SetDirty(c);EditorUtility.SetDirty(profile);
            var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var ao=rendererData.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if(ao==null){ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Cockpit Contact Occlusion";AssetDatabase.AddObjectToAsset(ao,rendererData);rendererData.rendererFeatures.Add(ao);}
            using(var so=new SerializedObject(ao))
            {
                var settings=so.FindProperty("m_Settings");settings.FindPropertyRelative("Downsample").boolValue=true;settings.FindPropertyRelative("Source").enumValueIndex=0;
                settings.FindPropertyRelative("Samples").enumValueIndex=2;settings.FindPropertyRelative("Intensity").floatValue=.65f;settings.FindPropertyRelative("Radius").floatValue=.12f;
                settings.FindPropertyRelative("AfterOpaque").boolValue=true;settings.FindPropertyRelative("Falloff").floatValue=8f;so.ApplyModifiedPropertiesWithoutUndo();
            }
            ao.SetActive(true);ao.Create();rendererData.SetDirty();EditorUtility.SetDirty(rendererData);
            var visual=GameObject.Find("VisualIntegration").transform;
            var field=Child(visual,"DistantDebris");var distant=Lit("DistantBasalt",new Color(.27f,.3f,.34f),.03f,.12f,"BasaltAlbedo");
            var random=new System.Random(1973);Mesh[] rocks={PremiumMeshBuilder.Rock("DistantA",8,12,8,Vector3.one),PremiumMeshBuilder.Rock("DistantB",21,12,8,new Vector3(1.2f,.7f,.85f))};
            for(int i=0;i<52;i++)
            {
                float z=60+(float)random.NextDouble()*90,x=((float)random.NextDouble()-.5f)*z*2.5f,y=((float)random.NextDouble()-.2f)*z;
                var rock=MeshObject(field,"Debris"+i,rocks[i%2],distant,new Vector3(x,y,z),new Vector3(i*21,i*37,i*19));rock.localScale=Vector3.one*(.4f+(float)random.NextDouble()*2.2f);
            }
            // No colliders: distant scenery is small/dim and outside the playable spawn depth.
            Combine(field,"Debris");
        }
        private static void Meteors()
        {
            string[] names={"Normal","Fast","Large","Boss","Tutorial"};
            for(int i=0;i<names.Length;i++)
            {
                string path="Assets/_Project/Meteor/Prefabs/Meteor_"+names[i]+".prefab";
                GameObject root=PrefabUtility.LoadPrefabContents(path);
                var material=Material("Asteroid"+names[i],"MeteorDefense/Premium Asteroid");material.SetTexture("_BaseMap",Texture("BasaltAlbedo"));
                material.SetColor("_BaseColor",i==1?new Color(.61f,.7f,.83f):i==2?new Color(.83f,.68f,.49f):new Color(.72f,.65f,.55f));
                material.SetFloat("_Heat",i==3?1:0);material.SetColor("_EmissionColor",new Color(2.4f,.3f,.035f));
                var mesh=PremiumMeshBuilder.Rock("Asteroid"+names[i],13+i*9,48,28,i==1?new Vector3(.76f,.82f,1.28f):i==2?new Vector3(1.12f,.88f,1):Vector3.one);
                root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
                foreach(Transform child in root.transform)if(child.name.StartsWith("RockLobe"))child.GetComponent<Renderer>().enabled=false;
                var boss=root.GetComponent<BossMeteorView>();if(boss!=null)boss.Configure(root.GetComponent<MeteorController>(),new Renderer[]{root.GetComponent<MeshRenderer>()},root.GetComponentInChildren<Light>(true),root.GetComponent<AudioSource>());
                var reticle=root.transform.Find("LockOnReticle");var ring=reticle.GetComponent<LineRenderer>();ring.widthMultiplier=.012f;
                var text=reticle.Find("StatusText").GetComponent<TextMesh>();text.transform.localPosition=new Vector3(0,.9f,-.015f);text.anchor=TextAnchor.MiddleCenter;
                Present(text,1.3f,2.5f,.3f);
                var reticleMaterial=Material("ReticleVertexColor","MeteorDefense/Vertex Glow");
                foreach(var lr in reticle.GetComponentsInChildren<LineRenderer>()){lr.sharedMaterial=reticleMaterial;lr.numCapVertices=2;}
                var outer=reticle.Find("OuterTacticalRing");outer.localScale=Vector3.one*.79f;
                var brackets=new Renderer[5];brackets[0]=outer.GetComponent<Renderer>();
                for(int b=0;b<4;b++)
                {
                    var bar=reticle.Find("TacticalBracket_"+(b+1));bar.GetComponent<MeshRenderer>().enabled=false;bar.localPosition=Vector3.zero;bar.localScale=Vector3.one;
                    var lr=Get<LineRenderer>(bar.gameObject);lr.sharedMaterial=reticleMaterial;lr.useWorldSpace=false;lr.widthMultiplier=.013f;lr.positionCount=2;
                    float angle=b*Mathf.PI*.5f;Vector3 axis=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);lr.SetPosition(0,axis*.65f);lr.SetPosition(1,axis*.78f);brackets[b+1]=lr;
                }
                var lockView=reticle.GetComponent<LockOnReticleView>();lockView.Configure(root.GetComponent<GazeLockOnTarget>(),ring,text,brackets);
                using(var serialized=new SerializedObject(lockView)){serialized.FindProperty("ringRadius").floatValue=.63f;serialized.ApplyModifiedPropertiesWithoutUndo();}
                var view=Get<PremiumReticlePresentation>(reticle.gameObject);view.Configure(root.GetComponent<GazeLockOnTarget>(),text);
                PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
            }
        }
        private static void Present(TextMesh source,float size,float width,float height)
        {
            var child=source.transform.Find("PolishedText");if(child==null){child=new GameObject("PolishedText",typeof(RectTransform)).transform;child.SetParent(source.transform,false);}
            var text=Get<TextMeshPro>(child.gameObject);text.font=font;text.fontSize=size;text.enableAutoSizing=false;text.fontStyle=FontStyles.Normal;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;text.rectTransform.sizeDelta=new Vector2(width,height);child.localPosition=new Vector3(0,0,-.01f);
            Get<WorldTextPresentation>(source.gameObject).Configure(source,text);
        }
        private static void Hud(Camera camera)
        {
            var ready=camera.transform.Find("ReadyScreen");var decor=ready.Find("ReadyDecorations");HideRenderers(decor.Find("ReadyTitleFrame"));
            Panel(decor,"PremiumReadyBacking",new Vector3(0,-.18f,4.08f),new Vector3(4.25f,3.48f,.008f),glass,default,.001f);
            var readyTrim=Child(decor,"PremiumReadyTrim");readyTrim.localPosition=new Vector3(0,-.18f,4.055f);Outline(readyTrim,"Edge",new Vector2(4.25f,3.48f),.18f,.005f);
            ready.Find("ReadyTitle").GetComponent<WorldTextPresentation>().Display.fontStyle=FontStyles.Bold;
            var startTrim=Child(decor,"PremiumStartTrim");startTrim.localPosition=new Vector3(0,-1.45f,3.89f);Outline(startTrim,"Edge",new Vector2(1.5f,.42f),.08f,.006f);
            decor.Find("StartLabel").localPosition=new Vector3(0,-1.45f,3.78f);
            ready.Find("ReadyStartButton").GetComponent<MeshFilter>().sharedMesh=PremiumMeshBuilder.Panel("StartButtonFace",1,1,.02f,.12f,.001f);
            var alert=camera.transform.Find("CinematicAlert");
            foreach(Transform child in alert)if(child.name!="AlertText")HideRenderers(child);
            Panel(alert,"PremiumAlertBacking",new Vector3(0,.35f,4.02f),new Vector3(3.75f,.96f,.008f),glass,default,.001f);
            var alertTrim=Child(alert,"PremiumAlertTrim");alertTrim.localPosition=new Vector3(0,.35f,3.99f);Outline(alertTrim,"Edge",new Vector2(3.75f,.96f),.12f,.006f);
            using(var so=new SerializedObject(Object.FindAnyObjectByType<CinematicEnvironmentView>())){so.FindProperty("alertFrame").objectReferenceValue=alertTrim.Find("Edge").GetComponent<Renderer>();so.ApplyModifiedPropertiesWithoutUndo();}
            var hud=camera.transform.Find("MissionHUD");var frames=hud.Find("TacticalHudFrames");
            foreach(string name in new[]{"HpFrame","MeteorFrame","ScoreFrame"})HideRenderers(frames.Find(name));
            string[] panels={"Panel_HP","Panel_Remaining","Panel_Score"},texts={"HudHealth","HudRemaining","HudScore"};
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*2.04f,y=2.05f;Transform p=hud.Find(panels[i]);p.localPosition=new Vector3(x,y,4.03f);p.localScale=Vector3.one;
                p.GetComponent<MeshFilter>().sharedMesh=PremiumMeshBuilder.Panel(panels[i],1.37f,.57f,.008f,.095f,.001f);p.GetComponent<Renderer>().sharedMaterial=glass;
                Outline(p,"Edge",new Vector2(1.37f,.57f),.095f,.005f);
                TextMesh t=hud.Find(texts[i]).GetComponent<TextMesh>();t.transform.localPosition=new Vector3(x,y,3.99f);t.color=new Color(.17f,.93f,1);Present(t,2.25f,1.32f,.54f);
                t.GetComponent<WorldTextPresentation>().Display.fontStyle=FontStyles.Bold;
            }
            for(int i=0;i<8;i++)
            {var t=frames.Find("HealthSegment_"+(i+1));t.localPosition=new Vector3(-2.49f+i*.129f,1.94f,3.94f);t.localScale=new Vector3(.096f,.105f,.012f);t.GetComponent<Renderer>().sharedMaterial=cyan;}
            var fx=hud.GetComponent<HudVisualEffectsView>();using(var so=new SerializedObject(fx)){so.FindProperty("healthyColor").colorValue=new Color(.02f,.88f,1);so.ApplyModifiedPropertiesWithoutUndo();}
            var feedback=frames.Find("FeedbackPanel");feedback.localPosition=new Vector3(0,1.22f,3.96f);feedback.localScale=Vector3.one;
            feedback.GetComponent<MeshFilter>().sharedMesh=PremiumMeshBuilder.Panel("FeedbackBackdrop",3.2f,.52f,.008f,.08f,.001f);feedback.GetComponent<Renderer>().sharedMaterial=glass;
            Outline(feedback,"Edge",new Vector2(3.2f,.52f),.08f,.004f);
            var feedbackText=hud.Find("HudFeedback").GetComponent<TextMesh>();feedbackText.transform.localPosition=new Vector3(0,1.22f,3.9f);Present(feedbackText,1.8f,3.1f,.48f);
            // Keep source/controller data unchanged: the bridge formats only the public display.
            foreach(string name in new[]{"MissionCompleteVisualFrame","ResultVisualFrame"})
            {
                var root=camera.transform.Find(name);HideRenderers(root);var p=Child(root,"PremiumPanel");
                bool result=name.StartsWith("Result");Vector2 size=result?new Vector2(3.0f,2.1f):new Vector2(3.8f,.95f);
                Panel(p,"Backing",new Vector3(0,result?.08f:.18f,4.03f),new Vector3(size.x,size.y,.008f),glass,default,.001f);
                var border=Child(p,"Trim");border.localPosition=new Vector3(0,result?.08f:.18f,4.015f);Outline(border,"Edge",size,.13f,.006f);
            }
        }
        private static void HideRenderers(Transform t){if(t!=null)foreach(var r in t.GetComponentsInChildren<Renderer>(true))r.enabled=false;}
        private static void Combine(Transform root,string prefix)
        {
            var combined=Child(root,"CombinedMeshes");
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&!r.transform.IsChildOf(combined)).ToArray();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {
                var instances=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances,true,true);
                MeshObject(combined,group.Key.name,PremiumMeshBuilder.Save(prefix+group.Key.name,mesh),group.Key,Vector3.zero);
                foreach(var r in group)r.enabled=false;
            }
        }
        private static void Outline(Transform root,string name,Vector2 size,float cut,float width)
        {
            var t=Child(root,name);var lr=Get<LineRenderer>(t.gameObject);lr.sharedMaterial=line;lr.useWorldSpace=false;lr.loop=true;lr.widthMultiplier=width;lr.positionCount=8;
            var points=PremiumMeshBuilder.Outline(size.x,size.y,cut);for(int j=0;j<8;j++)lr.SetPosition(j,new Vector3(points[j].x,points[j].y,-.009f));lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=false;lr.enabled=true;
        }
        private static void Effects()
        {
            var soft=Material("PlasmaSoft","MeteorDefense/Soft Energy");soft.SetColor("_BaseColor",Color.white);
            var halo=Material("LaserHalo","MeteorDefense/Soft Energy");halo.SetColor("_BaseColor",Color.white);halo.SetFloat("_Ring",-1);
            var shock=Material("ShockRing","MeteorDefense/Soft Energy");shock.SetColor("_BaseColor",Color.white);shock.SetFloat("_Ring",1);
            var fire=Material("ExplosionFire","MeteorDefense/Explosion Flipbook");fire.SetTexture("_BaseMap",Texture("ExplosionFlipbook"));
            var combat=GameObject.Find("CombatSystem");
            foreach(var beam in combat.GetComponentsInChildren<LaserBeamView>(true))
            {
                var core=beam.GetComponent<LineRenderer>();core.sharedMaterial=Unlit("LaserCore",new Color(2.6f,3.2f,5));core.startColor=Color.white;core.endColor=Color.white;core.widthMultiplier=.007f;
                var glow=beam.transform.Find("PlasmaGlow").GetComponent<LineRenderer>();glow.sharedMaterial=halo;glow.startColor=new Color(.08f,.26f,3,.8f);glow.endColor=new Color(.6f,.1f,2,.6f);beam.Configure(core,glow,.18f);
            }
            var old=GameObject.Find("CombatVFX");foreach(var r in old.GetComponentsInChildren<Renderer>(true))r.enabled=false;foreach(var l in old.GetComponentsInChildren<Light>(true))l.enabled=false;
            foreach(var particle in old.GetComponentsInChildren<ParticleSystem>(true)){var main=particle.main;main.playOnAwake=false;particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            var premium=Get<PremiumCombatVfx>(GameObject.Find("VisualIntegration"));premium.Configure(old.GetComponent<MeteorDefenseVR.VFX.CombatVfxController>(),soft,shock,fire,AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/DistantA.asset"),AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/AsteroidNormal.mat"),combat.GetComponentsInChildren<LaserBeamView>(true));
        }
    }
}
