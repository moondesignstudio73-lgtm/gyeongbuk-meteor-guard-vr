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
        private FlightLoadingVisual loadingVisual;
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
            loadingVisual = FlightLoadingVisual.Create(root.transform, font);
        }
        private void LateUpdate()
        {
            if (loader == null || loader.IsDone)
            {
                canvas.gameObject.SetActive(false); background.enabled = false; enabled = false; return;
            }
            // Scene loading is the first part; ReadyPrewarm completes the same flight display.
            loadingVisual.Present(loader.Progress * .4f, "우주로 향하는 길을 준비해요", Time.unscaledDeltaTime);
        }
    }
}
