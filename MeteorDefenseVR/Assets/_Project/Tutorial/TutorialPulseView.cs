using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialPulseView : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amplitude = 0.08f;
        [SerializeField, Min(0.1f)] private float frequency = 2.5f;

        private Vector3 baseScale;
        private float elapsedTime;

        public bool IsPulsing { get; private set; }

        private void Awake() => baseScale = transform.localScale;

        private void Update()
        {
            if (!IsPulsing) return;
            elapsedTime += Time.deltaTime;
            float multiplier = 1f + Mathf.Sin(elapsedTime * frequency * Mathf.PI * 2f) * amplitude;
            transform.localScale = baseScale * multiplier;
        }

        public void SetPulsing(bool pulsing)
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            IsPulsing = pulsing;
            elapsedTime = 0f;
            if (!pulsing) transform.localScale = baseScale;
        }
    }
}
