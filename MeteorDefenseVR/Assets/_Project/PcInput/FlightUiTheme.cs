using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeteorDefenseVR.PcInput
{
    // Shared presentation tokens. Gameplay controllers continue to own text and visibility.
    public static class FlightUiTheme
    {
        public static readonly Color Ink = new Color32(11, 21, 34, 255);
        public static readonly Color Surface = new Color32(19, 35, 52, 255);
        public static readonly Color Edge = new Color32(52, 72, 91, 255);
        public static readonly Color Text = new Color32(229, 237, 245, 255);
        public static readonly Color Muted = new Color32(156, 176, 196, 255);
        public static readonly Color Accent = new Color32(119, 214, 208, 255);
        public static readonly Color Warning = new Color32(237, 179, 107, 255);

        public static void Plate(Image image)
        {
            if (image == null) return;
            image.color = Color.white;
            var shape = image.GetComponent<ChamferedHudPanel>();
            if (shape == null) shape = image.gameObject.AddComponent<ChamferedHudPanel>();
            shape.Configure(Ink, Edge, 5f, 1f);
        }

        public static void Hud(GameplayHudOverlayView hud)
        {
            var scaler = hud.HudCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            string[] names = { "HealthPanel", "StagePanel", "ScorePanel" };
            float[] anchors = { .20f, .5f, .80f };
            float[] widths = { 240, 300, 264 };
            for (int i = 0; i < names.Length; i++)
            {
                var rect = hud.TelemetryRoot.transform.Find(names[i]) as RectTransform;
                if (rect == null) continue;
                rect.anchorMin = rect.anchorMax = new Vector2(anchors[i], 1);
                rect.sizeDelta = new Vector2(widths[i], 94);
                Plate(rect.GetComponent<Image>());
                foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == hud.AsteroidLabel) continue;
                    bool caption = text.name.EndsWith("Label") || text == hud.DifficultyStageValue;
                    text.fontSize = caption ? 24 : 30;
                    text.color = caption ? Muted : Text;
                    text.fontStyle = caption ? FontStyles.Normal : FontStyles.Bold;
                    text.characterSpacing = 0;
                    text.enableAutoSizing = false;
                    text.rectTransform.sizeDelta = new Vector2(widths[i] - 24, caption ? 34 : 48);
                }
            }
            Plate(hud.FeedbackPanel.GetComponent<Image>());
            // Eight solid shield segments need their own width budget.
            hud.HealthValue.fontSize = 26;
            hud.HealthValue.fontStyle = FontStyles.Normal;
            hud.FeedbackValue.color = Text;
            hud.FeedbackValue.fontSize = 28;
        }
    }
}
