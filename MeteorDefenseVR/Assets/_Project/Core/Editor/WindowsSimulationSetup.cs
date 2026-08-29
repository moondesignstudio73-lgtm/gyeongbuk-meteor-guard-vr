using System.Collections.Generic;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class WindowsSimulationSetup
    {
        public static void ApplyAndBake()
        {
            DifficultySetup.EnsureAssets();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); var camera = Camera.main;
            if (router == null || camera == null) throw new System.InvalidOperationException("Authored input/camera missing");
            var direction = Get<ViewDirectionProvider>(router.gameObject); direction.Configure(camera);
            Get<WindowsVRSimulation>(router.gameObject).Configure(router, direction);
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var frame = Child(spawner.transform, "ShipFixedFrame"); frame.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            var sphere = Get<ShipCollisionBoundary>(Child(frame, "SurroundShipBoundary").gameObject); sphere.ConfigureSphere(4.2f);
            spawner.SetShipSpace(frame, sphere);
            var warning = Get<AudioSource>(Child(router.transform, "PooledSpatialThreatWarning").gameObject);
            warning.playOnAwake = false; warning.loop = false; warning.spatialBlend = 1; warning.dopplerLevel = 0;
            warning.rolloffMode = AudioRolloffMode.Linear; warning.minDistance = 2; warning.maxDistance = 20; warning.priority = 80;
            Get<ThreatIndicatorView>(router.gameObject).Configure(direction, spawner, Object.FindAnyObjectByType<BossClimaxController>(),
                Object.FindAnyObjectByType<AudioManager>(), warning, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset"));
            const string overlayPath = "Assets/Art/Premium/Materials/ThreatFontOverlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(overlayPath);
            if (overlay == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
                overlay = new Material(font.material) { name = "ThreatFontOverlay", shader = Shader.Find("TextMeshPro/Distance Field Overlay") };
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }
            Get<ThreatIndicatorView>(router.gameObject).SetOverlayMaterial(overlay);
            TutorialSafeZoneSetup.Ensure(camera,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset"));
            BuildCanopy(camera);
            int turretLayer=EnsureLayer(TurretAimingSystem.PlayerHiddenLayerName);
            camera.cullingMask&=~(1<<turretLayer);
            BuildTurrets(spawner,router.GetComponent<GazeRaycaster>(),camera,turretLayer);
            var spectator=Object.FindAnyObjectByType<DesktopSpectatorController>(FindObjectsInactive.Include);
            if(spectator!=null)
            {
                spectator.IncludeLayerForSpectator(turretLayer);
                spectator.Settings.localPosition=new Vector3(5.5f,2.5f,-8f);
                spectator.Settings.localLookAt=new Vector3(0,.2f,16f);
                spectator.Settings.fieldOfView=70f;spectator.Settings.Validate();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            PcTestSetup.BakePresentationFont();
        }
        private static T Get<T>(GameObject root) where T : Component { var c = root.GetComponent<T>(); return c != null ? c : root.AddComponent<T>(); }
        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name); if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            return child;
        }
        private static void BuildCanopy(Camera camera)
        {
            var cockpit = GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry");
            var root = Child(cockpit, "PanoramicCanopy"); root.SetPositionAndRotation(camera.transform.position, Quaternion.identity);
            const string directory = "Assets/Art/Premium/Meshes";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/Art/Premium", "Meshes");
            var structure = new List<CombineInstance>(); var trim = new List<CombineInstance>();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (cube == null) throw new System.InvalidOperationException("Built-in cube mesh missing");
            void Bar(Vector3 a, Vector3 b, float width, List<CombineInstance> collection)
            {
                Vector3 delta = b-a;
                collection.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS((a+b)*.5f, Quaternion.FromToRotation(Vector3.up, delta.normalized), new Vector3(width, delta.magnitude, width)) });
            }
            // Open panoramic windows, not an opaque room: all six combat directions remain visible.
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i+1) * Mathf.PI / 12;
                foreach (float y in new[] { -1.05f, 1.65f })
                {
                    Vector3 p = new Vector3(Mathf.Sin(a)*2.65f,y,Mathf.Cos(a)*2.65f), q = new Vector3(Mathf.Sin(b)*2.65f,y,Mathf.Cos(b)*2.65f);
                    // Keep the authored front dashboard intact.
                    if (i > 2 && i < 21) { Bar(p,q,.10f,structure); Bar(p*.985f,q*.985f,.022f,trim); }
                }
            }
            foreach (float degrees in new[] { -135f,-75f,75f,135f })
            {
                float a=degrees*Mathf.Deg2Rad; Vector3 bottom=new Vector3(Mathf.Sin(a)*2.65f,-1.05f,Mathf.Cos(a)*2.65f);
                Bar(bottom, new Vector3(bottom.x*.85f,1.65f,bottom.z*.85f),.09f,structure);
            }
            // Rear service console below the window + overhead docking ribs; no forced camera motion.
            Bar(new Vector3(-1.6f,-1.12f,-2.25f),new Vector3(1.6f,-1.12f,-2.25f),.28f,structure);
            Bar(new Vector3(-1.5f,-.94f,-2.1f),new Vector3(1.5f,-.94f,-2.1f),.032f,trim);
            Bar(new Vector3(-1.75f,1.72f,-1.75f),new Vector3(1.75f,1.72f,-1.75f),.10f,structure);
            foreach (float x in new[] { -.9f, 0f, .9f })
            {
                Bar(new Vector3(x,-.92f,-2.15f),new Vector3(x,-.72f,-2.2f),.16f,structure);
                Bar(new Vector3(x-.16f,-.68f,-2.17f),new Vector3(x+.16f,-.68f,-2.17f),.045f,trim);
            }
            // A bounded observation window keeps a solid floor silhouette while exposing below threats.
            Bar(new Vector3(-.78f,-1.03f,-.58f),new Vector3(-.78f,-1.03f,1.02f),.075f,structure);
            Bar(new Vector3(.78f,-1.03f,-.58f),new Vector3(.78f,-1.03f,1.02f),.075f,structure);
            Bar(new Vector3(-.78f,-1.03f,-.58f),new Vector3(.78f,-1.03f,-.58f),.075f,structure);
            Bar(new Vector3(-.78f,-1.03f,1.02f),new Vector3(.78f,-1.03f,1.02f),.075f,structure);
            SaveBatch(root,"CanopyStructure",structure,"AnodizedCarbon",directory);
            SaveBatch(root,"CanopyNavigationLights",trim,"InstrumentCyan",directory);
            SaveCanopyGlass(root,directory,cockpit);
            // Keep the established five-monitor PremiumCockpit silhouette. The latest panoramic
            // metal cage is retained as authored data but hidden; only its low-cost transparent
            // glass remains for rear/roof/floor visibility.
            root.Find("CanopyStructure").gameObject.SetActive(false);
            root.Find("CanopyNavigationLights").gameObject.SetActive(false);
            var source = cockpit.Find("PremiumCockpit/Navigation/Readout");
            if (source != null)
                for (int i = 0; i < 3; i++)
                {
                    var screen = Child(root, "RearTelemetry"+i);
                    screen.localPosition = new Vector3((i-1)*.9f, -.76f, -2.12f);
                    screen.localRotation = Quaternion.Euler(12,180,0); screen.localScale = Vector3.one * .55f;
                    Get<MeshFilter>(screen.gameObject).sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                    Get<MeshRenderer>(screen.gameObject).sharedMaterial = source.GetComponent<Renderer>().sharedMaterial;
                    screen.gameObject.SetActive(false);
                }
        }
        private static void SaveCanopyGlass(Transform root,string directory,Transform cockpit)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                uv.Add(Vector2.zero);uv.Add(Vector2.up);uv.Add(Vector2.one);uv.Add(Vector2.right);
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            // One renderer/material for side, rear, roof and the bounded floor observation glass.
            for(int i=3;i<=20;i++)
            {
                float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                Vector3 p0=new Vector3(Mathf.Sin(a)*2.62f,-1f,Mathf.Cos(a)*2.62f),p1=new Vector3(Mathf.Sin(a)*2.62f,1.62f,Mathf.Cos(a)*2.62f);
                Vector3 p2=new Vector3(Mathf.Sin(b)*2.62f,1.62f,Mathf.Cos(b)*2.62f),p3=new Vector3(Mathf.Sin(b)*2.62f,-1f,Mathf.Cos(b)*2.62f);Quad(p0,p1,p2,p3);
            }
            int center=vertices.Count;vertices.Add(new Vector3(0,1.62f,0));uv.Add(new Vector2(.5f,.5f));
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;int n=vertices.Count;
                vertices.Add(new Vector3(Mathf.Sin(a)*2.2f,1.62f,Mathf.Cos(a)*2.2f));vertices.Add(new Vector3(Mathf.Sin(b)*2.2f,1.62f,Mathf.Cos(b)*2.2f));
                uv.Add(new Vector2(.5f+Mathf.Sin(a)*.5f,.5f+Mathf.Cos(a)*.5f));uv.Add(new Vector2(.5f+Mathf.Sin(b)*.5f,.5f+Mathf.Cos(b)*.5f));triangles.AddRange(new[]{center,n,n+1});
            }
            Quad(new Vector3(-.72f,-1.025f,-.52f),new Vector3(-.72f,-1.025f,.96f),new Vector3(.72f,-1.025f,.96f),new Vector3(.72f,-1.025f,-.52f));
            string path=directory+"/CanopyGlass360.asset";Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="CanopyGlass360"};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            Transform glass=Child(root,"CanopyGlass360");Get<MeshFilter>(glass.gameObject).sharedMesh=mesh;var renderer=Get<MeshRenderer>(glass.gameObject);
            Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/CockpitCanopy.mat");
            if(material==null)material=cockpit.Find("PremiumCockpit/CommercialCanopy/CanopyGlass_Port")?.GetComponent<Renderer>()?.sharedMaterial;
            renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private static void BuildTurrets(MeteorSpawner spawner,GazeRaycaster raycaster,Camera playerCamera,int playerHiddenLayer)
        {
            if(spawner==null)throw new System.InvalidOperationException("Meteor spawner missing for turret integration");
            Transform frame=spawner.ShipFrame;Transform systemRoot=Child(frame,"ExternalTurretSystem");systemRoot.localPosition=Vector3.zero;systemRoot.localRotation=Quaternion.identity;systemRoot.localScale=Vector3.one;
            Material metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/Titanium.mat");
            Material dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/AnodizedCarbon.mat");
            Material edge=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/MachinedEdges.mat");
            Material cyan=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/InstrumentCyan.mat");
            // Exterior/aft mounts keep the central +/-30 x +/-25 degree observation cone clear.
            // The runtime visibility guard is the final safety net for HMD/player cameras; the
            // spectator camera deliberately includes this layer so the moving hardware is visible.
            var top=BuildTurret(systemRoot,"LaserTurret_Top",new Vector3(-3.2f,1.95f,-2.15f),metal,dark,edge,cyan,false);
            var bottom=BuildTurret(systemRoot,"LaserTurret_Bottom",new Vector3(3.2f,-1.85f,-2.15f),metal,dark,edge,cyan,true);
            foreach(Renderer renderer in systemRoot.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=playerHiddenLayer;
            var visibility=Get<TurretPlayerVisibilityGuard>(systemRoot.gameObject);visibility.Configure(systemRoot,playerCamera);
            AudioSource servo=Get<AudioSource>(Child(systemRoot,"TurretServoAudio").gameObject);ConfigureTurretAudio(servo,true,.55f);
            AudioSource charge=Get<AudioSource>(Child(systemRoot,"TurretChargeAudio").gameObject);ConfigureTurretAudio(charge,false,.45f);
            var weapon=GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>();if(weapon==null)throw new System.InvalidOperationException("Laser weapon missing");
            var aiming=Get<TurretAimingSystem>(systemRoot.gameObject);aiming.Configure(spawner,weapon,frame,top,bottom,servo,charge,playerCamera.transform);
            var beams=weapon.GetComponentsInChildren<LaserBeamView>(true);weapon.Configure(raycaster,new[]{top.muzzle,bottom.muzzle},beams);weapon.SetTurretAiming(aiming);
            foreach(string oldName in new[]{"Cannon_Left","Cannon_Right"}){Transform old=weapon.transform.Find(oldName);if(old!=null)old.gameObject.SetActive(false);}
        }

        private static TurretAimingSystem.TurretMount BuildTurret(Transform parent,string name,Vector3 position,Material metal,Material dark,Material edge,Material cyan,bool inverted)
        {
            Transform root=Child(parent,name);root.localPosition=position;root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
            Mesh cylinder=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"),cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx"),sphere=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Part(root,"TurretBase",cylinder,dark,Vector3.zero,new Vector3(.40f,.09f,.40f),Quaternion.identity);
            Part(root,"BaseTrim",cylinder,edge,new Vector3(0,inverted?.09f:.09f,0),new Vector3(.33f,.05f,.33f),Quaternion.identity);
            Transform yaw=Child(root,"YawPivot");yaw.localPosition=Vector3.zero;yaw.localRotation=Quaternion.identity;yaw.localScale=Vector3.one;
            Part(yaw,"YawHousing",cylinder,metal,new Vector3(0,inverted?-.02f:.15f,0),new Vector3(.26f,.15f,.26f),Quaternion.identity);
            Transform pitch=Child(yaw,"PitchPivot");pitch.localPosition=new Vector3(0,inverted?-.11f:.23f,0);pitch.localRotation=Quaternion.identity;pitch.localScale=Vector3.one;
            Part(pitch,"Trunnion",cylinder,edge,Vector3.zero,new Vector3(.16f,.24f,.16f),Quaternion.Euler(0,0,90));
            Part(pitch,"Barrel",cylinder,dark,new Vector3(0,0,.23f),new Vector3(.085f,.16f,.085f),Quaternion.Euler(90,0,0));
            Part(pitch,"BarrelRail_L",cube,metal,new Vector3(-.12f,0,.23f),new Vector3(.04f,.04f,.30f),Quaternion.identity);
            Part(pitch,"BarrelRail_R",cube,metal,new Vector3(.12f,0,.23f),new Vector3(.04f,.04f,.30f),Quaternion.identity);
            Renderer core=Part(pitch,"EnergyCore",sphere,cyan,new Vector3(0,0,.13f),Vector3.one*.105f,Quaternion.identity);
            Renderer ring=Part(pitch,"MuzzleRing",cylinder,cyan,new Vector3(0,0,.40f),new Vector3(.13f,.05f,.13f),Quaternion.Euler(90,0,0));
            Transform muzzle=Child(pitch,"Muzzle");muzzle.localPosition=new Vector3(0,0,.48f);muzzle.localRotation=Quaternion.identity;muzzle.localScale=Vector3.one;
            Light light=Get<Light>(Child(muzzle,"MuzzleLight").gameObject);light.type=LightType.Point;light.range=2.8f;light.color=new Color(.2f,.72f,1);light.shadows=LightShadows.None;light.enabled=false;
            return new TurretAimingSystem.TurretMount{label=inverted?"BOTTOM":"TOP",root=root,yawPivot=yaw,pitchPivot=pitch,muzzle=muzzle,energyRenderers=new[]{core,ring},muzzleLight=light};
        }
        private static Renderer Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation)
        {
            Transform part=Child(parent,name);part.localPosition=position;part.localRotation=rotation;part.localScale=scale;Get<MeshFilter>(part.gameObject).sharedMesh=mesh;
            var renderer=Get<MeshRenderer>(part.gameObject);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        private static void ConfigureTurretAudio(AudioSource source,bool loop,float spatial)
        {source.playOnAwake=false;source.loop=loop;source.volume=0;source.spatialBlend=spatial;source.dopplerLevel=0;source.priority=190;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=.5f;source.maxDistance=8f;}
        private static void SaveBatch(Transform root, string name, List<CombineInstance> parts, string material, string directory)
        {
            string path = directory + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh,path); } else mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(),true,true); EditorUtility.SetDirty(mesh);
            var child = Child(root,name); Get<MeshFilter>(child.gameObject).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(child.gameObject); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/"+material+".mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static int EnsureLayer(string name)
        {
            int existing=LayerMask.NameToLayer(name);if(existing>=0)return existing;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
            for(int index=8;index<layers.arraySize;index++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
            {layers.GetArrayElementAtIndex(index).stringValue=name;tags.ApplyModifiedPropertiesWithoutUndo();return index;}
            throw new System.InvalidOperationException("No unused layer available for "+name);
        }
    }
}
