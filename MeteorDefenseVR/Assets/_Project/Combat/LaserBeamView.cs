using UnityEngine;

namespace MeteorDefenseVR.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LaserBeamView : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private LineRenderer glowRenderer;
        [SerializeField, Min(0.01f)] private float visibleDuration = 0.12f;
        [SerializeField] private AnimationCurve widthOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        private float remainingTime;
        private float elapsedTime;
        private float baseWidth = 0.04f;

        public bool IsPlaying => remainingTime > 0f;

        public void PrewarmGeometry()
        {
            if (IsPlaying) return;
            if (lineRenderer != null) lineRenderer.positionCount = 2;
            if (glowRenderer != null) glowRenderer.positionCount = 2;
        }

        private void Awake()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null) baseWidth = Mathf.Max(0.005f, lineRenderer.widthMultiplier);
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (glowRenderer != null) glowRenderer.enabled = false;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Configure(LineRenderer line, float duration = 0.12f)
        {
            lineRenderer = line;
            visibleDuration = Mathf.Max(0.01f, duration);
            if (lineRenderer != null) baseWidth = Mathf.Max(0.005f, lineRenderer.widthMultiplier);
            if (lineRenderer != null) lineRenderer.enabled = false;
        }

        public void Configure(LineRenderer line, LineRenderer glow, float duration = 0.12f)
        {
            Configure(line, duration);
            glowRenderer = glow;
            if (glowRenderer != null) glowRenderer.enabled = false;
        }

        public void Play(Vector3 origin, Vector3 destination)
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null) return;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, destination);
            lineRenderer.widthMultiplier = baseWidth;
            lineRenderer.enabled = true;
            if (glowRenderer != null)
            {
                glowRenderer.useWorldSpace = true;
                glowRenderer.positionCount = 2;
                glowRenderer.SetPosition(0, origin);
                glowRenderer.SetPosition(1, destination);
                glowRenderer.enabled = true;
            }
            remainingTime = visibleDuration;
            elapsedTime = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (remainingTime <= 0f || lineRenderer == null) return;
            float step = Mathf.Max(0f, deltaTime);
            remainingTime -= step;
            elapsedTime += step;
            float normalized = Mathf.Clamp01(elapsedTime / visibleDuration);
            float pulse = 1f + Mathf.Sin(normalized * Mathf.PI) * 0.35f;
            lineRenderer.widthMultiplier = baseWidth * Mathf.Max(0f, widthOverLifetime.Evaluate(normalized)) * pulse;
            if (glowRenderer != null)
                glowRenderer.widthMultiplier = baseWidth * 3.4f * Mathf.Max(0f, widthOverLifetime.Evaluate(normalized));
            if (remainingTime <= 0f) Stop();
        }

        public void Stop()
        {
            remainingTime = 0f;
            elapsedTime = 0f;
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (glowRenderer != null) glowRenderer.enabled = false;
        }
    }
}
