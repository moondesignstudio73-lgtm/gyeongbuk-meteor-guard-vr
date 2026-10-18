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
            FitToPanelMargin();
            Apply(0f);
        }

        private void OnEnable() => FitToPanelMargin();

        private void FitToPanelMargin()
        {
            if (scanLine == null || !(scanLine.parent is RectTransform panel)) return;
            // Keep the animated scan in the unused border, clear of the telemetry glyphs.
            scanLine.anchorMin = scanLine.anchorMax = new Vector2(.5f, .5f);
            scanLine.pivot = new Vector2(.5f, .5f);
            scanLine.sizeDelta = new Vector2(4f, 12f);
            scanLine.anchoredPosition = new Vector2(-panel.rect.width * .5f + 8f, scanLine.anchoredPosition.y);
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
