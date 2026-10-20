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
                    display.text = "<color=#BFF9FF>임무 완료</color>\n<size=1.8><color=#8FFFC4>잘했어요!\n지구를 지켜냈어요!</color></size>";
                if (sourceName == "AlertText" && content.StartsWith("WARNING"))
                    display.text = "<color=#FF7952>" + PlayerMessage(content) + "</color>";
                if (sourceName == "HudFeedback" && content.Contains("LARGE METEOR"))
                    display.text = "<color=#FF7952>위험!</color>\n<size=1.7>거대한 운석이 다가와요!</size>";
            }
            motion?.Present(content, display.enabled && !string.IsNullOrEmpty(content));
        }
        private static string FormatTelemetry(string name, string value)
        {
            if (name == "StartLabel") return "시작";
            if (name == "ReadyPrompt") return "시작에 조준점을 맞춰 보세요.";
            if (name == "HudHealth") return "<size=1.35>방어막</size>\n<size=1.0> </size>";
            if (name == "HudRemaining" && value.StartsWith("METEORS")) return "<size=1.35>남은 운석</size>\n" + value.Substring(7).Trim();
            if (name == "HudScore" && value.StartsWith("SCORE")) return "<size=1.35>점수</size>\n" + value.Substring(5).Trim();
            return PlayerMessage(value);
        }
        // Controllers retain their status tokens for timing and feedback; only visible copy changes.
        public static string PlayerMessage(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            switch (value)
            {
                case "HANGAR": return "출격을 준비해요";
                case "DOOR OPEN": return "격납고 문이 열려요";
                case "ENGINE START": return "엔진을 켜고 있어요";
                case "THRUST": return "우주로 출발해요!";
                case "SPACE": return "우주에 도착했어요";
                case "MISSION START": return "지구를 지켜 주세요!";
                case "LOCK ON": return "조준 완료!";
                case "WARNING\nMASSIVE OBJECT DETECTED\nBOSS ASTEROID APPROACHING": return "위험!\n거대한 운석이 다가와요!";
                case "MISSION COMPLETE\nALL THREAT LEVELS CLEARED": return "모든 임무 완료!\n지구를 지켜냈어요!";
            }
            return value.Replace("MISSION COMPLETE", "임무 완료").Replace("MISSION FAILED", "아쉽지만 다시 도전해요")
                .Replace("WARNING", "위험!").Replace("MISSION", "우리의 임무")
                .Replace("THREAT LEVEL INCREASED", "다음 구역을 준비하세요")
                .Replace("STAGE ", "구역 ").Replace(" CLEAR", " 완료")
                .Replace("SCORE", "점수").Replace("ACCURACY", "명중률")
                .Replace("LARGE METEOR DETECTED", "큰 운석이 다가와요!")
                .Replace("NICE!", "멋져요!").Replace("GOOD!", "잘했어요!");
        }
        public static string FormatResult(GameSessionSnapshot snapshot, string rank)
        {
            snapshot ??= new GameSessionSnapshot();
            string score = snapshot.Score.ToString("N0");
            string scoreSize = (2.6f * Mathf.Min(1f, 5f / score.Length)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return (snapshot.MissionFailed ? "<align=center><color=#FF7952>다시 도전해요!</color></align>\n" : "<align=center><color=#BAFAFF>이번 임무의 기록</color></align>\n")
                + $"<align=left><size=2.3>점수<pos=65%><color=#FFD18A><size={scoreSize}>{score}</size></color>\n"
                + $"파괴한 운석<pos=65%>{snapshot.DestroyedMeteors}\n"
                + $"명중률<pos=65%>{snapshot.AccuracyPercent}%\n"
                + $"등급<pos=65%><color={(rank == "S" || rank == "A" ? "#FFC96B" : "#82FFC1")}><size=2.6>{rank}</size></color></size></align>\n"
                + $"<align=center><size=1.5><color=#9CBAC5>난이도 : {Difficulty.DifficultyConfig.Label(snapshot.Difficulty)}</color></size>\n"
                + (snapshot.MissionFailed ? "<size=1.8><color=#FFAA88>다시 출격해 지구를 지켜요!</color></size></align>" : "<size=1.8><color=#82FFC1>지구를 지켜냈어요!</color></size></align>");
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
