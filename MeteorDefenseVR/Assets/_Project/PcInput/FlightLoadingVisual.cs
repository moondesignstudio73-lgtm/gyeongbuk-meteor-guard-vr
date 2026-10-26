using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed class FlightLoadingVisual : MonoBehaviour
    {
        private FlightPreparationGraphic graphic;
        private TextMeshProUGUI status, percent;
        private RectTransform fill;
        private float elapsed;
        private int previousPercent = -1;
        public float Progress { get; private set; }

        public static FlightLoadingVisual Create(Transform parent, TMP_FontAsset font)
        {
            var root = Rect("FlightPreparation", parent, Vector2.zero, new Vector2(1280, 720));
            var visual = root.gameObject.AddComponent<FlightLoadingVisual>();
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var plate=Rect("PreparationPanel",root,Vector2.zero,new Vector2(1120,660)).gameObject.AddComponent<UnityEngine.UI.Image>();
            plate.raycastTarget=false;FlightUiTheme.Plate(plate);
            var title=Label("FlightTitle", root, font, "출격 준비", 48, new Vector2(0, 265), new Vector2(1000, 80), FlightUiTheme.Accent);
            title.fontStyle=FontStyles.Bold;
            var graphicRect = Rect("FlightOrbit", root, new Vector2(0, 70), new Vector2(880, 350));
            graphicRect.localScale=Vector3.one*.8f;
            visual.graphic = graphicRect.gameObject.AddComponent<FlightPreparationGraphic>(); visual.graphic.raycastTarget = false;
            visual.status = Label("PreparationStatus", root, font, "우주선을 준비하고 있어요", 40, new Vector2(0, -105), new Vector2(1000, 64), FlightUiTheme.Text);
            var track = Rect("ProgressTrack", root, new Vector2(0, -220), new Vector2(560, 8));
            var image = track.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.1f, .3f, .35f); image.raycastTarget = false;
            visual.fill = Rect("ProgressFill", track, Vector2.zero, new Vector2(0, 8));
            visual.fill.anchorMin = visual.fill.anchorMax = visual.fill.pivot = new Vector2(0, .5f);
            image = visual.fill.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.14f, .9f, .95f); image.raycastTarget = false;
            visual.percent = Label("PreparationPercent", root, font, "0%", 42, new Vector2(0, -175), new Vector2(300, 64), FlightUiTheme.Text);
            visual.percent.fontStyle=FontStyles.Bold;
            Label("FlightTip", root, font, "조준점을 운석에 맞추면 자동으로 발사돼요.", 40, new Vector2(0, -275), new Vector2(1000, 64), FlightUiTheme.Text);
            return visual;
        }
        public void Present(float progress, string message, float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            Progress = Mathf.Clamp01(progress);
            graphic.Present(elapsed, Progress);
            if (status.text != message) status.text = message;
            int value = Mathf.FloorToInt(Progress * 100);
            if (value == previousPercent) return;
            previousPercent = value; percent.SetText("{0}%", value); fill.sizeDelta = new Vector2(560 * Progress, 8);
        }
        internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        internal static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string content, float size, Vector2 position, Vector2 bounds, Color color)
        {
            var rect = Rect(name, parent, position, bounds); rect.gameObject.SetActive(false);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = content; text.fontSize = size;
            text.color = color; text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
            rect.gameObject.SetActive(true); return text;
        }
    }
}
