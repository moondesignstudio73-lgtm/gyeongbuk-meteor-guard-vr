using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Presentation-only bridge: existing controllers and their TextMesh references remain intact.
    [DefaultExecutionOrder(900), DisallowMultipleComponent]
    public sealed class WorldTextPresentation : MonoBehaviour
    {
        [SerializeField] private TextMesh source;
        [SerializeField] private TextMeshPro display;
        [SerializeField] private ResultController result;
        private MeshRenderer legacyRenderer;
        [SerializeField, HideInInspector] private Font legacyFont;
        [SerializeField, HideInInspector] private Font dataOnlyFont;
        [SerializeField, HideInInspector] private int legacyFontSize;
        [SerializeField, HideInInspector] private FontStyle legacyFontStyle;
        private string previous;
        private WorldTextMotion motion;
        private bool prepared;
        private string sourceName;
        public TextMeshPro Display => display;
        public void Configure(TextMesh text, TextMeshPro presentation, ResultController resultController = null)
        { source = text; display = presentation; result = resultController; }
        private void Awake() => PrepareMotion();
        private void PrepareMotion()
        {
            if (prepared || source == null || display == null) return;
            prepared = true;
            sourceName = source.name;
            // The raised Ready title can sort behind its transparent backing by distance.
            if (sourceName == "ReadyTitle") display.GetComponent<MeshRenderer>().sortingOrder = 13;
            legacyRenderer = source != null ? source.GetComponent<MeshRenderer>() : null;
            DetachLegacyFont();
            if (WorldTextMotion.Supports(sourceName))
            {
                motion = GetComponent<WorldTextMotion>();
                if (motion == null) motion = gameObject.AddComponent<WorldTextMotion>();
                motion.Configure(display, sourceName);
            }
        }
        private void OnEnable() { if (prepared) DetachLegacyFont(); }
        private void DetachLegacyFont()
        {
            // TextMesh is only the existing controller's text/visibility data channel now.
            // forceRenderingOff hides it but still rasterizes a dynamic legacy font on .text
            // changes (Font.CacheFontForText). Null selects Unity's dynamic default font,
            // so use an authored empty static font. TMP owns the visible, baked glyphs.
            if (source == null || dataOnlyFont == null || source.font == dataOnlyFont) return;
            if (source.font != null) legacyFont = source.font;
            legacyFontSize = source.fontSize; legacyFontStyle = source.fontStyle;
            source.fontSize = 0; source.fontStyle = FontStyle.Normal;
            source.font = dataOnlyFont;
        }
        private void RestoreLegacyFont()
        {
            if (source == null || legacyFont == null) return;
            source.font = legacyFont;
            source.fontSize = legacyFontSize; source.fontStyle = legacyFontStyle;
        }
        public void PrewarmVisual(Transform activeWarmRoot)
        {
            PrepareMotion();
            if (display == null) return;
            var child = display.transform;
            var renderer = display.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            Transform parent = child.parent;
            int sibling = child.GetSiblingIndex();
            bool active = child.gameObject.activeSelf;
            bool renderingOff = renderer.forceRenderingOff;
            bool moved = !child.gameObject.activeInHierarchy;
            // ForceMeshUpdate(ignoreActiveState) still returns before TMP.Awake has run.
            // Warm only the visual child, invisibly; never activate its controller/state root.
            try
            {
                renderer.forceRenderingOff = true;
                if (moved) { child.SetParent(activeWarmRoot, false); child.gameObject.SetActive(true); }
                display.ForceMeshUpdate(true);
            }
            finally
            {
                if (moved) { child.SetParent(parent, false); child.SetSiblingIndex(sibling); child.gameObject.SetActive(active); }
                renderer.forceRenderingOff = renderingOff;
            }
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (source == null || display == null) return;
            PrepareMotion();
            string content = source.text;
            if (legacyRenderer == null) legacyRenderer = source.GetComponent<MeshRenderer>();
            if (legacyRenderer != null) { legacyRenderer.forceRenderingOff = true; display.enabled = legacyRenderer.enabled; }
            // One assignment: alternating source and presentation colors dirtied TMP every frame.
            display.color = sourceName == "MissionCompleteText" || sourceName == "AlertText"
                ? new Color(.75f, .98f, 1f, source.color.a) : source.color;
            if (previous != content)
            {
                previous = content;
                display.text = result != null && result.IsVisible ? FormatResult(result.Snapshot, result.Rank) : FormatTelemetry(sourceName, content);
                if (sourceName == "MissionCompleteText" && !string.IsNullOrEmpty(content))
                    display.text = "<color=#BFF9FF>MISSION COMPLETE</color>\n<size=1.8><color=#8FFFC4>미션 성공!\n지구를 지켜냈습니다!</color></size>";
                if (sourceName == "AlertText" && content.StartsWith("WARNING"))
                    display.text = "<color=#FF7952>" + content + "</color>";
                if (sourceName == "HudFeedback" && content.Contains("LARGE METEOR"))
                    display.text = "<color=#FF7952>WARNING</color>\n<size=1.7>LARGE METEOR DETECTED</size>";
            }
            motion?.Present(content, display.enabled && !string.IsNullOrEmpty(content));
        }
        private static string FormatTelemetry(string name, string value)
        {
            if (name == "HudHealth") return "<size=1.35>HP / SHIELD</size>\n<size=1.0> </size>";
            if (name == "HudRemaining" && value.StartsWith("METEORS")) return "<size=1.35>남은 운석</size>\n" + value.Substring(7).Trim();
            if (name == "HudScore" && value.StartsWith("SCORE")) return "<size=1.35>SCORE</size>\n" + value.Substring(5).Trim();
            return value;
        }
        public static string FormatResult(GameSessionSnapshot snapshot, string rank)
        {
            snapshot ??= new GameSessionSnapshot();
            return (snapshot.MissionFailed ? "<align=center><color=#FF7952>MISSION FAILED</color></align>\n" : "<align=center><color=#BAFAFF>MISSION RESULT</color></align>\n")
                + $"<align=left><size=2.3>SCORE<pos=65%><color=#FFD18A>{snapshot.Score:N0}</color>\n"
                + $"DESTROYED<pos=65%>{snapshot.DestroyedMeteors}\n"
                + $"ACCURACY<pos=65%>{snapshot.AccuracyPercent}%\n"
                + $"RANK<pos=65%><color={(rank == "S" || rank == "A" ? "#FFC96B" : "#82FFC1")}>{rank}</color></size></align>\n"
                + $"<align=center><size=1.5><color=#9CBAC5>난이도 : {Difficulty.DifficultyConfig.Label(snapshot.Difficulty)} / {Difficulty.DifficultyConfig.Code(snapshot.Difficulty)}</color></size>\n"
                + (snapshot.MissionFailed ? "<size=1.8><color=#FFAA88>기지로 귀환해 다시 도전하세요</color></size></align>" : "<size=1.8><color=#82FFC1>지구 방어 성공!</color></size></align>");
        }
        private void OnDisable()
        {
            if (legacyRenderer != null) legacyRenderer.forceRenderingOff = false;
            // Explicitly disabling this bridge still supports the original TextMesh fallback.
            // Hidden state roots keep their font detached for the next state activation.
            if (gameObject.activeInHierarchy) RestoreLegacyFont();
        }
        private void OnDestroy() => RestoreLegacyFont();
    }
}
