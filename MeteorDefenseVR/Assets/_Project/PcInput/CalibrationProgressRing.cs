using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Calibration-only visual. Reads progress; never changes dwell, samples or GameFlow.
    // One reusable UI mesh, with no textures, materials or objects created while dwelling.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CalibrationProgressRing : UnityEngine.UI.MaskableGraphic
    {
        public static readonly Color ActiveRed = new Color(1f, .05f, .04f);
        public static readonly Color CompleteGreen = Color.green;
        public float Progress { get; private set; }
        private const int Segments = 64;
        private Color trackColor = ActiveRed;

        public static CalibrationProgressRing Create(Transform parent, float diameter)
        {
            var go = new GameObject("CalibrationProgressRing", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = Vector2.one * diameter;
            var ring = go.AddComponent<CalibrationProgressRing>();
            ring.raycastTarget = false;
            ring.Present(0f, false);
            return ring;
        }

        public void Present(float progress, bool recognized)
        {
            PresentTinted(recognized ? 1f : progress, recognized ? CompleteGreen : ActiveRed, ActiveRed);
        }
        public void PresentTinted(float progress, Color tint, Color track)
        {
            color = tint;
            if (trackColor != track) { trackColor = track; SetVerticesDirty(); }
            float value = Mathf.Clamp01(progress);
            if (Mathf.Approximately(value, Progress)) return;
            Progress = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float outer = Mathf.Min(rect.width, rect.height) * .5f;
            float inner = outer * .90f;
            Color track = trackColor; track.a = .22f;
            for (int i = 0; i < Segments; i++)
            {
                float start = (float)i / Segments, end = (float)(i + 1) / Segments;
                Arc(mesh, rect.center, inner, outer, start, end, track);
                if (Progress > start) Arc(mesh, rect.center, inner, outer, start, Mathf.Min(end, Progress), color);
            }
        }

        private static void Arc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float inner, float outer, float start, float end, Color tint)
        {
            float a = (90f - start * 360f) * Mathf.Deg2Rad;
            float b = (90f - end * 360f) * Mathf.Deg2Rad;
            Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int first = mesh.currentVertCount;
            mesh.AddVert(center + from * inner, tint, Vector2.zero);
            mesh.AddVert(center + from * outer, tint, Vector2.zero);
            mesh.AddVert(center + to * outer, tint, Vector2.zero);
            mesh.AddVert(center + to * inner, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
