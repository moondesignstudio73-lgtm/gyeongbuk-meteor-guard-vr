using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class MissionCompleteView : MonoBehaviour
    {
        [SerializeField] private MissionCompleteController controller;
        [SerializeField] private TextMesh statusText;
        [SerializeField] private Transform earth;
        [SerializeField] private Light earthFocusLight;
        [SerializeField] private AudioSource victoryAudioSource;
        [SerializeField] private GameObject visualFrame;
        private Vector3 earthBaseScale;
        private Color textColor;

        private void Awake()
        {
            if (earth != null) earthBaseScale = earth.localScale;
            if (statusText != null) textColor = statusText.color;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            bool visible = controller != null && controller.PresentationProgress > 0f && IsCompletionState;
            if (statusText != null)
            {
                if (statusText.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = visible;
                Color color = textColor; color.a = controller != null ? controller.PresentationProgress : 0f;
                statusText.color = color;
            }
            if (visualFrame != null) visualFrame.SetActive(visible);
            if (earthFocusLight != null) earthFocusLight.enabled = visible;
            if (earth == null || controller == null || !controller.IsRunning) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.025f;
            earth.localScale = earthBaseScale * pulse;
        }

        public void Configure(
            MissionCompleteController sequenceController,
            TextMesh text,
            Transform earthTransform,
            Light focusLight,
            AudioSource audioSource)
        {
            Unsubscribe();
            controller = sequenceController;
            statusText = text;
            earth = earthTransform;
            earthFocusLight = focusLight;
            victoryAudioSource = audioSource;
            earthBaseScale = earth != null ? earth.localScale : Vector3.one;
            if (statusText != null) textColor = statusText.color;
            Subscribe();
            SetStatus(controller != null ? controller.StatusMessage : string.Empty);
        }

        public void SetVisualFrame(GameObject frame)
        {
            visualFrame = frame;
            if (visualFrame != null)
                visualFrame.SetActive(controller != null && !string.IsNullOrEmpty(controller.StatusMessage));
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.StatusChanged -= SetStatus;
            controller.PresentationStarted -= PlayVictoryFeedback;
            controller.StatusChanged += SetStatus;
            controller.PresentationStarted += PlayVictoryFeedback;
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.StatusChanged -= SetStatus;
            controller.PresentationStarted -= PlayVictoryFeedback;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
            bool visible = !string.IsNullOrEmpty(message) && controller != null && controller.PresentationProgress > 0f && IsCompletionState;
            if (statusText != null && statusText.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = visible;
            if (earthFocusLight != null) earthFocusLight.enabled = visible;
            if (visualFrame != null) visualFrame.SetActive(visible);
        }

        private void PlayVictoryFeedback()
        {
            if (victoryAudioSource != null && victoryAudioSource.clip != null) victoryAudioSource.Play();
        }
        private static bool IsCompletionState => GameFlowManager.Instance == null ||
            GameFlowManager.Instance.CurrentState == MeteorDefenseVR.Core.GameState.MissionComplete;
    }
}
