using System.Collections.Generic;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class FinalPresentationSetup
    {
        public static void Ensure(Camera camera, TMP_FontAsset font)
        {
            Transform ready = camera.transform.Find("ReadyScreen");
            Transform decor = ready.Find("ReadyDecorations");
            TextMesh instructions = EnsureText(decor, "ReadyInstructions");
            instructions.transform.localPosition = new Vector3(0, -.1f, 3.82f);
            instructions.text = "VR 운석 맞추기\n\n지구를 향해 날아오는 운석을\n시선으로 파괴하세요.";
            Present(instructions, font, 2.1f, 3.6f, 1.3f);
            ready.Find("ReadyTitle").localPosition = new Vector3(0, 1.05f, 3.82f);
            Present(ready.Find("ReadyTitle").GetComponent<TextMesh>(), font, 3.3f, 3.7f, .55f);
            decor.Find("ReadyTitleFrame").localPosition = new Vector3(0, .852f, 0);
            decor.Find("ReadyTitleFrame").localScale = new Vector3(1.6f, .55f, 1f);
            ready.Find("ReadyStartButton").localPosition = new Vector3(0, -1.45f, 3.95f);
            decor.Find("StartLabel").localPosition = new Vector3(0, -1.45f, 3.9f);
            Present(decor.Find("StartLabel").GetComponent<TextMesh>(), font, 2.6f, 1.3f, .4f);
            TextMesh prompt = EnsureText(decor, "ReadyPrompt");
            prompt.transform.localPosition = new Vector3(0, -1.03f, 3.82f);
            prompt.text = "준비가 되면 START를 바라보세요.";
            Present(prompt, font, 1.8f, 3.5f, .3f);

            Present(camera.transform.Find("MissionCompleteText").GetComponent<TextMesh>(), font, 3f, 3.15f, .9f);
            camera.transform.Find("ResultText").localPosition = new Vector3(0, .8f, 3.86f);
            Present(camera.transform.Find("ResultText").GetComponent<TextMesh>(), font, 2.8f, 2.65f, 2.45f,
                Object.FindAnyObjectByType<ResultController>());
            camera.transform.Find("MissionCompleteVisualFrame").localScale = new Vector3(1.2f, 1.3f, 1f);
            camera.transform.Find("ResultVisualFrame").localScale = new Vector3(1.2f, 1.18f, 1f);
            camera.transform.Find("ResultVisualFrame").localPosition = new Vector3(0, .706f, 0);
            var hud = camera.transform.Find("MissionHUD");
            foreach (string name in new[] { "HudHealth", "HudRemaining", "HudScore" })
                Present(hud.Find(name).GetComponent<TextMesh>(), font, 1.7f, name == "HudRemaining" ? 1.6f : 1.4f, .34f);
            TextMesh feedback = hud.Find("HudFeedback").GetComponent<TextMesh>();
            feedback.transform.localPosition = new Vector3(0, .62f, 3.9f);
            Present(feedback, font, 2.2f, 3.1f, .65f);
            Transform feedbackPanel = hud.Find("TacticalHudFrames/FeedbackPanel");
            feedbackPanel.localPosition = new Vector3(0, .62f, 3.96f);
            feedbackPanel.localScale = new Vector3(3.3f, .78f, .018f);

            var safeRoots = new List<Transform> { ready, hud, camera.transform.Find("MissionCompleteText"), camera.transform.Find("MissionCompleteVisualFrame"),
                camera.transform.Find("ResultText"), camera.transform.Find("ResultVisualFrame"), camera.transform.Find("CinematicAlert") };
            foreach (TextMesh text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
            {
                if (text.name == "InstructionText" || text.name == "PracticeStatus" || text.name == "PracticeHud" || text.name == "LaunchStatus" || text.name == "CalibrationStatus")
                {
                    // Keep controller ownership; only place the existing display in the camera's UI space.
                    Vector3 p = camera.transform.InverseTransformPoint(text.transform.position);
                    if (text.name == "PracticeHud") p.y = 1.95f;
                    else if (text.name == "PracticeStatus") p.y = 1.55f;
                    else p.y = text.name == "InstructionText" ? 1.8f : 1.38f;
                    text.transform.position = camera.transform.TransformPoint(p);
                    // The one-line practice HUD must also clear the cockpit canopy struts.
                    Present(text, font, text.name == "PracticeHud" ? 1.5f : 2.4f, 4.6f, .9f);
                    safeRoots.Add(text.transform);
                }
                if (text.name == "AlertText") Present(text, font, 2.2f, 3.3f, .8f);
            }
            Transform alert = camera.transform.Find("CinematicAlert");
            alert.localScale = Vector3.one;
            foreach (Transform border in alert)
            {
                if (border.name == "AlertText") continue;
                Vector3 p = border.localPosition, s = border.localScale;
                border.localPosition = new Vector3(p.x * 1.5f, .35f + (p.y - .35f) * 1.2f, p.z);
                border.localScale = new Vector3(s.x * 1.5f, s.y * 1.2f, s.z);
            }
            var safe = camera.GetComponent<WorldUiSafeArea>();
            if (safe == null) safe = camera.gameObject.AddComponent<WorldUiSafeArea>();
            safe.Configure(camera, safeRoots.ToArray());
            TutorialSafeZoneSetup.Ensure(camera, font);
        }
        private static TextMesh EnsureText(Transform parent, string name)
        {
            Transform root = parent.Find(name);
            if (root == null) { root = new GameObject(name).transform; root.SetParent(parent, false); }
            TextMesh text = root.GetComponent<TextMesh>();
            if (text == null) text = root.gameObject.AddComponent<TextMesh>();
            text.color = new Color(.7f, .96f, 1f); return text;
        }
        private static void Present(TextMesh source, TMP_FontAsset font, float size, float width, float height, ResultController result = null)
        {
            Transform child = source.transform.Find("PolishedText");
            if (child == null) { child = new GameObject("PolishedText", typeof(RectTransform)).transform; child.SetParent(source.transform, false); }
            var text = child.GetComponent<TextMeshPro>();
            if (text == null) text = child.gameObject.AddComponent<TextMeshPro>();
            child = text.transform;
            child.localPosition = new Vector3(0, 0, -.01f);
            text.font = font; text.fontSize = size; text.enableAutoSizing = false;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow; text.color = source.color;
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.raycastTarget = false; text.text = source.text;
            var bridge = source.GetComponent<WorldTextPresentation>();
            if (bridge == null) bridge = source.gameObject.AddComponent<WorldTextPresentation>();
            bridge.Configure(source, text, result);
        }
    }
}
