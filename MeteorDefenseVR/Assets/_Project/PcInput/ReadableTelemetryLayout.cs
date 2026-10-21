using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Reuse the authored panels. Run before warning placement, which reads these same rects.
    [DefaultExecutionOrder(1200), DisallowMultipleComponent]
    public sealed class ReadableTelemetryLayout : MonoBehaviour
    {
        private CockpitTelemetryPresentation telemetry;
        private MeteorSpawner spawner;
        private BossClimaxController boss;
        private Camera view;
        private RectTransform feedbackBounds;
        private readonly RectTransform[] panels = new RectTransform[2];

        private readonly Vector3[] corners = new Vector3[4];
        private bool ready;

        private void Start()
        {
            telemetry = FindAnyObjectByType<CockpitTelemetryPresentation>();
            var hud = GetComponent<GameplayHudOverlayView>();
            view = Camera.main;
            if (telemetry == null || hud == null || view == null || telemetry.ShipReadout == null || telemetry.ThreatReadout == null) return;
            var labels = new[] { telemetry.ShipReadout, telemetry.ThreatReadout };
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i]; var panel = (RectTransform)label.transform.parent; panels[i] = panel;
                // Keep the essential readouts in front of cockpit hands and dashboard geometry.
                var canvas = panel.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) canvas.planeDistance = .25f;
                var scaler = panel.GetComponentInParent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.referenceResolution = new Vector2(1280, 720);
                    scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                }
                panel.sizeDelta = new Vector2(300, 136);
                panel.pivot = new Vector2(i == 0 ? 0 : 1, 0);
                SetHeight(i, .03f);
                var scan = panel.GetComponent<CockpitHologramScan>();
                if (scan != null) { scan.enabled = false; if (scan.ScanLine != null) scan.ScanLine.gameObject.SetActive(false); }
                foreach (var child in panel.GetComponentsInChildren<TMP_Text>(true))
                    if (child != label) child.gameObject.SetActive(false);
                var plate = panel.GetComponent<UnityEngine.UI.Image>();
                if (plate != null)
                {
                    plate.material = hud.TelemetryRoot.GetComponentInChildren<UnityEngine.UI.Image>(true).material;
                    plate.color = new Color(.006f, .018f, .03f, .94f); plate.raycastTarget = false;
                }
                label.font = hud.HealthValue.font; label.fontSharedMaterial = hud.HealthValue.fontSharedMaterial;
                label.fontSize = 34; label.enableAutoSizing = false;
                label.color = Color.white; label.alpha = 1; label.lineSpacing = 8;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false;
                var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(16, 8); rect.offsetMax = new Vector2(-16, -8);
            }
            telemetry.ReadableSummary = true; telemetry.Refresh();
            if (telemetry.WeaponReadout != null)
                panels[1].gameObject.AddComponent<PowerUpWeaponFeedback>().Configure(telemetry.WeaponReadout, telemetry.ThreatReadout);
        }

        private void SetHeight(int side, float y)
        {
            var panel = panels[side]; panel.anchorMin = panel.anchorMax = new Vector2(side == 0 ? .02f : .98f, y);
            panel.anchoredPosition = Vector2.zero;
        }

        // Status readouts stay at the dashboard edges. Moving targets must never
        // reposition persistent UI: per-frame avoidance caused panels to jump across the view.
    }
}
