using MeteorDefenseVR.UI;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    [DisallowMultipleComponent]
    public sealed class HudVisualEffectsView : MonoBehaviour
    {
        [SerializeField] private MissionHudController controller;
        [SerializeField] private Renderer[] healthSegments;
        [SerializeField] private Renderer feedbackFrame;
        [SerializeField] private TextMesh feedbackText;
        [SerializeField] private Color healthyColor = new Color(0.2f, 1f, 0.38f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.16f, 0.045f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.03f, 0.12f, 0.16f, 1f);

        private MaterialPropertyBlock block;
        private float feedbackPulse;
        private float healthNormalized = 1f;
        private Vector3 feedbackBaseScale;
        private void Awake() { feedbackBaseScale = feedbackFrame != null ? feedbackFrame.transform.localScale : Vector3.one; }

        private void OnEnable()
        {
            Subscribe();
            if (controller != null) { SetHealth(controller.CurrentHealth, controller.MaxHealth); SetFeedback(controller.FeedbackMessage); }
        }
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (feedbackPulse > 0f)
            {
                feedbackPulse = Mathf.Max(0f, feedbackPulse - Time.unscaledDeltaTime);
                float amount = Mathf.Clamp01(feedbackPulse / 0.55f);
                if (feedbackFrame != null) feedbackFrame.transform.localScale = feedbackBaseScale * (1f + Mathf.Sin(amount * Mathf.PI) * 0.035f);
                if (feedbackText != null) feedbackText.color = Color.Lerp(new Color(0.35f, 0.95f, 1f), Color.white, amount);
            }
            if (healthNormalized > 0f && healthNormalized < .2f && healthSegments != null)
            {
                float pulse = .72f + .28f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f));
                for (int i=0;i<healthSegments.Length;i++) if(healthSegments[i]!=null&&healthSegments[i].enabled) SetRendererColor(healthSegments[i],dangerColor*pulse);
            }
        }

        public void Configure(
            MissionHudController hudController,
            Renderer[] segments,
            Renderer frame,
            TextMesh feedback)
        {
            Unsubscribe();
            controller = hudController;
            healthSegments = segments;
            feedbackFrame = frame;
            feedbackBaseScale = frame != null ? frame.transform.localScale : Vector3.one;
            feedbackText = feedback;
            block ??= new MaterialPropertyBlock();
            Subscribe();
            if (controller != null)
            {
                SetHealth(controller.CurrentHealth, controller.MaxHealth);
                SetFeedback(controller.FeedbackMessage);
            }
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.HealthChanged -= SetHealth;
            controller.FeedbackChanged -= SetFeedback;
            controller.HealthChanged += SetHealth;
            controller.FeedbackChanged += SetFeedback;
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.HealthChanged -= SetHealth;
            controller.FeedbackChanged -= SetFeedback;
        }

        private void SetHealth(float current, float maximum)
        {
            if (healthSegments == null || healthSegments.Length == 0) return;
            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            healthNormalized = normalized;
            int visible = Mathf.CeilToInt(normalized * healthSegments.Length);
            Color active = normalized < .2f ? dangerColor : normalized < .4f ? new Color(1f,.32f,.045f,1f) : normalized < .7f ? new Color(1f,.78f,.08f,1f) : healthyColor;
            for (int i = 0; i < healthSegments.Length; i++) SetRendererColor(healthSegments[i], i < visible ? active : emptyColor);
        }

        private void SetFeedback(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            if (feedbackFrame != null) feedbackFrame.gameObject.SetActive(visible);
            feedbackPulse = visible ? 0.55f : 0f;
        }

        private void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 2f);
            renderer.SetPropertyBlock(block);
        }
    }
}
