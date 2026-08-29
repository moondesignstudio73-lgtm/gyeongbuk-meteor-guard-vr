using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.PcInput
{
    // Scene-local, non-blocking presentation. No GameFlow calls, camera motion, RTs or audio sources.
    [DefaultExecutionOrder(1100), DisallowMultipleComponent]
    public sealed partial class SfPresentationDirector : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("MD.UI.Motion");
        private static readonly Color Cyan = new Color(.14f, .90f, .95f), Green = new Color(.56f, 1f, .77f), Warning = new Color(1f, .38f, .22f);
        public static float ReadyTitleDelay { get; private set; } = .45f;
        public static float ReadyStartDelay { get; private set; } = 1.35f;
        private GameFlowManager flow;
        private Camera view;
        private ReadyStartGazeTarget startTarget;
        private UIInteractionAnimator startFeedback;
        private GazeRaycaster raycaster;
        private MissionHudController hud;
        private WorldTextPresentation[] texts;
        private RectTransform overlay;
        private UnityEngine.UI.Image curtain, scan;
        private readonly UnityEngine.UI.Image[] edges = new UnityEngine.UI.Image[8];
        private readonly UnityEngine.UI.Image[] ring = new UnityEngine.UI.Image[40];
        private RectTransform ringRoot;
        private TextMeshPro bootText, ghost;
        private MeshRenderer originalStart, startVisual;
        private Transform healthPanel;
        private Vector3 healthScale;
        private float age, scanAge = 10f, pulseAge = 10f, ghostAge = 10f, healthAge = 10f;
        private Color pulseColor;
        private bool booting, resetting, startWasEnabled, initialized;
        private float previousHealth, lastAspect, lastFov;
        private float bootBeganAt;
        private int ringSteps = -1;
        public bool IsBootPresenting => booting;
        public int TransitionCount { get; private set; }
        public int PulseCount { get; private set; }
        public int RingSteps => ringSteps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() { ReadyTitleDelay = .45f; ReadyStartDelay = 1.35f; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode _)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var ready = root.GetComponentInChildren<ReadyScreenController>(true);
                if (ready == null) continue;
                if (ready.GetComponent<SfPresentationDirector>() == null) ready.gameObject.AddComponent<SfPresentationDirector>();
                break;
            }
        }
        private void Start()
        {
            flow = GameFlowManager.Instance; view = Camera.main;
            if (flow == null || view == null) { enabled = false; return; }
            texts = FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include);
            startTarget = FindAnyObjectByType<ReadyStartGazeTarget>(FindObjectsInactive.Include);
            raycaster = FindAnyObjectByType<GazeRaycaster>(); hud = FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            TMP_FontAsset font = null; Material fontMaterial = null;
            foreach (var bridge in texts)
                if (bridge.Display != null) { font = bridge.Display.font; fontMaterial = bridge.Display.fontSharedMaterial; break; }
            if (font == null) { enabled = false; return; }
            BuildOverlay(font, fontMaterial);
            InitializeFeel(font, fontMaterial);
            foreach (var point in FindObjectsByType<CalibrationPoint>(FindObjectsInactive.Include))
                if (point.GetComponent<CalibrationPointMotion>() == null) point.gameObject.AddComponent<CalibrationPointMotion>().ConfigureLegacy(point);
            if (startTarget != null)
            {
                startWasEnabled = startTarget.enabled;
                originalStart = startTarget.GetComponent<MeshRenderer>();
                var filter = startTarget.GetComponent<MeshFilter>();
                if (originalStart != null && filter != null)
                {
                    // A visual-only shell keeps dwell collider bounds completely stable.
                    var shell = new GameObject("StartFeedbackVisual", typeof(MeshFilter), typeof(MeshRenderer));
                    shell.transform.SetParent(startTarget.transform, false);
                    shell.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    startVisual = shell.GetComponent<MeshRenderer>(); startVisual.sharedMaterials = originalStart.sharedMaterials;
                    startVisual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    startVisual.receiveShadows = false; originalStart.forceRenderingOff = true;
                    startFeedback = startTarget.GetComponent<UIInteractionAnimator>();
                    if (startFeedback == null) startFeedback = startTarget.gameObject.AddComponent<UIInteractionAnimator>();
                    startFeedback.SetVisual(shell.transform);
                    startFeedback.EnableHoverCue(MeteorDefenseVR.Audio.AudioCue.TargetFocus);
                    startFeedback.EnableConfirmCue(MeteorDefenseVR.Audio.AudioCue.LockOn);
                }
            }
            if (hud != null) previousHealth = hud.CurrentHealth;
            foreach (var rect in view.GetComponentsInChildren<Transform>(true))
                if (rect.name == "Panel_HP") { healthPanel = rect; healthScale = rect.localScale; break; }
            initialized = true; Bind();
            if (flow.CurrentState == GameState.Boot) BeginBoot(false);
        }
        private void OnEnable() { if (initialized) Bind(); }
        private void Bind()
        {
            flow.OnStateTransitioned -= Transition; flow.OnStateTransitioned += Transition;
            GazeLockOnTarget.GlobalLocked -= Locked; GazeLockOnTarget.GlobalLocked += Locked;
            if (hud != null) { hud.HealthChanged -= HealthChanged; hud.HealthChanged += HealthChanged; }
            BindFeel();
        }
        private void BuildOverlay(TMP_FontAsset font, Material fontMaterial)
        {
            var root = new GameObject("SF_MotionCanvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(view.transform, false); overlay = (RectTransform)root.transform;
            overlay.localPosition = Vector3.forward * 2.8f;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view; canvas.sortingOrder = 20;
            overlay.sizeDelta = new Vector2(1280f, 720f);
            curtain = Image("SpaceReveal", overlay, Vector2.zero, new Vector2(4000, 3000)); curtain.color = Color.clear;
            for (int i = 0; i < edges.Length; i++)
            {
                bool left = i % 4 < 2, top = i < 4, vertical = i % 2 == 0;
                edges[i] = Image("HudCorner" + i, overlay, new Vector2(left ? -560 : 560, top ? 284 : -284), vertical ? new Vector2(2, 44) : new Vector2(72, 2));
                edges[i].color = Color.clear;
            }
            scan = Image("TransitionScan", overlay, new Vector2(-560, 284), new Vector2(64, 2)); scan.color = Color.clear;
            var ringObject = new GameObject("StartDwellRing", typeof(RectTransform)); ringRoot = (RectTransform)ringObject.transform;
            ringRoot.SetParent(overlay, false); ringRoot.sizeDelta = new Vector2(88, 88);
            for (int i = 0; i < ring.Length; i++)
            {
                float a = (90f - i * 360f / ring.Length) * Mathf.Deg2Rad;
                ring[i] = Image("DwellSegment" + i, ringRoot, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 38, new Vector2(2.5f, 4.5f));
                ring[i].rectTransform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                ring[i].color = Color.clear;
            }
            ringRoot.gameObject.SetActive(false);
            bootText = Text("SystemOnline", font, fontMaterial); bootText.fontSize = 1.3f;
            bootText.GetComponent<MeshRenderer>().sortingOrder = 21;
            // Exercise the same rich-text parser in Ready, not on the first Intro frame.
            bootText.text = "<align=center><color=#BFF9FF><size=1.3>SYSTEM ONLINE</size></color></align>";
            bootText.transform.localPosition = new Vector3(0, .15f, 2.65f); bootText.rectTransform.sizeDelta = new Vector2(3, .4f); bootText.enabled = false;
            ghost = Text("OutgoingPanel", font, fontMaterial); ghost.enabled = false;
        }
        private TextMeshPro Text(string name, TMP_FontAsset font, Material material)
        {
            var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(view.transform, false);
            var text = go.AddComponent<TextMeshPro>(); text.font = font; text.fontSharedMaterial = material;
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            go.SetActive(true); return text;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false; return image;
        }
        private void BeginBoot(bool reset)
        {
            booting = true; resetting = reset; age = 0f; scanAge = 0f;
            bootBeganAt = Time.unscaledTime;
            ReadyTitleDelay = reset ? .2f : .45f; ReadyStartDelay = reset ? .65f : 1.35f;
            if (startTarget != null) { startTarget.OnGazeExit(); startTarget.enabled = false; }
            startFeedback?.SetAvailable(false);
            if (startVisual != null) startVisual.enabled = false;
        }
        private void Transition(GameState from, GameState to)
        {
            TransitionFeel(to);
            TransitionCount++;
            if (to != GameState.Boot) CaptureOutgoing();
            if (to == GameState.Reset) { BeginBoot(true); return; }
            if (to == GameState.Boot) { if (!booting) BeginBoot(true); return; }
            EndBoot();
            scanAge = 0f;
            if (to == GameState.Intro) { startFeedback?.Confirm(); Pulse(Cyan); }
            if (to == GameState.Playing) Pulse(Cyan);
            if (to == GameState.BossMeteor) Pulse(Warning);
            if (to == GameState.MissionComplete) Pulse(Green);
            if (healthPanel != null) healthPanel.localScale = healthScale;
        }
        private void CaptureOutgoing()
        {
            ghost.enabled = false;
            // One pooled text mesh; no scene snapshots, duplicate controllers or render textures.
            foreach (var bridge in texts)
            {
                var text = bridge.Display;
                if (text == null || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                string name = bridge.name;
                if (name != "AlertText" && name != "ResultText" && name != "InstructionText" && name != "LaunchStatus" && name != "MissionCompleteText") continue;
                ghost.text = text.text; ghost.fontSize = text.fontSize; ghost.fontStyle = text.fontStyle;
                ghost.rectTransform.sizeDelta = text.rectTransform.sizeDelta; ghost.transform.SetPositionAndRotation(text.transform.position, text.transform.rotation);
                Vector3 parentScale = view.transform.lossyScale, textScale = text.transform.lossyScale;
                ghost.transform.localScale = new Vector3(textScale.x / parentScale.x, textScale.y / parentScale.y, textScale.z / parentScale.z);
                ghost.color = text.color; ghost.maxVisibleLines = text.maxVisibleLines;
                ghost.enabled = true; ghostAge = 0f; break;
            }
        }
        private void EndBoot()
        {
            booting = false;
            if (curtain != null) curtain.color = Color.clear;
            if (bootText != null) bootText.enabled = false;
            if (startTarget != null) startTarget.enabled = startWasEnabled;
            startFeedback?.SetAvailable(true);
            if (startVisual != null) startVisual.enabled = true;
        }
        public void DismissBootForLobby()
        {
            if (!initialized) return;
            EndBoot();
            if (ghost != null) ghost.enabled = false;
        }
        private void Locked(GazeLockOnTarget _) => Pulse(Green);
        private void HealthChanged(float current, float maximum)
        {
            if (current < previousHealth) { Pulse(Warning); healthAge = 0f; streak = 0; }
            previousHealth = current;
        }
        private void Pulse(Color color) { pulseColor = color; pulseAge = 0f; PulseCount++; }
        private void LateUpdate()
        {
            if (!initialized || Time.timeScale == 0) return;
            using (TickMarker.Auto()) Tick(Time.unscaledDeltaTime);
        }
        private void Tick(float dt)
        {
            age += dt; scanAge += dt; pulseAge += dt; ghostAge += dt; healthAge += dt;
            if (view.aspect != lastAspect || view.fieldOfView != lastFov)
            {
                lastAspect = view.aspect; lastFov = view.fieldOfView;
                overlay.localScale = Vector3.one * (2f * 2.8f * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) / 720f);
                overlay.sizeDelta = new Vector2(720f * view.aspect, 720f);
                float edgeX = Mathf.Min(560f, overlay.sizeDelta.x * .44f);
                for (int i = 0; i < edges.Length; i++) edges[i].rectTransform.anchoredPosition = new Vector2(i % 4 < 2 ? -edgeX : edgeX, i < 4 ? 284 : -284);
            }
            if (booting)
            {
                age = Mathf.Max(0f, Time.unscaledTime - bootBeganAt);
                // One initial space fade; Reset uses a brief low-opacity veil, never a repeated flash.
                float opacity = resetting ? .65f * WorldTextMotion.Pulse(age, .55f) : 1f - Mathf.Clamp01(age / .45f);
                curtain.color = new Color(0, .005f, .012f, opacity);
                bool preparing = preparation != null && !preparation.IsComplete;
                bool resetAudioPending = resetting && audio != null && audio.IsBgmTransitioning;
                bootText.enabled = age > .05f;
                bootText.color = Cyan;
                bootText.text = preparing ? preparation.Stage : resetting ? "SYSTEM RESET / READY" : "DEFENSE SYSTEM ONLINE";
                if (preparing)
                {
                    curtain.color = new Color(0, .005f, .012f, .92f);
                    ReadyTitleDelay = age + .1f; ReadyStartDelay = age + .15f;
                }
                else if (!resetAudioPending && age >= (resetting ? .55f : .25f))
                {
                    ReadyTitleDelay = 0f; ReadyStartDelay = .12f;
                    EndBoot();
                }
            }
            if (ghost.enabled)
            {
                ghost.color = new Color(.7f, .96f, 1f, (1f - Mathf.Clamp01(ghostAge / .14f)) * .75f);
                if (ghostAge >= .14f) ghost.enabled = false;
            }
            float flash = WorldTextMotion.Pulse(pulseAge, .32f) * .45f;
            Color corner = Color.Lerp(Cyan, pulseColor, Mathf.Clamp01(flash * 3f));
            corner.a = flash + (scanAge < .65f ? .2f * (1f - scanAge / .65f) : 0f);
            for (int i = 0; i < edges.Length; i++) if (edges[i].color != corner) edges[i].color = corner;
            float scanWidth = Mathf.Min(520f, overlay.sizeDelta.x * .41f);
            scan.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-scanWidth, scanWidth, Mathf.Clamp01(scanAge / .6f)), 284);
            scan.color = new Color(Cyan.r, Cyan.g, Cyan.b, Mathf.Clamp01(1f - scanAge / .6f) * .7f);
            if (healthPanel != null) healthPanel.localScale = healthScale * (1f + .035f * WorldTextMotion.Pulse(healthAge, .3f));
            UpdateDwell();
            TickFeel(dt);
        }
        private void UpdateDwell()
        {
            bool ready = flow.CurrentState == GameState.Boot && !booting && startTarget != null && startTarget.isActiveAndEnabled;
            bool focused = ready && raycaster != null && ReferenceEquals(raycaster.CurrentTarget, startTarget);
            startFeedback?.SetGaze(focused, ready ? startTarget.Progress : 0f);
            bool show = ready && focused;
            if (ringRoot.gameObject.activeSelf != show) ringRoot.gameObject.SetActive(show);
            if (!show) { ringSteps = -1; return; }
            Vector3 point = view.WorldToViewportPoint(startTarget.transform.position);
            // Beside START, not on top of the instruction line above it.
            ringRoot.anchoredPosition = new Vector2((point.x - .5f) * overlay.sizeDelta.x + Mathf.Min(165f, overlay.sizeDelta.x * .30f), (point.y - .5f) * 720f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(startTarget.Progress * ring.Length), 0, ring.Length);
            if (steps == ringSteps) return;
            ringSteps = steps;
            for (int i = 0; i < ring.Length; i++) ring[i].color = i < steps ? Green : new Color(Cyan.r, Cyan.g, Cyan.b, .14f);
        }
        private void OnDisable()
        {
            UnbindFeel();
            if (flow != null) flow.OnStateTransitioned -= Transition;
            GazeLockOnTarget.GlobalLocked -= Locked;
            if (hud != null) hud.HealthChanged -= HealthChanged;
            if (initialized) EndBoot();
            if (healthPanel != null) healthPanel.localScale = healthScale;
        }
        private void OnDestroy()
        {
            DestroyFeel();
            if (originalStart != null) originalStart.forceRenderingOff = false;
            if (startVisual != null) Destroy(startVisual.gameObject);
            if (overlay != null) Destroy(overlay.gameObject);
            if (bootText != null) Destroy(bootText.gameObject);
            if (ghost != null) Destroy(ghost.gameObject);
        }
    }
}
