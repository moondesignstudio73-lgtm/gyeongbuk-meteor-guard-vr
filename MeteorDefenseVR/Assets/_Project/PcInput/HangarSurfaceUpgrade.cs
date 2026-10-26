using System.Collections.Generic;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Bay-only materials and fittings; cockpit material assets remain untouched.
    public sealed class HangarSurfaceUpgrade : MonoBehaviour
    {
        private readonly Dictionary<Material, Material> copies = new();
        private readonly List<Material> owned = new();
        private Texture2D alloyGrain;
        private readonly List<Mesh> meshes = new();
        public void Build(Transform room, Transform leftDoor, Transform rightDoor)
        {
            if (room == null || room.Find("BaySurfaceDetails") != null) return;
            alloyGrain = CreateGrain();
            foreach (var root in new[] { room, leftDoor, rightDoor })
            {
                if (root == null) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var original = materials[i];
                        if (original == null || !original.HasProperty("_Metallic")) continue;
                        if (!copies.TryGetValue(original, out var copy))
                        {
                            copy = new Material(original) { name = "Bay_" + original.name };
                            bool carbon = original.name.Contains("Carbon");
                            bool edge = original.name.Contains("Edges");
                            copy.SetColor("_BaseColor", carbon ? new Color(.13f,.18f,.23f) : edge ? new Color(.42f,.48f,.53f) : new Color(.28f,.34f,.40f));
                            copy.SetTexture("_BaseMap", alloyGrain);
                            copy.SetTextureScale("_BaseMap", Vector2.one * 3f);
                            // The previous normal derived from a streaked albedo stretched over
                            // whole bulkheads. Bevel geometry supplies broad surface highlights.
                            copy.DisableKeyword("_NORMALMAP"); copy.SetTexture("_BumpMap", null);
                            copy.SetFloat("_Metallic", carbon ? .25f : .65f);
                            copy.SetFloat("_Smoothness", edge ? .48f : .26f);
                            copies.Add(original, copy); owned.Add(copy);
                        }
                        materials[i] = copy;
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            var safety = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            safety.SetColor("_BaseColor", new Color(.54f, .34f, .09f)); owned.Add(safety);
            var warm = new Material(safety); warm.SetColor("_BaseColor", new Color(.9f, .78f, .59f)); owned.Add(warm);
            var casing = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            casing.SetColor("_BaseColor",new Color(.055f,.08f,.11f));casing.SetFloat("_Metallic",.55f);casing.SetFloat("_Smoothness",.32f);owned.Add(casing);
            var alloy = new Material(casing); alloy.SetColor("_BaseColor",new Color(.33f,.39f,.44f));owned.Add(alloy);
            var detail = new GameObject("BaySurfaceDetails").transform; detail.SetParent(room, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(detail, "SafetyLane", new Vector3(side * 2.1f, -1.435f, 5.5f), new Vector3(.035f, .008f, 5.8f), safety);
                for (int i = 0; i < 5; i++)
                {
                    float z = 3 + i * 1.25f;
                    Part(detail, "ServiceLampHousing", new Vector3(side * 3.18f, 1.9f, z), new Vector3(.09f, .14f, .85f), casing);
                    Part(detail, "WarmServiceLamp", new Vector3(side * 3.12f, 1.9f, 3 + i * 1.25f), new Vector3(.025f, .06f, .7f), warm);
                    Part(detail, "RecessedServicePanel", new Vector3(side*3.18f,.25f,z), new Vector3(.08f,1.05f,.83f), casing);
                    for(int vent=0;vent<4;vent++)
                        Part(detail,"VentLouver",new Vector3(side*3.12f,.04f+vent*.13f,z),new Vector3(.05f,.035f,.65f),alloy);
                    Part(detail,"LowerWallRail",new Vector3(side*3.12f,-1.04f,z),new Vector3(.12f,.14f,1.1f),alloy);
                    var stripe = Part(detail, "ThresholdMark", new Vector3(side * (.3f + i * .33f), -1.43f, 7.8f), new Vector3(.14f, .01f, .4f), safety);
                    stripe.localRotation = Quaternion.Euler(0, 30, 0);
                }
            }
            CombineDetails(detail);
        }
        private static Texture2D CreateGrain()
        {
            // Deterministic, low contrast material grain, generated once per bay.
            const int size=256;
            var texture=new Texture2D(size,size,TextureFormat.RGB24,true) { name="BaySatinAlloy",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4 };
            var pixels=new Color32[size*size];var random=new System.Random(261026);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                byte shade=(byte)(224+random.Next(9)+3*Mathf.Sin(y*1.7f));
                pixels[y*size+x]=new Color32(shade,shade,shade,255);
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }
        private void CombineDetails(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            var filters=root.GetComponentsInChildren<MeshFilter>();
            foreach(var filter in filters)
            {
                var material=filter.GetComponent<Renderer>().sharedMaterial;
                if(!groups.TryGetValue(material,out var group))groups.Add(material,group=new List<CombineInstance>());
                group.Add(new CombineInstance {mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh {name="BayFittings_"+pair.Key.name};mesh.CombineMeshes(pair.Value.ToArray(),true,true);meshes.Add(mesh);
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var filter in filters){filter.gameObject.SetActive(false);Destroy(filter.gameObject);}
        }
        private static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
        private void OnDestroy()
        {
            foreach (var material in owned) if (material != null) Destroy(material);
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            if(alloyGrain!=null)Destroy(alloyGrain);
        }
    }
}
