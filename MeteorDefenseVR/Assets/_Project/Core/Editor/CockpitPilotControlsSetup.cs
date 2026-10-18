using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    public static class CockpitPilotControlsSetup
    {
        private const string ScenePath="Assets/_Project/Scenes/MeteorDefense.unity";
        private const string MaterialRoot="Assets/Art/Premium/Materials";

        [MenuItem("Meteor Defense/Apply Lower Pilot Controls")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Transform cockpit=GameObject.Find("LaunchSystem")?.transform.Find("CockpitGeometry");
            if(cockpit==null||Camera.main==null)throw new InvalidOperationException("CockpitGeometry or Player Camera is missing.");
            Transform existing=cockpit.Find("PilotControls");if(existing!=null)Object.DestroyImmediate(existing.gameObject);
            Transform root=Child(cockpit,"PilotControls");
            Material metal=Load("Titanium"),dark=Load("AnodizedCarbon"),edge=Load("MachinedEdges"),cyan=Load("InstrumentCyan"),amber=Load("SafetyAmber");
            Material glove=Lit("PilotGlove",new Color(.028f,.043f,.057f),.06f,.24f);
            Material suit=Lit("PilotSuit",new Color(.045f,.065f,.082f),.18f,.3f);

            Transform console=Child(root,"LowerConsole");
            Panel(console,"ConsoleBody",new Vector3(0,-1.19f,.88f),new Vector3(2.10f,.43f,.30f),dark,new Vector3(18,0,0),.045f);
            Panel(console,"ConsoleInset",new Vector3(0,-1.075f,.69f),new Vector3(1.88f,.25f,.045f),metal,new Vector3(18,0,0),.018f);
            Panel(console,"CenterSeam",new Vector3(0,-1.065f,.66f),new Vector3(.035f,.19f,.012f),edge,new Vector3(18,0,0),.004f);
            for(int side=-1;side<=1;side+=2)
            {
                Panel(console,side<0?"PortWristRest":"StarboardWristRest",new Vector3(side*.69f,-1.00f,.58f),new Vector3(.48f,.16f,.18f),suit,new Vector3(22,side*-5,0),.028f);
                Panel(console,side<0?"PortCyanRail":"StarboardCyanRail",new Vector3(side*.81f,-1.04f,.625f),new Vector3(.33f,.018f,.012f),cyan,new Vector3(18,side*-4,0),.002f);
            }
            for(int i=0;i<7;i++)
            {
                float x=-.45f+i*.15f;Panel(console,"ToggleGuard"+i,new Vector3(x,-1.01f,.635f),new Vector3(.085f,.065f,.038f),edge,new Vector3(18,0,0),.009f);
                Panel(console,"ToggleLight"+i,new Vector3(x,-.992f,.610f),new Vector3(.040f,.012f,.008f),i==3?amber:cyan,new Vector3(18,0,0),.001f);
            }
            Combine(console,"PilotConsole");

            Transform stick=Child(root,"FlightStickPivot");stick.localPosition=new Vector3(.61f,-1.06f,.91f);
            Cylinder(stick,"GimbalOuter",Vector3.zero,new Vector3(.25f,.055f,.25f),edge);
            Cylinder(stick,"GimbalInner",new Vector3(0,.038f,0),new Vector3(.18f,.06f,.18f),dark);
            Cylinder(stick,"StickShaft",new Vector3(0,.22f,0),new Vector3(.055f,.25f,.055f),metal,new Vector3(-7,0,0));
            Capsule(stick,"Grip",new Vector3(0,.43f,-.035f),new Vector3(.105f,.15f,.085f),glove,new Vector3(-10,0,0));
            Panel(stick,"Trigger",new Vector3(0,.45f,-.115f),new Vector3(.07f,.09f,.035f),edge,new Vector3(-8,0,0),.009f);
            Panel(stick,"StickLed",new Vector3(0,.535f,-.055f),new Vector3(.045f,.025f,.015f),cyan,new Vector3(-10,0,0),.003f);
            Transform rightHand=Child(stick,"RightHandVisual");rightHand.localPosition=new Vector3(.015f,.425f,-.015f);rightHand.localRotation=Quaternion.identity;rightHand.localScale=Vector3.one*.72f;
            BuildHand(rightHand,false,glove,suit,cyan);
            Combine(stick,"PilotStick",rightHand);
            Combine(rightHand,"PilotRightHand");

            Transform throttle=Child(root,"ThrottlePivot");throttle.localPosition=new Vector3(-.72f,-1.055f,.88f);
            Panel(throttle,"ThrottleRail",Vector3.zero,new Vector3(.42f,.09f,.26f),dark,new Vector3(18,0,0),.018f);
            Panel(throttle,"ThrottleSlot",new Vector3(0,.055f,-.015f),new Vector3(.075f,.035f,.19f),edge,new Vector3(18,0,0),.006f);
            Cylinder(throttle,"ThrottleStem",new Vector3(0,.19f,0),new Vector3(.045f,.20f,.045f),metal,new Vector3(-12,0,0));
            Panel(throttle,"ThrottleGrip",new Vector3(0,.36f,-.04f),new Vector3(.25f,.12f,.14f),glove,new Vector3(5,0,0),.025f);
            Panel(throttle,"ThrottleLed",new Vector3(0,.39f,-.115f),new Vector3(.11f,.025f,.012f),cyan,new Vector3(5,0,0),.002f);
            Transform leftHand=Child(throttle,"LeftHandVisual");leftHand.localPosition=new Vector3(0,.38f,-.025f);leftHand.localRotation=Quaternion.Euler(4,0,0);leftHand.localScale=Vector3.one*.72f;
            BuildHand(leftHand,true,glove,suit,cyan);
            Combine(throttle,"PilotThrottle",leftHand);
            Combine(leftHand,"PilotLeftHand");

            var lamps=new Renderer[2];
            for(int side=-1;side<=1;side+=2)
            {
                Transform lamp=Panel(root,side<0?"PortControlWarning":"StarboardControlWarning",new Vector3(side*.96f,-.96f,.78f),new Vector3(.12f,.025f,.018f),amber,new Vector3(18,0,0),.003f);
                lamps[side<0?0:1]=lamp.GetComponent<Renderer>();lamps[side<0?0:1].enabled=false;
            }
            var controller=root.gameObject.AddComponent<CockpitHandVisualController>();
            controller.Configure(stick,throttle,rightHand,leftHand,lamps,Camera.main,Object.FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include),Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include));
            foreach(Collider collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            RemoveUnusedIntermediateMeshes(root);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[PilotControls] Lower cockpit, visual hands and presentation controller applied without gameplay input bindings.");
        }

        private static void BuildHand(Transform root,bool left,Material glove,Material suit,Material cyan)
        {
            float mirror=left?-1f:1f;
            Capsule(root,"Palm",Vector3.zero,new Vector3(.108f,.095f,.064f),glove,new Vector3(8,0,mirror*5));
            Capsule(root,"Thumb",new Vector3(mirror*.088f,-.004f,-.016f),new Vector3(.032f,.086f,.032f),glove,new Vector3(62,0,mirror*-34));
            for(int i=0;i<4;i++)Capsule(root,"Finger"+i,new Vector3(mirror*(-.072f+i*.047f),.022f,-.061f),new Vector3(.022f,.074f,.023f),glove,new Vector3(78,0,mirror*(i-1.5f)*4));
            Capsule(root,"Forearm",new Vector3(0,-.155f,-.145f),new Vector3(.095f,.215f,.09f),suit,new Vector3(58,0,mirror*4));
            Panel(root,"Cuff",new Vector3(0,-.072f,-.055f),new Vector3(.205f,.10f,.135f),suit,new Vector3(25,0,mirror*3),.022f);
            Panel(root,"WristIndicator",new Vector3(mirror*.045f,-.075f,-.131f),new Vector3(.055f,.020f,.010f),cyan,new Vector3(25,0,mirror*3),.002f);
        }

        private static Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat")??throw new InvalidOperationException(name+" material missing");
        private static Material Lit(string name,Color color,float metallic,float smooth)
        {
            Directory.CreateDirectory(MaterialRoot);string path=$"{MaterialRoot}/{name}.mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        private static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;return t;}
        private static T Get<T>(GameObject go)where T:Component{var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
        private static Transform Panel(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rot=default,float bevel=.015f)
        {return MeshObject(parent,name,PremiumMeshBuilder.Panel("Pilot_"+name,size.x,size.y,size.z,Mathf.Min(size.x,size.y)*.14f,Mathf.Min(bevel,Mathf.Min(size.x,size.y)*.06f)),mat,pos,rot,size:Vector3.one);}
        private static Transform Cylinder(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rot=default)=>MeshObject(parent,name,Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"),mat,pos,rot,size);
        private static Transform Capsule(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rot=default)=>MeshObject(parent,name,Resources.GetBuiltinResource<Mesh>("Capsule.fbx"),mat,pos,rot,size);
        private static Transform MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 pos,Vector3 rot=default,Vector3 size=default)
        {Transform t=Child(parent,name);t.localPosition=pos;t.localRotation=Quaternion.Euler(rot);t.localScale=size==default?Vector3.one:size;Get<MeshFilter>(t.gameObject).sharedMesh=mesh;var r=Get<MeshRenderer>(t.gameObject);r.sharedMaterial=material;r.enabled=true;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;}
        private static void Combine(Transform root,string prefix,Transform exclude=null)
        {
            Transform combined=Child(root,"CombinedMeshes");
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&!r.transform.IsChildOf(combined)&&(exclude==null||!r.transform.IsChildOf(exclude))).ToArray();
            foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
            {
                var instances=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances,true,true);
                MeshObject(combined,group.Key.name,PremiumMeshBuilder.Save(prefix+group.Key.name,mesh),group.Key,Vector3.zero);
            }
            foreach(var renderer in renderers)if(renderer!=null)Object.DestroyImmediate(renderer.gameObject);
        }
        private static void RemoveUnusedIntermediateMeshes(Transform root)
        {
            var used=new HashSet<Mesh>(root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Where(m=>m!=null));
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Art/Premium/Meshes"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(!Path.GetFileNameWithoutExtension(path).StartsWith("Pilot_",StringComparison.Ordinal))continue;
                Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!used.Contains(mesh))AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
