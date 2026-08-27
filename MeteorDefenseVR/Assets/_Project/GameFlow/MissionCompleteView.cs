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
        private Vector3 earthBaseScale;

        private void Awake()
        {
            if (earth != null) earthBaseScale = earth.localScale;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
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
            Subscribe();
            SetStatus(controller != null ? controller.StatusMessage : string.Empty);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.StatusChanged -= SetStatus;
            controller.SequenceStarted -= PlayVictoryFeedback;
            controller.StatusChanged += SetStatus;
            controller.SequenceStarted += PlayVictoryFeedback;
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.StatusChanged -= SetStatus;
            controller.SequenceStarted -= PlayVictoryFeedback;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
            bool visible = !string.IsNullOrEmpty(message);
            if (earthFocusLight != null) earthFocusLight.enabled = visible;
        }

        private void PlayVictoryFeedback()
        {
            if (victoryAudioSource != null && victoryAudioSource.clip != null) victoryAudioSource.Play();
        }
    }
}
