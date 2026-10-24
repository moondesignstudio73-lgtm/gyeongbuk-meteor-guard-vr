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
            panel = (RectTransform)root.transform; panel.SetParent(ready.transform, false); panel.sizeDelta = new Vector2(752, 150);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view; canvas.sortingOrder = 12;
            string[] labels = { "체험", "쉬움", "보통", "어려움" };
            string[] subtitles = { "짧게 즐기는\n우주 임무", "앞을 보며\n차근차근", "좌우를 살피며\n도전", "모든 방향의\n운석에 도전" };
            for (int i = 0; i < choices.Length; i++)
            {
                DifficultyLevel level = DisplayOrder[i];
                var card = Image("Difficulty" + level, panel, new Vector2((i - 1.5f) * 176, 0), new Vector2(166, 150), new Color(.07f, .3f, .36f, 1));
                outlines[i] = card;
                var fill = Image("Surface", card.transform, Vector2.zero, new Vector2(162, 146), new Color(.012f, .045f, .062f, 1));
                // Physics IGazeTarget owns mouse dwell and clicks as well as webcam/VR.
                // GraphicRaycaster must not make GazeInputRouter.PointerOverUi block it.
                card.raycastTarget = false;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = fill;
                var body = new GameObject("Label"); body.transform.SetParent(card.transform, false);
                Label(body.transform, "Title", labels[i], new Vector2(0, 47), 300f, font, material);
                Label(body.transform, "Description", subtitles[i], new Vector2(0, -12), 240f, font, material);
                var tickRoot = new GameObject("SelectedCheck"); tickRoot.transform.SetParent(card.transform, false); tickRoot.transform.localPosition = new Vector3(64, 57, -.5f);
                var tick1 = Image("TickShort", tickRoot.transform, new Vector2(-4, -2), new Vector2(3, 8), new Color(.35f, 1, .8f));
                tick1.transform.localRotation = Quaternion.Euler(0, 0, 45);
                var tick2 = Image("TickLong", tickRoot.transform, new Vector2(1, 0), new Vector2(3, 14), new Color(.35f, 1, .8f));
                tick2.transform.localRotation = Quaternion.Euler(0, 0, -40);
                var progress = Image("DwellProgress", card.transform, new Vector2(-78, -67), new Vector2(156, 2), new Color(.35f, 1, .8f));
                progress.rectTransform.pivot = new Vector2(0, .5f); progress.rectTransform.localScale = new Vector3(0, 1, 1);
                var collider = card.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(166, 150, 2); collider.isTrigger = true;
                // Reuse existing feedback. No independent tween or audio source.
                var animator = card.gameObject.AddComponent<UIInteractionAnimator>(); animator.SetVisual(body.transform); animator.EnableConfirmCue(AudioCue.LockOn); animator.SetAutomaticPointerConfirm(false);
                animator.EnableHoverCue(AudioCue.TargetFocus);
                var choice = card.gameObject.AddComponent<DifficultyChoiceButton>();
                choice.Configure(manager, level, animator, card, progress, tickRoot); choices[i] = choice;
            }
            Label(panel, "ExtremeNotice", "체험은 짧은 임무로 조작을 익히기 좋아요.", new Vector2(0, 100), 390, font, material);
            extremeNotice = panel.Find("ExtremeNotice").GetComponent<TextMeshPro>(); extremeNotice.rectTransform.sizeDelta = new Vector2(900, 64);
            extremeNotice.color = Color.white; extremeNotice.gameObject.SetActive(false);
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
            text.rectTransform.sizeDelta = new Vector2(name == "Description" ? 156 : 162, name == "Description" ? 80 : 35); text.transform.localPosition = new Vector3(position.x, position.y, -2f);
            text.color = new Color(.78f, .97f, 1); text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = false;
            text.textWrappingMode = name == "Description" ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; text.text = content; text.raycastTarget = false;
            // TMP is a mesh, not a Canvas graphic: explicitly put it above the card Canvas.
            go.GetComponent<MeshRenderer>().sortingOrder = 13;
            go.SetActive(true);
        }
        private void Layout()
        {
            aspect = view.aspect; fov = view.fieldOfView;
            // Preserve enlarged type while using the extra vertical card padding only on narrow views.
            float compact = lobbyAnchor != null ? 0 : Mathf.InverseLerp(4f/3f, 16f/9f, aspect);
            float height = Mathf.Lerp(150,116,compact);
            panel.sizeDelta = new Vector2(704,height);
            foreach (var card in choices)
            {
                ((RectTransform)card.transform).sizeDelta = new Vector2(166,height);
                ((RectTransform)card.transform.Find("Surface")).sizeDelta = new Vector2(162,height-4);
                card.GetComponent<BoxCollider>().size = new Vector3(166,height,2);
                card.transform.Find("Label/Title").localPosition = new Vector3(0,Mathf.Lerp(47,37,compact),-2);
                card.transform.Find("Label/Description").localPosition = new Vector3(0,Mathf.Lerp(-12,-8,compact),-2);
                card.transform.Find("SelectedCheck").localPosition = new Vector3(64,height*.5f-18,-.5f);
                ((RectTransform)card.transform.Find("DwellProgress")).anchoredPosition = new Vector2(-78,-height*.5f+8);
            }
            if (lobbyAnchor != null)
            {
                panel.SetPositionAndRotation(lobbyAnchor.position, lobbyAnchor.rotation);
                panel.localScale = lobbyAnchor.lossyScale * 1.3f;
                return;
            }
            float scale = 2 * 3.65f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / 720f * Mathf.Min(1f, aspect / (16f / 9f));
            panel.localScale = Vector3.one * scale * 1.4f;
            panel.position = view.ViewportToWorldPoint(new Vector3(.5f, .42f, 3.65f)) + ready.transform.position - view.transform.position;
        }
        private void Refresh()
        {
            foreach (var choice in choices) if (choice != null) choice.Refresh();
            if (manager.Selected == DifficultyLevel.Experience && !extremeNoticeShown)
            { extremeNoticeShown = true; extremeNoticeRemaining = 4f; }
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
            RefreshVisibility();
            // Preparation hides the cards: spend the one-time tip only while the player can see it.
            if (panel.gameObject.activeInHierarchy)
                extremeNoticeRemaining = Mathf.Max(0, extremeNoticeRemaining - Time.deltaTime);
            extremeNotice.gameObject.SetActive(panel.gameObject.activeInHierarchy && extremeNoticeRemaining > 0);
            // Reserve a separate line for the full tip; narrow views receive larger type.
            float noticeY = lobbyAnchor != null ? 110 : manager.Settings.lockDifficultySelection
                ? Mathf.Lerp(150, 110, Mathf.InverseLerp(4f / 3f, 16f / 9f, view.aspect)) : 100;
            // The lobby has a smaller world scale; keep comparable screen-space reading size.
            extremeNotice.fontSize = Mathf.Lerp(lobbyAnchor != null ? 470 : 430, 390,
                Mathf.InverseLerp(4f / 3f, 16f / 9f, view.aspect));
            extremeNotice.transform.localPosition = new Vector3(0, noticeY, -2);
            if (instruction != null)
            {
                bool operatorLocked = panel.gameObject.activeInHierarchy && manager.Settings.lockDifficultySelection;
                string message = DifficultyConfig.SelectionInstruction(operatorLocked, manager.Selected);
                if (instruction.text != message) instruction.text = message;
                instruction.gameObject.SetActive(operatorLocked || extremeNoticeRemaining <= 0);
            }
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
