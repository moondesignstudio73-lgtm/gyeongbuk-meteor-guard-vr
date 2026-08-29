using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(1150), DisallowMultipleComponent]
    public sealed class DifficultySelectionView : MonoBehaviour
    {
        private DifficultyManager manager;
        private ReadyScreenController ready;
        private SfPresentationDirector presentation;
        private bool requestedVisible;
        private Camera view;
        private RectTransform panel;
        private TextMesh instruction;
        private string originalInstruction;
        private Vector3 originalPosition;
        private readonly DifficultyChoiceButton[] choices = new DifficultyChoiceButton[4];
        private readonly UnityEngine.UI.Image[] outlines = new UnityEngine.UI.Image[4];
        private static readonly DifficultyLevel[] DisplayOrder = { DifficultyLevel.Experience, DifficultyLevel.Easy, DifficultyLevel.Normal, DifficultyLevel.Hard };
        private TextMeshPro extremeNotice;
        private bool extremeNoticeShown;
        private float extremeNoticeRemaining;
        private EyeTracking.GazeRaycaster gaze;
        private float aspect, fov;
        private bool initialized;
        private Transform lobbyAnchor;
        public void SetLobbyContext(Transform anchor)
        {
            lobbyAnchor = anchor;
            if (initialized)
            {
                panel.GetComponent<Canvas>().sortingOrder = anchor != null ? 16 : 12;
                foreach(var text in panel.GetComponentsInChildren<TextMeshPro>(true)) text.GetComponent<MeshRenderer>().sortingOrder = anchor != null ? 17 : 13;
                Layout(); RefreshVisibility(); Refresh();
            }
        }
        public DifficultyChoiceButton[] Choices => choices;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode _)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var ready = root.GetComponentInChildren<ReadyScreenController>(true);
                if (ready == null) continue;
                if (ready.GetComponent<DifficultySelectionView>() == null) ready.gameObject.AddComponent<DifficultySelectionView>();
                break;
            }
        }
        private void Start()
        {
            manager = GetComponent<DifficultyManager>(); ready = GetComponent<ReadyScreenController>(); view = Camera.main;
            presentation = GetComponent<SfPresentationDirector>();
            var title = transform.Find("ReadyTitle")?.GetComponent<WorldTextPresentation>();
            if (manager == null || manager.Settings == null || title == null || title.Display == null || view == null) { enabled = false; return; }
            gaze = FindAnyObjectByType<EyeTracking.GazeRaycaster>();
            var instructions = transform.Find("ReadyDecorations/ReadyInstructions");
            if (instructions != null)
            {
                instruction = instructions.GetComponent<TextMesh>(); originalInstruction = instruction.text; originalPosition = instructions.localPosition;
                instruction.text = "난이도를 선택하세요"; instructions.localPosition = new Vector3(0, .32f, 3.82f);
            }
            Build(title.Display.font, title.Display.fontSharedMaterial);
            initialized = true; Bind(); Refresh(); SetVisible(ready.IsReadyVisible);
        }
        private void Bind() { manager.SelectionChanged -= Refresh; manager.SelectionChanged += Refresh; ready.VisibilityChanged -= SetVisible; ready.VisibilityChanged += SetVisible; }
        private void OnEnable() { if (initialized) { Bind(); Refresh(); SetVisible(ready.IsReadyVisible); } }
        private void OnDisable()
        {
            if (manager != null) manager.SelectionChanged -= Refresh;
            if (ready != null) ready.VisibilityChanged -= SetVisible;
            if (panel != null) panel.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
            if (instruction != null) { instruction.text = originalInstruction; instruction.transform.localPosition = originalPosition; }
        }
        private void Build(TMP_FontAsset font, Material material)
        {
            var root = new GameObject("DifficultySelection", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            panel = (RectTransform)root.transform; panel.SetParent(ready.transform, false); panel.sizeDelta = new Vector2(752, 106);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view; canvas.sortingOrder = 12;
            string[] labels = { "체험", "쉬움", "보통", "어려움" };
            string[] subtitles = { "짧은 기관·행사 체험", "전방 중심 / 초보자 추천", "전방·좌우 / 방향 인지", "360° 전 방향 / 최종 훈련" };
            for (int i = 0; i < choices.Length; i++)
            {
                DifficultyLevel level = DisplayOrder[i];
                var card = Image("Difficulty" + level, panel, new Vector2((i - 1.5f) * 188, 0), new Vector2(178, 102), new Color(.07f, .3f, .36f, 1));
                outlines[i] = card;
                var fill = Image("Surface", card.transform, Vector2.zero, new Vector2(174, 98), new Color(.012f, .045f, .062f, 1));
                // Physics IGazeTarget owns mouse dwell and clicks as well as webcam/VR.
                // GraphicRaycaster must not make GazeInputRouter.PointerOverUi block it.
                card.raycastTarget = false;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = fill;
                var body = new GameObject("Label"); body.transform.SetParent(card.transform, false);
                Label(body.transform, "Title", labels[i], new Vector2(0, 16), 240f, font, material);
                Label(body.transform, "Description", subtitles[i], new Vector2(0, -18), 108f, font, material);
                var tickRoot = new GameObject("SelectedCheck"); tickRoot.transform.SetParent(card.transform, false); tickRoot.transform.localPosition = new Vector3(70, 33, -.5f);
                var tick1 = Image("TickShort", tickRoot.transform, new Vector2(-4, -2), new Vector2(3, 8), new Color(.35f, 1, .8f));
                tick1.transform.localRotation = Quaternion.Euler(0, 0, 45);
                var tick2 = Image("TickLong", tickRoot.transform, new Vector2(1, 0), new Vector2(3, 14), new Color(.35f, 1, .8f));
                tick2.transform.localRotation = Quaternion.Euler(0, 0, -40);
                var progress = Image("DwellProgress", card.transform, new Vector2(-84, -43), new Vector2(168, 2), new Color(.35f, 1, .8f));
                progress.rectTransform.pivot = new Vector2(0, .5f); progress.rectTransform.localScale = new Vector3(0, 1, 1);
                var collider = card.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(178, 102, 2); collider.isTrigger = true;
                // Reuse existing feedback. No independent tween or audio source.
                var animator = card.gameObject.AddComponent<UIInteractionAnimator>(); animator.SetVisual(body.transform); animator.EnableConfirmCue(AudioCue.LockOn); animator.SetAutomaticPointerConfirm(false);
                animator.EnableHoverCue(AudioCue.TargetFocus);
                var choice = card.gameObject.AddComponent<DifficultyChoiceButton>();
                choice.Configure(manager, level, animator, card, progress, tickRoot); choices[i] = choice;
            }
            Label(panel, "ExtremeNotice", "체험 모드는 3~5분의 짧은 운영 세션입니다.", new Vector2(0, 82), 165, font, material);
            extremeNotice = panel.Find("ExtremeNotice").GetComponent<TextMeshPro>(); extremeNotice.rectTransform.sizeDelta = new Vector2(760, 40);
            extremeNotice.color = new Color(1, .65f, .25f); extremeNotice.gameObject.SetActive(false);
            Layout();
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private static void Label(Transform parent, string name, string content, Vector2 position, float size, TMP_FontAsset font, Material material)
        {
            var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>(); text.font = font; text.fontSharedMaterial = material; text.fontSize = size;
            text.rectTransform.sizeDelta = new Vector2(174, 35); text.transform.localPosition = new Vector3(position.x, position.y, -2f);
            text.color = new Color(.78f, .97f, 1); text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.text = content; text.raycastTarget = false;
            // TMP is a mesh, not a Canvas graphic: explicitly put it above the card Canvas.
            go.GetComponent<MeshRenderer>().sortingOrder = 13;
            go.SetActive(true);
        }
        private void Layout()
        {
            aspect = view.aspect; fov = view.fieldOfView;
            if (lobbyAnchor != null)
            {
                panel.SetPositionAndRotation(lobbyAnchor.position, lobbyAnchor.rotation);
                panel.localScale = lobbyAnchor.lossyScale * .78f;
                return;
            }
            float scale = 2 * 3.65f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / 720f * Mathf.Min(1f, aspect / (16f / 9f));
            panel.localScale = Vector3.one * scale;
            panel.position = view.ViewportToWorldPoint(new Vector3(.5f, .42f, 3.65f)) + ready.transform.position - view.transform.position;
        }
        private void Refresh()
        {
            foreach (var choice in choices) if (choice != null) choice.Refresh();
            if (manager.Selected == DifficultyLevel.Experience && !extremeNoticeShown)
            { extremeNoticeShown = true; extremeNoticeRemaining = 1.8f; }
        }
        private void SetVisible(bool visible) { requestedVisible = visible; RefreshVisibility(); }
        private void RefreshVisibility()
        {
            if (panel == null) return;
            bool visible = lobbyAnchor != null || (requestedVisible && (presentation == null || (!presentation.IsBootPresenting && !presentation.IsPreparing)));
            if (panel.gameObject.activeSelf == visible) return;
            panel.gameObject.SetActive(visible);
            if (visible) Refresh();
        }
        private void Update()
        {
            if (!initialized || panel == null) return;
            extremeNoticeRemaining = Mathf.Max(0, extremeNoticeRemaining - Time.deltaTime);
            extremeNotice.gameObject.SetActive(extremeNoticeRemaining > 0);
            if (instruction != null) instruction.gameObject.SetActive(extremeNoticeRemaining <= 0);
            RefreshVisibility();
            if (!panel.gameObject.activeInHierarchy) return;
            // Direct mouse click uses the already-resolved gaze ray; dwell remains the shared path.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && gaze != null &&
                gaze.CurrentTarget is DifficultyChoiceButton choice) choice.Choose();
            foreach (var card in choices)
            {
                bool hovering = gaze != null && ReferenceEquals(gaze.CurrentTarget, card);
                // Existing animator implements the same 1.00 -> 1.04 hover and confirmation pulse.
                card.SetFocus(hovering);
            }
        }
        private void LateUpdate()
        {
            // WorldUiSafeArea (order 950) adjusts Ready first; follow its final position.
            if (initialized && panel != null && panel.gameObject.activeInHierarchy && (aspect != view.aspect || fov != view.fieldOfView)) Layout();
        }
    }
}
