using System.Collections.Generic;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Bay-only materials and fittings; cockpit material assets remain untouched.
    public sealed class HangarSurfaceUpgrade : MonoBehaviour
    {
        private readonly Dictionary<Material, Material> copies = new();
        private readonly List<Material> owned = new();
        public void Build(Transform room, Transform leftDoor, Transform rightDoor)
        {
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
                            var color = original.GetColor("_BaseColor");
                            copy.SetColor("_BaseColor", new Color(Mathf.Min(.7f, color.r * 1.8f), Mathf.Min(.75f, color.g * 1.8f), Mathf.Min(.8f, color.b * 1.8f)));
                            copy.SetFloat("_Metallic", .35f); copy.SetFloat("_Smoothness", .38f);
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
            var detail = new GameObject("BaySurfaceDetails").transform; detail.SetParent(room, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(detail, "SafetyLane", new Vector3(side * 2.1f, -1.435f, 5.5f), new Vector3(.035f, .008f, 5.8f), safety);
                for (int i = 0; i < 5; i++)
                {
                    Part(detail, "WarmServiceLamp", new Vector3(side * 3.12f, 1.9f, 3 + i * 1.25f), new Vector3(.025f, .06f, .7f), warm);
                    var stripe = Part(detail, "ThresholdMark", new Vector3(side * (.3f + i * .33f), -1.43f, 7.8f), new Vector3(.14f, .01f, .4f), safety);
                    stripe.localRotation = Quaternion.Euler(0, 30, 0);
                }
            }
        }
        private static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
        private void OnDestroy() { foreach (var material in owned) if (material != null) Destroy(material); }
    }
}
