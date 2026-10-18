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
            Label("FlightTitle", root, font, "출격 준비", 28, new Vector2(0, 255), new Vector2(720, 46), new Color(.8f, .98f, 1));
            var graphicRect = Rect("FlightOrbit", root, new Vector2(0, 50), new Vector2(880, 350));
            visual.graphic = graphicRect.gameObject.AddComponent<FlightPreparationGraphic>(); visual.graphic.raycastTarget = false;
            visual.status = Label("PreparationStatus", root, font, "우주선을 준비하고 있어요", 25, new Vector2(0, -155), new Vector2(840, 44), new Color(.8f, .98f, 1));
            var track = Rect("ProgressTrack", root, new Vector2(0, -202), new Vector2(420, 3));
            var image = track.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.1f, .3f, .35f); image.raycastTarget = false;
            visual.fill = Rect("ProgressFill", track, Vector2.zero, new Vector2(0, 3));
            visual.fill.anchorMin = visual.fill.anchorMax = visual.fill.pivot = new Vector2(0, .5f);
            image = visual.fill.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.14f, .9f, .95f); image.raycastTarget = false;
            visual.percent = Label("PreparationPercent", root, font, "0%", 18, new Vector2(0, -235), new Vector2(300, 32), new Color(.45f, .78f, .85f));
            Label("FlightTip", root, font, "조준점을 운석에 맞추면 자동으로 발사돼요.", 18, new Vector2(0, -295), new Vector2(900, 38), new Color(.55f, .75f, .8f));
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
            previousPercent = value; percent.SetText("{0}%", value); fill.sizeDelta = new Vector2(420 * Progress, 3);
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
