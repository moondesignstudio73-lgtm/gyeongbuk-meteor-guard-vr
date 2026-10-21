using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed partial class BaseFlightCinematic
    {
        private Mesh bevelUnit;
        private Material ExistingMetal()
        {
            // Reuse the authored brushed-metal albedo/normal maps already shipped in the cockpit.
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name == "Titanium") return material;
            return null;
        }

        private Mesh BeveledUnit()
        {
            if (bevelUnit != null) return bevelUnit;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            float[] points = { -.5f, -.46f, .46f, .5f };
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u); int first = vertices.Count;
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                {
                    Vector3 p = normal * .5f + u * points[x] + v * points[y];
                    Vector3 core = new Vector3(Mathf.Clamp(p.x, -.46f, .46f), Mathf.Clamp(p.y, -.46f, .46f), Mathf.Clamp(p.z, -.46f, .46f));
                    Vector3 n = (p - core).normalized; vertices.Add(core + n * .04f); normals.Add(n);
                    uv.Add(new Vector2(points[x] + .5f, points[y] + .5f));
                }
                for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                { int a = first + y * 4 + x; triangles.AddRange(new[] { a, a + 1, a + 5, a, a + 5, a + 4 }); }
            }
            bevelUnit = new Mesh { name = "HangarChamferedUnit" };
            bevelUnit.SetVertices(vertices); bevelUnit.SetNormals(normals); bevelUnit.SetUVs(0, uv); bevelUnit.SetTriangles(triangles, 0);
            bevelUnit.RecalculateBounds(); bevelUnit.RecalculateTangents(); resources.Add(bevelUnit); return bevelUnit;
        }

        private static void ShapeAirframe(Mesh mesh)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            float[] z = { -.5f, -.22f, .13f, .4f, .5f };
            float[] width = { .055f, .34f, .5f, .39f, .28f };
            for (int ring = 0; ring < z.Length; ring++) for (int side = 0; side < 8; side++)
            {
                float a = side * Mathf.PI / 4;
                vertices.Add(new Vector3(Mathf.Cos(a) * width[ring], Mathf.Sin(a) * width[ring] * .6f, z[ring]));
                uv.Add(new Vector2(side / 7f, ring / 4f));
            }
            for (int ring = 0; ring < 4; ring++) for (int side = 0; side < 8; side++)
            { int a = ring * 8 + side, b = ring * 8 + (side + 1) % 8; triangles.AddRange(new[] { a, b + 8, a + 8, a, b, b + 8 }); }
            for (int side = 1; side < 7; side++) triangles.AddRange(new[] { 0, side + 1, side, 32, 32 + side, 33 + side });
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        }

        private void DetailHangar()
        {
            var warm = Paint("WorkLight", new Color(1, .84f, .57f), false);
            var paint = Paint("SafetyPaint", new Color(.88f, .55f, .13f), true);
            paint.SetFloat("_Metallic", .08f);
            // Layered floor plates and service trenches create scale without adding physics.
            for (int row = 0; row < 9; row++)
            {
                float z = 11.8f + row * 2.8f;
                for (int col = -2; col <= 2; col++)
                {
                    Box("DeckPlate", stage, new Vector3(col * 3.3f, -1.7f, z), new Vector3(3.22f, .12f, 2.7f), hull);
                    for (int side = -1; side <= 1; side += 2)
                        Box("DeckFastener", stage, new Vector3(col * 3.3f + side * 1.45f, -1.625f, z - 1.1f), new Vector3(.08f, .018f, .08f), trim);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("ServiceTrench", stage, new Vector3(side * 6, -1.6f, z), new Vector3(.55f, .04f, 2.6f), dark);
                    for (int grate = 0; grate < 6; grate++)
                        Box("TrenchGrate", stage, new Vector3(side * 6, -1.56f, z - 1 + grate * .4f), new Vector3(.5f, .03f, .09f), trim);
                    Box("WallRecess", stage, new Vector3(side * 8.65f, 2.4f, z), new Vector3(.14f, 5.8f, 2.5f), hull);
                    Box("WallInlay", stage, new Vector3(side * 8.54f, 2.6f, z), new Vector3(.08f, 3.8f, 2.1f), dark);
                    Box("WallWorkLamp", stage, new Vector3(side * 8.4f, 4.65f, z), new Vector3(.15f, .14f, 1.6f), warm);
                    Box("OverheadRail", stage, new Vector3(side * 5.5f, 6.35f, z), new Vector3(.3f, .45f, 2.8f), trim);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box("RunwayPaint", stage, new Vector3(side * 3.55f, -1.61f, 23), new Vector3(.11f, .018f, 25), paint);
                Box("DoorSeal", stage, new Vector3(side * 7.15f, 2, 9.5f), new Vector3(.3f, 8, .4f), dark);
                Box("HydraulicColumn", stage, new Vector3(side * 8.6f, 2.2f, 9), new Vector3(.45f, 6.9f, .45f), hull);
                Box("HydraulicPiston", stage, new Vector3(side * 8.6f, 3, 8.7f), new Vector3(.19f, 4.2f, .19f), trim);
                for (int i = 0; i < 7; i++)
                {
                    var stripe = Box("ThresholdHazard", stage, new Vector3(side * (1.2f + i * .8f), -1.58f, 9.5f), new Vector3(.35f, .02f, 1), paint);
                    stripe.localRotation = Quaternion.Euler(0, 30, 0);
                }
                for (int i = 0; i < 3; i++)
                {
                    Box("EngineCoolingFin", ship, new Vector3(side * 1.3f, .45f, .5f + i * .5f), new Vector3(.55f, .12f, .1f), trim);
                    Box("WingMarking", ship, new Vector3(side * (1.55f + i * .22f), -.04f, 1.25f), new Vector3(.12f, .025f, .5f), paint);
                }
                Box("IntakeLip", ship, new Vector3(side * 1.3f, 0, -.15f), new Vector3(.73f, .73f, .14f), trim);
                Box("IntakeRecess", ship, new Vector3(side * 1.3f, 0, -.235f), new Vector3(.5f, .46f, .04f), dark);
            }
            foreach (var door in new[] { leftDoor, rightDoor })
            {
                for (int i = 0; i < 4; i++)
                {
                    Box("RecessedDoorArmor", door, new Vector3(0, -.35f + i * .23f, -.54f), new Vector3(.82f, .19f, .12f), dark);
                    Box("DoorLatch", door, new Vector3(.4f, -.35f + i * .23f, -.63f), new Vector3(.07f, .10f, .12f), trim);
                }
            }
            var sign = Node("BayNumber", stage); sign.localPosition = new Vector3(0, 6.35f, 8.85f);
            var text = sign.gameObject.AddComponent<TextMeshPro>(); text.text = "07  /  ORBITAL DEFENCE"; text.fontSize = 7;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(14, 1); text.color = new Color(.75f, .85f, .88f);
            text.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var lamp = Node("CeilingWorkLight", stage); lamp.localPosition = new Vector3(0, 5.7f, 14 + i * 8);
                Box("WorkLightHousing", stage, lamp.localPosition + Vector3.up * .25f, new Vector3(5, .25f, 1), dark);
                Box("WorkLightDiffuser", stage, lamp.localPosition, new Vector3(4.6f, .08f, .65f), warm);
                var light = lamp.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 13; light.intensity = 180;
                light.color = new Color(1, .82f, .6f); light.cullingMask = 1 << FilmLayer; light.shadows = LightShadows.None;
            }
        }
    }
}
