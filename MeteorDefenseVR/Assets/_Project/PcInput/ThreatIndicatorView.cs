using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public static class ThreatProjection
    {
        public static float TimeToImpact(Vector3 center, MeteorController meteor, float boundaryRadius)
            => ThreatRadarMath.TimeToCollision(center, meteor, boundaryRadius);
        public static Vector2 Edge(Vector3 local, float aspect)
        {
            // Rear hemisphere: yaw's sign is the shortest turn. Do not suggest
            // looking vertically through a pitch clamp to reach a rear target.
            if (local.z < 0) return new Vector2(local.x < 0 ? -1 : 1, Mathf.Clamp(local.y / Mathf.Max(1, Mathf.Abs(local.z)), -.7f, .7f));
            Vector2 direction = new Vector2(local.x / Mathf.Max(.5f, aspect), local.y);
            if (direction.sqrMagnitude < .001f) direction = Vector2.right;
            return direction / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        }
        public static string Direction(Vector3 local)
        {
            if (local.z < 0) return local.x < 0 ? "◀ REAR" : "REAR ▶";
            if (Mathf.Abs(local.y) > Mathf.Abs(local.x)) return local.y > 0 ? "▲ ABOVE" : "▼ BELOW";
            return local.x < 0 ? "◀ LEFT" : "RIGHT ▶";
        }
        public static string Arrow(Vector3 local) => local.z < 0 || Mathf.Abs(local.x) >= Mathf.Abs(local.y) ? (local.x < 0 ? "◀" : "▶") : local.y > 0 ? "▲" : "▼";
    }

    [DefaultExecutionOrder(1400), DisallowMultipleComponent]
    public sealed class ThreatIndicatorView : MonoBehaviour
    {
        [SerializeField] private ViewDirectionProvider view;
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private AudioSource rearWarning;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Material overlayFontMaterial;
        public void SetOverlayMaterial(Material material) => overlayFontMaterial = material;
        [SerializeField, Range(3, 5)] private int maximumIndicators = 4;
        [Header("Distance warning bands in this scene's metre scale")]
        [SerializeField] private float yellowDistance = 32, orangeDistance = 22, redDistance = 12;
        [SerializeField, Min(1)] private float rearWarningInterval = 4;
        private readonly MeteorController[] candidates = new MeteorController[5];
        private readonly float[] arrival = new float[5], distances = new float[5];
        private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[5];
        private RectTransform root;
        private DifficultyManager difficulty;
        private GameFlowManager flow;
        private float nextText, nextWarning;
        private MeteorController audioThreat, playingThreat;
        private float audioArrival;
        private bool worldCanvas;
        private Canvas canvas;
        public int IndicatorLimit => difficulty != null ? Mathf.Min(maximumIndicators, difficulty.Session.WarningCount) : maximumIndicators;
        public int VisibleCount { get; private set; }
        public bool VisualsSuppressed { get; set; }
        public int RearWarnings { get; private set; }
        public void Configure(ViewDirectionProvider direction, MeteorSpawner meteors, BossClimaxController climax, AudioManager audio, AudioSource warning, TMP_FontAsset hudFont)
        { view = direction; spawner = meteors; boss = climax; audioManager = audio; rearWarning = warning; font = hudFont; }
        private void Start()
        {
            difficulty = FindAnyObjectByType<DifficultyManager>(); flow = GameFlowManager.Instance;
            var go = new GameObject("ThreatIndicators", typeof(RectTransform), typeof(Canvas));
            root = (RectTransform)go.transform; root.SetParent(view.Camera.transform, false); root.localPosition = new Vector3(0, 0, 2.65f);
            go.layer = LayerMask.NameToLayer("Player HUD");
            canvas = go.GetComponent<Canvas>(); canvas.worldCamera = view.Camera; canvas.sortingOrder = 24; canvas.planeDistance = 2.65f;
            worldCanvas = view.Camera.stereoEnabled;
            canvas.renderMode = worldCanvas ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            for (int i = 0; i < labels.Length; i++)
            {
                var item = new GameObject("Threat" + i, typeof(RectTransform)); item.SetActive(false); item.transform.SetParent(root, false); item.layer = go.layer;
                var text = item.AddComponent<TextMeshProUGUI>(); text.font = font; if (overlayFontMaterial != null) text.fontSharedMaterial = overlayFontMaterial; text.fontSize = 21; text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.rectTransform.sizeDelta = new Vector2(160, 55); labels[i] = text;
            }
        }
        private void LateUpdate()
        {
            if (root == null) return;
            bool combat = flow != null && (flow.CurrentState == GameState.Playing || flow.CurrentState == GameState.BossMeteor);
            bool enabledForSession = combat && Time.timeScale > 0 && difficulty != null &&
                (difficulty.Session.SpawnDirections == SpawnDirectionMode.FrontAndSides || difficulty.Session.SpawnDirections == SpawnDirectionMode.FullSphere || spawner.ScenarioControlled);
            root.gameObject.SetActive(enabledForSession);
            if (!enabledForSession) { VisibleCount = 0; if (rearWarning != null) rearWarning.Stop(); playingThreat = null; nextWarning = 0; return; }
            if (Time.timeScale == 0) return;
            if (worldCanvas != view.Camera.stereoEnabled)
            {
                worldCanvas = view.Camera.stereoEnabled;
                canvas.renderMode = worldCanvas ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
                if (worldCanvas) { root.localPosition = Vector3.forward * 2.65f; root.localRotation = Quaternion.identity; }
            }
            if (worldCanvas)
            {
                root.sizeDelta = new Vector2(720 * view.Camera.aspect, 720);
                root.localScale = Vector3.one * (2 * 2.65f * Mathf.Tan(view.Camera.fieldOfView * .5f * Mathf.Deg2Rad) / 720);
            }
            for (int i = 0; i < candidates.Length; i++) { candidates[i] = null; arrival[i] = distances[i] = float.PositiveInfinity; }
            var active = spawner.ActiveMeteors;
            audioThreat = null; audioArrival = float.PositiveInfinity;
            for (int i = 0; i < active.Count; i++) Consider(active[i]);
            if (boss != null) Consider(boss.ActiveBoss);
            // Audio is independent of the reduced visual warning budget. One
            // reusable source follows the actual threat direction as the view turns.
            if (playingThreat != null && playingThreat.IsTargetable && rearWarning != null && rearWarning.isPlaying)
                rearWarning.transform.position = view.Position + (playingThreat.transform.position - view.Position).normalized * 2;
            if (audioThreat != null && Time.unscaledTime >= nextWarning && audioArrival < 18 && audioManager != null)
            {
                if (audioManager.TryPlaySpatial(AudioCue.Warning, rearWarning, view.Position + (audioThreat.transform.position - view.Position).normalized * 2, audioArrival < 6 ? .8f : .35f))
                { playingThreat = audioThreat; RearWarnings++; nextWarning = Time.unscaledTime + difficulty.Session.SpatialCueInterval; }
            }
            bool updateText = Time.unscaledTime >= nextText;
            if (updateText) nextText = Time.unscaledTime + .1f;
            VisibleCount = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                var text = labels[i]; var meteor = candidates[i]; bool show = !VisualsSuppressed && i < IndicatorLimit && meteor != null && arrival[i] <= difficulty.Session.WarningHorizon;
                text.gameObject.SetActive(show); if (!show) continue; VisibleCount++;
                Vector3 local = Quaternion.Inverse(view.Rotation) * (meteor.transform.position - view.Position);
                Vector2 edge = ThreatProjection.Edge(local, view.Camera.aspect);
                Vector2 point = new Vector2(edge.x * (root.rect.width * .5f - 96), edge.y * (root.rect.height * .5f - 115));
                // Separate labels which occupy nearly the same edge; keep a fixed bounded pool.
                for (int j = 0; j < i; j++)
                    if (labels[j].gameObject.activeSelf && Vector2.Distance(point, labels[j].rectTransform.anchoredPosition) < 58)
                        point.y = Mathf.Clamp(point.y - 60, -295, 295);
                text.rectTransform.anchoredPosition = Vector2.Lerp(text.rectTransform.anchoredPosition, point, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18));
                float distance = distances[i];
                text.color = distance < redDistance ? new Color(1,.25f,.15f) : distance < orangeDistance ? new Color(1,.55f,.16f) : distance < yellowDistance ? new Color(1,.88f,.3f) : new Color(.2f,.95f,1);
                float urgency = Mathf.Clamp01(1f-arrival[i]/Mathf.Max(1f,difficulty.Session.WarningHorizon));
                float pulse = .5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.Lerp(4f,9f,urgency)+i*.8f);
                text.rectTransform.localScale = Vector3.one * (1f + pulse * urgency * .075f);
                if (updateText) text.text = (difficulty.Session.WarningDirectionText ? ThreatProjection.Direction(local) : ThreatProjection.Arrow(local)) +
                    (difficulty.Session.WarningDistance ? "\n" + Mathf.RoundToInt(distance) + "m" : "");
            }
        }
        private void Consider(MeteorController meteor)
        {
            if (meteor == null || !meteor.IsTargetable) return;
            float time = ThreatProjection.TimeToImpact(spawner.ShipCenter, meteor, 4.2f), distance = Vector3.Distance(view.Position, meteor.transform.position);
            if (time < audioArrival) { audioArrival = time; audioThreat = meteor; }
            Vector3 viewport = view.Camera.WorldToViewportPoint(meteor.transform.position);
            if (viewport.z > 0 && viewport.x > .06f && viewport.x < .94f && viewport.y > .12f && viewport.y < .88f) return;
            for (int i = 0; i < maximumIndicators; i++)
            {
                if (time > arrival[i] || (Mathf.Approximately(time, arrival[i]) && distance >= distances[i])) continue;
                for (int j = maximumIndicators - 1; j > i; j--) { candidates[j] = candidates[j-1]; arrival[j] = arrival[j-1]; distances[j] = distances[j-1]; }
                candidates[i] = meteor; arrival[i] = time; distances[i] = distance; return;
            }
        }
        private void OnDisable() { if (rearWarning != null) rearWarning.Stop(); if (root != null) root.gameObject.SetActive(false); }
        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }
        private void OnValidate() { maximumIndicators = Mathf.Clamp(maximumIndicators,3,5); redDistance = Mathf.Max(1, redDistance); orangeDistance = Mathf.Max(redDistance, orangeDistance); yellowDistance = Mathf.Max(orangeDistance, yellowDistance); }
    }
}
