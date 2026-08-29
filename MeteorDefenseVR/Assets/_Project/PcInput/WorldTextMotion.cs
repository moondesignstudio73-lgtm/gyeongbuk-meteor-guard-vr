using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Only the TMP child moves. Safe-area roots, colliders and controller TextMeshes stay untouched.
    [DisallowMultipleComponent]
    public sealed class WorldTextMotion : MonoBehaviour
    {
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private TextMeshPro text;
        private Renderer mesh;
        private MaterialPropertyBlock properties;
        private Vector3 restScale, restPosition;
        private Color face = Color.white;
        private string role, lastContent;
        private float elapsed, beganAt;
        private bool wasVisible;
        private readonly Vector3[] rankCorners = new Vector3[4];
        private int rankVertex, rankMaterial;
        private bool rankCached;
        private float rankScale = 1f;
        public float Opacity { get; private set; }
        public int VisibleLines => text != null ? text.maxVisibleLines : 0;
        public static bool Supports(string name) => name == "ReadyTitle" || name == "ReadyInstructions" || name == "ReadyPrompt" ||
            name == "StartLabel" || name == "AlertText" || name == "MissionCompleteText" || name == "ResultText" ||
            name == "InstructionText" || name == "TutorialStatus" || name == "PracticeStatus" || name == "LaunchStatus" ||
            name == "CalibrationStatus" || name == "HudScore" || name == "HudFeedback";
        public void Configure(TextMeshPro target, string sourceName)
        {
            text = target; role = sourceName; mesh = target.GetComponent<Renderer>();
            restScale = target.transform.localScale; restPosition = target.transform.localPosition;
            properties = new MaterialPropertyBlock();
            if (target.fontSharedMaterial != null && target.fontSharedMaterial.HasProperty(FaceColor)) face = target.fontSharedMaterial.GetColor(FaceColor);
            if (role == "ResultText") text.OnPreRenderText += PrepareRank;
        }
        public void Present(string content, bool visible)
        {
            if (text == null) return;
            if (!visible) { wasVisible = false; elapsed = 0f; return; }
            bool changed = lastContent != content;
            if (wasVisible && !changed && elapsed > 2.2f && Opacity >= .999f) return;
            if (!wasVisible || changed) { elapsed = 0f; beganAt = Time.unscaledTime; rankCached = false; }
            wasVisible = true; lastContent = content;
            elapsed = Mathf.Max(0f, Time.unscaledTime - beganAt);
            float delay = role == "ReadyTitle" ? SfPresentationDirector.ReadyTitleDelay :
                role == "ReadyInstructions" ? SfPresentationDirector.ReadyTitleDelay + .12f :
                role == "StartLabel" || role == "ReadyPrompt" ? SfPresentationDirector.ReadyStartDelay : .14f;
            float t = Mathf.Max(0f, elapsed - delay);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .28f));
            float scale = Mathf.Lerp(.96f, 1f, Ease(t / .45f));
            int lines = int.MaxValue;
            if (role == "AlertText") lines = t < .22f ? 1 : int.MaxValue;
            if (role == "HudFeedback" && content.StartsWith("WARNING")) lines = t < .2f ? 1 : int.MaxValue;
            if (role == "MissionCompleteText") lines = t < .4f ? 1 : int.MaxValue;
            if (role == "ResultText")
            {
                lines = t < .16f ? 1 : t < .30f ? 2 : t < .44f ? 3 : t < .58f ? 4 : t < .86f ? 5 : int.MaxValue;
                scale += .025f * Pulse(t - .58f, .3f);
                rankScale = t < .58f ? 1f : Mathf.Lerp(1.65f, 1f, Ease((t - .58f) / .32f));
                if (rankCached && t < 1.05f) { ApplyRank(text.textInfo); text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices); }
            }
            bool countdown = role == "LaunchStatus" && (content == "3" || content == "2" || content == "1");
            if (countdown)
            {
                alpha = Mathf.Clamp01(elapsed / .07f) * (1f - Mathf.Clamp01((elapsed - .64f) / .22f));
                scale = Mathf.Lerp(1.3f, 1f, Ease(elapsed / .25f));
            }
            else if (role == "LaunchStatus" && content.Contains("MISSION START"))
            { scale = Mathf.Lerp(1.15f, 1f, Ease(elapsed / .22f)); alpha *= 1f - Mathf.Clamp01((elapsed - .65f) / .18f); }
            if (role == "HudScore") { alpha = 1f; scale = 1f + .07f * Pulse(elapsed, .3f); }
            Opacity = alpha;
            text.transform.localScale = restScale * scale;
            text.transform.localPosition = restPosition + (role == "HudScore" || countdown ? Vector3.zero : Vector3.up * (.035f * (1f - Ease(t / .4f))));
            if (text.maxVisibleLines != lines) text.maxVisibleLines = lines;
            mesh.GetPropertyBlock(properties);
            Color tint = face; tint.a *= alpha;
            properties.SetColor(FaceColor, tint); mesh.SetPropertyBlock(properties);
        }
        public static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float Pulse(float time, float duration) => time < 0f || time >= duration ? 0f : Mathf.Sin(time / duration * Mathf.PI);
        private void PrepareRank(TMP_TextInfo info)
        {
            if (info.lineCount < 5) return;
            int index = info.lineInfo[4].lastVisibleCharacterIndex;
            if (index < 0 || index >= info.characterCount) return;
            var character = info.characterInfo[index];
            if (!character.isVisible) return;
            rankVertex = character.vertexIndex; rankMaterial = character.materialReferenceIndex;
            var vertices = info.meshInfo[rankMaterial].vertices;
            if (rankVertex + 3 >= vertices.Length) return;
            for (int i = 0; i < 4; i++) rankCorners[i] = vertices[rankVertex + i];
            rankCached = true; ApplyRank(info);
        }
        private void ApplyRank(TMP_TextInfo info)
        {
            var vertices = info.meshInfo[rankMaterial].vertices;
            if (rankVertex + 3 >= vertices.Length) return;
            Vector3 center = (rankCorners[0] + rankCorners[2]) * .5f;
            for (int i = 0; i < 4; i++) vertices[rankVertex + i] = center + (rankCorners[i] - center) * rankScale;
        }
        private void OnDisable()
        {
            wasVisible = false; elapsed = 0f;
            if (text == null) return;
            text.transform.localScale = restScale; text.transform.localPosition = restPosition; text.maxVisibleLines = int.MaxValue;
            if (mesh != null && properties != null) { mesh.GetPropertyBlock(properties); properties.SetColor(FaceColor, face); mesh.SetPropertyBlock(properties); }
        }
        private void OnDestroy() { if (text != null) text.OnPreRenderText -= PrepareRank; }
    }
}
