using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class CalibrationPointMotion : MonoBehaviour
    {
        private int lastIndex = -1;
        private bool completed;
        private float age, pulseAge = 1f;
        private Vector3 rest = Vector3.one;
        private Transform visual;
        private CalibrationPoint legacy;
        private Renderer source, shell;
        private MaterialPropertyBlock properties;
        private CalibrationProgressRing progressRing;
        private Transform ringCanvas, view;
        public void ConfigurePc() { visual = transform; rest = transform.localScale; }
        public void ConfigureLegacy(CalibrationPoint point)
        {
            legacy = point; source = point.GetComponent<Renderer>();
            var filter = point.GetComponent<MeshFilter>();
            if (source == null || filter == null) return;
            var go = new GameObject("CalibrationFeedbackVisual", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(point.transform, false); visual = go.transform;
            go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            shell = go.GetComponent<MeshRenderer>(); shell.sharedMaterials = source.sharedMaterials;
            shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            source.forceRenderingOff = true; properties = new MaterialPropertyBlock();
            var canvas = new GameObject("CalibrationRingCanvas", typeof(RectTransform), typeof(Canvas));
            ringCanvas = canvas.transform; ringCanvas.SetParent(visual, false);
            ringCanvas.localPosition = new Vector3(0f, 0f, -.55f);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)ringCanvas).sizeDelta = Vector2.one * 1.45f;
            progressRing = CalibrationProgressRing.Create(ringCanvas, 1.45f);
            view = Camera.main != null ? Camera.main.transform : null;
        }
        public void Present(int index, float progress)
        {
            if (visual == null) return;
            if (lastIndex != index) { age = 0f; completed = false; lastIndex = index; }
            if (progress >= .99f && !completed) { pulseAge = 0f; completed = true; }
            age += Time.unscaledDeltaTime; pulseAge += Time.unscaledDeltaTime;
            float pop = WorldTextMotion.Ease(age / .16f) + .1f * WorldTextMotion.Pulse(age - .10f, .18f);
            visual.localScale = rest * (pop + .16f * WorldTextMotion.Pulse(pulseAge, .28f));
        }
        private void LateUpdate()
        {
            if (legacy == null) return;
            Present(0, legacy.Progress);
            progressRing?.Present(legacy.Progress, legacy.IsComplete);
            if (ringCanvas != null && view != null) ringCanvas.rotation = view.rotation;
            if (source != null && shell != null) { source.GetPropertyBlock(properties); shell.SetPropertyBlock(properties); }
        }
        private void OnDisable() { lastIndex = -1; completed = false; age = 0; if (visual != null) visual.localScale = rest; }
        private void OnDestroy() { if (source != null) source.forceRenderingOff = false; if (shell != null) Destroy(shell.gameObject); }
    }
}
