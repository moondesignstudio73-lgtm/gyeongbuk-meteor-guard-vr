using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class MeteorCleanupFade : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float duration = 0.65f;
        private Vector3 startScale;
        private float elapsed;

        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }

        public void Begin(float fadeDuration = 0.65f)
        {
            duration = Mathf.Max(0.05f, fadeDuration);
            startScale = transform.localScale;
            elapsed = 0f;
            IsRunning = true;
            IsComplete = false;
        }

        private void Update()
        {
            if (IsRunning) Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning) return;
            elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = startScale * (1f - progress);
            if (progress < 1f) return;
            IsRunning = false;
            IsComplete = true;
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
