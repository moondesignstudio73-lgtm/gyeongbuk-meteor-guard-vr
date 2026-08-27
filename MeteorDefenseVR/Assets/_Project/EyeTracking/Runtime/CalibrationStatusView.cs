using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class CalibrationStatusView : MonoBehaviour
    {
        [SerializeField] private CalibrationSequenceController controller;
        [SerializeField] private TextMesh statusText;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<CalibrationSequenceController>();
            if (statusText == null) statusText = GetComponent<TextMesh>();
            if (controller == null) return;
            controller.StatusMessageChanged += SetMessage;
            SetMessage(controller.StatusMessage);
        }

        private void OnDisable()
        {
            if (controller != null) controller.StatusMessageChanged -= SetMessage;
        }

        public void Configure(CalibrationSequenceController sequence, TextMesh text)
        {
            controller = sequence;
            statusText = text;
        }

        public void SetMessage(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
        }
    }
}
