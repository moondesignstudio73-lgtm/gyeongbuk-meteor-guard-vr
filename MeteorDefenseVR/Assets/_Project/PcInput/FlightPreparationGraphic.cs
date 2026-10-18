using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // A single pooled UI mesh: no textures, particles, camera motion or per-frame objects.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FlightPreparationGraphic : UnityEngine.UI.MaskableGraphic
    {
        private float elapsed, progress;
        public void Present(float time, float value) { elapsed = time; progress = Mathf.Clamp01(value); SetVerticesDirty(); }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Color cyan = new Color(.14f, .9f, .95f), dim = new Color(.14f, .9f, .95f, .18f);
            // Slow orbital motion keeps the ship still and the loading screen comfortable.
            for (int i = 0; i < 64; i++)
            {
                float angle = 90 - i * 360f / 64;
                Arc(mesh, 128, angle, angle - 3.2f, 3, i < progress * 64 ? cyan : dim);
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = elapsed * 25 + i * 120;
                Arc(mesh, 107, angle, angle + 62, 1.5f, new Color(.55f, .96f, 1, .7f));
                Arc(mesh, 145, -angle, -angle + 18, 1, dim);
            }
            // Symmetric flight path and distant stars, deliberately outside the text bands.
            for (int i = 0; i < 18; i++)
            {
                float travel = Mathf.Repeat(elapsed * .14f + i * .137f, 1);
                float x = 180 + travel * 240, y = Mathf.Sin(i * 2.39f) * 125;
                Color star = new Color(.5f, .85f, 1, Mathf.Sin(travel * Mathf.PI) * .35f);
                Line(mesh, new Vector2(x, y), new Vector2(x + 3 + travel * 12, y), 1, star);
                Line(mesh, new Vector2(-x, -y), new Vector2(-x - 3 - travel * 12, -y), 1, star);
            }
            Line(mesh, new Vector2(-290, 0), new Vector2(-170, 0), 1, dim);
            Line(mesh, new Vector2(170, 0), new Vector2(290, 0), 1, dim);
            // An upward-pointing ship with a quiet, pulsing engine trail.
            Vector2 nose = new Vector2(0, 62), left = new Vector2(-42, -34), right = new Vector2(42, -34);
            Triangle(mesh, nose, left, new Vector2(0, -17), new Color(.14f, .9f, .95f, .18f));
            Triangle(mesh, nose, new Vector2(0, -17), right, new Color(.14f, .9f, .95f, .32f));
            Line(mesh, nose, left, 2, cyan); Line(mesh, nose, right, 2, cyan);
            Line(mesh, left, new Vector2(0, -17), 2, cyan); Line(mesh, right, new Vector2(0, -17), 2, cyan);
            Line(mesh, new Vector2(0, 40), new Vector2(0, -8), 1, new Color(.75f, 1, 1, .8f));
            float flame = 12 + 9 * (.5f + .5f * Mathf.Sin(elapsed * 5));
            Triangle(mesh, new Vector2(-8, -25), new Vector2(0, -25 - flame), new Vector2(8, -25), new Color(1, .72f, .36f, .85f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90 * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Line(mesh, direction * 154, direction * 163, 2, cyan);
            }
        }
        private static void Arc(UnityEngine.UI.VertexHelper mesh, float radius, float from, float to, float width, Color tint)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / 5));
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(from, to, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
            }
        }
        private static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * (width * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(a - n, tint, Vector2.zero); mesh.AddVert(a + n, tint, Vector2.zero);
            mesh.AddVert(b + n, tint, Vector2.zero); mesh.AddVert(b - n, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
        private static void Triangle(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
