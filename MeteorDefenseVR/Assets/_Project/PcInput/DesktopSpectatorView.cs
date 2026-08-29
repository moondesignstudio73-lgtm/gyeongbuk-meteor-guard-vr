using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // URP 17.5 renders ScreenSpaceOverlay after the XR mirror, NOT into HMD eyes.
    // This view contains only game metrics. No camera preview, tracking/face data, or file paths.
    public sealed class DesktopSpectatorView : IDisposable
    {
        private static readonly Color Cyan = new Color32(38, 223, 243, 255);
        private static readonly Color Ink = new Color32(3, 18, 29, 247);
        private static readonly Color Quiet = new Color32(139, 186, 196, 255);
        private static readonly Color LockGreen = new Color32(114, 255, 149, 255);
        private static readonly Color Danger = new Color32(255, 113, 82, 255);
        private readonly GameObject root, picture, hudRoot, operatorRoot;
        private readonly UnityEngine.UI.RawImage output;
        private readonly UnityEngine.UI.AspectRatioFitter aspect;
        private readonly TextMeshProUGUI health, remaining, score, lockText, modeText, switchText;
        private readonly UnityEngine.UI.Image lockFill;
        private readonly TMP_FontAsset font;
        private int lastHp = -1, lastRemaining = -1, lastTotal = -1, lastScore = -1;
        private DesktopViewMode? previousMode;
        public Canvas Canvas { get; }
        public UnityEngine.UI.Button SwitchButton { get; }
        public bool OperatorVisible => operatorRoot.activeSelf;
        public string ScoreText => score.text;
        public string HealthText => health.text;
        public string RemainingText => remaining.text;
        public string LockText => lockText.text;

        public DesktopSpectatorView(Transform parent, TMP_FontAsset uiFont, UnityEngine.Events.UnityAction switchMode)
        {
            font = uiFont;
            root = new GameObject("DesktopSpectatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            Canvas = root.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.sortingOrder = 85; Canvas.targetDisplay = 0;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            picture = Rect("ObserverFrame", root.transform, Vector2.one * .5f, Vector2.zero, Vector2.zero).gameObject;
            Stretch(picture.GetComponent<RectTransform>());
            Backdrop(picture, Color.black);
            var imageRect = Rect("LetterboxedScene", picture.transform, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            output = imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); output.raycastTarget = false;
            aspect = imageRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 16f / 9f;
            picture.SetActive(false);

            hudRoot = Rect("MonitorTelemetry", root.transform, new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(1060, 158)).gameObject;
            var cards = Rect("Metrics", hudRoot.transform, new Vector2(.5f, 1), Vector2.zero, new Vector2(1060, 110));
            var layout = cards.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 18; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            health = Card(cards, "HP / SHIELD"); remaining = Card(cards, "남은 운석"); score = Card(cards, "SCORE");
            var lockPanel = Rect("LockReadout", hudRoot.transform, new Vector2(.5f, 0), new Vector2(0, 0), new Vector2(440, 36));
            lockPanel.pivot = new Vector2(.5f, 0);
            Backdrop(lockPanel.gameObject, Ink);
            lockText = Label(lockPanel, "LockStatus", 21); Stretch(lockText.rectTransform);
            var track = Rect("LockProgress", lockPanel, new Vector2(0, 0), Vector2.zero, new Vector2(440, 3)); track.pivot = Vector2.zero;
            lockFill = Backdrop(track.gameObject, Cyan);
            hudRoot.SetActive(false);
            modeText = Label(Rect("ViewBadge", root.transform, new Vector2(1, 0), new Vector2(-24, 28), new Vector2(400, 26)), "Mode", 18);
            Stretch(modeText.rectTransform); modeText.alignment = TextAlignmentOptions.Right; modeText.color = Quiet;
            modeText.gameObject.SetActive(false);
            operatorRoot = Rect("MonitorOperatorControl", root.transform, new Vector2(.5f, 0), new Vector2(0, 72), new Vector2(440, 44)).gameObject;
            var background = Backdrop(operatorRoot, new Color32(6, 49, 65, 255)); background.raycastTarget = true;
            SwitchButton = operatorRoot.AddComponent<UnityEngine.UI.Button>(); SwitchButton.targetGraphic = background;
            SwitchButton.onClick.AddListener(switchMode);
            switchText = Label(operatorRoot.transform, "SwitchView", 21); Stretch(switchText.rectTransform);
            operatorRoot.AddComponent<UIInteractionAnimator>(); operatorRoot.SetActive(false);
        }

        public void SetOutput(RenderTexture texture)
        {
            bool visible = texture != null;
            if (output.texture != texture) { output.texture = texture; if (visible) aspect.aspectRatio = (float)texture.width / texture.height; }
            if (picture.activeSelf != visible) picture.SetActive(visible);
        }

        public void Present(MissionHudController hud, GazeLockOnTarget target, GameState state, DesktopViewMode mode, bool xrRunning, bool operatorMode, bool recentlyFired = false)
        {
            operatorRoot.SetActive(operatorMode);
            if (previousMode != mode)
            {
                previousMode = mode;
                switchText.text = mode == DesktopViewMode.Mirror ? "SPECTATOR VIEW · 관전 시점으로 전환" : "MIRROR VIEW · 플레이어 시점으로 전환";
                modeText.text = mode == DesktopViewMode.Mirror ? "MIRROR VIEW" : "SPECTATOR VIEW";
            }
            modeText.gameObject.SetActive(mode == DesktopViewMode.Spectator);
            bool combat = state == GameState.Practice || state == GameState.Playing || state == GameState.BossMeteor;
            bool visible = hud != null && combat && (mode == DesktopViewMode.Spectator || xrRunning);
            hudRoot.SetActive(visible);
            if (!visible) return;
            int hp = Mathf.RoundToInt(hud.HealthNormalized * 100);
            if (lastHp != hp) { lastHp = hp; health.text = hp + "%"; health.color = hp <= 30 ? Danger : Cyan; }
            if (lastRemaining != hud.RemainingMeteors || lastTotal != hud.TotalMeteors)
            { lastRemaining = hud.RemainingMeteors; lastTotal = hud.TotalMeteors; remaining.text = lastRemaining + " / " + lastTotal; }
            if (lastScore != hud.Score) { lastScore = hud.Score; score.text = hud.Score.ToString("N0"); }
            bool locked = recentlyFired || (target != null && target.IsLocked) || hud.FeedbackMessage == "LOCK ON";
            bool locking = target != null && target.State == LockOnState.Focusing;
            string status = locked ? "LOCK ON" : locking ? "LOCKING" : "TARGET SEARCH";
            if (lockText.text != status) lockText.text = status;
            lockText.color = locked ? LockGreen : Cyan; lockFill.color = lockText.color;
            lockFill.rectTransform.localScale = new Vector3(locked ? 1 : locking ? target.Progress : 0, 1, 1);
        }

        private TextMeshProUGUI Card(Transform parent, string heading)
        {
            var card = Rect(heading, parent, Vector2.one * .5f, Vector2.zero, new Vector2(330, 110));
            Backdrop(card.gameObject, Ink);
            var title = Label(Rect("Title", card, new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(310, 28)), "Text", 22); Stretch(title.rectTransform); title.text = heading; title.color = Quiet;
            var value = Label(Rect("Value", card, new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(310, 52)), "Text", 40); Stretch(value.rectTransform);
            var line = Rect("TelemetryLine", card, new Vector2(.5f, 0), Vector2.zero, new Vector2(310, 2)); Backdrop(line.gameObject, Cyan);
            return value;
        }
        private TextMeshProUGUI Label(Transform parent, string name, float size)
        {
            var rect = Rect(name, parent, Vector2.one * .5f, Vector2.zero, new Vector2(300, 30));
            rect.gameObject.SetActive(false);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font;
            label.fontSize = size; label.fontStyle = FontStyles.Bold; label.enableAutoSizing = false; label.color = Cyan; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
            rect.gameObject.SetActive(true); return label;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Image Backdrop(GameObject target, Color color)
        { var image = target.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image; }
        public void Dispose() { if (root != null) DesktopSpectatorController.DestroyOwned(root); }
    }
}
