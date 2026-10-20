using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThreatPulseGraphic : UnityEngine.UI.MaskableGraphic
    {
        private float radius, opacity;
        private Color tint;
        public void Present(float size, float alpha, Color value) { radius = size; opacity = alpha; tint = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); tint.a = opacity;
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60 + 30) * Mathf.Deg2Rad, b = ((i + 1) * 60 + 30) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, q = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                Vector2 d = (q - p).normalized, n = new Vector2(-d.y, d.x) * 1.5f;
                int start = mesh.currentVertCount;
                mesh.AddVert(p - n, tint, Vector2.zero); mesh.AddVert(p + n, tint, Vector2.zero);
                mesh.AddVert(q + n, tint, Vector2.zero); mesh.AddVert(q - n, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
