using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [DisallowMultipleComponent]
    public sealed class ChamferedHudPanel : UnityEngine.UI.BaseMeshEffect
    {
        [SerializeField, Min(0f)] private float cornerCut = 12f;
        [SerializeField, Min(.5f)] private float borderWidth = 1.5f;
        [SerializeField] private Color fillColor = new Color(.006f, .025f, .038f, .82f);
        [SerializeField] private Color borderColor = new Color(.08f, .72f, .82f, .82f);
        public Color FillColor => fillColor;
        public Color BorderColor => borderColor;
        public float CornerCut => cornerCut;

        public void Configure(Color fill, Color border, float cut = 12f, float width = 1.5f)
        {
            fillColor = fill;
            borderColor = border;
            cornerCut = Mathf.Max(0f, cut);
            borderWidth = Mathf.Max(.5f, width);
            graphic?.SetVerticesDirty();
        }

        public override void ModifyMesh(UnityEngine.UI.VertexHelper mesh)
        {
            if (!IsActive()) return;
            mesh.Clear();
            Rect rect = graphic.rectTransform.rect;
            float cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * .24f);
            Vector2[] outer = Corners(rect, cut);
            Rect innerRect = new Rect(rect.xMin + borderWidth, rect.yMin + borderWidth,
                Mathf.Max(0f, rect.width - borderWidth * 2f), Mathf.Max(0f, rect.height - borderWidth * 2f));
            Vector2[] inner = Corners(innerRect, Mathf.Max(0f, cut - borderWidth * .45f));

            AddPolygon(mesh, inner, fillColor);
            for (int i = 0; i < outer.Length; i++)
            {
                int next = (i + 1) % outer.Length;
                int start = mesh.currentVertCount;
                AddVertex(mesh, outer[i], borderColor);
                AddVertex(mesh, outer[next], borderColor);
                AddVertex(mesh, inner[next], borderColor);
                AddVertex(mesh, inner[i], borderColor);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static Vector2[] Corners(Rect r, float cut) => new[]
        {
            new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMax - cut, r.yMax),
            new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax, r.yMin + cut),
            new Vector2(r.xMax - cut, r.yMin), new Vector2(r.xMin + cut, r.yMin),
            new Vector2(r.xMin, r.yMin + cut), new Vector2(r.xMin, r.yMax - cut)
        };

        private static void AddPolygon(UnityEngine.UI.VertexHelper mesh, Vector2[] points, Color color)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in points) center += point;
            center /= points.Length;
            int start = mesh.currentVertCount;
            AddVertex(mesh, center, color);
            foreach (Vector2 point in points) AddVertex(mesh, point, color);
            for (int i = 0; i < points.Length; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + ((i + 1) % points.Length));
        }

        private static void AddVertex(UnityEngine.UI.VertexHelper mesh, Vector2 position, Color color) =>
            mesh.AddVert(new Vector3(position.x, position.y), color, Vector2.zero);
    }
}
