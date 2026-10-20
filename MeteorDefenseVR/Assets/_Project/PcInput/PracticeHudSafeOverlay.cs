using UnityEngine;
using TMPro;
using MeteorDefenseVR.Tutorial;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class PracticeHudSafeOverlay : MonoBehaviour
    {
        [SerializeField] private PracticeController controller;
        [SerializeField] private TextMesh hudText;

        private Canvas safeCanvas;
        private TextMeshProUGUI safeText;
        private TextMesh statusText;
        private TextMeshProUGUI safeStatus;
        private Camera playerCamera;
        private bool? stereoMode;
        public Canvas SafeCanvas => safeCanvas;
        public TextMeshProUGUI SafeText => safeText;
        public TextMeshProUGUI SafeStatus => safeStatus;

        private void LateUpdate()
        {
            bool visible = hudText != null && hudText.gameObject.activeInHierarchy
                && controller != null && (controller.IsRunning || controller.IsComplete);
            if (visible) EnsureSafeHud();
            if (safeCanvas != null)
            {
                ApplyCanvasMode(playerCamera.stereoEnabled);
                safeCanvas.enabled = visible;
                if (hudText != null && safeText.text != hudText.text) safeText.text = hudText.text;
                if (statusText != null && safeStatus.text != statusText.text) safeStatus.text = statusText.text;
                // The old TextMesh remains a data channel; its 3D TMP bridge must not
                // draw a second, depth-tested copy behind the cockpit.
                if (hudText != null)
                    foreach (Renderer renderer in hudText.GetComponentsInChildren<Renderer>(true))
                    { renderer.enabled = false; renderer.forceRenderingOff = true; }
                if (statusText != null)
                    foreach (Renderer renderer in statusText.GetComponentsInChildren<Renderer>(true))
                    { renderer.enabled = false; renderer.forceRenderingOff = true; }
            }
            else if (hudText != null && hudText.TryGetComponent<Renderer>(out var hudRenderer)) hudRenderer.enabled = visible;

        }

        private void EnsureSafeHud()
        {
            if (safeCanvas != null) return;
            var mainHud = FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            playerCamera = Camera.main;
            if (mainHud == null || playerCamera == null) return;
            var root = new GameObject("PracticeSafeHUD", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            safeCanvas = root.GetComponent<Canvas>();
            safeCanvas.worldCamera = playerCamera;
            safeCanvas.overrideSorting = true;
            safeCanvas.sortingOrder = 225;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var panel = new GameObject("PracticeTelemetry", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(.06f, .865f);
            rect.anchorMax = new Vector2(.94f, .935f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var background = panel.GetComponent<UnityEngine.UI.Image>();
            background.material = mainHud.TelemetryRoot.GetComponentInChildren<UnityEngine.UI.Image>(true).material;
            background.color = new Color(.006f, .025f, .038f, .9f);
            background.raycastTarget = false;
            var label = new GameObject("PracticeTelemetryText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            safeText = label.GetComponent<TextMeshProUGUI>();
            safeText.rectTransform.anchorMin = Vector2.zero;
            safeText.rectTransform.anchorMax = Vector2.one;
            safeText.rectTransform.offsetMin = new Vector2(20, 0);
            safeText.rectTransform.offsetMax = new Vector2(-20, 0);
            safeText.font = mainHud.HealthValue.font;
            safeText.fontSharedMaterial = mainHud.HealthValue.fontSharedMaterial;
            safeText.fontSize = 32;
            safeText.enableAutoSizing = false;
            safeText.textWrappingMode = TextWrappingModes.NoWrap;
            safeText.alignment = TextAlignmentOptions.Center;
            safeText.color = new Color(.65f, .94f, 1f);
            safeText.raycastTarget = false;
            var statusPanel = Instantiate(panel, root.transform);
            statusPanel.name = "PracticeInstruction";
            var statusRect = (RectTransform)statusPanel.transform;
            statusRect.anchorMin = new Vector2(.06f, .74f);
            statusRect.anchorMax = new Vector2(.94f, .84f);
            safeStatus = statusPanel.GetComponentInChildren<TextMeshProUGUI>();
            safeStatus.name = "PracticeInstructionText";
            safeStatus.fontSize = 40;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = mainHud.gameObject.layer;
            ApplyCanvasMode(playerCamera.stereoEnabled);
        }

        public void ApplyCanvasMode(bool stereo)
        {
            if (safeCanvas == null || stereoMode == stereo) return;
            stereoMode = stereo;
            var rect = (RectTransform)safeCanvas.transform;
            rect.SetParent(stereo ? playerCamera.transform : null, false);
            safeCanvas.renderMode = stereo ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            safeCanvas.planeDistance = .45f;
            if (stereo)
            {
                rect.sizeDelta = new Vector2(1920, 1080);
                rect.localPosition = Vector3.forward * 1.6f;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * .00085f;
            }
            else rect.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            var source = FindAnyObjectByType<PracticeHudView>(FindObjectsInactive.Include);
            controller = FindAnyObjectByType<PracticeController>(FindObjectsInactive.Include);
            hudText = source != null ? source.HudText : null;
            statusText = source != null ? source.StatusText : null;
        }
        private void OnDisable() { if (safeCanvas != null) safeCanvas.enabled = false; }
        private void OnDestroy() { if (safeCanvas != null) Destroy(safeCanvas.gameObject); }
    }
}
