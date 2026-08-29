using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Bootstrap scene only. No artificial minimum duration, no camera device access.
    [DefaultExecutionOrder(-1100), DisallowMultipleComponent]
    public sealed class SystemBootView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private BootstrapSceneLoader loader;
        private Canvas canvas;
        private Camera background;
        private TextMeshProUGUI progressLabel;
        private RectTransform bar;
        private int lastPercent = -1;
        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public void Configure(TMP_FontAsset asset, BootstrapSceneLoader source) { font = asset; loader = source; }
        private void Awake()
        {
            var cameraObject = new GameObject("BootBackgroundCamera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            background = cameraObject.GetComponent<Camera>();
            background.clearFlags = CameraClearFlags.SolidColor; background.backgroundColor = new Color(.008f, .018f, .035f);
            background.cullingMask = 0; background.depth = -100;
            var root = new GameObject("BootCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            Label(root.transform, "METEOR DEFENSE / FLIGHT COMPUTER", 20, new Vector2(0, 110), new Color(.5f,.68f,.76f));
            Label(root.transform, "SYSTEM INITIALIZING", 44, new Vector2(0, 50), new Color(.14f,.9f,.95f));
            Label(root.transform, "GAZE SENSOR / INPUT CHECK", 18, new Vector2(0, -80), new Color(.5f,.68f,.76f));
            progressLabel = Label(root.transform, "SCENE 0%", 22, new Vector2(0,-125), new Color(.75f,.98f,1f));
            var track = Rect("LoadingTrack", root.transform, new Vector2(0,-20), new Vector2(520,4));
            var image = track.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.04f,.14f,.19f); image.raycastTarget = false;
            bar = Rect("SceneProgress", track, Vector2.zero, new Vector2(0,4));
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0,.5f); bar.anchoredPosition = Vector2.zero;
            image = bar.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.14f,.9f,.95f); image.raycastTarget = false;
        }
        private void LateUpdate()
        {
            if (loader == null || loader.IsDone)
            {
                canvas.gameObject.SetActive(false); background.enabled = false; enabled = false; return;
            }
            int percent = Mathf.FloorToInt(loader.Progress * 100);
            if (percent != lastPercent) { lastPercent = percent; progressLabel.SetText("SCENE {0}%", percent); bar.sizeDelta = new Vector2(520 * loader.Progress, 4); }
        }
        private TextMeshProUGUI Label(Transform parent, string content, float size, Vector2 position, Color color)
        {
            var rect = Rect("BootLabel", parent, position, new Vector2(900,70)); rect.gameObject.SetActive(false);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = content; text.fontSize = size;
            text.color = color; text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            rect.gameObject.SetActive(true); return text;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
    }
}
