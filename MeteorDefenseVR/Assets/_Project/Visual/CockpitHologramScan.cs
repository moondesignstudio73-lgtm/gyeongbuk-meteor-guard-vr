using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    [DisallowMultipleComponent]
    public sealed class CockpitHologramScan : MonoBehaviour
    {
        [SerializeField] private RectTransform scanLine;
        [SerializeField, Min(.05f)] private float period = 3.2f;
        [SerializeField] private float verticalRange = 96f;

        public RectTransform ScanLine => scanLine;
        public float Period => period;

        public void Configure(RectTransform line, float seconds = 3.2f, float range = 96f)
        {
            scanLine = line;
            period = Mathf.Max(.05f, seconds);
            verticalRange = Mathf.Max(0f, range);
            Apply(0f);
        }

        private void Update() => Apply(Mathf.Repeat(Time.unscaledTime / period, 1f));

        private void Apply(float phase)
        {
            if (scanLine == null) return;
            Vector2 position = scanLine.anchoredPosition;
            position.y = Mathf.Lerp(verticalRange, -verticalRange, phase);
            scanLine.anchoredPosition = position;
        }
    }
}
