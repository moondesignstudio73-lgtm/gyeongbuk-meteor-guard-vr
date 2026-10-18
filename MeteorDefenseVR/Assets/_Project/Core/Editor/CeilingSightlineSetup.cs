using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Editor
{
    public static class CeilingSightlineSetup
    {
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            var rear = GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PremiumCockpit/RestoredRearCockpit");
            var spine = rear.Find("CeilingSpine");
            if (spine == null) throw new InvalidOperationException("Authored ceiling spine missing");
            var material = spine.GetComponent<MeshRenderer>().sharedMaterial;
            var mesh = PremiumMeshBuilder.Panel("CeilingSightlineRail", .08f, .14f, 3.78f, .0096f, .0048f);
            spine.localPosition = new Vector3(-.35f, 1.76f, -.40f);
            spine.GetComponent<MeshFilter>().sharedMesh = mesh;
            var other = rear.Find("CeilingSpine_Starboard");
            if (other == null)
            {
                other = UnityEngine.Object.Instantiate(spine.gameObject, rear).transform;
                other.name = "CeilingSpine_Starboard";
            }
            other.localPosition = new Vector3(.35f, 1.76f, -.40f);
            other.GetComponent<MeshFilter>().sharedMesh = mesh;
            var combined = rear.Find("CombinedMeshes");
            var sources = rear.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => !r.transform.IsChildOf(combined) && r.sharedMaterial == material).ToArray();
            var instances = sources.Select(r => new CombineInstance {
                mesh = r.GetComponent<MeshFilter>().sharedMesh,
                transform = rear.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray();
            var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(instances, true, true);
            combined.Find(material.name).GetComponent<MeshFilter>().sharedMesh =
                PremiumMeshBuilder.Save("CeilingSightline_" + material.name, merged);
            foreach (var source in sources) source.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CeilingSightline] Split center spine into paired rails; only rear carbon mesh rebuilt.");
        }
    }
}
