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
        private readonly float[] heights = { .18f, .76f, .36f, .57f };
        private readonly Vector3[] corners = new Vector3[4];
        private bool ready;

        private void Start()
        {
            telemetry = FindAnyObjectByType<CockpitTelemetryPresentation>();
            var hud = GetComponent<GameplayHudOverlayView>();
            if (hud != null && hud.FeedbackValue != null) feedbackBounds = hud.FeedbackValue.rectTransform;
            view = Camera.main; spawner = FindAnyObjectByType<MeteorSpawner>();
            boss = FindAnyObjectByType<BossClimaxController>();
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
                panel.sizeDelta = new Vector2(400, 160);
                panel.pivot = new Vector2(i == 0 ? 0 : 1, .5f);
                SetHeight(i, heights[0]);
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
                label.fontSize = 48; label.enableAutoSizing = false;
                label.color = Color.white; label.alpha = 1; label.lineSpacing = 8;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false;
                var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(16, 8); rect.offsetMax = new Vector2(-16, -8);
            }
            telemetry.ReadableSummary = true; telemetry.Refresh(); ready = true;
            if (telemetry.WeaponReadout != null)
                panels[1].gameObject.AddComponent<PowerUpWeaponFeedback>().Configure(telemetry.WeaponReadout, telemetry.ThreatReadout);
        }

        private void SetHeight(int side, float y)
        {
            var panel = panels[side]; panel.anchorMin = panel.anchorMax = new Vector2(side == 0 ? .02f : .98f, y);
            panel.anchoredPosition = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (!ready || spawner == null) return;
            for (int side = 0; side < panels.Length; side++)
            {
                Vector2 bestAnchor = panels[side].anchorMin;
                float best = Obstruction(panels[side]);
                if (best <= 0) continue; // Stable until a target crosses the panel.
                foreach (float height in heights)
                {
                    SetHeight(side, height);
                    float score = Obstruction(panels[side]);
                    if (score < best) { best = score; bestAnchor = panels[side].anchorMin; }
                    if (score <= 0) break;
                }
                // Closely spaced targets can leave a gap between the preferred rows.
                // Search that remaining vertical space without shrinking or hiding values.
                if (best > 0)
                {
                    float halfHeight = ViewBounds(panels[side]).height * .5f;
                    for (float y = .03f + halfHeight; y <= .88f - halfHeight; y += .015f)
                    {
                        SetHeight(side, y);
                        float score = Obstruction(panels[side]);
                        if (score < best) { best = score; bestAnchor = panels[side].anchorMin; }
                        if (score <= 0) break;
                    }
                }
                // A larger buff list may have no clear vertical gap on its original side.
                // Try the opposite edge before considering the central bands; preserve size.
                if (best > 0)
                {
                    Rect current = ViewBounds(panels[side]);
                    for (float y = .03f + current.height * .5f; y <= .88f - current.height * .5f; y += .015f)
                    {
                        panels[side].anchorMin = panels[side].anchorMax = new Vector2(side == 0 ? .98f - current.width : .02f + current.width, y);
                        float score = Obstruction(panels[side]);
                        if (score < best) { best = score; bestAnchor = panels[side].anchorMin; }
                        if (score <= 0) break;
                    }
                }
                // If the other status panel divides that edge into two small gaps, move
                // both panels as a pair. Neither is hidden, shrunk, or allowed over a target.
                if (best > 0)
                {
                    int other = 1 - side;
                    Vector2 originalOther = panels[other].anchorMin;
                    float width = ViewBounds(panels[side]).width;
                    bool paired = false;
                    foreach (float otherHeight in heights)
                    {
                        SetHeight(other, otherHeight);
                        foreach (float height in heights)
                        {
                            panels[side].anchorMin = panels[side].anchorMax = new Vector2(side == 0 ? .98f - width : .02f + width, height);
                            if (Obstruction(panels[side]) > .000001f || Obstruction(panels[other]) > .000001f) continue;
                            best = 0; bestAnchor = panels[side].anchorMin; paired = true; break;
                        }
                        if (paired) break;
                    }
                    if (!paired) panels[other].anchorMin = panels[other].anchorMax = originalOther;
                }
                // A dense vertical column can occupy the entire side. Use the empty
                // central lower/upper band, keeping both the aim area and other panel clear.
                if (best > 0)
                {
                    float halfWidth = ViewBounds(panels[side]).width * .5f;
                    foreach (float y in new[] { .18f, .75f })
                    {
                        panels[side].anchorMin = panels[side].anchorMax = new Vector2(.5f + (side == 0 ? -halfWidth : halfWidth), y);
                        float score = Obstruction(panels[side]);
                        if (score < best) { best = score; bestAnchor = panels[side].anchorMin; }
                        if (score <= 0) break;
                    }
                }
                panels[side].anchorMin = panels[side].anchorMax = bestAnchor;
            }
        }

        private Rect ViewBounds(RectTransform panel)
        {
            panel.GetWorldCorners(corners);
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            foreach (var corner in corners) { Vector2 p = view.WorldToViewportPoint(corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private float Obstruction(RectTransform panel)
        {
            var bounds = ViewBounds(panel);
            bounds.xMin -= .006f; bounds.xMax += .006f; bounds.yMin -= .006f; bounds.yMax += .006f;
            float score = 0;
            // A temporary weapon extension must stay on-screen and below the top HUD too.
            score += bounds.width * bounds.height - Intersection(bounds, new Rect(.01f, .02f, .98f, .86f));
            score += Intersection(bounds, new Rect(.42f, .4f, .16f, .2f));
            if (feedbackBounds != null && feedbackBounds.gameObject.activeInHierarchy)
                score += Intersection(bounds, ViewBounds(feedbackBounds));
            foreach (var other in panels) if (other != null && other != panel) score += Intersection(bounds, ViewBounds(other));
            foreach (var meteor in spawner.ActiveMeteors) score += Overlap(bounds, meteor);
            if (boss != null) score += Overlap(bounds, boss.ActiveBoss);
            return score;
        }

        private static float Intersection(Rect a, Rect b) =>
            Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin))
            * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        private float Overlap(Rect bounds, MeteorController meteor)
        {
            if (meteor == null || !meteor.IsTargetable) return 0;
            Vector3 p = view.WorldToViewportPoint(meteor.transform.position);
            if (p.z <= 0) return 0;
            float radius = Mathf.Max(1, meteor.CollisionRadius * 1.4f);
            Vector3 up = view.WorldToViewportPoint(meteor.transform.position + view.transform.up * radius);
            Vector3 right = view.WorldToViewportPoint(meteor.transform.position + view.transform.right * radius);
            float dx = Mathf.Abs(right.x - p.x), dy = Mathf.Abs(up.y - p.y);
            return Mathf.Max(0, Mathf.Min(bounds.xMax, p.x + dx) - Mathf.Max(bounds.xMin, p.x - dx))
                * Mathf.Max(0, Mathf.Min(bounds.yMax, p.y + dy) - Mathf.Max(bounds.yMin, p.y - dy));
        }
    }
}
